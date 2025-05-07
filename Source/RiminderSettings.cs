using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Riminder
{
    public class RiminderSettings : ModSettings
    {
        // Settings
        public bool pauseOnReminder = false;

        public void DoWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.CheckboxLabeled("Pause on reminder", ref pauseOnReminder, "Pause the game when a reminder is triggered.");
            
            listing.End();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref pauseOnReminder, "pauseOnReminder", false);
        }
    }
} 