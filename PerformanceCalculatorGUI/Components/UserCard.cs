// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Colour;
using osu.Framework.Extensions.Color4Extensions;
using osuTK.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Platform;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.API.Requests.Responses;
using osuTK;
using osu.Game.Users;
using osu.Game.Users.Drawables;

namespace PerformanceCalculatorGUI.Components
{
    public struct UserCardData
    {
        public double LocalPP { get; set; }
        public double PlaycountPP { get; set; }
    }

    public partial class UserCard : UserListPanel
    {
        private OsuSpriteText localLabel;
        private OsuSpriteText playcountLabel;

        public Bindable<UserCardData> Data = new Bindable<UserCardData>();

        public UserCard(APIUser user)
            : base(user)
        {
            RelativeSizeAxes = Axes.X;
            Height = 40;
        }

        [BackgroundDependencyLoader]
        private void load(GameHost host)
        {
            Background.Width = 0.5f;
            Background.Origin = Anchor.CentreRight;
            Background.Anchor = Anchor.CentreRight;
            Background.Colour = ColourInfo.GradientHorizontal(Color4.White.Opacity(1), Color4.White.Opacity(0.5f));

            Data.ValueChanged += val =>
            {
                localLabel.Text = $"New: {val.NewValue.LocalPP:N1} pp";
                playcountLabel.Text = $"{val.NewValue.PlaycountPP:N1} from playcount";
            };

            Action = () => { host.OpenUrlExternally($"https://osu.ppy.sh/u/{User.Id}"); };
        }

        protected override void LoadComplete()
        {
            Status.UnbindAll();
            Activity.UnbindAll();
        }

        protected override Drawable CreateLayout()
        {
            var layout = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    new FillFlowContainer
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(10, 0),
                        Children = new Drawable[]
                        {
                            new UpdateableAvatar(User, false)
                            {
                                Masking = true,
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                CornerRadius = 0.6f,
                                Size = new Vector2(40)
                            },
                            CreateFlag().With(flag =>
                            {
                                flag.Anchor = Anchor.CentreLeft;
                                flag.Origin = Anchor.CentreLeft;
                            }),
                            CreateUsername().With(username =>
                            {
                                username.Anchor = Anchor.CentreLeft;
                                username.Origin = Anchor.CentreLeft;
                            }),
                        }
                    },
                    new FillFlowContainer
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Padding = new MarginPadding { Right = 10 },
                        Spacing = new Vector2(20.0f),
                        Children = new Drawable[]
                        {
                            new FillFlowContainer
                            {
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Vertical,
                                Origin = Anchor.CentreLeft,
                                Anchor = Anchor.CentreLeft,
                                Children = new[]
                                {
                                    localLabel = new OsuSpriteText
                                    {
                                        Colour = Colours.RedLighter,
                                        Anchor = Anchor.CentreRight,
                                        Origin = Anchor.CentreRight,
                                        Font = OsuFont.GetFont(size: 14, weight: FontWeight.Bold),
                                    },
                                }
                            },
                            new FillFlowContainer
                            {
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Origin = Anchor.CentreLeft,
                                Anchor = Anchor.CentreLeft,
                                Width = 100,
                                Children = new[]
                                {
                                    playcountLabel = new OsuSpriteText
                                    {
                                        Colour = Colours.GrayA,
                                        Font = OsuFont.GetFont(size: 10),
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft
                                    }
                                }
                            }
                        }
                    }
                }
            };

            return layout;
        }
    }
}
