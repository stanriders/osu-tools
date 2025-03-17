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
using osu.Framework.Input.Events;
using osu.Framework.Logging;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;
using PerformanceCalculatorGUI.Components;
using PerformanceCalculatorGUI.Components.TextBoxes;
using PerformanceCalculatorGUI.Configuration;
using ButtonState = PerformanceCalculatorGUI.Components.ButtonState;

namespace PerformanceCalculatorGUI.Screens
{
    public partial class ProfileScreen : PerformanceCalculatorScreen
    {
        [Cached]
        private OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Plum);

        private StatefulButton calculationButton;
        private SwitchButton includePinnedCheckbox;
        private SwitchButton onlyDisplayBestCheckbox;
        private VerboseLoadingLayer loadingLayer;

        private GridContainer layout;

        private FillFlowContainer<ExtendedProfileScore> scores;

        private LabelledTextBox usernameTextBox;
        private Container userPanelContainer;
        private UserCard userPanel;

        private string[] currentUsers = Array.Empty<string>();

        private CancellationTokenSource calculationCancellatonToken;

        private OverlaySortTabControl<ProfileSortCriteria> sortingTabControl;
        private readonly Bindable<ProfileSortCriteria> sorting = new Bindable<ProfileSortCriteria>(ProfileSortCriteria.Local);

        [Resolved]
        private NotificationDisplay notificationDisplay { get; set; }

        [Resolved]
        private APIManager apiManager { get; set; }

        [Resolved]
        private Bindable<RulesetInfo> ruleset { get; set; }

        [Resolved]
        private SettingsManager configManager { get; set; }

        [Resolved]
        private RulesetStore rulesets { get; set; }

        public override bool ShouldShowConfirmationDialogOnSwitch => false;

        private const float username_container_height = 40;

