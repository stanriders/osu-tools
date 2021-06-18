// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using osu.Framework.IO.Network;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Legacy;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using PerformanceCalculator.Caching;

namespace PerformanceCalculator.Simulate
{
    public abstract class SimulateCommand : ProcessorCommand
    {
        public abstract string Beatmap { get; set; }

        public abstract Ruleset Ruleset { get; }

        [UsedImplicitly]
        public virtual double Accuracy { get; }

        [UsedImplicitly]
        public virtual int? Combo { get; }

        [UsedImplicitly]
        public virtual double PercentCombo { get; }

        [UsedImplicitly]
        public virtual int Score { get; }

        [UsedImplicitly]
        public virtual string[] Mods { get; }

        [UsedImplicitly]
        public virtual int Misses { get; }

        [UsedImplicitly]
        public virtual int? Mehs { get; }

        [UsedImplicitly]
        public virtual int? Goods { get; }

        private class ComboGraph
        {
            [JsonProperty("x")]
            public double Time { get; set; }

            [JsonProperty("y")]
            public double PP { get; set; }
        }

        private class OsuHitObjectWithCombo : OsuHitObject
        {
            public long Combo { get; set; }

            public Type OriginalType { get; set; }

            public override string ToString() => $"{Combo}({StartTime}) - {OriginalType.Name}";

            public OsuHitObjectWithCombo(OsuHitObject o)
            {
                ComboOffset = o.ComboOffset;
                ComboIndex = o.ComboIndex;
                StartTime = o.StartTime;
                IndexInCurrentCombo = o.IndexInCurrentCombo;

                OriginalType = o.GetType();
            }
        }

