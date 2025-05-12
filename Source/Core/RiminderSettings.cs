using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Riminder
{
    public class RiminderSettings : ModSettings
    {
        public bool showNotifications = true;
        public bool autoCreateTendReminders = true;
        public float notificationDuration = 5f;
        public bool pauseOnReminder = false;
        public bool removeOnImmunity = true;
        public bool removeOnHealed = true;
        
        // New settings for controlling alerts and reminders separately
        public bool enableNormalReminders = true;
        public bool enableNormalAlerts = true;
        public bool enableTendReminders = true;
        public bool enableTendAlerts = true;

        public void DoWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            // Global notification settings
            listing.CheckboxLabeled("Show notifications", ref showNotifications, "Show notifications when reminders trigger");
            if (showNotifications)
            {
                listing.Label("Notification duration: " + notificationDuration.ToString("F1") + " seconds");
                notificationDuration = listing.Slider(notificationDuration, 1f, 10f);
            }

            listing.GapLine();
            listing.Label("Reminder Type Settings");
            
            // Normal reminders settings
            listing.CheckboxLabeled("Enable normal reminders", ref enableNormalReminders, 
                "Show normal reminders in the reminder list");
            listing.CheckboxLabeled("Enable normal alerts", ref enableNormalAlerts, 
                "Show alerts for normal reminders");
                
            listing.GapLine();
                
            // Tend reminders settings
            listing.CheckboxLabeled("Auto-create tend reminders", ref autoCreateTendReminders, 
                "Automatically create reminders for conditions that need tending");
            listing.CheckboxLabeled("Enable tend reminders", ref enableTendReminders, 
                "Show tend reminders in the reminder list");
            listing.CheckboxLabeled("Enable tend alerts", ref enableTendAlerts, 
                "Show alerts for tend reminders");
                
            listing.GapLine();
                
            // Other settings
            listing.CheckboxLabeled("Pause game on reminder", ref pauseOnReminder, 
                "Pause the game when a reminder triggers");
            listing.CheckboxLabeled("Remove on immunity", ref removeOnImmunity, 
                "Automatically remove tend reminders when immunity is reached");
            listing.CheckboxLabeled("Remove on healed", ref removeOnHealed, 
                "Automatically remove tend reminders when the condition is fully healed");

            listing.End();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref showNotifications, "showNotifications", true);
            Scribe_Values.Look(ref autoCreateTendReminders, "autoCreateTendReminders", true);
            Scribe_Values.Look(ref notificationDuration, "notificationDuration", 5f);
            Scribe_Values.Look(ref pauseOnReminder, "pauseOnReminder", false);
            Scribe_Values.Look(ref removeOnImmunity, "removeOnImmunity", true);
            Scribe_Values.Look(ref removeOnHealed, "removeOnHealed", true);
            
            // Save new settings
            Scribe_Values.Look(ref enableNormalReminders, "enableNormalReminders", true);
            Scribe_Values.Look(ref enableNormalAlerts, "enableNormalAlerts", true);
            Scribe_Values.Look(ref enableTendReminders, "enableTendReminders", true);
            Scribe_Values.Look(ref enableTendAlerts, "enableTendAlerts", true);
        }
    }
}