using System;
using Verse;
using RimWorld;
using UnityEngine;

namespace Riminder
{
    public abstract class BaseReminder : IExposable
    {
        public string id;
        public int createdTick;
        public int triggerTick;
        public ReminderFrequency frequency;
        public bool completed;
        public bool dismissed;
        
        // Add a separate field for recurrence interval
        public int recurrenceInterval;
        
        // Add a field to track the last time this reminder was triggered
        public int lastTriggerTick;
        
        public ReminderDef def;
        protected internal IReminderDataProvider dataProvider;

        public BaseReminder()
        {
            id = Guid.NewGuid().ToString();
            createdTick = Find.TickManager.TicksGame;
            lastTriggerTick = createdTick; // Initialize to creation time
            completed = false;
            dismissed = false;
            recurrenceInterval = 0;
        }

        public virtual void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref createdTick, "createdTick");
            Scribe_Values.Look(ref triggerTick, "triggerTick");
            Scribe_Values.Look(ref frequency, "frequency");
            Scribe_Values.Look(ref completed, "completed", false);
            Scribe_Values.Look(ref dismissed, "dismissed", false);
            Scribe_Values.Look(ref recurrenceInterval, "recurrenceInterval", 0);
            Scribe_Values.Look(ref lastTriggerTick, "lastTriggerTick", createdTick);
            Scribe_Defs.Look(ref def, "def");
            
