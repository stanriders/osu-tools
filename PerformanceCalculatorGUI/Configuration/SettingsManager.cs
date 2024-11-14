﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Reflection;
using osu.Framework.Configuration;
using osu.Framework.Platform;

namespace PerformanceCalculatorGUI.Configuration
{
    public enum Settings
    {
        CachePath
    }

    public class SettingsManager : IniConfigManager<Settings>
    {
        protected override string Filename => "perfcalc.ini";

        public SettingsManager(Storage storage)
            : base(storage)
        {
        }

        protected override void InitialiseDefaults()
        {
            SetDefault(Settings.CachePath, Path.Combine(Path.GetDirectoryName(AppContext.BaseDirectory) ?? ".", "cache"));
        }
    }
}
