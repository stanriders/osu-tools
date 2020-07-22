// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Beatmaps;

namespace PerformanceCalculator.Profile
{
    /// <summary>
    /// Holds the live pp value, beatmap name, and mods for a user play.
    /// </summary>
    public class UserPlayInfo
    {
        public double LocalPP;
        public double LivePP;
        public double AimPP;
        public double TapPP;
        public double AccPP;
        public double ReadingPP;

        public string BeatmapId;
        public BeatmapInfo Beatmap;

        public string Mods;

        public string Accuracy;
        public string Combo;
        public string Misses;
    }
}
