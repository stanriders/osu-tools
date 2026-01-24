// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Performance;
using osu.Framework.Graphics.Pooling;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Catch.Difficulty.Skills;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Pooling;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

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
                Container panel;
                AddInternal(panel = new Container
                {
                    Alpha = 0
                });
                
                if (entry.DifficultyHitObject.NormalAngle.Count > 0)
                {
                    var line1 = new Line(entry.DifficultyHitObject.NormalAngle[0], entry.DifficultyHitObject.NormalAngle[1]);
                    panel.Add(new Box
                    {
                        Position = entry.DifficultyHitObject.NormalAngle[0],
                        Width = line1.Rho,
                        Height = 2,
                        Rotation = float.RadiansToDegrees(line1.Theta),
                        EdgeSmoothness = Vector2.One,
                        Colour = Colour4.White
                    });

                    var line2 = new Line(entry.DifficultyHitObject.NormalAngle[1], entry.DifficultyHitObject.NormalAngle[2]);
                    panel.Add(new Box
                    {
                        Position = entry.DifficultyHitObject.NormalAngle[1],
                        Width = line2.Rho,
                        Height = 2,
                        Rotation = float.RadiansToDegrees(line2.Theta),
                        EdgeSmoothness = Vector2.One,
                        Colour = Colour4.White
                    });
                }
                
                if (entry.DifficultyHitObject.SliderAngle.Count > 0)
                {
                    var sliderLine1 = new Line(entry.DifficultyHitObject.SliderAngle[0], entry.DifficultyHitObject.SliderAngle[1]);
                    panel.Add(new Box
                    {
                        Position = entry.DifficultyHitObject.SliderAngle[0],
                        Width = sliderLine1.Rho,
                        Height = 2,
                        Rotation = float.RadiansToDegrees(sliderLine1.Theta),
                        EdgeSmoothness = Vector2.One,
                        Colour = Colour4.LightGreen
                    });

                    var sliderLine2 = new Line(entry.DifficultyHitObject.SliderAngle[1], entry.DifficultyHitObject.SliderAngle[2]);
                    panel.Add(new Box
                    {
                        Position = entry.DifficultyHitObject.SliderAngle[1],
                        Width = sliderLine2.Rho,
                        Height = 2,
                        Rotation = float.RadiansToDegrees(sliderLine2.Theta),
                        EdgeSmoothness = Vector2.One,
                        Colour = Colour4.LightGreen
                    });
                }

                using (panel.BeginAbsoluteSequence(entry.DifficultyHitObject.StartTime))
                {
                    panel.FadeIn().Delay(entry.DifficultyHitObject.EndTime - entry.DifficultyHitObject.StartTime).FadeOut().Expire();
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