        public override void Execute()
        {
            var ruleset = Ruleset;

            var mods = getMods(ruleset).ToArray();

            if (Beatmap.Contains("osu.ppy.sh"))
            {
                // only osu.ppy.sh/b/123
                var id = Beatmap.Split('/').Last();
                var fileName = $"cache/{id}.osu";

                if (!File.Exists(fileName))
                {
                    using (var req = new WebRequest($"https://osu.ppy.sh/osu/{id}"))
                    {
                        req.Perform();
                        if (!req.Completed)
                            return;

                        File.WriteAllText(fileName, req.GetResponseString());
                    }
                }

                Beatmap = fileName;
            }

            var workingBeatmap = new ProcessorWorkingBeatmap(Beatmap);

            var beatmap = workingBeatmap.GetPlayableBeatmap(ruleset.RulesetInfo, mods);

            var beatmapMaxCombo = GetMaxCombo(beatmap);
            var maxCombo = Combo ?? (int)Math.Round(PercentCombo / 100 * beatmapMaxCombo);
            var score = Score;

            var mapId = Path.GetFileNameWithoutExtension(Beatmap);

            OsuDifficultyAttributes attributes;

            using (var diffDb = new DifficultyAttributeCacheContext())
            {
                try
                {
                    if (workingBeatmap.BeatmapInfo.OnlineBeatmapID != null && workingBeatmap.BeatmapInfo.OnlineBeatmapID > 0)
                    {
                        var diffAttributes = diffDb.Attributes.SingleOrDefault(x => x.MapId == workingBeatmap.BeatmapInfo.OnlineBeatmapID && x.Mods == convertToLegacyMods(mods));

                        if (diffAttributes != null)
                        {
                            var calcUpdateDate = File.GetLastWriteTime("osu.Game.Rulesets.Osu.dll").ToUniversalTime();

                            if (diffAttributes.UpdateDate > calcUpdateDate)
                            {
                                attributes = diffAttributes.ToOsuDifficultyAttributes();
                            }
                            else
                            {
                                var newAttr = (OsuDifficultyAttributes)new OsuDifficultyCalculator(ruleset, workingBeatmap).Calculate(mods);
                                attributes = newAttr;
                                diffAttributes.MapId = workingBeatmap.BeatmapInfo.OnlineBeatmapID ?? 0;
                                diffAttributes.UpdateDate = DateTime.Now.ToUniversalTime();
                                diffAttributes.Mods = convertToLegacyMods(mods);
                                diffAttributes.FromOsuDifficultyAttributes(newAttr);
                            }
                        }
                        else
                        {
                            var newAttr = (OsuDifficultyAttributes)new OsuDifficultyCalculator(ruleset, workingBeatmap).Calculate(mods);
                            attributes = newAttr;
                            var newdiff = new DiffAttributesDbModel
                            {
                                MapId = workingBeatmap.BeatmapInfo.OnlineBeatmapID ?? 0,
                                UpdateDate = DateTime.Now.ToUniversalTime(),
                                Mods = convertToLegacyMods(mods)
                            };
                            newdiff.FromOsuDifficultyAttributes(newAttr);
                            diffDb.Attributes.Add(newdiff);
                        }
                    }
                    else
                    {
                        // maps without online id cant be cached properly so dont bother
                        attributes = (OsuDifficultyAttributes)new OsuDifficultyCalculator(ruleset, workingBeatmap).Calculate(mods);
                    }
                }
                catch (Exception e)
                {
                    // if we somehow failed on db interaction - ignore and calculate manually
                    attributes = (OsuDifficultyAttributes)new OsuDifficultyCalculator(ruleset, workingBeatmap).Calculate(mods);
                    Console.WriteLine(e);
                }

                diffDb.SaveChanges();
            }

            const int acc_start = 90;

            var ppList = new List<double>(100 - acc_start);
            var aimppList = new List<double>(100 - acc_start);
            var tapppList = new List<double>(100 - acc_start);
            var accppList = new List<double>(100 - acc_start);
            var readingppList = new List<double>(100 - acc_start);

            for (int i = acc_start; i <= 100; i++)
            {
                var (pp, aimpp, tappp, accpp, readingpp) = getPPForAccuracy(i, workingBeatmap, beatmap, mods, maxCombo, score, attributes, ruleset);
                ppList.Add(pp);
                aimppList.Add(aimpp);
                tapppList.Add(tappp);
                accppList.Add(accpp);
                readingppList.Add(readingpp);
            }

            var misspp = new List<ComboGraph>();
            /*
            var objects = beatmap.HitObjects.Select(x => new OsuHitObjectWithCombo((OsuHitObject)x)).ToList();

            foreach (var c in beatmap.HitObjects)
            {
                if (c.NestedHitObjects.Count > 0)
                    foreach (var nestedObj in c.NestedHitObjects)
                        if (nestedObj is SliderTailCircle || nestedObj is SliderRepeat || nestedObj is SliderTick)
                            objects.Add(new OsuHitObjectWithCombo((OsuHitObject)nestedObj));
            }

            objects = objects.OrderBy(x => x.StartTime).ToList();

            for (int i = 0; i < objects.Count; i++)
                objects[i].Combo = i + 1;

            while (objects[objects.Count - 1].OriginalType == typeof(SliderTailCircle)
                   || objects[objects.Count - 1].OriginalType == typeof(SliderTick)
                   || objects[objects.Count - 1].OriginalType == typeof(SliderRepeat))
            {
                // remove ending slider parts to match aimprob graph time scale
                objects.RemoveAt(objects.Count - 1);
            }

            var comboFrequency = Math.Max(beatmapMaxCombo / 200, 1);

            for (int i = 1; i <= beatmapMaxCombo; i += comboFrequency)
            {
                var objTime = objects.FirstOrDefault(x => x.Combo == i)?.StartTime;

                if (i >= objects.Count)
                    objTime = objects.Last().StartTime;

                if (objTime != null)
                {
                    double pp = getPPForCombo(workingBeatmap, beatmap, mods, i, score, attributes, ruleset);
                    misspp.Add(new ComboGraph
                    {
                        PP = pp,
                        Time = (double)(objTime / 1000)
                    });
                }

                if (i >= objects.Count)
                    break;
            }
            */
            var obj = new
            {
                Id = mapId,
                BeatmapSetId = beatmap.BeatmapInfo.BeatmapSet?.OnlineBeatmapSetID,
                Title = workingBeatmap.BeatmapInfo.ToString(),
                PP = ppList,
                AimPP = aimppList,
                TapPP = tapppList,
                AccPP = accppList,
                ReadingPP = readingppList,
                MissPP = misspp,
                Stars = attributes.StarRating,
                AimSR = attributes.AimStarRating,
                TapSR = attributes.TapStarRating,
                //ReadingSR = attributes.ReadingSr,
                FingerControlSR = attributes.FingerControlStarRating,
                Mods = mods
            };

            var json = JsonConvert.SerializeObject(obj, new JsonSerializerSettings
            {
                Culture = CultureInfo.InvariantCulture,
#if DEBUG
                Formatting = Formatting.Indented
#endif
            });

            if (!Directory.Exists("mapinfo"))
                Directory.CreateDirectory("mapinfo");

            File.WriteAllText(Path.Combine("mapinfo", $"{mapId}_{string.Join(string.Empty, mods.Select(x => x.Acronym))}.json"), json);
        }

