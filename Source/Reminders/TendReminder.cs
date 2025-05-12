using System;
using System.Collections.Generic;
using Verse;
using RimWorld;
using System.Linq;

namespace Riminder
{
    public class TendReminder : BaseReminder
    {
        // Fields for tracking tend progress (used by Harmony patches)
        public int totalTendDuration = -1;
        public float tendProgress = 0f;
        public int actualTendTicksLeft = -1;

        // Field to track if tending was needed last tick
        private bool wasNeedingTendLastTick = false;

        // Our data provider cast for convenience
        protected internal TendReminderDataProvider TendData => dataProvider as TendReminderDataProvider;
        
        // Constructor for creating a new reminder
        public TendReminder() : base()
        {
            this.frequency = ReminderFrequency.WhenTendingRequired;
        }
        
        // Constructor with pawn and hediff
        public TendReminder(Pawn pawn, Hediff hediff, bool removeOnImmunity) : base()
        {
            this.frequency = ReminderFrequency.WhenTendingRequired;
            
            // Create and configure the data provider
            var hediffs = new List<Hediff>();
            if (hediff != null)
            {
                hediffs.Add(hediff);
            }
            
            // We now focus only on the specified hediff instead of adding all tendable hediffs
            // This prevents potential duplicate reminder creation issues
            this.dataProvider = new TendReminderDataProvider(pawn, hediffs, removeOnImmunity);
            
            // First refresh to set up
            this.dataProvider.Refresh();
            
            // Set initial trigger time
            this.triggerTick = CalculateNextTendTick(pawn);
        }
        
        public override void ExposeData()
        {
            base.ExposeData();
            
            // Create data provider if it doesn't exist
            if (Scribe.mode == LoadSaveMode.LoadingVars && dataProvider == null)
            {
                dataProvider = new TendReminderDataProvider();
            }
            
            // Save the data provider
            if (dataProvider is TendReminderDataProvider tendData)
            {
                tendData.ExposeData();
            }
        }
        
        public override void Trigger()
        {
            // Update data first
            dataProvider?.Refresh();
            
            // Ensure progress is synchronized
            if (dataProvider != null)
            {
                // Force progress update from data provider
                tendProgress = ((TendReminderDataProvider)dataProvider).GetProgress();
            }
            
            bool needsAttention = dataProvider != null && ((TendReminderDataProvider)dataProvider).NeedsAttention();
            
            // Only trigger if state changed from not needing to needing tend
            // And only if tend alerts are enabled
            if (needsAttention && !wasNeedingTendLastTick && 
                RiminderMod.Settings.showNotifications && RiminderMod.Settings.enableTendAlerts)
            {
                Find.LetterStack.ReceiveLetter(
                    "Tend Reminder: " + GetLabel(),
                    GetDescription(),
                    LetterDefOf.NeutralEvent,
                    new LookTargets(FindPawn()));
                
                if (RiminderMod.Settings.pauseOnReminder)
                {
                    Find.TickManager.Pause();
                }
            }
            
            wasNeedingTendLastTick = needsAttention;
            
            // Update trigger time
            this.triggerTick = CalculateNextTendTick(FindPawn());
            
            // Check if we should complete the reminder
            if (ShouldComplete())
            {
                this.completed = true;
            }
        }
        
        public bool ShouldComplete()
        {
            // No data provider or pawn means we should complete
            if (dataProvider == null || FindPawn() == null) 
                return true;
                
            // Get the pawn
            Pawn pawn = FindPawn();
            
            // Check remove on immunity condition
            if (TendData.removeOnImmunity)
            {
                // Check if all tracked hediffs are either cured or immune
                foreach (var hediff in pawn.health.hediffSet.hediffs)
                {
                    if (hediff is HediffWithComps hwc)
                    {
                        var immComp = hwc.TryGetComp<HediffComp_Immunizable>();
                        if (immComp != null && immComp.Immunity >= 1.0f)
                        {
                            return true;
                        }
                    }
                }
            }
            
            // If there are no tendable hediffs, complete the reminder
            return !pawn.health.hediffSet.hediffs.Any(h => 
                h is HediffWithComps hwc && 
                hwc.TryGetComp<HediffComp_TendDuration>() != null);
        }
        
        private int CalculateNextTendTick(Pawn pawn)
        {
            int currentTick = Find.TickManager.TicksGame;
            int soonestTendTick = currentTick + GenDate.TicksPerDay;
            bool needsTendingNow = false;
            
            if (pawn == null) return currentTick;

            foreach (var hediff in pawn.health.hediffSet.hediffs)
            {
                if (!hediff.def.tendable || hediff.IsPermanent()) continue;

                if (hediff is Hediff_Injury injury && injury.Bleeding)
                {
                    return currentTick; // Immediate tending needed
                }

                if (hediff is HediffWithComps hwc)
                {
                    var tendComp = hwc.TryGetComp<HediffComp_TendDuration>();
                    if (tendComp == null) continue;

                    if (!tendComp.IsTended || tendComp.tendTicksLeft <= 0)
                    {
                        needsTendingNow = true;
                    }
                    else if (tendComp.tendTicksLeft > 0)
                    {
                        // Use the corrected calculation
                        int nextTendableInTicks = tendComp.tendTicksLeft - tendComp.TProps.TendTicksOverlap;
                        
                        if (nextTendableInTicks <= 0)
                        {
                            needsTendingNow = true; // Can be tended now
                        }
                        else
                        {
                            soonestTendTick = Math.Min(soonestTendTick, currentTick + nextTendableInTicks);
                        }
                    }
                }
            }

            return needsTendingNow ? currentTick : soonestTendTick;
        }
        
