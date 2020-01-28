// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using osu.Framework.IO.Network;
using osu.Game.Beatmaps.Legacy;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace PerformanceCalculator.Profile
{
    [Command(Name = "profile", Description = "Computes the total performance (pp) of a profile.")]
    public class ProfileCommand : ProcessorCommand
    {
        [UsedImplicitly]
        [Required]
        [Argument(0, Name = "user", Description = "User ID is preferred, but username should also work.")]
        public string ProfileName { get; }

        [UsedImplicitly]
        [Required]
        [Argument(1, Name = "api key", Description = "API Key, which you can get from here: https://osu.ppy.sh/p/api")]
        public string Key { get; }

        [UsedImplicitly]
        [Option(Template = "-d", Description = "Use osu_scores_high.sql.db instead of API for scores")]
        public bool UseDatabase { get; }

        [UsedImplicitly]
        [Option(Template = "-s", Description = "Add _suffix to the final file name")]
        public string Suffix { get; }

        private const string base_url = "https://osu.ppy.sh";

        class ResultBeatmap
        {
            public string Beatmap { get; set; }
            public string LivePP { get; set; }
            public string LocalPP { get; set; }
            public string PPChange { get; set; }
            public string PositionChange { get; set; }
            public string AimPP { get; set; }
            public string TapPP { get; set; }
            public string AccPP { get; set; }
        }

        public override void Execute()
        {
            var displayPlays = new List<UserPlayInfo>();

            var ruleset = new OsuRuleset();

            Console.WriteLine("Getting user data...");
            dynamic userData = getJsonFromApi($"get_user?k={Key}&u={ProfileName}")[0];

            Console.WriteLine("Getting user top scores...");

            dynamic scores;

            if (UseDatabase)
            {
                Console.WriteLine("Loading database...");
                using (var db = new ScoreDbContext())
                {
                    scores = db.osu_scores_high.Where(x => x.user_id == Convert.ToInt32(ProfileName) && x.pp != null).ToArray();
                }
            }
            else
            {
                scores = getJsonFromApi($"get_user_best?k={Key}&u={ProfileName}&limit=100");
            }

            Console.WriteLine("Calculating...");
            foreach (var play in scores)
            {
                string beatmapID;
                if (UseDatabase)
                    beatmapID = ((int)play.beatmap_id).ToString();
                else
                    beatmapID = play.beatmap_id;

                string cachePath = Path.Combine("cache", $"{beatmapID}.osu");

                if (!File.Exists(cachePath))
                {
                    Console.WriteLine($"Downloading {beatmapID}.osu...");
                    new FileWebRequest(cachePath, $"{base_url}/osu/{beatmapID}").Perform();
                }

                Mod[] mods = ruleset.ConvertLegacyMods((LegacyMods)play.enabled_mods).ToArray();

                if (new FileInfo(cachePath).Length <= 0)
                    continue;

                var working = new ProcessorWorkingBeatmap(cachePath, (int)play.beatmap_id);

                var score = new ProcessorScoreParser(working).Parse(new ScoreInfo
                {
                    Ruleset = ruleset.RulesetInfo,
                    MaxCombo = play.maxcombo,
                    Mods = mods,
                    Statistics = new Dictionary<HitResult, int>
                    {
                        { HitResult.Perfect, (int)play.countgeki },
                        { HitResult.Great, (int)play.count300 },
                        { HitResult.Good, (int)play.count100 },
                        { HitResult.Ok, (int)play.countkatu },
                        { HitResult.Meh, (int)play.count50 },
                        { HitResult.Miss, (int)play.countmiss }
                    }
                });

                var perfCalc = ruleset.CreatePerformanceCalculator(working, score.ScoreInfo);

                var diffCache = $"cache/{working.BeatmapInfo.OnlineBeatmapID}{string.Join(string.Empty, mods.Select(x => x.Acronym))}_diff.json";

                if (File.Exists(diffCache))
                {
                    var userCalcDate = File.GetLastWriteTime(diffCache).ToUniversalTime();
                    var calcUpdateDate = File.GetLastWriteTime("osu.Game.Rulesets.Osu.dll").ToUniversalTime();

                    if (userCalcDate > calcUpdateDate)
                    {
                        var file = File.ReadAllText(diffCache);
                        file = file.Replace("Mods", "nommods"); // stupid hack!!!!!!!!!!
                        var attr = JsonConvert.DeserializeObject<OsuDifficultyAttributes>(file);
                        attr.Mods = mods;
                        perfCalc.Attributes = attr;
                    }
                    else
                    {
                        perfCalc.Attributes = new ProcessorOsuDifficultyCalculator(ruleset, working).Calculate(mods);
                    }
                }
                else
                    perfCalc.Attributes = new ProcessorOsuDifficultyCalculator(ruleset, working).Calculate(mods);

                var pp = 0.0;
                var maxCombo = 0.0;
                var aimPP = 0.0;
                var tapPP = 0.0;
                var accPP = 0.0;
                var categories = new Dictionary<string, double>();

                try
                {
                    pp = perfCalc.Calculate(categories);
                    maxCombo = categories["Max Combo"];
                    aimPP = categories["Aim"];
                    tapPP = categories["Tap"];
                    accPP = categories["Accuracy"];
                }
                catch (Exception e)
                {
                    System.Console.WriteLine(e);
                }

                var thisPlay = new UserPlayInfo
                {
                    BeatmapId = beatmapID,
                    Beatmap = working.BeatmapInfo,
                    LocalPP = double.IsNormal(pp) ? pp : 0,
                    LivePP = play.pp ?? 0,
                    Mods = mods.Length > 0 ? mods.Select(m => m.Acronym).Aggregate((c, n) => $"{c}, {n}") : "None",
                    Accuracy = Math.Round(score.ScoreInfo.Accuracy * 100, 2).ToString(CultureInfo.InvariantCulture),
                    Combo = $"{play.maxcombo.ToString()}/{maxCombo}x",
                    Misses = play.countmiss == 0 ? "" : $", {play.countmiss} {(play.countmiss == 1 ? "miss" : "misses")}",
                    AimPP = aimPP,
                    TapPP = tapPP,
                    AccPP = accPP
                };

                displayPlays.Add(thisPlay);
            }

            if (UseDatabase)
            {
                var scores2 = new List<UserPlayInfo>();

                foreach (var s in displayPlays)
                {
                    var beatmapScores = displayPlays.Where(x => x.BeatmapId == s.BeatmapId).OrderByDescending(x => x.LocalPP);

                    if (beatmapScores.Count() > 1)
                    {
                        if (!scores2.Any(x => x.BeatmapId == s.BeatmapId))
                        {
                            scores2.Add(beatmapScores.First());
                        }
                    }
                    else
                    {
                        scores2.Add(s);
                    }
                }

                displayPlays = scores2;
            }

            var localOrdered = displayPlays.OrderByDescending(p => p.LocalPP).ToList();
            var liveOrdered = displayPlays.OrderByDescending(p => p.LivePP).ToList();

            int index = 0;
            double totalLocalPP = localOrdered.Sum(play => Math.Pow(0.95, index++) * play.LocalPP);
            double totalLivePP = userData.pp_raw;

            index = 0;
            double nonBonusLivePP = liveOrdered.Sum(play => Math.Pow(0.95, index++) * play.LivePP);

            // inactive players have 0 pp and databased profiles are always outdated
            if (totalLivePP <= 0.0 || UseDatabase)
                totalLivePP = nonBonusLivePP;

            //todo: implement properly. this is pretty damn wrong.
            var playcountBonusPP = (totalLivePP - nonBonusLivePP);
            totalLocalPP += playcountBonusPP;
            double totalDiffPP = totalLocalPP - totalLivePP;

            var obj = new
            {
                UserID = userData.user_id,
                Username = userData.username,
                UserCountry = userData.country,
                LivePP = FormattableString.Invariant($"{totalLivePP:F1} (including {playcountBonusPP:F1}pp from playcount)"),
                LocalPP = FormattableString.Invariant($"{totalLocalPP:F1} ({totalDiffPP:+0.0;-0.0;-})"),
                Beatmaps = new List<ResultBeatmap>()
            };

            localOrdered = localOrdered.Take(1000).ToList();

            foreach (var item in localOrdered)
            {
                var mods = item.Mods == "None" ? string.Empty : item.Mods.Insert(0, "+");
                obj.Beatmaps.Add(new ResultBeatmap()
                {
                    Beatmap = FormattableString.Invariant($"{item.Beatmap.OnlineBeatmapID} - {item.Beatmap} {mods} ({item.Accuracy}%, {item.Combo}{item.Misses})"),
                    LivePP = FormattableString.Invariant($"{item.LivePP:F1}"),
                    LocalPP = FormattableString.Invariant($"{item.LocalPP:F1}"),
                    PositionChange = FormattableString.Invariant($"{liveOrdered.IndexOf(item) - localOrdered.IndexOf(item):+0;-0;-}"),
                    PPChange = FormattableString.Invariant($"{item.LocalPP - item.LivePP:+0.0;-0.0}"),
                    AimPP = FormattableString.Invariant($"{item.AimPP:F1}"),
                    AccPP = FormattableString.Invariant($"{item.AccPP:F1}"),
                    TapPP = FormattableString.Invariant($"{item.TapPP:F1}")
                });
            }

            var json = JsonConvert.SerializeObject(obj, new JsonSerializerSettings { Culture = CultureInfo.InvariantCulture });

            if (!Directory.Exists("players"))
                Directory.CreateDirectory("players");

            if (UseDatabase)
                File.WriteAllText(Path.Combine("players", $"{userData.username.ToString().ToLower()}_full.json"), json);
            else if (!string.IsNullOrEmpty(Suffix))
                File.WriteAllText(Path.Combine("players", $"{ProfileName}_{Suffix}.json"), json);
            else
                File.WriteAllText(Path.Combine("players", $"{ProfileName}.json"), json);
        }

        private dynamic getJsonFromApi(string request)
        {
            using (var req = new JsonWebRequest<dynamic>($"{base_url}/api/{request}"))
            {
                req.Perform();
                return req.ResponseObject;
            }
        }
    }
}
