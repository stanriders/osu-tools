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
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

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

            var diffCache = $"cache/{mapId}{string.Join(string.Empty, mods.Select(x => x.Acronym))}_diff.json";

            DifficultyAttributes attributes;

            if (File.Exists(diffCache))
            {
                var diffCalcDate = File.GetLastWriteTime(diffCache).ToUniversalTime();
                var calcUpdateDate = File.GetLastWriteTime("osu.Game.Rulesets.Osu.dll").ToUniversalTime();

                if (diffCalcDate > calcUpdateDate)
                {
                    var file = File.ReadAllText(diffCache);
                    file = file.Replace("Mods", "nommods"); // stupid hack!!!!!!!!!!
                    var attr = JsonConvert.DeserializeObject<OsuDifficultyAttributes>(file);
                    attr.Mods = mods;
                    attributes = attr;
                }
                else
                {
                    attributes = new ProcessorOsuDifficultyCalculator(ruleset, workingBeatmap).Calculate(mods);
                }
            }
            else
                attributes = new ProcessorOsuDifficultyCalculator(ruleset, workingBeatmap).Calculate(mods);

            double pp100 = getPPForAccuracy(100, workingBeatmap, beatmap, mods, maxCombo, score, attributes, ruleset);
            double pp99 =  getPPForAccuracy(99, workingBeatmap, beatmap, mods, maxCombo, score, attributes, ruleset);
            double pp98 =  getPPForAccuracy(98, workingBeatmap, beatmap, mods, maxCombo, score, attributes, ruleset);
            double pp97 =  getPPForAccuracy(97, workingBeatmap, beatmap, mods, maxCombo, score, attributes, ruleset);
            double pp96 =  getPPForAccuracy(96, workingBeatmap, beatmap, mods, maxCombo, score, attributes, ruleset);
            double pp95 =  getPPForAccuracy(95, workingBeatmap, beatmap, mods, maxCombo, score, attributes, ruleset);
            double pp94 =  getPPForAccuracy(94, workingBeatmap, beatmap, mods, maxCombo, score, attributes, ruleset);
            double pp93 =  getPPForAccuracy(93, workingBeatmap, beatmap, mods, maxCombo, score, attributes, ruleset);
            double pp92 =  getPPForAccuracy(92, workingBeatmap, beatmap, mods, maxCombo, score, attributes, ruleset);
            double pp91 =  getPPForAccuracy(91, workingBeatmap, beatmap, mods, maxCombo, score, attributes, ruleset);
            double pp90 =  getPPForAccuracy(90, workingBeatmap, beatmap, mods, maxCombo, score, attributes, ruleset);

            var obj = new
            {
                Id = mapId,
                BeatmapSetId = beatmap.BeatmapInfo.BeatmapSet?.OnlineBeatmapSetID,
                Title = workingBeatmap.BeatmapInfo.ToString(),
                PP = new [] { pp90, pp91, pp92, pp93, pp94, pp95, pp96, pp97, pp98, pp99, pp100 },
                Stars = attributes.StarRating,
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

        private double getPPForAccuracy(double acc, ProcessorWorkingBeatmap workingBeatmap, IBeatmap beatmap, Mod[] mods, int maxCombo, int score, DifficultyAttributes attributes, Ruleset ruleset)
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
            return Math.Round(perfCalc.Calculate(), 2);
        }

        protected abstract void WritePlayInfo(ScoreInfo scoreInfo, IBeatmap beatmap);

        protected abstract int GetMaxCombo(IBeatmap beatmap);

        protected abstract Dictionary<HitResult, int> GenerateHitResults(double accuracy, IBeatmap beatmap, int countMiss, int? countMeh, int? countGood);

        protected virtual double GetAccuracy(Dictionary<HitResult, int> statistics) => 0;

        protected void WriteAttribute(string name, string value) => Console.WriteLine($"{name.PadRight(15)}: {value}");
    }
}
