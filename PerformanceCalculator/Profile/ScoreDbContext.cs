using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using osu.Game.Beatmaps.Legacy;

namespace PerformanceCalculator.Profile
{
    public class ScoreDbModel
    {
        [Key]
        public long score_id { get; set; }
        public long beatmap_id { get; set; }
        public long user_id { get; set; }
        public long score { get; set; }
        public int maxcombo { get; set; }
        public string rank { get; set; }
        public int count50 { get; set; }
        public int count100 { get; set; }
        public int count300 { get; set; }
        public int countmiss { get; set; }
        public int countgeki { get; set; }
        public int countkatu { get; set; }
        public int perfect { get; set; }
        public LegacyMods enabled_mods { get; set; }
        public DateTime date { get; set; }
        public double? pp { get; set; }
        public int replay { get; set; }
        public int hidden { get; set; }
        public string country_acronym { get; set; }
    }

    public class ScoreDbContext : DbContext
    {
        private const string connection_string = "Filename=./osu_scores_high.sql.db";

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite(connection_string);
        }

        public DbSet<ScoreDbModel> osu_scores_high { get; set; }
    }
}
