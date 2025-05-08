using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using UnityEngine;

namespace Riminder
{
    public class RiminderMod : Mod
    {
        public static RiminderSettings Settings { get; private set; }

        public RiminderMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<RiminderSettings>();
            RiminderManager.Initialize();
            AutoTendReminderManager.Initialize();
        }

        public override string SettingsCategory()
        {
            return "Riminder";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DoWindowContents(inRect);
        }
    }
}
