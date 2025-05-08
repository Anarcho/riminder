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

        public PawnTendReminder() : base() { }

        public PawnTendReminder(Pawn pawn, Hediff hediff, bool removeOnImmunity)
        {
            if (pawn == null)
            {
                Log.Error("[Riminder] Attempted to create a PawnTendReminder with null pawn");
                this.id = Guid.NewGuid().ToString();
                this.label = "Invalid Reminder";
                this.description = "This reminder was created incorrectly and should be deleted.";
                this.createdTick = Find.TickManager.TicksGame;
                this.triggerTick = Find.TickManager.TicksGame;
                return;
            }

            this.pawnId = pawn.ThingID;
            this.hediffId = hediff?.loadID > 0 ? hediff.loadID.ToString() : $"{pawn.ThingID}_{hediff?.def.defName}";
            this.removeOnImmunity = removeOnImmunity;
            this.pawnLabel = pawn.LabelShort;
            this.hediffLabel = hediff?.def?.label ?? "Unknown";

            UpdateLabelAndDescription(pawn);
            
            this.frequency = ReminderFrequency.WhenTendingRequired;
            this.createdTick = Find.TickManager.TicksGame;
            this.triggerTick = CalculateNextTendTick(pawn);
            this.id = Guid.NewGuid().ToString();
        }

        public void UpdateLabelAndDescription(Pawn pawn)
        {
            var nextTendableHediff = GetNextTendableHediff(pawn);
            if (nextTendableHediff != null)
            {
                this.label = $"Tend {pawnLabel}'s {nextTendableHediff.Label}";
            }
            else
            {
                this.label = $"Tend {pawnLabel}'s conditions";
            }
            this.description = GetTendDescription(pawn);

            // Force UI refresh after title update
            var openDialogs = Find.WindowStack?.Windows?.OfType<Dialog_ViewReminders>();
            if (openDialogs != null && openDialogs.Any())
            {
                foreach (var dialog in openDialogs)
                {
                    dialog.RefreshAndRedraw();
                }
            }
        }

        private Hediff GetNextTendableHediff(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return null;

            Hediff mostUrgent = null;
            float mostUrgentPriority = float.MinValue;

            foreach (var hediff in pawn.health.hediffSet.hediffs)
            {
                if (!hediff.def.tendable || hediff.IsPermanent()) continue;

                if (hediff is HediffWithComps hwc)
                {
                    var tendComp = hwc.TryGetComp<HediffComp_TendDuration>();
                    if (tendComp == null) continue;

                    float priority = CalculateHediffPriority(hediff);
                    if (priority > mostUrgentPriority)
                    {
                        mostUrgentPriority = priority;
                        mostUrgent = hediff;
                    }
                }
            }

            return mostUrgent;
        }

        private float CalculateHediffPriority(Hediff hediff)
        {
            float priority = 0f;

            // Life-threatening conditions get highest priority
            if (hediff.CurStage?.lifeThreatening ?? false)
            {
                priority += 1000f;

                // Add extra priority based on time until death
                if (hediff.def.lethalSeverity > 0)
                {
                    var severityComp = hediff.TryGetComp<HediffComp_SeverityPerDay>();
                    if (severityComp != null)
                    {
                        float severityPerHour = severityComp.SeverityChangePerDay() / 24f;
                        if (severityPerHour > 0)
                        {
                            float hoursUntilDeath = (hediff.def.lethalSeverity - hediff.Severity) / severityPerHour;
                            priority += 1000f / Math.Max(1f, hoursUntilDeath); // Higher priority for less time
                        }
                    }
                }
            }

            // Bleeding wounds get high priority
            if (hediff is Hediff_Injury injury && injury.Bleeding)
            {
                priority += 500f;
            }

            if (hediff is HediffWithComps hwc)
            {
                var tendComp = hwc.TryGetComp<HediffComp_TendDuration>();
                if (tendComp != null)
                {
                    // Untended conditions get high priority
                    if (!tendComp.IsTended)
                    {
                        priority += 100f;
                    }
                    else
                    {
                        // For tended conditions, higher priority as tend quality decreases
                        priority += (1f - tendComp.tendQuality) * 50f;
                        
                        // And higher priority as time until next tend decreases
                        float hoursLeft = tendComp.tendTicksLeft / (float)GenDate.TicksPerHour;
                        priority += 50f / Math.Max(1f, hoursLeft);
                    }
                }
            }

            // Add base priority from severity
            priority += hediff.Severity;

            return priority;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref pawnId, "pawnId");
            Scribe_Values.Look(ref hediffId, "hediffId");
            Scribe_Values.Look(ref alerted, "alerted", false);
            Scribe_Values.Look(ref lastTendQuality, "lastTendQuality", 0f);
            Scribe_Values.Look(ref lastMaxTendTicks, "lastMaxTendTicks", 0f);
            Scribe_Values.Look(ref removeOnImmunity, "removeOnImmunity", false);
        }

        public override void Trigger()
        {
            try
            {
                Pawn pawn = FindPawn();
                if (pawn == null)
                {
                    completed = true;
                    return;
                }

                // Update the label to show the current most urgent condition
                UpdateLabelAndDescription(pawn);

                bool anyNeedTending = false;
                bool allHealed = true;

                foreach (var hediff in pawn.health.hediffSet.hediffs)
                {
                    if (!hediff.def.tendable || hediff.IsPermanent()) continue;

                    if (hediff is HediffWithComps hwc)
                    {
                        var tendComp = hwc.TryGetComp<HediffComp_TendDuration>();
                        if (tendComp == null) continue;

                        allHealed = false;

                        if (removeOnImmunity)
                        {
                            var immunityComp = hwc.TryGetComp<HediffComp_Immunizable>();
                            if (immunityComp != null && immunityComp.Immunity >= 1f)
                            {
                                continue;
                            }
                        }

                        if (!tendComp.IsTended)
                        {
                            anyNeedTending = true;
                            break;
                        }
                    }
                }

                if (allHealed || !anyNeedTending)
                {
                    description = GetTendDescription(pawn);
                    triggerTick = CalculateNextTendTick(pawn);
                    return;
                }

                if (anyNeedTending && !alerted)
                {
                    Find.LetterStack.ReceiveLetter(
                        "Tend Reminder: " + pawn.LabelShort,
                        GetTendDescription(pawn),
                        LetterDefOf.NeutralEvent,
                        new LookTargets(pawn));

                    if (RiminderMod.Settings.pauseOnReminder)
                    {
                        Find.TickManager.Pause();
                    }

                    alerted = true;
                    triggerTick = Find.TickManager.TicksGame + GenDate.TicksPerHour;
                    return;
                }

                if (alerted)
                {
                    description = GetTendDescription(pawn);
                    triggerTick = Find.TickManager.TicksGame + GenDate.TicksPerHour;
                    return;
                }

                description = GetTendDescription(pawn);
                triggerTick = CalculateNextTendTick(pawn);
            }
            catch (Exception) { }
        }

        public Pawn FindPawn()
        {
            return Find.CurrentMap?.mapPawns?.AllPawns
                .FirstOrDefault(p => p.ThingID == pawnId);
        }

        public Hediff FindHediff(Pawn pawn)
        {
            if (pawn == null) return null;

            if (long.TryParse(hediffId, out long loadID) && loadID != 0)
            {
                Hediff byLoadId = pawn.health.hediffSet.hediffs
                    .FirstOrDefault(h => h.loadID == loadID);
                if (byLoadId != null) return byLoadId;
            }

            string[] parts = hediffId.Split('_');
            if (parts.Length == 2)
            {
                string defName = parts[1];
                var matchingHediffs = pawn.health.hediffSet.hediffs
                    .Where(h => h.def.defName == defName)
                    .ToList();
                if (matchingHediffs.Count > 0)
                {
                    return matchingHediffs.FirstOrDefault();
                }
            }

            if (!string.IsNullOrEmpty(hediffLabel))
            {
                var byExactLabel = pawn.health.hediffSet.hediffs
                    .FirstOrDefault(h => h.def.label == hediffLabel);
                if (byExactLabel != null) return byExactLabel;

                var byCaseInsensitiveLabel = pawn.health.hediffSet.hediffs
                    .FirstOrDefault(h => h.def.label.ToLower() == hediffLabel.ToLower());
                if (byCaseInsensitiveLabel != null) return byCaseInsensitiveLabel;

                var byPartialLabel = pawn.health.hediffSet.hediffs
                    .FirstOrDefault(h => h.def.label.ToLower().Contains(hediffLabel.ToLower()) ||
                                        hediffLabel.ToLower().Contains(h.def.label.ToLower()));
                if (byPartialLabel != null) return byPartialLabel;
            }

            foreach (var hediff in pawn.health.hediffSet.hediffs)
            {
                if (hediffId.Contains(hediff.def.defName) ||
                    hediff.def.defName.Contains(hediffLabel))
                {
                    return hediff;
                }
            }

            return null;
        }

        private static string GetTendDescription(Pawn pawn)
        {
            string desc = $"Tending required for {pawn.LabelShort}:";
            bool foundTendable = false;

            foreach (var hediff in pawn.health.hediffSet.hediffs)
            {
                if (!hediff.def.tendable || hediff.IsPermanent()) continue;

                if (hediff is HediffWithComps hwc)
                {
                    var tendComp = hwc.TryGetComp<HediffComp_TendDuration>();
                    if (tendComp == null) continue;

                    foundTendable = true;
                    desc += $"\n- {hediff.Label} ({hediff.def.label})";
                    
                    if (hediff.CurStage != null && hediff.CurStage.lifeThreatening && hediff.def.lethalSeverity > 0)
                    {
                        float severityPerHour = 0f;
                        HediffComp_SeverityPerDay severityComp = hediff.TryGetComp<HediffComp_SeverityPerDay>();
                        if (severityComp != null)
                        {
                            severityPerHour = severityComp.SeverityChangePerDay() / 24f;
                            if (severityPerHour > 0)
                            {
                                float hoursUntilDeath = (hediff.def.lethalSeverity - hediff.Severity) / severityPerHour;
                                if (hoursUntilDeath > 0)
                                {
                                    if (hoursUntilDeath > 24)
                                    {
                                        desc += $" (Lethal in: {hoursUntilDeath/24f:F1} days)";
                                    }
                                    else
                                    {
                                        desc += $" (Lethal in: {hoursUntilDeath:F1} hours)";
                                    }
                                }
                            }
                        }
                    }

                    if (tendComp.IsTended)
                    {
                        desc += $" - Quality: {tendComp.tendQuality:P0}";
                        float hoursLeft = tendComp.tendTicksLeft / (float)GenDate.TicksPerHour;
                        if (hoursLeft > 0)
                        {
                            desc += $", next tend in {hoursLeft:F1}h";
                        }
                    }
                    else
                    {
                        desc += " - Needs tending now!";
                    }
                }
            }

            if (!foundTendable)
            {
                desc += "\nNo conditions currently require tending.";
            }

            return desc;
        }

        private static int CalculateNextTendTick(Pawn pawn)
        {
            int currentTick = Find.TickManager.TicksGame;
            int soonestTendTick = currentTick + GenDate.TicksPerDay; 

            foreach (var hediff in pawn.health.hediffSet.hediffs)
            {
                if (!hediff.def.tendable || hediff.IsPermanent()) continue;

                if (hediff is HediffWithComps hwc)
                {
                    var tendComp = hwc.TryGetComp<HediffComp_TendDuration>();
                    if (tendComp == null) continue;

                    if (!tendComp.IsTended)
                    {
                        return currentTick;
                    }

                    soonestTendTick = Math.Min(soonestTendTick, currentTick + tendComp.tendTicksLeft);
                }
            }

            return soonestTendTick;
        }

        public override void OpenEditDialog()
        {
            Find.WindowStack.Add(new Dialog_EditTendReminder(this));
        }

        public override float GetProgressPercent()
        {
            Pawn pawn = FindPawn();

            if (pawn == null) return 1f;

            float totalProgress = 0f;
            int tendableCount = 0;

            foreach (var hediff in pawn.health.hediffSet.hediffs)
            {
                if (!hediff.def.tendable || hediff.IsPermanent()) continue;

                if (hediff is HediffWithComps hediffWithComps)
                {
                    var tendComp = hediffWithComps.TryGetComp<HediffComp_TendDuration>();
                    if (tendComp != null)
                    {
                        if (tendComp.IsTended && lastMaxTendTicks > 0f)
                        {
                            float remainingDuration = tendComp.tendTicksLeft;
                            totalProgress += 1f - (remainingDuration / lastMaxTendTicks);
                        }
                        else
                        {
                            totalProgress += 0f; // Not tended yet
                        }
                        tendableCount++;
                    }

                    var immunityComp = hediffWithComps.TryGetComp<HediffComp_Immunizable>();
                    if (immunityComp != null)
                    {
                        totalProgress += immunityComp.Immunity;
                        tendableCount++;
                    }
                }

                if (hediff is Hediff_Injury injury)
                {
                    totalProgress += 1f - injury.Severity / injury.def.initialSeverity;
                    tendableCount++;
                }
            }

            return tendableCount > 0 ? totalProgress / tendableCount : 1f;
        }

        private bool alerted = false;
        private float lastTendQuality = 0f;
        private float lastMaxTendTicks = 0f;
    }
}
