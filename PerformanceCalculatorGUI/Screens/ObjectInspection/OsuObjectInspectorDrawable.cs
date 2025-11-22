// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Performance;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Threading;
using osu.Framework.Utils;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Pooling;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;
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
            double startTime = hitObject.StartTime - hitObject.TimePreempt;
            double movementTime = hitObject.GetEndTime() - hitObject.StartTime;
            double visibleTime = hitObject.GetEndTime() - startTime;

            if (entry.DifficultyHitObject is not null)
            {
                foreach (var movement in entry.DifficultyHitObject.Movements)
                {
                    Container panel;
                    AddInternal(panel = new Container
                    {
                        Alpha = 0
                    });

                    var line = new Line(movement.Start, movement.End);
                    panel.Add(new Box
                    {
                        Position = movement.Start,
                        Width = line.Rho,
                        Height = 2,
                        Rotation = float.RadiansToDegrees(line.Theta)
                    });

                    using (panel.BeginAbsoluteSequence(movement.StartTime - 500))
                    {
                        panel.FadeIn(500).Delay(movement.Time + 500).FadeOut(500).Expire();
                    }
                }
            }

            entry.LifetimeEnd = hitObject.GetEndTime();
        }
    }

    public class OsuObjectInspectorLifetimeEntry : LifetimeEntry
    {
        public event Action Invalidated;
        public readonly OsuHitObject HitObject;
        public readonly OsuDifficultyHitObject DifficultyHitObject;

        public OsuObjectInspectorLifetimeEntry(OsuHitObject hitObject, OsuDifficultyHitObject difficultyHitObject)
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
}