        public ProfileScreen()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                layout = new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    ColumnDimensions = new[] { new Dimension() },
                    RowDimensions = new[]
                    {
                        new Dimension(GridSizeMode.Absolute, username_container_height),
                        new Dimension(GridSizeMode.Absolute),
                        new Dimension(GridSizeMode.AutoSize),
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
                                        usernameTextBox = new ExtendedLabelledTextBox
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            Anchor = Anchor.TopLeft,
                                            Label = "Username(s)",
                                            PlaceholderText = "peppy, rloseise, peppy2",
                                            CommitOnFocusLoss = false
                                        },
                                        calculationButton = new StatefulButton("Start calculation")
                                        {
                                            Width = 150,
                                            Height = username_container_height,
                                            Action = () => { calculateProfiles(usernameTextBox.Current.Value.Split(", ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)); }
                                        }
                                    }
                                }
                            },
                        },
                        new Drawable[]
                        {
                            userPanelContainer = new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y
                            }
                        },
                        new Drawable[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Children = new Drawable[]
                                {
                                    new FillFlowContainer
                                    {
                                        AutoSizeAxes = Axes.Both,
                                        Direction = FillDirection.Horizontal,
                                        Margin = new MarginPadding { Vertical = 2, Left = 10 },
                                        Spacing = new Vector2(5),
                                        Children = new Drawable[]
                                        {
                                            includePinnedCheckbox = new SwitchButton
                                            {
                                                Anchor = Anchor.CentreLeft,
                                                Origin = Anchor.CentreLeft,
                                                Current = { Value = true },
                                            },
                                            new OsuSpriteText
                                            {
                                                Anchor = Anchor.CentreLeft,
                                                Origin = Anchor.CentreLeft,
                                                Font = OsuFont.Torus.With(weight: FontWeight.SemiBold, size: 14),
                                                UseFullGlyphHeight = false,
                                                Text = "Include pinned scores"
                                            },
                                            onlyDisplayBestCheckbox = new SwitchButton
                                            {
                                                Anchor = Anchor.CentreLeft,
                                                Origin = Anchor.CentreLeft,
                                                Current = { Value = true },
                                            },
                                            new OsuSpriteText
                                            {
                                                Anchor = Anchor.CentreLeft,
                                                Origin = Anchor.CentreLeft,
                                                Font = OsuFont.Torus.With(weight: FontWeight.SemiBold, size: 14),
                                                UseFullGlyphHeight = false,
                                                Text = "Only display best score on each beatmap"
                                            }
                                        }
                                    },
                                    sortingTabControl = new OverlaySortTabControl<ProfileSortCriteria>
                                    {
                                        Anchor = Anchor.CentreRight,
                                        Origin = Anchor.CentreRight,
                                        Margin = new MarginPadding { Right = 22 },
                                        Current = { BindTarget = sorting },
                                        Alpha = 0
                                    }
                                }
                            }
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

            usernameTextBox.OnCommit += (_, _) => { calculateProfiles(usernameTextBox.Current.Value.Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)); };
            sorting.ValueChanged += e => { updateSorting(e.NewValue); };
            includePinnedCheckbox.Current.ValueChanged += e => { calculateProfiles(currentUsers); };
            onlyDisplayBestCheckbox.Current.ValueChanged += e => { calculateProfiles(currentUsers); };

            if (RuntimeInfo.IsDesktop)
                HotReloadCallbackReceiver.CompilationFinished += _ => Schedule(() => { calculateProfiles(currentUsers); });
        }

        private void calculateProfiles(string[] usernames)
        {
            currentUsers = usernames.Distinct().ToArray();

            if (usernames.Length < 1)
            {
                usernameTextBox.FlashColour(Color4.Red, 1);
                return;
            }

            calculationCancellatonToken?.Cancel();
            calculationCancellatonToken?.Dispose();

            loadingLayer.Show();
            calculationButton.State.Value = ButtonState.Loading;

            scores.Clear();

            calculationCancellatonToken = new CancellationTokenSource();
            var token = calculationCancellatonToken.Token;

            Task.Run(async () =>
            {
                Schedule(() =>
                {
                    if (userPanel != null)
                        userPanelContainer.Remove(userPanel, true);

                    sortingTabControl.Alpha = 1.0f;
                    sortingTabControl.Current.Value = ProfileSortCriteria.Local;

                    layout.RowDimensions = new[]
                    {
                        new Dimension(GridSizeMode.Absolute, username_container_height),
                        new Dimension(GridSizeMode.AutoSize),
                        new Dimension(GridSizeMode.AutoSize),
                        new Dimension()
                    };
                });

                if (token.IsCancellationRequested)
                    return;

                var plays = new List<ExtendedScore>();
                var players = new List<RXPlayer>();
                var rulesetInstance = ruleset.Value.CreateInstance();

                foreach (string username in currentUsers)
                {
                    try
                    {
                        Schedule(() => loadingLayer.Text.Value = $"Getting {username} user data...");

                        var player = await apiManager.GetJsonFromApi<RXPlayer>($"players/{username}").ConfigureAwait(false);
                        if (player == null)
                            continue;

                        players.Add(player);

                        Schedule(() => loadingLayer.Text.Value = $"Calculating {player.Username} top scores...");

                        var apiScores = await apiManager.GetJsonFromApi<List<RXScore>>($"players/{player.Id}/scores").ConfigureAwait(false);

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
                                //TotalScore = score.TotalScore
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
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(ex.ToString(), level: LogLevel.Error);
                        notificationDisplay.Display(new Notification($"Failed to calculate {username}: {ex.Message}"));
                    }
                }

                if (token.IsCancellationRequested)
                    return;

                bool calculatingSingleProfile = players.Count == 1;

                // Add user card if only calculating single profile
                if (calculatingSingleProfile)
                {
                    Schedule(() =>
                    {
                        userPanelContainer.Add(userPanel = new UserCard(players[0].ToAPIUser())
                        {
                            RelativeSizeAxes = Axes.X
                        });
                    });
                }

                // Filter plays if only displaying best score on each beatmap
                if (onlyDisplayBestCheckbox.Current.Value)
                {
                    Schedule(() => loadingLayer.Text.Value = "Filtering plays");

                    var filteredPlays = new List<ExtendedScore>();

                    // List of all beatmap IDs in plays without duplicates
                    var beatmapIDs = plays.Select(x => x.SoloScore.BeatmapId).Distinct().ToList();

                    foreach (int id in beatmapIDs)
                    {
                        var bestPlayOnBeatmap = plays.Where(x => x.SoloScore.BeatmapId == id).OrderByDescending(x => x.SoloScore.Pp).First();
                        filteredPlays.Add(bestPlayOnBeatmap);
                    }

                    plays = filteredPlays;
                }

                var localOrdered = plays.OrderByDescending(x => x.SoloScore.Pp).ToList();
                var liveOrdered = plays.OrderByDescending(x => x.LivePP ?? 0).ToList();

                Schedule(() =>
                {
                    foreach (var play in plays)
                    {
                        scores.Add(new ExtendedProfileScore(play, !calculatingSingleProfile));

                        if (play.LivePP != null)
                        {
                            play.Position.Value = localOrdered.IndexOf(play) + 1;
                            play.PositionChange.Value = liveOrdered.IndexOf(play) - localOrdered.IndexOf(play);
                        }
                    }
                });

                if (calculatingSingleProfile)
                {
                    var player = players.First();

                    decimal totalLocalPP = 0;
                    for (int i = 0; i < localOrdered.Count; i++)
                        totalLocalPP += (decimal)(Math.Pow(0.95, i) * (localOrdered[i].SoloScore.Pp ?? 0));

                    decimal totalLivePP = (decimal?)player.TotalPp ?? (decimal)0.0;

                    decimal nonBonusLivePP = 0;
                    for (int i = 0; i < liveOrdered.Count; i++)
                        nonBonusLivePP += (decimal)(Math.Pow(0.95, i) * liveOrdered[i].LivePP ?? 0);

                    //todo: implement properly. this is pretty damn wrong.
                    decimal playcountBonusPP = (totalLivePP - nonBonusLivePP);
                    totalLocalPP += playcountBonusPP;

                    Schedule(() =>
                    {
                        userPanel.Data.Value = new UserCardData
                        {
                            LivePP = totalLivePP,
                            LocalPP = totalLocalPP,
                            PlaycountPP = playcountBonusPP
                        };
                    });
                }
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
                    updateSorting(ProfileSortCriteria.Local);
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

        private void updateSorting(ProfileSortCriteria sortCriteria)
        {
            if (!scores.Children.Any())
                return;

            ExtendedProfileScore[] sortedScores;

            switch (sortCriteria)
            {
                case ProfileSortCriteria.Live:
                    sortedScores = scores.Children.OrderByDescending(x => x.Score.LivePP).ToArray();
                    break;

                case ProfileSortCriteria.Local:
                    sortedScores = scores.Children.OrderByDescending(x => x.Score.PerformanceAttributes.Total).ToArray();
                    break;

                case ProfileSortCriteria.Difference:
                    sortedScores = scores.Children.OrderByDescending(x => x.Score.PerformanceAttributes.Total - x.Score.LivePP).ToArray();
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(sortCriteria), sortCriteria, null);
            }

            for (int i = 0; i < sortedScores.Length; i++)
            {
                scores.SetLayoutPosition(sortedScores[i], i);
            }
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
