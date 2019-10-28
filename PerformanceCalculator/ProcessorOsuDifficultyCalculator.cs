
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty;

namespace PerformanceCalculator
{
    public class ProcessorOsuDifficultyCalculator : OsuDifficultyCalculator
    {
        public ProcessorOsuDifficultyCalculator(Ruleset ruleset, WorkingBeatmap beatmap)
            : base(ruleset, beatmap)
        {
        }

        protected override DifficultyAttributes Calculate(IBeatmap beatmap, Mod[] mods, double clockRate)
        {
            var result = base.Calculate(beatmap, mods, clockRate);

            var json = JsonConvert.SerializeObject(result);
            File.WriteAllText($"cache/{beatmap.BeatmapInfo.OnlineBeatmapID}{string.Join(string.Empty, mods.Select(x => x.Acronym))}_diff.json", json);

            return result;
        }
    }
}
