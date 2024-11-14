// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterfaceV2;
using osuTK;
using PerformanceCalculatorGUI.Configuration;

namespace PerformanceCalculatorGUI.Components
{
    public partial class SettingsPopover : OsuPopover
    {
        private SettingsManager configManager;

        private Bindable<string> cacheBindable;
        private Bindable<float> scaleBindable;

        [BackgroundDependencyLoader]
        private void load(SettingsManager configManager, OsuConfigManager osuConfig)
        {
            this.configManager = configManager;
            cacheBindable = configManager.GetBindable<string>(Settings.CachePath);
            scaleBindable = osuConfig.GetBindable<float>(OsuSetting.UIScale);

            Add(new Container
            {
                AutoSizeAxes = Axes.Y,
                Width = 600,
                Children = new Drawable[]
                {
                    new FillFlowContainer
                    {
                        Direction = FillDirection.Vertical,
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Spacing = new Vector2(18),
                        Children = new Drawable[]
                        {
                            new LabelledTextBox
                            {
                                RelativeSizeAxes = Axes.X,
                                Label = "Beatmap cache path",
                                Current = { BindTarget = cacheBindable }
                            },
                            new Box
                            {
                                RelativeSizeAxes = Axes.X,
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Size = new Vector2(0.8f, 3f),
                                Colour = OsuColour.Gray(0.5f)
                            },
                            new LabelledSliderBar<float>
                            {
                                RelativeSizeAxes = Axes.X,
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Label = "UI Scale",
                                Current = { BindTarget = scaleBindable }
                            },
                            new RoundedButton
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Width = 150,
                                Height = 40,
                                Text = "Save",
                                Action = saveConfig
                            }
                        }
                    }
                }
            });
        }

        private void saveConfig()
        {
            configManager.Save();

            this.HidePopover();
        }
    }
}
