// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osu.Framework.Logging;
using osu.Framework.Screens;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Scoring;
using osu.Game.Users;
using osuTK.Input;
using PerformanceCalculatorGUI.Components;
using PerformanceCalculatorGUI.Configuration;
using SQLite;
using ButtonState = PerformanceCalculatorGUI.Components.ButtonState;

namespace PerformanceCalculatorGUI.Screens
{
    public partial class ProfileScreen : Screen
    {
        [Cached]
        private OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Plum);

        private StatefulButton calculationButton;
        private VerboseLoadingLayer loadingLayer;

        private FillFlowContainer<ExtendedProfileScore> scores;

        private UserCard userPanel;

        private CancellationTokenSource calculationCancellatonToken;

        [Resolved]
        private NotificationDisplay notificationDisplay { get; set; }

        [Resolved]
        private SettingsManager configManager { get; set; }

        private const float username_container_height = 40;
        private const int user_id = 5100305;

        private LabelledNumberBox scoreAmountNumberBox;

        public ProfileScreen()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var player = new APIUser()
            {
                Username = "vetochka",
                Id = user_id,
                AvatarUrl = "https://i.ytimg.com/vi/YFB_Dms-_Js/hqdefault.jpg",
                CountryCode = CountryCode.RU
            };

            InternalChildren = new Drawable[]
            {
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    ColumnDimensions = new[] { new Dimension() },
                    RowDimensions = new[]
                    {
                        new Dimension(GridSizeMode.Absolute, username_container_height),
                        new Dimension()
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new GridContainer
                            {
                                Name = "Settings",
                                Height = username_container_height,
                                RelativeSizeAxes = Axes.X,
                                ColumnDimensions = new[]
                                {
                                    new Dimension(),
                                    new Dimension(GridSizeMode.Absolute, 200),
                                    new Dimension(GridSizeMode.AutoSize)
                                },
                                RowDimensions = new[]
                                {
                                    new Dimension(GridSizeMode.AutoSize)
                                },
                                Content = new[]
                                {
                                    new Drawable[]
                                    {
                                        new Container
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Child = userPanel = new UserCard(player)
                                            {
                                                RelativeSizeAxes = Axes.X
                                            }
                                        },
                                        scoreAmountNumberBox = new LabelledNumberBox()
                                        {
                                            Label = "Scores",
                                            Anchor = Anchor.TopLeft,
                                            PlaceholderText = "0",
                                        },
                                        calculationButton = new StatefulButton("Start calculation")
                                        {
                                            Width = 150,
                                            Height = username_container_height,
                                            Action = calculateProfile
                                        }
                                    }
                                }
                            },
                        },
                        new Drawable[]
                        {
                            new OsuScrollContainer(Direction.Vertical)
                            {
                                RelativeSizeAxes = Axes.Both,
                                Child = scores = new FillFlowContainer<ExtendedProfileScore>
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Vertical
                                }
                            }
                        },
                    }
                },
                loadingLayer = new VerboseLoadingLayer(true)
                {
                    RelativeSizeAxes = Axes.Both
                }
            };
        }

        private void calculateProfile()
        {
            calculationCancellatonToken?.Cancel();
            calculationCancellatonToken?.Dispose();

            loadingLayer.Show();
            calculationButton.State.Value = ButtonState.Loading;

            scores.Clear();

            calculationCancellatonToken = new CancellationTokenSource();
            var token = calculationCancellatonToken.Token;

            Task.Run(async () =>
            {
                Schedule(() => loadingLayer.Text.Value = "Getting user data...");

                if (token.IsCancellationRequested)
                    return;

                var plays = new List<ExtendedScore>();

                var rulesetInstance = new OsuRuleset();

                Schedule(() => loadingLayer.Text.Value = "Calculating top scores...");

                var db = new SQLiteConnection("osu_scores_high.vetochka.db");

                var apiScores = db.Table<DbScore>().OrderByDescending(x => x.pp);

                if (int.TryParse(scoreAmountNumberBox.Current.Value, out var amount) && amount > 0)
                    apiScores = apiScores.Take(amount);

                foreach (var score in apiScores)
                {
                    if (token.IsCancellationRequested)
                        return;

                    var working = ProcessorWorkingBeatmap.FromFileOrId(score.beatmap_id.ToString(), cachePath: configManager.GetBindable<string>(Settings.CachePath).Value);

                    Schedule(() => loadingLayer.Text.Value = $"Calculating {working.Metadata}");

                    Mod[] mods = rulesetInstance.ConvertFromLegacyMods(score.enabled_mods).ToArray();

                    score.BeatmapInfo = working.BeatmapInfo;
                    score.Accuracy = RulesetHelper.GetAccuracyForRuleset(rulesetInstance.RulesetInfo, new Dictionary<HitResult, int>()
                    {
                        { HitResult.Great, score.count300 },
                        { HitResult.Ok, score.count100 },
                        { HitResult.Meh, score.count50 },
                        { HitResult.Miss, score.countmiss }
                    });

                    var scoreInfo = score.ToScoreInfo(mods, working.BeatmapInfo);

                    var parsedScore = new ProcessorScoreDecoder(working).Parse(scoreInfo);

                    var difficultyCalculator = rulesetInstance.CreateDifficultyCalculator(working);
                    var difficultyAttributes = difficultyCalculator.Calculate(RulesetHelper.ConvertToLegacyDifficultyAdjustmentMods(rulesetInstance, mods));
                    var performanceCalculator = rulesetInstance.CreatePerformanceCalculator();

                    var livePp = score.pp;
                    var perfAttributes = await performanceCalculator?.CalculateAsync(parsedScore.ScoreInfo, difficultyAttributes, token)!;
                    score.pp = perfAttributes?.Total ?? 0.0;

                    var extendedScore = new ExtendedScore(score, livePp, perfAttributes);
                    plays.Add(extendedScore);

                    Schedule(() => scores.Add(new ExtendedProfileScore(extendedScore)));
                }

                if (token.IsCancellationRequested)
                    return;

                var localOrdered = plays.OrderByDescending(x => x.SoloScore.pp).ToList();
                var liveOrdered = plays.OrderByDescending(x => x.LivePP).ToList();

                Schedule(() =>
                {
                    foreach (var play in plays)
                    {
                        play.Position.Value = localOrdered.IndexOf(play) + 1;
                        play.PositionChange.Value = liveOrdered.IndexOf(play) - localOrdered.IndexOf(play);
                        scores.SetLayoutPosition(scores[liveOrdered.IndexOf(play)], localOrdered.IndexOf(play));
                    }
                });

                double totalLocalPP = 0;
                for (var i = 0; i < localOrdered.Count; i++)
                    totalLocalPP += Math.Pow(0.95, i) * (localOrdered[i].SoloScore.pp);

                var playcountBonusPP = (417.0 - 1.0 / 3.0) * (1.0 - Math.Pow(0.995, scores.Count));
                totalLocalPP += playcountBonusPP;

                Schedule(() =>
                {
                    userPanel.Data.Value = new UserCardData
                    {
                        LocalPP = totalLocalPP,
                        PlaycountPP = playcountBonusPP
                    };
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
                    calculationButton.State.Value = ButtonState.Done;
                });
            }, TaskContinuationOptions.None);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            calculationCancellatonToken?.Cancel();
            calculationCancellatonToken?.Dispose();
            calculationCancellatonToken = null;
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.Key == Key.Escape && !calculationCancellatonToken.IsCancellationRequested)
            {
                calculationCancellatonToken?.Cancel();
            }

            return base.OnKeyDown(e);
        }
    }
}
