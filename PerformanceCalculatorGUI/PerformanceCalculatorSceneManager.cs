// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Screens;
using osu.Game.Beatmaps.Drawables.Cards;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;
using PerformanceCalculatorGUI.Components;
using PerformanceCalculatorGUI.Screens;

namespace PerformanceCalculatorGUI
{
    public partial class PerformanceCalculatorSceneManager : CompositeDrawable
    {
        private ScreenStack screenStack;

        private Box hoverGradientBox;

        public const float CONTROL_AREA_HEIGHT = 45;

        [Cached]
        private OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Blue);

        public PerformanceCalculatorSceneManager()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            InternalChildren = new Drawable[]
            {
                new PopoverContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        ColumnDimensions = new[] { new Dimension() },
                        RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize), new Dimension() },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                new HoverHandlingContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    Height = CONTROL_AREA_HEIGHT,
                                    Hovered = e =>
                                    {
                                        hoverGradientBox.FadeIn(100);
                                        return false;
                                    },
                                    Unhovered = e =>
                                    {
                                        hoverGradientBox.FadeOut(100);
                                    },
                                    Children = new Drawable[]
                                    {
                                        new Box
                                        {
                                            Colour = OsuColour.Gray(0.1f),
                                            RelativeSizeAxes = Axes.Both,
                                        },
                                        new FillFlowContainer
                                        {
                                            Anchor = Anchor.TopRight,
                                            Origin = Anchor.TopRight,
                                            Direction = FillDirection.Horizontal,
                                            RelativeSizeAxes = Axes.Y,
                                            AutoSizeAxes = Axes.X,
                                            Spacing = new Vector2(5),
                                            Children = new Drawable[]
                                            {
                                                new SettingsButton()
                                            }
                                        },
                                    },
                                }
                            },
                            new Drawable[]
                            {
                                new ScalingContainer(ScalingMode.Everything)
                                {
                                    Depth = 1,
                                    Children = new Drawable[]
                                    {
                                        screenStack = new ScreenStack
                                        {
                                            RelativeSizeAxes = Axes.Both
                                        },
                                        hoverGradientBox = new Box
                                        {
                                            Colour = ColourInfo.GradientVertical(Color4.Black.Opacity(1.0f), Color4.Black.Opacity(1)),
                                            RelativeSizeAxes = Axes.X,
                                            Height = 100,
                                            Alpha = 0
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };
            screenStack.Push(new ProfileScreen());
        }
    }
}
