using System;
using System.Collections.Generic;
using Verse;
using RimWorld;

namespace Riminder
{
    public enum ReminderFrequency
    {
        OneTime,
        Days,
        Quadrums,
        Years,
        Custom,
        WhenTendingRequired
    }

    public class Reminder : BaseReminder
    {
        private string _label;
        private string _description;
        public Dictionary<string, string> metadata;

        public Reminder() : base() 
        { 
            metadata = new Dictionary<string, string>();
        }

        public Reminder(string label, string description, int triggerTick, ReminderFrequency frequency = ReminderFrequency.OneTime)
            : base()
        {
            this._label = label;
            this._description = description;
            this.triggerTick = triggerTick;
            this.frequency = frequency;
            this.metadata = new Dictionary<string, string>();
        }

        public override string GetLabel() => _label ?? base.GetLabel();
        public override string GetDescription() => _description ?? base.GetDescription();

        public void SetLabel(string label) => _label = label;
        public void SetDescription(string description) => _description = description;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _label, "label");
            Scribe_Values.Look(ref _description, "description");
            Scribe_Collections.Look(ref metadata, "metadata", LookMode.Value, LookMode.Value);
            
            if (metadata == null)
                metadata = new Dictionary<string, string>();
        }

        public override void Trigger()
        {
            if (dismissed) return;

            Find.LetterStack.ReceiveLetter(
                "Reminder: " + GetLabel(),
                GetDescription(),
                LetterDefOf.NeutralEvent);

            Log.Message($"[Riminder] Reminder triggered: {GetLabel()}");

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

        public override void OpenEditDialog()
        {
            Find.WindowStack.Add(new Dialog_EditReminder(this));
        }

        public override float GetProgress()
        {
            float progress = DefaultProgressValue();
            
            // Always log in dev mode to track progress calculation
            if (Prefs.DevMode)
            {
                int currentTick = Find.TickManager.TicksGame;
                Log.Message($"[Riminder] GetProgress for '{GetLabel()}': {progress:P0} " +
                            $"[current: {currentTick}, created: {createdTick}, trigger: {triggerTick}, " +
                            $"elapsed: {currentTick - createdTick}, total: {triggerTick - createdTick}]");
            }
            
            return progress;
        }
    }
}
