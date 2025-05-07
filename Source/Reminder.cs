using System;
using Verse;
using RimWorld;

namespace Riminder
{
    public enum ReminderFrequency
    {
        OneTime,
        Daily,
        Weekly,
        Monthly,
        Quarterly,
        Yearly,
        WhenTendingRequired
    }

    public class Reminder : IExposable
    {
        public string id;
        public string label;
        public string description;
        public int createdTick;
        public int triggerTick;
        public ReminderFrequency frequency;
        public bool completed;
        public bool dismissed;

        // Empty constructor for loading
        public Reminder() { }

        public Reminder(string label, string description, int triggerTick, ReminderFrequency frequency = ReminderFrequency.OneTime)
        {
            this.id = Guid.NewGuid().ToString();
            this.label = label;
            this.description = description;
            this.createdTick = Find.TickManager.TicksGame;
            this.triggerTick = triggerTick;
            this.frequency = frequency;
            this.completed = false;
            this.dismissed = false;
        }

        public virtual void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref description, "description");
            Scribe_Values.Look(ref createdTick, "createdTick");
            Scribe_Values.Look(ref triggerTick, "triggerTick");
            Scribe_Values.Look(ref frequency, "frequency");
            Scribe_Values.Look(ref completed, "completed", false);
            Scribe_Values.Look(ref dismissed, "dismissed", false);
        }

        public virtual void Trigger()
        {
            if (dismissed) return;
            
            // Always show letter notifications
            Find.LetterStack.ReceiveLetter(
                "Reminder: " + label,
                description,
                LetterDefOf.NeutralEvent);
            
            // Log the reminder trigger
            Log.Message($"[Riminder] Reminder triggered: {label}");

            if (RiminderMod.Settings.pauseOnReminder)
            {
                Find.TickManager.Pause();
            }

            // If one-time reminder, mark as completed
            if (frequency == ReminderFrequency.OneTime)
            {
                completed = true;
                return;
            }

            // Schedule next occurrence based on frequency
            RescheduleNext();
        }

        public void RescheduleNext()
        {
            int currentTick = Find.TickManager.TicksGame;
            
            // Calculate next occurrence based on frequency
            switch (frequency)
            {
                case ReminderFrequency.Daily:
                    triggerTick = currentTick + GenDate.TicksPerDay;
                    break;
                case ReminderFrequency.Weekly:
                    triggerTick = currentTick + GenDate.TicksPerDay * 7;
                    break;
                case ReminderFrequency.Monthly:
                    triggerTick = currentTick + GenDate.TicksPerDay * 30; // Approximation
                    break;
                case ReminderFrequency.Quarterly:
                    triggerTick = currentTick + GenDate.TicksPerDay * 90; // Approximation
                    break;
                case ReminderFrequency.Yearly:
                    triggerTick = currentTick + GenDate.TicksPerDay * 60; // One year in RimWorld (60 days)
                    break;
            }
        }

        public virtual float GetProgressPercent()
        {
            int currentTick = Find.TickManager.TicksGame;
            if (currentTick >= triggerTick) return 1f;
            return (float)(currentTick - createdTick) / (triggerTick - createdTick);
        }

        public string GetTimeLeftString()
        {
            int ticksLeft = triggerTick - Find.TickManager.TicksGame;
            if (ticksLeft <= 0) return "Now";
            
            // RimWorld calendar conversion
            // 1 year = 60 days
            // 1 quadrum (season/quarter) = 15 days
            // Use RimWorld's own calendar system for accurate display
            
            const int daysPerYear = 60;
            const int daysPerQuadrum = 15; // 4 quadrums per year
            
            int daysLeft = ticksLeft / GenDate.TicksPerDay;
            int hoursLeft = (ticksLeft % GenDate.TicksPerDay) / GenDate.TicksPerHour;
            
            // Calculate years, quadrums, and remaining days
            int years = daysLeft / daysPerYear;
            int remainingDaysAfterYears = daysLeft % daysPerYear;
            int quadrums = remainingDaysAfterYears / daysPerQuadrum;
            int remainingDays = remainingDaysAfterYears % daysPerQuadrum;
            
            // Build the string
            System.Text.StringBuilder result = new System.Text.StringBuilder("in ");
            bool hasAdded = false;
            
            // Years
            if (years > 0)
            {
                result.Append(years == 1 ? "1 year" : $"{years} years");
                hasAdded = true;
            }
            
            // Quadrums (seasons/quarters)
            if (quadrums > 0)
            {
                if (hasAdded) result.Append(", ");
                result.Append(quadrums == 1 ? "1 quadrum" : $"{quadrums} quadrums");
                hasAdded = true;
            }
            
            // Days
            if (remainingDays > 0 || (!hasAdded && hoursLeft == 0))
            {
                if (hasAdded) result.Append(", ");
                result.Append(remainingDays == 1 ? "1 day" : $"{remainingDays} days");
                hasAdded = true;
            }
            
            // Hours - only show if less than 1 day left
            if (hoursLeft > 0 && daysLeft == 0)
            {
                if (hasAdded) result.Append(", ");
                result.Append(hoursLeft == 1 ? "1 hour" : $"{hoursLeft} hours");
            }
            
            return result.ToString();
        }

        public virtual void OpenEditDialog()
        {
            Find.WindowStack.Add(new Dialog_EditReminder(this));
        }

        public string GetFrequencyDisplayString()
        {
            switch (frequency)
            {
                case ReminderFrequency.OneTime:
                    return "One Time";
                case ReminderFrequency.Daily:
                    return "Daily";
                case ReminderFrequency.Weekly:
                    return "Weekly";
                case ReminderFrequency.Monthly:
                    return "Monthly";
                case ReminderFrequency.Quarterly:
                    return "Quarterly";
                case ReminderFrequency.Yearly:
                    return "Yearly";
                case ReminderFrequency.WhenTendingRequired:
                    return "Tending";
                default:
                    return frequency.ToString();
            }
        }
    }
} 