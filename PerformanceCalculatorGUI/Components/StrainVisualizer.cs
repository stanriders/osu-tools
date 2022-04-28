﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Screens.Edit.Compose.Components.Timeline;
using osuTK;

namespace PerformanceCalculatorGUI.Components
{
    internal class StrainVisualizer : Container
    {
        public readonly Bindable<Skill[]> Skills = new();

        private readonly List<Bindable<bool>> graphToggles = new();

        private ZoomableScrollContainer graphsContainer;
        private FillFlowContainer legendContainer;

        private ColourInfo[] skillColours;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; }

        public StrainVisualizer()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        private void updateGraphs(ValueChangedEvent<Skill[]> val)
        {
            graphsContainer.Clear();

            var skills = val.NewValue.Where(x => x is StrainSkill).ToArray();

            // dont bother if there are no strain skills to draw
            if (skills.Length == 0)
            {
                legendContainer.Clear();
                graphToggles.Clear();
                return;
            }

            var graphAlpha = Math.Min(1.5f / skills.Length, 0.9f);

            List<float[]> strains = new List<float[]>();
            foreach (var skill in skills)
                strains.Add(((StrainSkill)skill).GetCurrentStrainPeaks().Select(x => (float)x).ToArray());

            var strainMaxValue = strains.Max(x => x.Max());

            for (int i = 0; i < skills.Length; i++)
            {
                graphsContainer.AddRange(new Drawable[]
                {
                    new BarGraph
                    {
                        Alpha = graphAlpha,
                        RelativeSizeAxes = Axes.Both,
                        MaxValue = strainMaxValue,
                        Values = strains[i],
                        Colour = skillColours[i]
                    }
                });
            }

            graphsContainer.Add(new OsuSpriteText
            {
                Font = OsuFont.GetFont(size: 10),
                Text = $"{strainMaxValue:0.00}"
            });

            if (val.OldValue == null || !val.NewValue.All(x => val.OldValue.Any(y => y.GetType().Name == x.GetType().Name)))
            {
                // skill list changed - recreate toggles
                legendContainer.Clear();
                graphToggles.Clear();

                // we do Children - 1 because max strain value is in the graphsContainer too and we don't want it to have a toggle
                for (int i = 0; i < graphsContainer.Children.Count - 1; i++)
                {
                    // this is ugly, but it works
                    var graphToggleBindable = new Bindable<bool>();
                    var graphNum = i;
                    graphToggleBindable.BindValueChanged(state =>
                    {
                        if (state.NewValue)
                        {
                            graphsContainer[graphNum].FadeTo(graphAlpha);
                        }
                        else
                        {
                            graphsContainer[graphNum].Hide();
                        }
                    });
                    graphToggles.Add(graphToggleBindable);

                    legendContainer.Add(new Container
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Masking = true,
                        CornerRadius = 10,
                        AutoSizeAxes = Axes.Both,
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = colourProvider.Background5
                            },
                            new ExtendedOsuCheckbox
                            {
                                Padding = new MarginPadding(10),
                                RelativeSizeAxes = Axes.None,
                                Width = 200,
                                Current = { BindTarget = graphToggleBindable, Default = true, Value = true },
                                LabelText = skills[i].GetType().Name,
                                TextColour = skillColours[i]
                            }
                        }
                    });
                }
            }
            else
            {
                // skill list is the same, keep graph toggles
                for (int i = 0; i < graphsContainer.Children.Count - 1; i++)
                {
                    // graphs are visible by default, we want to hide ones that were disabled before
                    if (!graphToggles[i].Value)
                        graphsContainer[i].Hide();
                }
            }
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            skillColours = new ColourInfo[]
            {
                colours.Blue,
                colours.Green,
                colours.Red,
                colours.Yellow,
                colours.Pink,
                colours.Cyan
            };

            Add(new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Masking = true,
                CornerRadius = 15,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colourProvider.Background6,
                        Alpha = 0.6f
                    },
                    new FillFlowContainer
                    {
                        Padding = new MarginPadding(10),
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(5),
                        Children = new Drawable[]
                        {
                            graphsContainer = new ZoomableScrollContainer
                            {
                                Height = 150,
                                RelativeSizeAxes = Axes.X
                            },
                            legendContainer = new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Full,
                                Spacing = new Vector2(5)
                            }
                        }
                    }
                }
            });

            Skills.BindValueChanged(updateGraphs);
        }
    }
}