        public override void OpenEditDialog()
        {
            Find.WindowStack.Add(new Dialog_EditTendReminder(GetReminderData()));
        }
        
        // Helper to convert to a generic Reminder for UI compatibility
        public Reminder GetReminderData()
        {
            if (dataProvider is TendReminderDataProvider tendData)
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
        
        // Helper to find the pawn for this reminder
        public Pawn FindPawn()
        {
            if (dataProvider is TendReminderDataProvider tendData)
            {
                string pawnId = tendData.pawnId;
                return Find.CurrentMap?.mapPawns?.AllPawns.FirstOrDefault(p => p.ThingID == pawnId);
            }
            return null;
        }
        
        // Return the latest progress value for UI
        public override float GetProgress()
        {
            // Force a recalculation first
            ForceProgressUpdate();
            return tendProgress;
        }
        
        // Force progress update method that can be called from any code path
        public void ForceProgressUpdate()
        {
            Pawn pawn = FindPawn();
            if (pawn == null) return;
            
            // DEBUG: Log all hediff information in dev mode
            if (Prefs.DevMode)
            {
                Log.Message($"[Riminder] ForceProgressUpdate for {pawn.LabelShort} - Starting hediff check");
                foreach (var h in pawn.health.hediffSet.hediffs)
                {
                    if (h is HediffWithComps hwc && hwc.TryGetComp<HediffComp_TendDuration>() != null)
                    {
                        var comp = hwc.TryGetComp<HediffComp_TendDuration>();
                        Log.Message($"[Riminder] Hediff: {h.Label}, isTended: {comp.IsTended}, tendTicksLeft: {comp.tendTicksLeft}, chronic: {h.def.chronic}");
                    }
                }
            }
            
            // Reset progress to ensure a clean calculation
            tendProgress = 0f;
            
            // Update the data provider first to ensure we have latest data
            if (dataProvider != null)
            {
                dataProvider.Refresh();
                
                // After refresh, get the latest progress directly from the data provider
                // which handles prioritization between multiple hediffs
                tendProgress = dataProvider.GetProgress();
                
                if (Prefs.DevMode && tendProgress > 0)
                {
                    Log.Message($"[Riminder] Progress from data provider: {tendProgress}");
                }
                
                // If data provider gave us a valid progress, use it directly
                if (tendProgress > 0)
                {
                    return;
                }
            }
            
            // Get the most urgent hediff based on label matching
            string reminderLabel = GetLabel().ToLowerInvariant();
            var matchingHediffs = new List<Hediff>();
            
            // First pass - find all hediffs mentioned in the reminder label
            foreach (var hediff in pawn.health.hediffSet.hediffs)
            {
                if (hediff is HediffWithComps hwc && hwc.TryGetComp<HediffComp_TendDuration>() != null)
                {
                    if (reminderLabel.ToLowerInvariant().Contains(hediff.def.label.ToLowerInvariant()))
                    {
                        matchingHediffs.Add(hediff);
                    }
                }
            }
            
            // If no matches by label, fall back to checking all tendable hediffs
            if (matchingHediffs.Count == 0)
            {
                foreach (var hediff in pawn.health.hediffSet.hediffs)
                {
                    if (hediff is HediffWithComps hwc && hwc.TryGetComp<HediffComp_TendDuration>() != null)
                    {
                        matchingHediffs.Add(hediff);
                    }
                }
            }
            
            // Sort hediffs by priority, bleeding first, then disease, then by tend ticks remaining
            var prioritizedHediffs = matchingHediffs
                .OrderByDescending(h => h is Hediff_Injury injury && injury.Bleeding)
                .ThenByDescending(h => h.TendPriority)
                .ThenByDescending(h => h.def.HasComp(typeof(HediffComp_Immunizable)) || 
                                       (h is HediffWithComps hwc && hwc.TryGetComp<HediffComp_Immunizable>() != null))
                .ThenBy(h => {
                    // Sort by tend ticks left, placing hediffs that need tending now first
                    if (h is HediffWithComps hwc)
                    {
                        var tendComp = hwc.TryGetComp<HediffComp_TendDuration>();
                        if (tendComp != null)
                        {
                            if (!tendComp.IsTended) return int.MinValue; // Not tended at all = highest priority
                            return tendComp.tendTicksLeft;
                        }
                    }
                    return int.MaxValue;
                })
                .ToList();
                
            if (prioritizedHediffs.Count > 0)
            {
                // Use the highest priority hediff for progress
                var urgentHediff = prioritizedHediffs[0];
                if (urgentHediff is HediffWithComps hwc)
                {
                    var tendComp = hwc.TryGetComp<HediffComp_TendDuration>();
                    if (tendComp != null)
                    {
                        // Initialize totalDuration if not set
                        if (totalTendDuration <= 0)
                        {
                            totalTendDuration = GenDate.TicksPerDay;
                        }
                        
                        actualTendTicksLeft = tendComp.tendTicksLeft;
                        
                        // Use the unified progress calculation method
                        tendProgress = TendReminderDataProvider.CalculateHediffProgress(urgentHediff, tendComp);
                        
                        if (Prefs.DevMode)
                        {
                            Log.Message($"[Riminder] Progress calculated for {urgentHediff.Label}: {tendProgress}, tendTicksLeft: {tendComp.tendTicksLeft}, totalDuration: {totalTendDuration}");
                        }
                    }
                }
            }
        }
    }
}