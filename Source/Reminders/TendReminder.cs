using System;
using System.Collections.Generic;
using Verse;
using RimWorld;
using System.Linq;

namespace Riminder
{
    public class TendReminder : BaseReminder
    {
        
        public int totalTendDuration = -1;
        public float tendProgress = 0f;
        public int actualTendTicksLeft = -1;

        
        private bool wasNeedingTendLastTick = false;

        
        protected internal TendReminderDataProvider TendData => dataProvider as TendReminderDataProvider;
        
        
        public TendReminder() : base()
        {
            this.frequency = ReminderFrequency.WhenTendingRequired;
        }
        
        
        public TendReminder(Pawn pawn, Hediff hediff, bool removeOnImmunity) : base()
        {
            this.frequency = ReminderFrequency.WhenTendingRequired;
            
            
            var hediffs = new List<Hediff>();
            if (hediff != null)
            {
                hediffs.Add(hediff);
            }
            
            
            
            this.dataProvider = new TendReminderDataProvider(pawn, hediffs, removeOnImmunity);
            
            
            this.dataProvider.Refresh();
            
            
            this.triggerTick = CalculateNextTendTick(pawn);
        }
        
        public override void ExposeData()
        {
            base.ExposeData();
            
            
            if (Scribe.mode == LoadSaveMode.LoadingVars && dataProvider == null)
            {
                dataProvider = new TendReminderDataProvider();
            }
            
            
            if (dataProvider is TendReminderDataProvider tendData)
            {
                tendData.ExposeData();
            }
        }
        
        public override void Trigger()
        {
            
            dataProvider?.Refresh();
            
            
            if (dataProvider != null)
            {
                
                tendProgress = ((TendReminderDataProvider)dataProvider).GetProgress();
            }
            
            bool needsAttention = dataProvider != null && ((TendReminderDataProvider)dataProvider).NeedsAttention();
            
            
            
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
            
            
            this.triggerTick = CalculateNextTendTick(FindPawn());
            
            
            if (ShouldComplete())
            {
                this.completed = true;
            }
        }
        
        public bool ShouldComplete()
        {
            
            if (dataProvider == null || FindPawn() == null) 
                return true;
                
            
            Pawn pawn = FindPawn();
            
            
            if (TendData.removeOnImmunity)
            {
                
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
                    return currentTick; 
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
                        
                        int nextTendableInTicks = tendComp.tendTicksLeft - tendComp.TProps.TendTicksOverlap;
                        
                        if (nextTendableInTicks <= 0)
                        {
                            needsTendingNow = true; 
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
        
        
        public Pawn FindPawn()
        {
            if (dataProvider is TendReminderDataProvider tendData)
            {
                string pawnId = tendData.pawnId;
                return Find.CurrentMap?.mapPawns?.AllPawns.FirstOrDefault(p => p.ThingID == pawnId);
            }
            return null;
        }
        
        
        public override float GetProgress()
        {
            
            ForceProgressUpdate();
            return tendProgress;
        }
        
        
        public void ForceProgressUpdate()
        {
            Pawn pawn = FindPawn();
            if (pawn == null) return;
            
            
            tendProgress = 0f;
            
            
            if (dataProvider != null)
            {
                dataProvider.Refresh();
                tendProgress = dataProvider.GetProgress();
                
                if (tendProgress > 0)
                {
                    return;
                }
            }
            
            
            string reminderLabel = GetLabel().ToLowerInvariant();
            var matchingHediffs = new List<Hediff>();
            
            
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
            
            
            var prioritizedHediffs = matchingHediffs
                .OrderByDescending(h => h is Hediff_Injury injury && injury.Bleeding)
                .ThenByDescending(h => h.TendPriority)
                .ThenByDescending(h => h.def.HasComp(typeof(HediffComp_Immunizable)) || 
                                       (h is HediffWithComps hwc && hwc.TryGetComp<HediffComp_Immunizable>() != null))
                .ThenBy(h => {
                    if (h is HediffWithComps hwc)
                    {
                        var tendComp = hwc.TryGetComp<HediffComp_TendDuration>();
                        if (tendComp != null)
                        {
                            if (!tendComp.IsTended) return int.MinValue;
                            return tendComp.tendTicksLeft;
                        }
                    }
                    return int.MaxValue;
                })
                .ToList();
                
            if (prioritizedHediffs.Count > 0)
            {
                var urgentHediff = prioritizedHediffs[0];
                if (urgentHediff is HediffWithComps hwc)
                {
                    var tendComp = hwc.TryGetComp<HediffComp_TendDuration>();
                    if (tendComp != null)
                    {
                        
                        if (totalTendDuration <= 0)
                        {
                            totalTendDuration = GenDate.TicksPerDay;
                        }
                        
                        actualTendTicksLeft = tendComp.tendTicksLeft;
                        
                        
                        tendProgress = TendReminderDataProvider.CalculateHediffProgress(urgentHediff, tendComp);
                    }
                }
            }
        }
    }
}