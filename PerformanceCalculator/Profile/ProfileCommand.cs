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
using osu.Game.Rulesets.Osu.Difficulty.Skills;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using PerformanceCalculator.Caching;

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
        [Option(Template = "-p", Description = "Only calculate scores higher than this threshold")]
        public double PPThreshold { get; }

        [UsedImplicitly]
        [Option(Template = "-s", Description = "Add _suffix to the final file name")]
        public string Suffix { get; }

        [UsedImplicitly]
        [Option(Template = "-t", Description = "Add compare to newpp column")]
        public bool NewppCompare { get; }

        [UsedImplicitly]
        [Option(Template = "-td", Description = "Add compare to newpp column for full dump profiles")]
        public bool NewppCompareDatabase { get; }

        private const string base_url = "https://osu.ppy.sh";

        private class ResultProfile
        {
            public int UserID { get; set; }
            public string Username { get; set; }
            public string UserCountry { get; set; }
            public string LivePP { get; set; }
            public string LocalPP { get; set; }
            public string SitePP { get; set; }
            public List<ResultBeatmap> Beatmaps { get; set; } = new List<ResultBeatmap>();
        }

        private class ResultBeatmap
        {
            public string Beatmap { get; set; }
            public string LivePP { get; set; }
            public string LocalPP { get; set; }
            public string ComparePP { get; set; }
            public string PPChange { get; set; }
            public string PositionChange { get; set; }
            public string AimPP { get; set; }
            public string TapPP { get; set; }
            public string AccPP { get; set; }
            public string ReadingPP { get; set; }
        }

        public class NewppPlayer
        {
            public int Id { get; set; }
            public string Name { get; set; } = "Unknown Player";
            public double LivePp { get; set; }
            public double LocalPp { get; set; }
            public double PlaycountPp { get; set; }
            public string? Country { get; set; }
            public DateTime UpdateTime { get; set; }
            public List<NewppScore>? Scores { get; set; }
        }

        public class NewppMap
        {
            public int Id { get; set; }
            public string Name { get; set; } = "Unknown Map";
            public int MaxCombo { get; set; }
        }

        public class NewppScore
        {
            public long Id { get; set; }
            public double LocalPp { get; set; }
            public double? LivePp { get; set; }
            public string? Mods { get; set; }
            public double Accuracy { get; set; }
            public int Combo { get; set; }
            public int Misses { get; set; }
            public DateTime UpdateTime { get; set; }
            public string? AdditionalPpData { get; set; }
            public int PositionChange { get; set; }
            public int PlayerId { get; set; }
            public NewppScore? Player { get; set; }
            public int MapId { get; set; }
            public NewppMap? Map { get; set; }
        }

        public override void Execute()
        {
            var displayPlays = new List<UserPlayInfo>();

            var ruleset = new OsuRuleset();

            Console.WriteLine("Getting user data...");
            dynamic userData = getJsonFromApi<dynamic>($"get_user?k={Key}&u={ProfileName}")[0];

            Console.WriteLine("Getting user top scores...");

            ScoreDbModel[] scores;

            if (UseDatabase)
            {
                Console.WriteLine("Loading database...");

                using (var db = new ScoreDbContext())
                {
                    scores = db.osu_scores_high.Where(x => x.user_id == Convert.ToInt32(ProfileName) &&
                                                           x.pp != null &&
                                                           x.pp != 0.0 &&
                                                           x.hidden == 0).ToArray();
                }
            }
            else
            {
                scores = getJsonFromApi<ScoreDbModel[]>($"get_user_best?k={Key}&u={ProfileName}&limit=100");
            }

            Console.WriteLine("Calculating...");

            using (var diffDb = new DifficultyAttributeCacheContext())
            {
                foreach (var play in scores)
                {
                    if (play.pp < PPThreshold)
                        continue;

                    string beatmapID = ((int)play.beatmap_id).ToString();
                    string cachePath = Path.Combine("cache", $"{beatmapID}.osu");

                    if (!File.Exists(cachePath))
                    {
                        Console.WriteLine($"Downloading {beatmapID}.osu...");
                        new FileWebRequest(cachePath, $"{base_url}/osu/{beatmapID}").Perform();
                    }

                    LegacyMods legacyMods = play.enabled_mods;
                    Mod[] mods = ruleset.ConvertFromLegacyMods((LegacyMods)play.enabled_mods).ToArray();

                    if (new FileInfo(cachePath).Length <= 0)
                        continue;

                    var working = new ProcessorWorkingBeatmap(cachePath, (int)play.beatmap_id);

                    var score = new ProcessorScoreDecoder(working).Parse(new ScoreInfo
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

                    try
                    {
                        if (working.BeatmapInfo.OnlineBeatmapID != null && working.BeatmapInfo.OnlineBeatmapID > 0)
                        {
                            var diffAttributes = diffDb.Attributes.SingleOrDefault(x => x.MapId == working.BeatmapInfo.OnlineBeatmapID && x.Mods == legacyMods);

                            if (diffAttributes != null)
                            {
                                var calcUpdateDate = File.GetLastWriteTime("osu.Game.Rulesets.Osu.dll").ToUniversalTime();

                                if (diffAttributes.UpdateDate > calcUpdateDate)
                                {
                                    perfCalc.Attributes = diffAttributes.ToOsuDifficultyAttributes();
                                }
                                else
                                {
                                    var newAttr = (OsuDifficultyAttributes)new OsuDifficultyCalculator(ruleset, working).Calculate(mods);
                                    perfCalc.Attributes = newAttr;
                                    diffAttributes.MapId = working.BeatmapInfo.OnlineBeatmapID ?? 0;
                                    diffAttributes.UpdateDate = DateTime.Now.ToUniversalTime();
                                    diffAttributes.Mods = legacyMods;
                                    diffAttributes.FromOsuDifficultyAttributes(newAttr);
                                }
                            }
                            else
                            {
                                var newAttr = (OsuDifficultyAttributes)new OsuDifficultyCalculator(ruleset, working).Calculate(mods);
                                perfCalc.Attributes = newAttr;
                                var newdiff = new DiffAttributesDbModel
                                {
                                    MapId = working.BeatmapInfo.OnlineBeatmapID ?? 0,
                                    UpdateDate = DateTime.Now.ToUniversalTime(),
                                    Mods = legacyMods
                                };
                                newdiff.FromOsuDifficultyAttributes(newAttr);
                                diffDb.Attributes.Add(newdiff);
                            }
                        }
                        else
                        {
                            // maps without online id cant be cached properly so dont bother
                            perfCalc.Attributes = new OsuDifficultyCalculator(ruleset, working).Calculate(mods);
                        }
                    }
                    catch (Exception e)
                    {
                        // if we somehow failed on db interaction - ignore and calculate manually
                        perfCalc.Attributes = new OsuDifficultyCalculator(ruleset, working).Calculate(mods);
                        Console.WriteLine(e);
                    }

                    var pp = 0.0;
                    var maxCombo = 0.0;
                    var aimPP = 0.0;
                    var tapPP = 0.0;
                    var accPP = 0.0;
                    var readingPP = 0.0;
                    var categories = new Dictionary<string, double>();

                    try
                    {
                        pp = perfCalc.Calculate(categories);
                        maxCombo = categories["Max Combo"];
                        aimPP = categories["Aim"];
                        tapPP = categories["Tap"];
                        accPP = categories["Accuracy"];
                        //readingPP = categories["Reading"];
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
                        AccPP = accPP,
                        ReadingPP = readingPP
                    };

                    displayPlays.Add(thisPlay);
                }

                try
                {
                    diffDb.SaveChanges();
                }
                catch (Exception e)
                {
                    // dont fail if we cant save attributes - results are more important than caching
                    Console.WriteLine("Failed to write diff attributes!");
                    Console.WriteLine(e);
                }
            }

            var liveOrdered = displayPlays.OrderByDescending(p => p.LivePP).ToList();
            var localOrdered = displayPlays.OrderByDescending(p => p.LocalPP).ToList();

            if (UseDatabase)
            {
                // if a map has more than one score take the biggest pp score

                liveOrdered = displayPlays.GroupBy(x => x.BeatmapId)
                                          .Select(x => x.OrderByDescending(x => x.LivePP).First())
                                          .OrderByDescending(p => p.LivePP)
                                          .ToList();

                localOrdered = displayPlays.GroupBy(x => x.BeatmapId)
                                           .Select(x => x.OrderByDescending(x => x.LocalPP).First())
                                           .OrderByDescending(p => p.LocalPP)
                                           .ToList();
            }

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

            if (UseDatabase)
            {
                var scoreCount = scores.GroupBy(x => x.beatmap_id)
                                       .Select(x => x.OrderByDescending(x => x.pp).First())
                                       .Count();

                playcountBonusPP = 416.6667 * (1 - Math.Pow(0.9994, scoreCount));
                totalLivePP += playcountBonusPP;
            }

            totalLocalPP += playcountBonusPP;
            double totalDiffPP = totalLocalPP - totalLivePP;

            NewppPlayer newppProfile = null;
            if (NewppCompare)
            {
                var profile = userData.user_id;
                if (UseDatabase && NewppCompareDatabase)
                    profile = $"{userData.username}_full".ToLower();

                using (var req = new JsonWebRequest<NewppPlayer>($"https://newpp.stanr.info/api/player/{profile}"))
                {
                    req.Perform();
                    newppProfile = req.ResponseObject;
                }
                /*
                if (NewppCompareDatabase && !string.IsNullOrEmpty(newppProfile?.LivePP))
                {
                    var siteBonus = newppProfile.PlaycountPp;

                    var changeIndex = newppProfile.LocalPp.IndexOf(" (");
                    var localNoChange = double.Parse(newppProfile.LocalPp.Substring(0, changeIndex), CultureInfo.InvariantCulture);

                    localNoChange = localNoChange - siteBonus + playcountBonusPP;
                    newppProfile.LocalPp = FormattableString.Invariant($"{localNoChange:F1} ({localNoChange - totalLivePP:F1})");
                }*/
            }

            var obj = new ResultProfile
            {
                UserID = userData.user_id,
                Username = userData.username,
                UserCountry = userData.country,
                LivePP = FormattableString.Invariant($"{totalLivePP:F1} (including {playcountBonusPP:F1}pp from playcount)"),
                LocalPP = FormattableString.Invariant($"{totalLocalPP:F1} ({totalDiffPP:+0.0;-0.0;-})"),
                SitePP = newppProfile?.LocalPp.ToString(),
                Beatmaps = new List<ResultBeatmap>()
            };

            const int score_amt = 1000;
            localOrdered = localOrdered.Take(score_amt).ToList();


            foreach (var item in localOrdered)
            {
                var mods = item.Mods == "None" ? string.Empty : item.Mods.Insert(0, "+");
                var beatmapName = FormattableString.Invariant($"{item.Beatmap.OnlineBeatmapID} - {item.Beatmap} {mods} ({item.Accuracy}%, {item.Combo}{item.Misses})");
                var ppChange = FormattableString.Invariant($"{item.LocalPP - item.LivePP:+0.0;-0.0}");

                string newppVal = null;

                if (NewppCompare)
                {
                    var map = newppProfile?.Scores.SingleOrDefault(x => x.MapId == item.Beatmap.OnlineBeatmapID);
                   
                    if (map != null)
                    {
                        var newppLocal = map.LocalPp;
                        newppVal = map.LocalPp.ToString();
                        ppChange = FormattableString.Invariant($"{item.LocalPP - newppLocal:+0.0;-0.0}"); 
                    }
                    else
                    {
                        ppChange += "<br>(vs live)";
                    }
                }

                obj.Beatmaps.Add(new ResultBeatmap()
                {
                    Beatmap = beatmapName,
                    LivePP = FormattableString.Invariant($"{item.LivePP:F1}"),
                    LocalPP = FormattableString.Invariant($"{item.LocalPP:F1}"),
                    ComparePP = newppVal,
                    PositionChange = FormattableString.Invariant($"{liveOrdered.IndexOf(item) - localOrdered.IndexOf(item):+0;-0;-}"),
                    PPChange = ppChange,
                    AimPP = FormattableString.Invariant($"{item.AimPP:F1}"),
                    AccPP = FormattableString.Invariant($"{item.AccPP:F1}"),
                    TapPP = FormattableString.Invariant($"{item.TapPP:F1}"),
                    ReadingPP = FormattableString.Invariant($"{item.ReadingPP:F1}")
                });
            }

            var json = JsonConvert.SerializeObject(obj, new JsonSerializerSettings { Culture = CultureInfo.InvariantCulture });

            if (!Directory.Exists("players"))
                Directory.CreateDirectory("players");

            var filename = ProfileName;

            if (UseDatabase)
                filename = $"{userData.username.ToString().ToLower()}_full";

            if (!string.IsNullOrEmpty(Suffix))
                filename += $"_{Suffix}";

            File.WriteAllText(Path.Combine("players", $"{filename}.json"), json);
        }

        private T getJsonFromApi<T>(string request)
        {
            using (var req = new JsonWebRequest<T>($"{base_url}/api/{request}"))
            {
                req.Perform();
                return req.ResponseObject;
            }
        }
    }
}
