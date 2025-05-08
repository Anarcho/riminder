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

            Find.LetterStack.ReceiveLetter(
                "Reminder: " + label,
                description,
                LetterDefOf.NeutralEvent);

            Log.Message($"[Riminder] Reminder triggered: {label}");

            if (RiminderMod.Settings.pauseOnReminder)
            {
                Find.TickManager.Pause();
            }

            if (frequency == ReminderFrequency.OneTime)
            {
                completed = true;
                return;
            }

            RescheduleNext();
        }

        public void RescheduleNext()
        {
            int currentTick = Find.TickManager.TicksGame;

            switch (frequency)
            {
                case ReminderFrequency.Daily:
                    triggerTick = currentTick + GenDate.TicksPerDay;
                    break;
                case ReminderFrequency.Weekly:
                    triggerTick = currentTick + GenDate.TicksPerDay * 7;
                    break;
                case ReminderFrequency.Monthly:
                    triggerTick = currentTick + GenDate.TicksPerDay * 30;
                    break;
                case ReminderFrequency.Quarterly:
                    triggerTick = currentTick + GenDate.TicksPerDay * 90;
                    break;
                case ReminderFrequency.Yearly:
                    triggerTick = currentTick + GenDate.TicksPerDay * 60;
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

            const int daysPerYear = 60;
            const int daysPerQuadrum = 15;

            int daysLeft = ticksLeft / GenDate.TicksPerDay;
            int hoursLeft = (ticksLeft % GenDate.TicksPerDay) / GenDate.TicksPerHour;

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

            if (remainingDays > 0 || (!hasAdded && hoursLeft == 0))
            {
                if (hasAdded) result.Append(", ");
                result.Append(remainingDays == 1 ? "1 day" : $"{remainingDays} days");
                hasAdded = true;
            }

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