        private List<Mod> getMods(Ruleset ruleset)
        {
            var mods = new List<Mod>();
            if (Mods == null)
                return mods;

            var availableMods = ruleset.GetAllMods().ToList();

            foreach (var modString in Mods)
            {
                Mod newMod = availableMods.FirstOrDefault(m => string.Equals(m.Acronym, modString, StringComparison.CurrentCultureIgnoreCase));
                if (newMod == null)
                    throw new ArgumentException($"Invalid mod provided: {modString}");

                mods.Add(newMod);
            }

            return mods;
        }

        private LegacyMods convertToLegacyMods(IEnumerable<Mod> mods)
        {
            var flags = LegacyMods.None;

            foreach (var mod in mods)
            {
                if (mod is OsuModNightcore)
                    flags |= LegacyMods.Nightcore | LegacyMods.DoubleTime;
                if (mod is OsuModDoubleTime)
                    flags |= LegacyMods.DoubleTime;
                if (mod is OsuModEasy)
                    flags |= LegacyMods.Easy;
                if (mod is OsuModFlashlight)
                    flags |= LegacyMods.Flashlight;
                if (mod is OsuModHalfTime)
                    flags |= LegacyMods.HalfTime;
                if (mod is OsuModHardRock)
                    flags |= LegacyMods.HardRock;
                if (mod is OsuModHidden)
                    flags |= LegacyMods.Hidden;
                if (mod is OsuModNoFail)
                    flags |= LegacyMods.NoFail;
                if (mod is OsuModPerfect)
                    flags |= LegacyMods.Perfect | LegacyMods.SuddenDeath;
                if (mod is OsuModSpunOut)
                    flags |= LegacyMods.SpunOut;
                if (mod is OsuModSuddenDeath)
                    flags |= LegacyMods.SuddenDeath;
                if (mod is OsuModTouchDevice)
                    flags |= LegacyMods.TouchDevice;
            }

            return flags;
        }

        private (double, double, double, double, double) getPPForAccuracy(double acc, ProcessorWorkingBeatmap workingBeatmap, IBeatmap beatmap, Mod[] mods, int maxCombo, int score, DifficultyAttributes attributes, Ruleset ruleset)
        {
            var statistics = GenerateHitResults(acc / 100, beatmap, 0, null, null);
            var scoreInfo = new ScoreInfo
            {
                Accuracy = GetAccuracy(statistics),
                MaxCombo = maxCombo,
                Statistics = statistics,
                Mods = mods,
                TotalScore = score
            };
            var perfCalc = ruleset.CreatePerformanceCalculator(workingBeatmap, scoreInfo);
            perfCalc.Attributes = attributes;
            var diffCats = new Dictionary<string, double>();
            var pp = perfCalc.Calculate(diffCats);
            return (Math.Round(pp, 2), Math.Round(diffCats["Aim"], 2), Math.Round(diffCats["Tap"], 2), Math.Round(diffCats["Accuracy"], 2), /*Math.Round(diffCats["Reading"], 2)*/ 0);
        }

        private double getPPForCombo(ProcessorWorkingBeatmap workingBeatmap, IBeatmap beatmap, Mod[] mods, int maxCombo, int score, DifficultyAttributes attributes, Ruleset ruleset, int misses = 0)
        {
            var statistics = GenerateHitResults(1, beatmap, misses, null, null);
            var scoreInfo = new ScoreInfo
            {
                Accuracy = GetAccuracy(statistics),
                MaxCombo = maxCombo,
                Statistics = statistics,
                Mods = mods,
                TotalScore = score
            };
            var perfCalc = ruleset.CreatePerformanceCalculator(workingBeatmap, scoreInfo);
            perfCalc.Attributes = attributes;
            return Math.Round(perfCalc.Calculate(), 2);
        }

        protected abstract void WritePlayInfo(ScoreInfo scoreInfo, IBeatmap beatmap);

        protected abstract int GetMaxCombo(IBeatmap beatmap);

        protected abstract Dictionary<HitResult, int> GenerateHitResults(double accuracy, IBeatmap beatmap, int countMiss, int? countMeh, int? countGood);

        protected virtual double GetAccuracy(Dictionary<HitResult, int> statistics) => 0;

        protected void WriteAttribute(string name, string value) => Console.WriteLine($"{name.PadRight(15)}: {value}");
    }
}