            // Data provider is loaded separately by concrete reminder classes
        }

        public virtual void Trigger()
        {
            if (dismissed) return;
            
            dataProvider?.Refresh();
            
            int currentTick = Find.TickManager.TicksGame;
            
            if (Prefs.DevMode)
            {
                Log.Message($"[Riminder] Reminder triggered: {GetLabel()}, frequency={frequency}");
            }

            Find.LetterStack.ReceiveLetter(
                "Reminder: " + GetLabel(),
                GetDescription(),
                LetterDefOf.NeutralEvent);

            if (RiminderMod.Settings.pauseOnReminder)
            {
                Find.TickManager.Pause();
            }

            if (frequency == ReminderFrequency.OneTime)
            {
                completed = true;
                return;
            }

            // For recurring reminders, we need to:
            // 1. Update lastTriggerTick to current tick
            // 2. Calculate new trigger time based on interval
            lastTriggerTick = currentTick;
            
            // RescheduleNext also sets lastTriggerTick again just to be safe
            RescheduleNext();
        }

        public void RescheduleNext()
        {
            int currentTick = Find.TickManager.TicksGame;
            
            // For recurring reminders, the startpoint for progress should ALWAYS be now
            lastTriggerTick = currentTick;

            switch (frequency)
            {
                case ReminderFrequency.Days:
                    triggerTick = currentTick + GenDate.TicksPerDay;
                    break;
                case ReminderFrequency.Quadrums:
                    triggerTick = currentTick + GenDate.TicksPerQuadrum;
                    break;
                case ReminderFrequency.Years:
                    triggerTick = currentTick + GenDate.TicksPerYear;
                    break;
                case ReminderFrequency.Custom:
                    // For custom reminders, use the recurrenceInterval field for the interval in ticks
                    int interval = recurrenceInterval;
                    if (interval <= 0) // Fallback in case something went wrong
                    {
                        interval = triggerTick - currentTick;
                        if (interval <= 0)
                            interval = GenDate.TicksPerDay; // Default to 1 day
                    }
                    triggerTick = currentTick + interval;
                    break;
            }
            
            if (Prefs.DevMode)
            {
                Log.Message($"[Riminder] Rescheduled {GetLabel()}: lastTriggerTick={lastTriggerTick}, " +
                           $"triggerTick={triggerTick}, interval={(triggerTick - lastTriggerTick)} ticks");
            }
        }

        // Method delegations to data provider
        public virtual string GetLabel() => dataProvider?.GetLabel() ?? "Unknown";
        public virtual string GetDescription() => dataProvider?.GetDescription() ?? "No description";
        public virtual string GetTimeLeftString() => dataProvider?.GetTimeLeftString() ?? DefaultTimeLeftString();
        public virtual float GetProgress() => dataProvider?.GetProgress() ?? DefaultProgressValue();
        
        protected virtual string DefaultTimeLeftString()
        {
            int ticksLeft = triggerTick - Find.TickManager.TicksGame;
            if (ticksLeft <= 0) return "Now";

            const int daysPerYear = 60;
            const int daysPerQuadrum = 15;

            int daysLeft = ticksLeft / GenDate.TicksPerDay;
            int hoursLeft = (ticksLeft % GenDate.TicksPerDay) / GenDate.TicksPerHour;
            
            if (ticksLeft % GenDate.TicksPerHour > 0)
                hoursLeft++;
                
            if (hoursLeft == 24)
            {
                hoursLeft = 0;
                daysLeft++;
            }

            int years = daysLeft / daysPerYear;
            int remainingDaysAfterYears = daysLeft % daysPerYear;
            int quadrums = remainingDaysAfterYears / daysPerQuadrum;
            int remainingDays = remainingDaysAfterYears % daysPerQuadrum;

            System.Text.StringBuilder result = new System.Text.StringBuilder("in ");
            bool hasAdded = false;

            if (years > 0)
            {
                result.Append(years == 1 ? "1 year" : $"{years} years");
                hasAdded = true;
            }

            if (quadrums > 0)
            {
                if (hasAdded) result.Append(", ");
                result.Append(quadrums == 1 ? "1 quadrum" : $"{quadrums} quadrums");
                hasAdded = true;
            }

            if (remainingDays > 0)
            {
                if (hasAdded) result.Append(", ");
                result.Append(remainingDays == 1 ? "1 day" : $"{remainingDays} days");
                hasAdded = true;
            }

            if (hoursLeft > 0 || (!hasAdded && daysLeft == 0))
            {
                if (hasAdded) result.Append(", ");
                result.Append(hoursLeft == 1 ? "1 hour" : $"{hoursLeft} hours");
            }

            return result.ToString();
        }
        
        protected virtual float DefaultProgressValue()
        {
            int currentTick = Find.TickManager.TicksGame;
            
            // If deadline has passed, return 100% progress
            if (currentTick >= triggerTick) return 1f;
            
            // Always calculate from lastTriggerTick to triggerTick
            int startTick = lastTriggerTick;
            int endTick = triggerTick;
            
            // Sanity check: If start is after current time, use current time
            if (startTick > currentTick)
            {
                startTick = currentTick;
            }
            
            // If start and end are the same, return 0 progress to avoid division by zero
            if (endTick <= startTick) 
            {
                return 0f;
            }
            
            // Calculate progress as percentage of time elapsed from start to now, relative to total duration
            float progress = (float)(currentTick - startTick) / (endTick - startTick);
            
            // Ensure progress is between 0 and 1
            return Mathf.Clamp01(progress);
        }

        public virtual void OpenEditDialog()
        {
            // Implement in concrete classes or create a factory pattern
        }

        public string GetFrequencyDisplayString()
        {
            switch (frequency)
            {
                case ReminderFrequency.OneTime:
                    return "One Time";
                case ReminderFrequency.Days:
                    return "Days";
                case ReminderFrequency.Quadrums:
                    return "Quadrums";
                case ReminderFrequency.Years:
                    return "Years";
                case ReminderFrequency.Custom:
                    // Check if there's a recurrence description
                    if (this is Reminder reminderObj && 
                        reminderObj.metadata != null && 
                        reminderObj.metadata.TryGetValue("recurrenceDescription", out string description))
                    {
                        return "Every " + description;
                    }
                    return "Custom";
                case ReminderFrequency.WhenTendingRequired:
                    return "Tending";
                default:
                    return frequency.ToString();
            }
        }
    }
} 