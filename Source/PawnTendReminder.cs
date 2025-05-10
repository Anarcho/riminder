using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace Riminder
{
    public class PawnTendReminder : Reminder
    {
        public string pawnId;
        public string hediffId;
        public bool removeOnImmunity;
        public string pawnLabel;
        public string hediffLabel;
        private bool alerted = false;
        public float tendProgress = 0f;
        public int totalTendDuration = -1;

        
        public int actualTendTicksLeft = -1;
        public bool needsImmediateTending = false;
        private string cachedTendStatus = string.Empty;

        public class TrackedHediffInfo : IExposable
        {
            public long loadID;
            public string defName;
            public int? partIndex;


            public TrackedHediffInfo()
            {
            }

            public TrackedHediffInfo(long loadID, string defName, int? partIndex = null)
            {
                this.loadID = loadID;
                this.defName = defName;
                this.partIndex = partIndex;
            }

            public void ExposeData()
            {
                Scribe_Values.Look(ref loadID, "loadID");
                Scribe_Values.Look(ref defName, "defName");
                Scribe_Values.Look(ref partIndex, "partIndex");
            }
        }

        public List<TrackedHediffInfo> trackedHediffs = new List<TrackedHediffInfo>();

        public PawnTendReminder() : base() { }

        public PawnTendReminder(Pawn pawn, Hediff hediff, bool removeOnImmunity)
        {
            if (pawn == null)
            {
                this.id = Guid.NewGuid().ToString();
                this.label = "Invalid Reminder";
                this.description = "This reminder was created incorrectly and should be deleted.";
                this.createdTick = Find.TickManager.TicksGame;
                this.triggerTick = Find.TickManager.TicksGame;
                return;
            }
            this.pawnId = pawn.ThingID;
            this.removeOnImmunity = removeOnImmunity;
            this.pawnLabel = pawn.LabelShort;
            this.frequency = ReminderFrequency.WhenTendingRequired;
            this.createdTick = Find.TickManager.TicksGame;
            this.id = Guid.NewGuid().ToString();

            this.trackedHediffs = pawn.health.hediffSet.hediffs
                .Where(h => h is HediffWithComps hwc && hwc.TryGetComp<HediffComp_TendDuration>() != null)
                .Select(h => new TrackedHediffInfo(h.loadID, h.def.defName, h.Part != null ? (int?)h.Part.Index : null))
                .ToList();

            UpdateLabelAndDescription(pawn);
            this.triggerTick = CalculateNextTendTick(pawn);
        }

        private Hediff GetMostUrgentTendableHediff(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return null;
            Hediff mostUrgent = null;
            int minTicksLeft = int.MaxValue;

            foreach (var hediff in pawn.health.hediffSet.hediffs)
            {
                if (hediff.IsPermanent()) continue;
                if (hediff is Hediff_Injury injury && injury.Bleeding && injury.TendableNow())
                {
                    return hediff;
                }
            }

            foreach (var hediff in pawn.health.hediffSet.hediffs)
            {
                if (hediff.IsPermanent()) continue;
                if (hediff is HediffWithComps hwc)
                {
                    var tendComp = hwc.TryGetComp<HediffComp_TendDuration>();
                    if (tendComp == null) continue;
                    if (tendComp.tendTicksLeft == -1)
                    {

                        if (mostUrgent == null) mostUrgent = hediff;
                    }
                    else if (tendComp.tendTicksLeft > 0 && tendComp.tendTicksLeft < minTicksLeft)
                    {
                        minTicksLeft = tendComp.tendTicksLeft;
                        mostUrgent = hediff;
                    }
                }
            }
            return mostUrgent;
        }

        public void UpdateLabelAndDescription(Pawn pawn)
        {
            if (pawn == null || pawn.health == null || pawn.health.hediffSet == null)
            {
                this.label = "Invalid Pawn Data";
                this.description = "Pawn or health data not found for this reminder.";
                this.hediffId = null;
                this.hediffLabel = "Unknown";
                return;
            }
            this.pawnLabel = pawn.LabelShort;
            var next = GetMostUrgentTendableHediff(pawn);
            if (next != null)
            {
                this.hediffId = next.loadID > 0 ? next.loadID.ToString() : null;
                this.hediffLabel = next.def?.label ?? "Unknown";
                this.label = $"Tend {pawn.LabelShort}'s {next.Label}";
            }
            else
            {
                this.label = $"Monitor {pawn.LabelShort}";
                this.hediffId = null;
                this.hediffLabel = "None";
            }

            
            this.description = GetTendDescription(pawn, this.actualTendTicksLeft, this.needsImmediateTending);

            
            this.cachedTendStatus = this.description;
        }

        private Hediff GetNextTendableHediff(Pawn pawn)
        {

            return GetMostUrgentTendableHediff(pawn);
        }

        private static int CalculateNextTendTick(Pawn pawn)
        {
            int currentTick = Find.TickManager.TicksGame;
            int soonestTendTick = currentTick + GenDate.TicksPerDay;
            bool needsTendingNow = false;

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
                        soonestTendTick = Math.Min(soonestTendTick, currentTick + tendComp.tendTicksLeft);
                    }
                }
            }

            return needsTendingNow ? currentTick : soonestTendTick;
        }

        private static string FormatTimeLeft(float ticksLeft)
        {
            float daysLeft = ticksLeft / (float)GenDate.TicksPerDay;
            float hoursLeft = ticksLeft / (float)GenDate.TicksPerHour;
            float minutesLeft = ticksLeft / (float)(GenDate.TicksPerHour / 60);
            float secondsLeft = ticksLeft / (float)(GenDate.TicksPerHour / 3600);

            if (daysLeft >= 1.0f)
                return $"{daysLeft:F1} days";
            else if (hoursLeft >= 1.0f)
                return $"{hoursLeft:F1} hours";
            else if (minutesLeft >= 1.0f)
                return $"{minutesLeft:F0} minutes";
            else
                return $"{Math.Max(1, (int)secondsLeft):F0} seconds";
        }

        private static string GetTendDescription(Pawn pawn, int overrideTendTicksLeft = -1, bool overrideNeedsTending = false)
        {
            if (pawn == null) return "Invalid pawn";
            var hediffs = new List<Hediff>();
            TendUtility.GetOptimalHediffsToTendWithSingleTreatment(pawn, false, hediffs);
            if (!hediffs.Any())
                return $"No conditions currently require tending for {pawn.LabelShort}.";
            var desc = $"Tending status for {pawn.LabelShort}:";
            foreach (var h in hediffs)
            {
                desc += $"\n- {h.Label}";
                if (h is HediffWithComps hwc)
                {
                    var tendComp = hwc.TryGetComp<HediffComp_TendDuration>();
                    if (tendComp != null)
                    {
                        int tendTicksLeft = overrideTendTicksLeft >= 0 ?
                            overrideTendTicksLeft : tendComp.tendTicksLeft;

                        bool needsTending = overrideNeedsTending ||
                            !tendComp.IsTended || tendTicksLeft <= 0 || h.TendableNow();

                        if (tendComp.IsTended && !needsTending)
                        {
                            // Calculate when it can be tended again - this is the "Can be tended in X" time
                            int nextTendableInTicks = 0;
                            if (tendTicksLeft > 0)
                            {
                                nextTendableInTicks = tendTicksLeft - tendComp.TProps.TendTicksOverlap;
                            }
                            
                            float hoursUntilTendable = nextTendableInTicks / (float)GenDate.TicksPerHour;
                            
                            if (nextTendableInTicks <= 0)
                            {
                                desc += " (Can be tended now)";
                            }
                            else
                            {
                                // Show next tendable time, matching exactly what the game shows
                                desc += $" (Tended {tendComp.tendQuality:P0}, next tend in {hoursUntilTendable:F1}h)";
                            }
                        }
                        else
                        {
                            desc += " (Needs tending)";
                        }
                    }

                    var imm = hwc.TryGetComp<HediffComp_Immunizable>();
                    if (imm != null)
                    {
                        desc += $"\n<color=#FFFF00>Immunity: {imm.Immunity:P0}";
                        if (imm.Immunity >= 0.8f)
                            desc += " (Almost immune)";
                        else if (imm.Immunity >= 0.5f)
                            desc += " (Good progress)";
                        desc += "</color>";
                    }
                }

                if (h is Hediff_Injury injury && injury.Bleeding)
                {
                    int ticksUntilDeath = HealthUtility.TicksUntilDeathDueToBloodLoss(pawn);
                    if (ticksUntilDeath < int.MaxValue)
                    {
                        desc += $"\n<color=#FF4444>Death from blood loss in {FormatTimeLeft(ticksUntilDeath)}</color>";
                    }
                }
            }
            return desc;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref pawnId, "pawnId");
            Scribe_Values.Look(ref hediffId, "hediffId");
            Scribe_Values.Look(ref removeOnImmunity, "removeOnImmunity", false);
            Scribe_Values.Look(ref alerted, "alerted", false);
            Scribe_Values.Look(ref totalTendDuration, "totalTendDuration", -1);
            Scribe_Values.Look(ref pawnLabel, "pawnLabel");
            Scribe_Values.Look(ref hediffLabel, "hediffLabel");
            Scribe_Values.Look(ref tendProgress, "tendProgress", 0f);
            Scribe_Collections.Look(ref trackedHediffs, "trackedHediffs", LookMode.Deep);

            
            Scribe_Values.Look(ref actualTendTicksLeft, "actualTendTicksLeft", -1);
            Scribe_Values.Look(ref needsImmediateTending, "needsImmediateTending", false);
            Scribe_Values.Look(ref cachedTendStatus, "cachedTendStatus", string.Empty);

            if (Scribe.mode == LoadSaveMode.LoadingVars && trackedHediffs == null)
            {
                trackedHediffs = new List<TrackedHediffInfo>();
            }
        }

        public override void Trigger()
        {
            Pawn pawn = FindPawn();
            if (pawn == null || pawn.health == null || pawn.health.hediffSet == null)
            {
                if (Prefs.DevMode)
                    Log.Warning($"[Riminder] Completing reminder {id} because pawn or health/hediffSet is null. PawnId: {pawnId}");
                completed = true;
                return;
            }

            RefreshTrackedHediffs(pawn);
            var next = FindHediff(pawn);
            
            // If no tendable hediffs are found at all, mark as not needing tending
            if (next == null)
            {
                if (Prefs.DevMode)
                    Log.Warning($"[Riminder] No tendable hediff found for pawn {pawn.LabelShort} ({pawnId}) in reminder {id}. Setting needsImmediateTending=false.");
                
                this.needsImmediateTending = false;
                this.actualTendTicksLeft = -1;
                
                // Should we complete the reminder instead?
                if (GetTrackedHediffs(pawn).Count == 0)
                {
                    if (Prefs.DevMode)
                        Log.Message($"[Riminder] No tracked hediffs left for {pawn.LabelShort}. Completing reminder.");
                    
                    completed = true;
                    return;
                }
            }
            
            bool forceRefresh = this.actualTendTicksLeft == -1;
            
            if (forceRefresh) 
            {
                this.needsImmediateTending = false;
            }

            // Only proceed with checks if we found a valid hediff
            if (next != null)
            {
                bool needsTending = false;
                
                // First check: direct TendableNow
                if (next.TendableNow())
                {
                    needsTending = true;
                    this.actualTendTicksLeft = 0;
                }
                // Check for bleeding injury
                else if (next is Hediff_Injury injury && injury.Bleeding)
                {
                    needsTending = true;
                    this.actualTendTicksLeft = 0;
                }
                // Check for tending components
                else if (next is HediffWithComps hwc)
                {
                    var tendComp = hwc.TryGetComp<HediffComp_TendDuration>();
                    if (tendComp != null)
                    {
                        int newTendTicksLeft = tendComp.tendTicksLeft;
                        
                        if (forceRefresh || newTendTicksLeft >= 0)
                        {
                            this.actualTendTicksLeft = newTendTicksLeft;
                            
                            if (Prefs.DevMode && forceRefresh)
                                Log.Message($"[Riminder] Force refreshed tend ticks for {pawn.LabelShort}: {this.actualTendTicksLeft}");
                        }

                        if (this.actualTendTicksLeft > 0 && totalTendDuration > 0)
                        {
                            tendProgress = 1f - ((float)this.actualTendTicksLeft / totalTendDuration);
                        }
                        else if (tendComp.IsTended)
                        {
                            if (totalTendDuration <= 0 && this.actualTendTicksLeft > 0)
                            {
                                totalTendDuration = this.actualTendTicksLeft;
                                if (Prefs.DevMode)
                                    Log.Message($"[Riminder] Set total tend duration for {pawn.LabelShort}: {totalTendDuration}");
                            }
                        }

                        // Only set needs tending if it's NOT tended or tend duration has expired
                        if (!tendComp.IsTended || tendComp.tendTicksLeft <= 0)
                        {
                            needsTending = true;
                        }
                        else if (tendComp.IsTended && tendComp.tendTicksLeft > 0)
                        {
                            // Explicitly set to false if it's tended and has time left
                            needsTending = false;
                        }
                    }
                }
                
                // Only update flag if we found something needs tending or if forcing refresh
                if (needsTending || forceRefresh)
                {
                    this.needsImmediateTending = needsTending;
                    
                    if (Prefs.DevMode)
                        Log.Message($"[Riminder] Updated tend status for {pawn.LabelShort}: needsImmediateTending={this.needsImmediateTending}");
                }
            }
            else
            {
                // No valid hediff, definitely doesn't need tending
                this.needsImmediateTending = false;
            }

            // Update label and description
            UpdateLabelAndDescription(pawn);

            // Handle the notification state
            if (this.needsImmediateTending)
            {
                // Double-check we actually have tendable hediffs before alerting
                List<Hediff> tendableHediffs = new List<Hediff>();
                TendUtility.GetOptimalHediffsToTendWithSingleTreatment(pawn, false, tendableHediffs);
                
                if (!tendableHediffs.Any())
                {
                    if (Prefs.DevMode)
                        Log.Warning($"[Riminder] TendUtility reports no tendable hediffs for {pawn.LabelShort} but needsImmediateTending=true. Correcting state.");
                    
                    this.needsImmediateTending = false;
                }
                
                // Only alert if there are actually tendable hediffs and we haven't alerted yet
                if (this.needsImmediateTending && !alerted)
                {
                    if (Prefs.DevMode)
                        Log.Message($"[Riminder] Triggering tend reminder for {pawn.LabelShort} ({pawnId}) for hediff {next?.Label ?? "unknown"}");
                    
                    Find.LetterStack.ReceiveLetter(
                        "Tend Reminder: " + pawn.LabelShort,
                        this.description,
                        LetterDefOf.NeutralEvent,
                        new LookTargets(pawn));
                    
                    if (RiminderMod.Settings.pauseOnReminder)
                        Find.TickManager.Pause();
                    
                    alerted = true;
                }
            }
            else
            {
                if (alerted && Prefs.DevMode)
                    Log.Message($"[Riminder] Resetting alert for {pawn.LabelShort} ({pawnId}) in reminder {id}.");
                
                alerted = false;
            }

            // Set reminder's next trigger based on tend ticks if available
            int oldTriggerTick = this.triggerTick;
            
            if (this.actualTendTicksLeft > 0)
            {
                this.triggerTick = Find.TickManager.TicksGame + this.actualTendTicksLeft;
                if (Prefs.DevMode && oldTriggerTick != this.triggerTick)
                    Log.Message($"[Riminder] Updated trigger time for {pawn.LabelShort} ({pawnId}): {this.actualTendTicksLeft} ticks (in {this.actualTendTicksLeft / (float)GenDate.TicksPerHour:F1}h)");
            }
            else if (this.needsImmediateTending)
            {
                this.triggerTick = Find.TickManager.TicksGame;
                if (Prefs.DevMode)
                    Log.Message($"[Riminder] Pawn {pawn.LabelShort} needs immediate tending");
            }
            else
            {
                this.triggerTick = CalculateNextTendTick(pawn);
                if (Prefs.DevMode && oldTriggerTick != this.triggerTick)
                    Log.Message($"[Riminder] Calculated trigger for {pawn.LabelShort} ({pawnId}): {GetTimeLeftString()}");
            }
        }


        public List<Hediff> GetTrackedHediffs(Pawn pawn)
        {
            if (pawn == null || trackedHediffs == null) return new List<Hediff>();
            var hediffs = new List<Hediff>();
            foreach (var info in trackedHediffs.ToList())
            {
                var found = pawn.health.hediffSet.hediffs.FirstOrDefault(h =>
                    h.loadID == info.loadID ||
                    (h.def.defName == info.defName && (info.partIndex == null || (h.Part != null && h.Part.Index == info.partIndex))));
                if (found != null)
                    hediffs.Add(found);
                else
                    trackedHediffs.Remove(info);
            }
            return hediffs;
        }


        public Hediff FindHediff(Pawn pawn)
        {
            var tracked = GetTrackedHediffs(pawn);
            if (tracked.Count == 0) return null;

            var bleeding = tracked.FirstOrDefault(h => h is Hediff_Injury inj && inj.Bleeding && inj.TendableNow());
            if (bleeding != null) return bleeding;
            return tracked.OrderBy(h =>
                h is HediffWithComps hwc && hwc.TryGetComp<HediffComp_TendDuration>() != null ?
                    (hwc.TryGetComp<HediffComp_TendDuration>().tendTicksLeft == -1 ? int.MinValue : hwc.TryGetComp<HediffComp_TendDuration>().tendTicksLeft)
                    : int.MaxValue).First();
        }

        public Pawn FindPawn()
        {
            return Find.CurrentMap?.mapPawns?.AllPawns.FirstOrDefault(p => p.ThingID == pawnId);
        }


        public override string GetTimeLeftString()
        {
            Pawn pawn = FindPawn();
            if (pawn == null) return base.GetTimeLeftString();

            var next = FindHediff(pawn);
            
            // Check if we have a hediff with a tend duration component
            if (next != null && next is HediffWithComps hwc)
            {
                var tendComp = hwc.TryGetComp<HediffComp_TendDuration>();
                if (tendComp != null && tendComp.IsTended && tendComp.tendTicksLeft > 0)
                {
                    // Use EXACTLY the game's calculation for "Can be tended in X hours"
                    int nextTendableInTicks = tendComp.tendTicksLeft - tendComp.TProps.TendTicksOverlap;
                    
                    if (nextTendableInTicks <= 0)
                    {
                        return "Now"; // Can be tended now
                    }
                    else
                    {
                        return $"in {FormatTimeLeft(nextTendableInTicks)}";
                    }
                }
            }
    
            // Then fall back to our cached logic
            if (this.needsImmediateTending)
                return "Now";

            if (this.actualTendTicksLeft > 0)
            {
                // Use the same calculation for the cached value
                int nextTendableInTicks = this.actualTendTicksLeft;
                
                if (next != null && next is HediffWithComps hwc3)
                {
                    var tendComp = hwc3.TryGetComp<HediffComp_TendDuration>();
                    if (tendComp != null)
                    {
                        nextTendableInTicks = this.actualTendTicksLeft - tendComp.TProps.TendTicksOverlap;
                    }
                }
                
                if (nextTendableInTicks <= 0)
                    return "Now";
                    
                return $"in {FormatTimeLeft(nextTendableInTicks)}";
            }
            
            // Last resort - try to find any tendable hediff
            Hediff nextTendable = FindHediff(pawn);
            if (nextTendable != null && nextTendable is HediffWithComps hwc2)
            {
                var tendComp = hwc2.TryGetComp<HediffComp_TendDuration>();
                if (tendComp != null)
                {
                    int tendTicksLeft = tendComp.tendTicksLeft;
                    if (tendTicksLeft > 0)
                    {
                        // Again use the "Can be tended in X" time
                        int nextTendableInTicks = tendTicksLeft - tendComp.TProps.TendTicksOverlap;
                        if (nextTendableInTicks <= 0)
                            return "Now";
                        return $"in {FormatTimeLeft(nextTendableInTicks)}";
                    }
                    else if (tendTicksLeft <= 0 && tendComp.IsTended)
                    {
                        return "ready soon"; 
                    }
                }
            }
            
            return base.GetTimeLeftString();
        }


        public void RefreshTrackedHediffs(Pawn pawn)
        {
            if (pawn == null || pawn.health == null || pawn.health.hediffSet == null) return;
            var current = new HashSet<string>(trackedHediffs.Select(t => $"{t.loadID}_{t.defName}_{t.partIndex}"));
            foreach (var h in pawn.health.hediffSet.hediffs)
            {
                if (h is HediffWithComps hwc && hwc.TryGetComp<HediffComp_TendDuration>() != null)
                {
                    string key = $"{h.loadID}_{h.def.defName}_{(h.Part != null ? h.Part.Index.ToString() : "")}";
                    if (!current.Contains(key))
                    {
                        trackedHediffs.Add(new TrackedHediffInfo(h.loadID, h.def.defName, h.Part != null ? (int?)h.Part.Index : null));
                    }
                }
            }
        }

        public override void OpenEditDialog()
        {
            Find.WindowStack.Add(new Dialog_EditTendReminder(this));
        }
    }
}

