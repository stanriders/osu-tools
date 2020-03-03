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
        public abstract string Beatmap { get; }

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

            (double pp100, var aimpp100, var tappp100, var accpp100) = getPPForAccuracy(100, workingBeatmap, beatmap, mods, maxCombo, score, attributes, ruleset);
            (double pp99, var aimpp99, var tappp99, var accpp99) =  getPPForAccuracy(99, workingBeatmap, beatmap, mods, maxCombo, score, attributes, ruleset);
            (double pp98, var aimpp98, var tappp98, var accpp98) =  getPPForAccuracy(98, workingBeatmap, beatmap, mods, maxCombo, score, attributes, ruleset);
            (double pp97, var aimpp97, var tappp97, var accpp97) =  getPPForAccuracy(97, workingBeatmap, beatmap, mods, maxCombo, score, attributes, ruleset);
            (double pp96, var aimpp96, var tappp96, var accpp96) =  getPPForAccuracy(96, workingBeatmap, beatmap, mods, maxCombo, score, attributes, ruleset);
            (double pp95, var aimpp95, var tappp95, var accpp95) =  getPPForAccuracy(95, workingBeatmap, beatmap, mods, maxCombo, score, attributes, ruleset);
            (double pp94, var aimpp94, var tappp94, var accpp94) =  getPPForAccuracy(94, workingBeatmap, beatmap, mods, maxCombo, score, attributes, ruleset);
            (double pp93, var aimpp93, var tappp93, var accpp93) =  getPPForAccuracy(93, workingBeatmap, beatmap, mods, maxCombo, score, attributes, ruleset);
            (double pp92, var aimpp92, var tappp92, var accpp92) =  getPPForAccuracy(92, workingBeatmap, beatmap, mods, maxCombo, score, attributes, ruleset);
            (double pp91, var aimpp91, var tappp91, var accpp91) =  getPPForAccuracy(91, workingBeatmap, beatmap, mods, maxCombo, score, attributes, ruleset);
            (double pp90, var aimpp90, var tappp90, var accpp90) =  getPPForAccuracy(90, workingBeatmap, beatmap, mods, maxCombo, score, attributes, ruleset);

            var misspp = new List<ComboGraph>();

            var objects = beatmap.HitObjects.Select(x => new OsuHitObjectWithCombo((OsuHitObject)x)).ToList();
            
            foreach (var c in beatmap.HitObjects)
            {
                if (c.NestedHitObjects.Count > 0)
                    foreach (var nestedObj in c.NestedHitObjects)
                        if (nestedObj is SliderTailCircle || nestedObj is RepeatPoint || nestedObj is SliderTick)
                            objects.Add(new OsuHitObjectWithCombo((OsuHitObject)nestedObj));
            }

            objects = objects.OrderBy(x => x.StartTime).ToList();

            for (int i = 0; i < objects.Count; i++)
                objects[i].Combo = i + 1;

            while (objects[objects.Count - 1].OriginalType == typeof(SliderTailCircle)
                   || objects[objects.Count - 1].OriginalType == typeof(SliderTick)
                   || objects[objects.Count - 1].OriginalType == typeof(RepeatPoint))
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

            var obj = new
            {
                Id = mapId,
                BeatmapSetId = beatmap.BeatmapInfo.BeatmapSet?.OnlineBeatmapSetID,
                Title = workingBeatmap.BeatmapInfo.ToString(),
                PP = new [] { pp90, pp91, pp92, pp93, pp94, pp95, pp96, pp97, pp98, pp99, pp100 },
                AimPP = new[] { aimpp90, aimpp91, aimpp92, aimpp93, aimpp94, aimpp95, aimpp96, aimpp97, aimpp98, aimpp99, aimpp100 },
                TapPP = new[] { tappp90, tappp91, tappp92, tappp93, tappp94, tappp95, tappp96, tappp97, tappp98, tappp99, tappp100 },
                AccPP = new[] { accpp90, accpp91, accpp92, accpp93, accpp94, accpp95, accpp96, accpp97, accpp98, accpp99, accpp100 },
                MissPP = misspp,
                Stars = attributes.StarRating,
                AimSR = attributes.AimSR,
                TapSR = attributes.TapSR,
                FingerControlSR = attributes.FingerControlSR,
                Mods = mods
            };

            var json = JsonConvert.SerializeObject(obj, new JsonSerializerSettings { Culture = CultureInfo.InvariantCulture });

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

        private (double, double, double, double) getPPForAccuracy(double acc, ProcessorWorkingBeatmap workingBeatmap, IBeatmap beatmap, Mod[] mods, int maxCombo, int score, DifficultyAttributes attributes, Ruleset ruleset)
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
            return (Math.Round(pp, 2), Math.Round(diffCats["Aim"], 2), Math.Round(diffCats["Tap"], 2), Math.Round(diffCats["Accuracy"], 2));
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
