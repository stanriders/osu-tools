
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using osu.Game.Beatmaps.Legacy;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Osu.Mods;

namespace PerformanceCalculator.Caching
{
    public class DiffAttributesDbModel
    {
        public long MapId { get; set; }

        public DateTime UpdateDate { get; set; }

        public LegacyMods Mods { get; set; }

        public double TapSR { get; set; }
        public double TapDiff { get; set; }
        public double StreamNoteCount { get; set; }
        public string MashLevels { get; set; }
        public string TapSkills { get; set; }

        public double FingerControlSR { get; set; }
        public double FingerControlDiff { get; set; }

        public double AimSR { get; set; }
        public double AimDiff { get; set; }
        public double AimHiddenFactor { get; set; }
        public string ComboTPs { get; set; }
        public string MissTPs { get; set; }
        public string MissCounts { get; set; }
        public double CheeseNoteCount { get; set; }
        public string CheeseLevels { get; set; }
        public string CheeseFactors { get; set; }

        public double Length { get; set; }
        public double ApproachRate { get; set; }
        public double OverallDifficulty { get; set; }
        public int MaxCombo { get; set; }
        public double StarRating { get; set; }

        public DiffAttributesDbModel() {}

        public DiffAttributesDbModel(OsuDifficultyAttributes osuAttrs)
        {
            FromOsuDifficultyAttributes(osuAttrs);
        }

        public void FromOsuDifficultyAttributes(OsuDifficultyAttributes osuAttrs)
        {
            TapSR = osuAttrs.TapSR;
            TapDiff = osuAttrs.TapDiff;
            StreamNoteCount = osuAttrs.StreamNoteCount;
            MashLevels = fromDoubleArray(osuAttrs.MashLevels);
            TapSkills = fromDoubleArray(osuAttrs.TapSkills);

            FingerControlSR = osuAttrs.FingerControlSR;
            FingerControlDiff = osuAttrs.FingerControlDiff;

            AimSR = osuAttrs.AimSR;
            AimDiff = osuAttrs.AimDiff;
            AimHiddenFactor = osuAttrs.AimHiddenFactor;
            ComboTPs = fromDoubleArray(osuAttrs.ComboTPs);
            MissTPs = fromDoubleArray(osuAttrs.MissTPs);
            MissCounts = fromDoubleArray(osuAttrs.MissCounts);
            CheeseNoteCount = osuAttrs.CheeseNoteCount;
            CheeseLevels = fromDoubleArray(osuAttrs.CheeseLevels);
            CheeseFactors = fromDoubleArray(osuAttrs.CheeseFactors);

            Length = osuAttrs.Length;
            ApproachRate = osuAttrs.ApproachRate;
            OverallDifficulty = osuAttrs.OverallDifficulty;
            MaxCombo = osuAttrs.MaxCombo;

            StarRating = osuAttrs.StarRating;
        }

        public OsuDifficultyAttributes ToOsuDifficultyAttributes()
        {
            return new OsuDifficultyAttributes
            {
                TapSR = TapSR,
                TapDiff = TapDiff,
                StreamNoteCount = StreamNoteCount,
                MashLevels = toDoubleArray(MashLevels),
                TapSkills = toDoubleArray(TapSkills),

                FingerControlSR = FingerControlSR,
                FingerControlDiff = FingerControlDiff,

                AimSR = AimSR,
                AimDiff = AimDiff,
                AimHiddenFactor = AimHiddenFactor,
                ComboTPs = toDoubleArray(ComboTPs),
                MissTPs = toDoubleArray(MissTPs),
                MissCounts = toDoubleArray(MissCounts),
                CheeseNoteCount = CheeseNoteCount,
                CheeseLevels = toDoubleArray(CheeseLevels),
                CheeseFactors = toDoubleArray(CheeseFactors),

                Length = Length,
                ApproachRate = ApproachRate,
                OverallDifficulty = OverallDifficulty,
                MaxCombo = MaxCombo,

                StarRating = StarRating,
                Mods = convertLegacyMods(Mods).ToArray()
            };
        }

        private IEnumerable<Mod> convertLegacyMods(LegacyMods mods)
        {
            if (mods.HasFlag(LegacyMods.Nightcore))
                yield return new OsuModNightcore();
            else if (mods.HasFlag(LegacyMods.DoubleTime))
                yield return new OsuModDoubleTime();

            if (mods.HasFlag(LegacyMods.Autopilot))
                yield return new OsuModAutopilot();

            if (mods.HasFlag(LegacyMods.Autoplay))
                yield return new OsuModAutoplay();

            if (mods.HasFlag(LegacyMods.Easy))
                yield return new OsuModEasy();

            if (mods.HasFlag(LegacyMods.Flashlight))
                yield return new OsuModFlashlight();

            if (mods.HasFlag(LegacyMods.HalfTime))
                yield return new OsuModHalfTime();

            if (mods.HasFlag(LegacyMods.HardRock))
                yield return new OsuModHardRock();

            if (mods.HasFlag(LegacyMods.Hidden))
                yield return new OsuModHidden();

            if (mods.HasFlag(LegacyMods.NoFail))
                yield return new OsuModNoFail();

            if (mods.HasFlag(LegacyMods.Perfect))
                yield return new OsuModPerfect();

            if (mods.HasFlag(LegacyMods.Relax))
                yield return new OsuModRelax();

            if (mods.HasFlag(LegacyMods.SpunOut))
                yield return new OsuModSpunOut();

            if (mods.HasFlag(LegacyMods.SuddenDeath))
                yield return new OsuModSuddenDeath();

            if (mods.HasFlag(LegacyMods.Target))
                yield return new OsuModTarget();

            if (mods.HasFlag(LegacyMods.TouchDevice))
                yield return new OsuModTouchDevice();
        }

        private string fromDoubleArray(double[] arr)
        {
            return string.Join(' ', arr);
        }

        private double[] toDoubleArray(string arr)
        {
            return arr.Split(' ').Select(x=> double.Parse(x, CultureInfo.InvariantCulture)).ToArray();
        }
    }

    public class DifficultyAttributeCacheContext : DbContext
    {
        private const string connection_string = "Filename=./difficultycache.db";

        public DifficultyAttributeCacheContext()
        {
            Database.EnsureCreated();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite(connection_string);
            optionsBuilder.EnableSensitiveDataLogging();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DiffAttributesDbModel>().HasKey(p => new { p.MapId, p.Mods });
            modelBuilder.Entity<DiffAttributesDbModel>().HasIndex(p => new { p.MapId, p.Mods }).IsUnique();
        }

        public DbSet<DiffAttributesDbModel> Attributes { get; set; }
    }
}
