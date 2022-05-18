using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Scoring;

namespace PerformanceCalculatorGUI.Components
{
    internal class HitResultsContainer : Container
    {
        public readonly Bindable<IEnumerable<(HitResult hitResult, string displayName)>> HitResultsNames = new();

        [Resolved]
        private Bindable<Dictionary<HitResult, Bindable<int>>> hitResults { get; set; }

        private FillFlowContainer flow;

        [BackgroundDependencyLoader]
        private void load()
        {
            Add(flow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical
            });

            HitResultsNames.BindValueChanged((_) => populateHitResults());
        }

        private void populateHitResults()
        {
            flow.Clear();
            flow.AddRange(HitResultsNames.Value.Select(x => new LimitedLabelledNumberBox
            {
                RelativeSizeAxes = Axes.X,
                Anchor = Anchor.TopLeft,
                Label = x.displayName,
                PlaceholderText = "0",
                MinValue = 0,
                Value = { BindTarget = hitResults.Value[x.hitResult] }
            }));
        }
    }
}
