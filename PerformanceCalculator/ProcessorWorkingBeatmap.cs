﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Network;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;

namespace PerformanceCalculator
{
    /// <summary>
    /// A <see cref="WorkingBeatmap"/> which reads from a .osu file.
    /// </summary>
    public class ProcessorWorkingBeatmap : WorkingBeatmap
    {
        private readonly Beatmap beatmap;

        /// <summary>
        /// Constructs a new <see cref="ProcessorWorkingBeatmap"/> from a .osu file.
        /// </summary>
        /// <param name="file">The .osu file.</param>
        /// <param name="beatmapId">An optional beatmap ID (for cases where .osu file doesn't have one).</param>
        public ProcessorWorkingBeatmap(string file, int? beatmapId = null)
            : this(readFromFile(file), beatmapId)
        {
        }

        private ProcessorWorkingBeatmap(Beatmap beatmap, int? beatmapId = null)
            : base(beatmap.BeatmapInfo, null)
        {
            this.beatmap = beatmap;

            beatmap.BeatmapInfo.Ruleset = LegacyHelper.GetRulesetFromLegacyID(beatmap.BeatmapInfo.RulesetID).RulesetInfo;

            if (beatmapId.HasValue)
                beatmap.BeatmapInfo.OnlineBeatmapID = beatmapId;
        }

        private static Beatmap readFromFile(string filename)
        {
            try
            {
                using (var stream = File.OpenRead(filename))
                using (var streamReader = new LineBufferedReader(stream))
                    return Decoder.GetDecoder<Beatmap>(streamReader).Decode(streamReader);
            }
            catch (Exception)
            {
                Console.WriteLine($"Failed to decode {filename}! Redownloading...");
                new FileWebRequest(filename, $"https://osu.ppy.sh/osu/{Path.GetFileNameWithoutExtension(filename)}").Perform();

                using (var stream = File.OpenRead(filename))
                using (var streamReader = new LineBufferedReader(stream))
                    return Decoder.GetDecoder<Beatmap>(streamReader).Decode(streamReader);
            }
        }

        protected override IBeatmap GetBeatmap() => beatmap;
        protected override Texture GetBackground() => null;
        protected override Track GetTrack() => null;
    }
}
