// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json;
using osu.Game.Scoring;
using osu.Game.Beatmaps;
using System.ComponentModel.DataAnnotations.Schema;

namespace PerformanceCalculatorGUI
{
    public class RXScore
    {
        public required long Id { get; set; }

        [ForeignKey("User")]
        public int UserId { get; set; }

        public required int BeatmapId { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("grade", DefaultValueHandling = DefaultValueHandling.Include)]
        public ScoreRank Rank { get; set; }

        public required double Accuracy { get; set; }

        public required int Combo { get; set; }

        public required string[] Mods { get; set; } = Array.Empty<string>();

        public required DateTime Date { get; set; }

        public required int TotalScore { get; set; }

        public required int Count50 { get; set; }

        public required int Count100 { get; set; }

        public required int Count300 { get; set; }

        public required int CountMiss { get; set; }

        public required int? SpinnerBonus { get; set; }

        public required int? SpinnerSpins { get; set; }

        public required int? LegacySliderEnds { get; set; }

        public required int? SliderTicks { get; set; }

        public required int? SliderEnds { get; set; }

        public required int? LegacySliderEndMisses { get; set; }

        public required int? SliderTickMisses { get; set; }

        public double? Pp { get; set; }

        public IBeatmapInfo? BeatmapInfo { get; set; }
    }
}
