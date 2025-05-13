using System;
using RimWorld;
using Verse;
using System.Linq;

namespace Riminder
{
    public class RitualReminder : BaseReminder
    {
        public RitualReminderDataProvider RitualData => dataProvider as RitualReminderDataProvider;

        public RitualReminder() : base()
        {
            this.frequency = ReminderFrequency.Custom;
        }

        public RitualReminder(Precept_Ritual ritual) : base()
        {
            this.frequency = ReminderFrequency.Custom;
            this.dataProvider = new RitualReminderDataProvider(ritual);
            this.dataProvider.Refresh();
            this.triggerTick = CalculateNextRitualTick(ritual);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.LoadingVars && dataProvider == null)
            {
                dataProvider = new RitualReminderDataProvider();
            }
            if (dataProvider is RitualReminderDataProvider ritualData)
            {
                ritualData.ExposeData();
            }
        }

        public override void Trigger()
        {
            dataProvider?.Refresh();
            bool needsAttention = dataProvider != null && RitualData.NeedsAttention();
            if (needsAttention && RiminderMod.Settings.showNotifications)
            {
                Find.LetterStack.ReceiveLetter(
                    "Ritual Reminder: " + GetLabel(),
                    GetDescription(),
                    LetterDefOf.NeutralEvent);
                if (RiminderMod.Settings.pauseOnReminder)
                {
                    Find.TickManager.Pause();
                }
            }
            this.triggerTick = CalculateNextRitualTick(RitualData?.GetRitual());
            if (ShouldComplete())
            {
                this.completed = true;
            }
        }

        public bool ShouldComplete()
        {
            return dataProvider == null || RitualData?.GetRitual() == null;
        }

        private int CalculateNextRitualTick(Precept_Ritual ritual)
        {
            if (ritual == null) return Find.TickManager.TicksGame;
            if (ritual.isAnytime)
            {
                return Find.TickManager.TicksGame + 60000; 
            }
            if (ritual.IsDateTriggered)
            {
                
                var dateTrigger = ritual.obligationTriggers?.OfType<RitualObligationTrigger_Date>().FirstOrDefault();
                if (dateTrigger != null)
                {
                    return dateTrigger.OccursOnTick();
                }
            }
            
            return Find.TickManager.TicksGame + 60000;
        }

        public override void OpenEditDialog()
        {
            Find.WindowStack.Add(new Dialog_EditRitualReminder(this));
        }

        public Reminder GetReminderData()
        {
            if (dataProvider is RitualReminderDataProvider ritualData)
            {
                var result = new Reminder();
                result.SetLabel(GetLabel());
                result.SetDescription(GetDescription());
                result.frequency = this.frequency;
                result.createdTick = this.createdTick;
                result.triggerTick = this.triggerTick;
                return result;
            }
            return new Reminder();
        }

        public override float GetProgress()
        {
            dataProvider?.Refresh();
            return dataProvider?.GetProgress() ?? 0f;
        }
    }
}
