using System;
using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Legacy;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets;
using osu.Game.Scoring;
using SQLite;

namespace PerformanceCalculatorGUI
{
    [Table("osu_scores_high")]
    public class DbScore
    {
        [Column("score_id")]
        public long score_id { get; set; }

        [Column("beatmap_id")]
        public int beatmap_id { get; set; }

        [Column("maxcombo")]
        public int maxcombo { get; set; }

        [Column("count50")]
        public int count50 { get; set; }

        [Column("count100")]
        public int count100 { get; set; }

        [Column("count300")]
        public int count300 { get; set; }

        [Column("countmiss")]
        public int countmiss { get; set; }

        [Column("enabled_mods")]
        public LegacyMods enabled_mods { get; set; }

        [Column("date")]
        public string date { get; set; }

        [Column("pp")]
        public double pp { get; set; }

        [Column("rank")]
        public ScoreRank rank { get; set; }

        public BeatmapInfo BeatmapInfo { get; set; }

        public double Accuracy { get; set; }

        public ScoreInfo ToScoreInfo(Mod[] mods, IBeatmapInfo? beatmap = null)
        {
            ScoreInfo scoreInfo1 = new ScoreInfo();
            scoreInfo1.OnlineID = score_id;
            scoreInfo1.LegacyOnlineID = score_id;
            scoreInfo1.IsLegacyScore = true;
            scoreInfo1.User = new APIUser() { Id = 1 };
            scoreInfo1.BeatmapInfo = new BeatmapInfo()
            {
                OnlineID = this.beatmap_id
            };
            scoreInfo1.Ruleset = new RulesetInfo()
            {
                OnlineID = 0
            };
            scoreInfo1.Passed = true;
            scoreInfo1.TotalScore = 1;
            scoreInfo1.TotalScoreWithoutMods = 1;
            int? legacyTotalScore = 1;
            scoreInfo1.LegacyTotalScore = legacyTotalScore.HasValue ? new long?((long)legacyTotalScore.GetValueOrDefault()) : new long?();
            scoreInfo1.Accuracy = 1;
            scoreInfo1.MaxCombo = this.maxcombo;
            scoreInfo1.Rank = rank;
            scoreInfo1.Statistics = new Dictionary<HitResult, int>() { {HitResult.Great, count300}, { HitResult.Ok, count100}, { HitResult.Meh, count50}, { HitResult.Miss, countmiss} };
            scoreInfo1.MaximumStatistics = null;
            scoreInfo1.Date = DateTime.Parse(date);
            scoreInfo1.HasOnlineReplay = false;
            scoreInfo1.Mods = mods;
            scoreInfo1.PP = this.pp;
            scoreInfo1.Ranked = true;
            ScoreInfo scoreInfo2 = scoreInfo1;
            if (beatmap is BeatmapInfo beatmapInfo)
                scoreInfo2.BeatmapInfo = beatmapInfo;
            else if (beatmap != null)
            {
                scoreInfo2.BeatmapInfo.Ruleset.OnlineID = beatmap.Ruleset.OnlineID;
                scoreInfo2.BeatmapInfo.Ruleset.Name = beatmap.Ruleset.Name;
                scoreInfo2.BeatmapInfo.Ruleset.ShortName = beatmap.Ruleset.ShortName;
            }
            return scoreInfo2;
        }
    }
}
