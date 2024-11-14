// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Scoring;
using osu.Game.Skinning;
using osu.Game.Utils;

namespace PerformanceCalculatorGUI
{
    public static class RulesetHelper
    {
        /// <summary>
        /// Transforms a given <see cref="Mod"/> combination into one which is applicable to legacy scores.
        /// This is used to match osu!stable/osu!web calculations for the time being, until such a point that these mods do get considered.
        /// </summary>
        public static Mod[] ConvertToLegacyDifficultyAdjustmentMods(Ruleset ruleset, Mod[] mods)
        {
            var beatmap = new EmptyWorkingBeatmap
            {
                BeatmapInfo =
                {
                    Ruleset = ruleset.RulesetInfo,
                    Difficulty = new BeatmapDifficulty()
                }
            };

            var allMods = ruleset.CreateAllMods().ToArray();

            var allowedMods = ModUtils.FlattenMods(
                                          ruleset.CreateDifficultyCalculator(beatmap).CreateDifficultyAdjustmentModCombinations())
                                      .Select(m => m.GetType())
                                      .Distinct()
                                      .ToHashSet();

            // Special case to allow either DT or NC.
            if (allowedMods.Any(type => type.IsSubclassOf(typeof(ModDoubleTime))) && mods.Any(m => m is ModNightcore))
                allowedMods.Add(allMods.Single(m => m is ModNightcore).GetType());

            var result = new List<Mod>();

            var classicMod = allMods.SingleOrDefault(m => m is ModClassic);
            if (classicMod != null)
                result.Add(classicMod);

            result.AddRange(mods.Where(m => allowedMods.Contains(m.GetType())));

            return result.ToArray();
        }

        public static Ruleset GetRulesetFromLegacyID(int id)
        {
            return id switch
            {
                0 => new OsuRuleset(),
                _ => throw new ArgumentException("Invalid ruleset ID provided.")
            };
        }

        public static double GetAccuracyForRuleset(RulesetInfo ruleset, Dictionary<HitResult, int> statistics)
        {
            return ruleset.OnlineID switch
            {
                0 => getOsuAccuracy(statistics),
                _ => 0.0
            };
        }

        private static double getOsuAccuracy(Dictionary<HitResult, int> statistics)
        {
            var countGreat = statistics[HitResult.Great];
            var countGood = statistics[HitResult.Ok];
            var countMeh = statistics[HitResult.Meh];
            var countMiss = statistics[HitResult.Miss];
            var total = countGreat + countGood + countMeh + countMiss;

            return (double)((6 * countGreat) + (2 * countGood) + countMeh) / (6 * total);
        }

        private class EmptyWorkingBeatmap : WorkingBeatmap
        {
            public EmptyWorkingBeatmap()
                : base(new BeatmapInfo(), null)
            {
            }

            protected override IBeatmap GetBeatmap() => throw new NotImplementedException();

            public override Texture GetBackground() => throw new NotImplementedException();

            protected override Track GetBeatmapTrack() => throw new NotImplementedException();

            protected override ISkin GetSkin() => throw new NotImplementedException();

            public override Stream GetStream(string storagePath) => throw new NotImplementedException();
        }
    }
}
