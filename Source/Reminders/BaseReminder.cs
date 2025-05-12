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
        
        
        public int recurrenceInterval;
        
        
        public int lastTriggerTick;
        
        public ReminderDef def;
        protected internal IReminderDataProvider dataProvider;

        public BaseReminder()
        {
            id = Guid.NewGuid().ToString();
            createdTick = Find.TickManager.TicksGame;
            lastTriggerTick = createdTick; 
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
            
            
        }

        public virtual void Trigger()
        {
            if (dismissed) return;
            
            dataProvider?.Refresh();
            
            int currentTick = Find.TickManager.TicksGame;

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

            lastTriggerTick = currentTick;
            
            RescheduleNext();
        }

        public void RescheduleNext()
        {
            int currentTick = Find.TickManager.TicksGame;
            
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
                    int interval = recurrenceInterval;
                    if (interval <= 0) 
                    {
                        interval = triggerTick - currentTick;
                        if (interval <= 0)
                            interval = GenDate.TicksPerDay; 
                    }
                    triggerTick = currentTick + interval;
                    break;
            }
        }

        
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
            
            
            if (currentTick >= triggerTick) return 1f;
            
            
            int startTick = lastTriggerTick;
            int endTick = triggerTick;
            
            
            if (startTick > currentTick)
            {
                startTick = currentTick;
            }
            
            
            if (endTick <= startTick) 
            {
                return 0f;
            }
            
            
            float progress = (float)(currentTick - startTick) / (endTick - startTick);
            
            
            return Mathf.Clamp01(progress);
        }

        public virtual void OpenEditDialog()
        {
            
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