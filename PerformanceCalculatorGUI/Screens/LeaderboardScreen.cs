// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Game.Graphics.Containers;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using PerformanceCalculatorGUI.Components;
using PerformanceCalculatorGUI.Configuration;

namespace PerformanceCalculatorGUI.Screens
{
    public partial class LeaderboardScreen : PerformanceCalculatorScreen
    {
        [Cached]
        private OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Green);

        private VerboseLoadingLayer loadingLayer;

        private FillFlowContainer scores;

        private CancellationTokenSource calculationCancellatonToken;

        public override bool ShouldShowConfirmationDialogOnSwitch => false;

        [Resolved]
        private NotificationDisplay notificationDisplay { get; set; }

        [Resolved]
        private APIManager apiManager { get; set; }

        [Resolved]
        private Bindable<RulesetInfo> ruleset { get; set; }

        [Resolved]
        private SettingsManager configManager { get; set; }

        public LeaderboardScreen()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                new OsuScrollContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        scores = new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                        }
                    }
                },
                loadingLayer = new VerboseLoadingLayer(true)
                {
                    RelativeSizeAxes = Axes.Both
                }
            };

            calculate();

            if (RuntimeInfo.IsDesktop)
                HotReloadCallbackReceiver.CompilationFinished += _ => Schedule(calculate);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            calculationCancellatonToken?.Cancel();
            calculationCancellatonToken?.Dispose();
            calculationCancellatonToken = null;
        }

        private void calculate()
        {
            calculationCancellatonToken?.Cancel();
            calculationCancellatonToken?.Dispose();

            loadingLayer.Show();

            scores.Clear();

            calculationCancellatonToken = new CancellationTokenSource();
            var token = calculationCancellatonToken.Token;

            Task.Run(async () =>
            {
                Schedule(() => loadingLayer.Text.Value = "Calculating top scores...");

                var apiScores = await apiManager.GetJsonFromApi<List<RXScore>>("scores?take=500").ConfigureAwait(false);

                var plays = new List<ExtendedScore>();
                var rulesetInstance = ruleset.Value.CreateInstance();

                foreach (var score in apiScores)
                {
                    if (token.IsCancellationRequested)
                        return;

                    var working = ProcessorWorkingBeatmap.FromFileOrId(score.BeatmapId.ToString(), cachePath: configManager.GetBindable<string>(Settings.CachePath).Value);

                    Schedule(() => loadingLayer.Text.Value = $"Calculating {working.Metadata}");

                    Mod[] mods = GetMods(rulesetInstance, score.Mods);

                    score.BeatmapInfo = working.BeatmapInfo;

                    var scoreInfo = new ScoreInfo(working.BeatmapInfo, ruleset.Value)
                    {
                        Accuracy = score.Accuracy,
                        MaxCombo = score.Combo,
                        Statistics = new Dictionary<HitResult, int>
                        {
                            { HitResult.Great, score.Count300 },
                            { HitResult.Ok, score.Count100 },
                            { HitResult.Meh, score.Count50 },
                            { HitResult.Miss, score.CountMiss }
                        },
                        Mods = mods,
                        TotalScore = score.TotalScore
                    };

                    if (score.SliderEnds != null)
                    {
                        scoreInfo.Statistics.Add(HitResult.SliderTailHit, score.SliderEnds.Value);
                    }

                    if (score.SliderTicks != null)
                    {
                        scoreInfo.Statistics.Add(HitResult.LargeTickHit, score.SliderTicks.Value);
                    }

                    if (score.SpinnerBonus != null)
                    {
                        scoreInfo.Statistics.Add(HitResult.LargeBonus, score.SpinnerBonus.Value);
                    }

                    if (score.SpinnerSpins != null)
                    {
                        scoreInfo.Statistics.Add(HitResult.SmallBonus, score.SpinnerSpins.Value);
                    }

                    if (score.LegacySliderEnds != null)
                    {
                        scoreInfo.Statistics.Add(HitResult.SmallTickHit, score.LegacySliderEnds.Value);
                    }

                    if (score.LegacySliderEndMisses != null)
                    {
                        scoreInfo.Statistics.Add(HitResult.SmallTickMiss, score.LegacySliderEndMisses.Value);
                    }

                    if (score.SliderTickMisses != null)
                    {
                        scoreInfo.Statistics.Add(HitResult.LargeTickMiss, score.SliderTickMisses.Value);
                    }

                    var difficultyCalculator = rulesetInstance.CreateDifficultyCalculator(working);
                    var difficultyAttributes = difficultyCalculator.Calculate(mods);
                    var performanceCalculator = rulesetInstance.CreatePerformanceCalculator();
                    if (performanceCalculator == null)
                        continue;

                    double? livePp = score.Pp;
                    var perfAttributes = await performanceCalculator.CalculateAsync(scoreInfo, difficultyAttributes, token).ConfigureAwait(false);
                    score.Pp = perfAttributes.Total;

                    var extendedScore = new ExtendedScore(score, livePp, perfAttributes);
                    plays.Add(extendedScore);
                }

                var localOrdered = plays.OrderByDescending(x => x.SoloScore.Pp).ToList();
                var liveOrdered = plays.OrderByDescending(x => x.LivePP ?? 0).ToList();

                Schedule(() =>
                {
                    foreach (var play in plays)
                    {
                        scores.Add(new ExtendedProfileScore(play));

                        if (play.LivePP != null)
                        {
                            play.Position.Value = localOrdered.IndexOf(play) + 1;
                            play.PositionChange.Value = liveOrdered.IndexOf(play) - localOrdered.IndexOf(play);
                        }
                    }
                });
            }, token).ContinueWith(t =>
            {
                Logger.Log(t.Exception?.ToString(), level: LogLevel.Error);
                notificationDisplay.Display(new Notification(t.Exception?.Flatten().Message));
            }, TaskContinuationOptions.OnlyOnFaulted).ContinueWith(t =>
            {
                Schedule(() =>
                {
                    loadingLayer.Hide();
                });
            }, token);
        }

        private static Mod[] GetMods(Ruleset ruleset, string[] modNames)
        {
            var mods = new List<Mod>();

            foreach (var modName in modNames)
            {
                var mod = ruleset.CreateModFromAcronym(modName);

                if (mod == null)
                {
                    var modNameSplit = modName.Split("x");

                    mod = ruleset.CreateModFromAcronym(modNameSplit[0]);

                    if (mod is ModRateAdjust speedAdjustMod)
                    {
                        speedAdjustMod.SpeedChange.Value = double.Parse(modNameSplit[1]);
                        mods.Add(speedAdjustMod);
                    }
                    else
                    {
                        throw new ArgumentException($"Invalid mod provided: {modName}");
                    }
                }
                else
                {
                    mods.Add(mod);
                }
            }

            return mods.ToArray();
        }
    }
}
