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
            
            // Initialize the reminder manager
            RiminderManager.Initialize();
            
            // Initialize the auto tend reminder manager
            AutoTendReminderManager.Initialize();
            
            // No need to manually register hotkeys - they're handled by the KeyPrefs system
            // and our harmony patch
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