// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Performance;
using osu.Framework.Graphics.Pooling;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Threading;
using osu.Game.Rulesets.Catch.Difficulty.Skills;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Pooling;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;
using SharpCompress.Common;

namespace PerformanceCalculatorGUI.Screens.ObjectInspection
{
    public partial class OsuObjectInspectorDrawable : PoolableDrawableWithLifetime<OsuObjectInspectorLifetimeEntry>
    {
        protected override void OnApply(OsuObjectInspectorLifetimeEntry entry)
        {
            base.OnApply(entry);

            entry.Invalidated += onEntryInvalidated;
            refresh();
        }

        protected override void OnFree(OsuObjectInspectorLifetimeEntry entry)
        {
            base.OnFree(entry);

            entry.Invalidated -= onEntryInvalidated;
            ClearInternal(false);
        }

        private void onEntryInvalidated() => Scheduler.AddOnce(refresh);

        private void refresh()
        {
            ClearInternal(false);

            var entry = Entry;
            if (entry == null) return;

            var hitObject = entry.HitObject;
            double startTime = hitObject.StartTime;
            double visibleTime = hitObject.GetEndTime() - startTime + 200;

            if (entry.DifficultyHitObject?.FlowPoints is not null)
            {
                for (int i = 0; i < entry.DifficultyHitObject.FlowPoints.Count; i++)
                {
                    var flowPoint = entry.DifficultyHitObject.FlowPoints[i];
                    Container panel;
                    AddInternal(panel = new Container
                    {
                        Alpha = 0
                    });
                    float scalingFactor = OsuDifficultyHitObject.NORMALISED_RADIUS / (float)((OsuHitObject)(entry.DifficultyHitObject.BaseObject)).Radius;
                    /*panel.Add(new Box
                    {
                        Position = flowPoint / scalingFactor,
                        Width = 2,
                        Height = 2,
                        Origin = Anchor.Centre,
                        EdgeSmoothness = Vector2.One,
                        Colour = Colour4.White
                    });*/

                    if (i > 0)
                    {
                        var prevFlowPoint = entry.DifficultyHitObject.FlowPoints[i - 1];
                        var line = new Line(flowPoint / scalingFactor, prevFlowPoint / scalingFactor);
                        panel.Add(new Box
                        {
                            Position = flowPoint / scalingFactor,
                            Width = line.Rho,
                            Height = 1,
                            Rotation = float.RadiansToDegrees(line.Theta),
                            EdgeSmoothness = Vector2.One
                        });
                    }
                    
                    using (panel.BeginAbsoluteSequence(startTime-100))
                    {
                        panel.FadeIn().Delay(visibleTime).FadeOut().Expire();
                    }
                }
            }

            entry.LifetimeEnd = hitObject.GetEndTime() + 100;
        }
    }

    public class OsuObjectInspectorLifetimeEntry : LifetimeEntry
    {
        public event Action? Invalidated;
        public readonly OsuHitObject HitObject;
        public readonly OsuDifficultyHitObject? DifficultyHitObject;

        public OsuObjectInspectorLifetimeEntry(OsuHitObject hitObject, OsuDifficultyHitObject? difficultyHitObject)
        {
            HitObject = hitObject;
            DifficultyHitObject = difficultyHitObject;
            LifetimeStart = HitObject.StartTime - HitObject.TimePreempt;

            bindEvents();
            refreshLifetimes();
        }

        private bool wasBound;

        private void bindEvents()
        {
            UnbindEvents();
            HitObject.DefaultsApplied += onDefaultsApplied;
            wasBound = true;
        }

        public void UnbindEvents()
        {
            if (!wasBound)
                return;

            HitObject.DefaultsApplied -= onDefaultsApplied;

            wasBound = false;
        }

        private void onDefaultsApplied(HitObject obj) => refreshLifetimes();

        private void refreshLifetimes()
        {
            if (HitObject is Spinner)
            {
                LifetimeEnd = LifetimeStart;
                return;
            }

            LifetimeStart = HitObject.StartTime - HitObject.TimePreempt;
            LifetimeEnd = HitObject.GetEndTime() + 10;

            Invalidated?.Invoke();
        }
    }

    public partial class OsuObjectInspectorRenderer : PooledDrawableWithLifetimeContainer<OsuObjectInspectorLifetimeEntry, OsuObjectInspectorDrawable>
    {
        private DrawablePool<OsuObjectInspectorDrawable> pool;

        private readonly List<OsuObjectInspectorLifetimeEntry> lifetimeEntries = new();

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                pool = new DrawablePool<OsuObjectInspectorDrawable>(1, 200)
            };
        }

        public void AddDifficultyDataPanel(OsuHitObject hitObject, OsuDifficultyHitObject difficultyHitObject)
        {
            var newEntry = new OsuObjectInspectorLifetimeEntry(hitObject, difficultyHitObject);
            lifetimeEntries.Add(newEntry);
            Add(newEntry);
        }

        public void RemoveDifficultyDataPanel(OsuHitObject hitObject)
        {
            int index = lifetimeEntries.FindIndex(e => e.HitObject == hitObject);

            var entry = lifetimeEntries[index];
            entry.UnbindEvents();

            lifetimeEntries.RemoveAt(index);
            Remove(entry);
        }

        protected override OsuObjectInspectorDrawable GetDrawable(OsuObjectInspectorLifetimeEntry entry)
        {
            var connection = pool.Get();
            connection.Apply(entry);
            return connection;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            foreach (var entry in lifetimeEntries)
                entry.UnbindEvents();
            lifetimeEntries.Clear();
        }
    }
}
