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

            if (hediff == null)
            {
                Log.Error("[Riminder] Attempted to create a PawnTendReminder with null hediff for pawn " + pawn.LabelShort);
                this.id = Guid.NewGuid().ToString();
                this.label = "Invalid Reminder for " + pawn.LabelShort;
                this.description = "This reminder was created incorrectly and should be deleted.";
                this.createdTick = Find.TickManager.TicksGame;
                this.triggerTick = Find.TickManager.TicksGame;
                return;
            }

            this.pawnId = pawn.ThingID;
            this.hediffId = hediff.loadID > 0 ? hediff.loadID.ToString() : $"{pawn.ThingID}_{hediff.def.defName}";
            this.removeOnImmunity = removeOnImmunity;
            this.pawnLabel = pawn.LabelShort;
            this.hediffLabel = hediff.def?.label ?? "Unknown";

            this.label = $"Tend {pawnLabel}'s {hediffLabel}";
            this.description = GetTendDescription(pawn, hediff);
            this.frequency = ReminderFrequency.WhenTendingRequired;
            this.createdTick = Find.TickManager.TicksGame;
            this.triggerTick = CalculateTendTick(hediff);
            this.id = Guid.NewGuid().ToString();
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
                Hediff hediff = FindHediff(pawn);
                if (pawn == null || hediff == null || hediff.Severity <= 0)
                {
                    completed = true;
                    return;
                }

                if (removeOnImmunity && hediff is HediffWithComps hwc)
                {
                    var immunityComp = hwc.TryGetComp<HediffComp_Immunizable>();
                    if (immunityComp != null && immunityComp.Immunity >= 1f)
                    {
                        completed = true;
                        return;
                    }
                }

                if (hediff is HediffWithComps hediffWithComps)
                {
                    var tendComp = hediffWithComps.TryGetComp<HediffComp_TendDuration>();
                    if (tendComp != null)
                    {
                        float currentQuality = tendComp.tendQuality;
                        description = GetTendDescription(pawn, hediff);

                        if (tendComp.IsTended && (currentQuality > lastTendQuality || (lastTendQuality == 0f && currentQuality > 0f)))
                        {
                            alerted = false;
                            lastTendQuality = currentQuality;
                            lastMaxTendTicks = tendComp.tendTicksLeft;
                            label = $"Tend {pawnLabel}'s {hediffLabel}";
                            triggerTick = Find.TickManager.TicksGame + tendComp.tendTicksLeft;
                            return;
                        }

                        if (hediff.TendableNow() && !tendComp.IsTended && !alerted)
                        {
                            Find.LetterStack.ReceiveLetter(
                                "Tend Reminder: " + pawn.LabelShort,
                                description,
                                LetterDefOf.NeutralEvent,
                                new LookTargets(pawn));
                            if (RiminderMod.Settings.pauseOnReminder)
                            {
                                Find.TickManager.Pause();
                            }
                            alerted = true;
                            lastTendQuality = currentQuality;
                            triggerTick = Find.TickManager.TicksGame + GenDate.TicksPerHour;
                            return;
                        }

                        if (alerted)
                        {
                            triggerTick = Find.TickManager.TicksGame + GenDate.TicksPerHour;
                            return;
                        }

                        if (tendComp.IsTended && tendComp.tendTicksLeft > 0)
                        {
                            triggerTick = Find.TickManager.TicksGame + tendComp.tendTicksLeft;
                            return;
                        }

                        triggerTick = Find.TickManager.TicksGame + (GenDate.TicksPerHour / 4);
                    }
                }
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

        private static string GetTendDescription(Pawn pawn, Hediff hediff)
        {
            string desc = $"Reminder to tend to {pawn.LabelShort}'s {hediff.Label}";

            if (hediff is Hediff_Injury injury)
            {
                desc += $" ({injury.Severity:0.0}% severity)";
            }
            else if (hediff is HediffWithComps hwc)
            {
                var tendComp = hwc.TryGetComp<HediffComp_TendDuration>();
                if (tendComp != null)
                {
                    if (tendComp.IsTended)
                    {
                        desc += $" (Quality: {tendComp.tendQuality:P0})";
                        float hoursLeft = tendComp.tendTicksLeft / (float)GenDate.TicksPerHour;
                        if (hoursLeft > 0)
                        {
                            desc += $", next tend in {hoursLeft:F1}h";
                        }
                    }
                    else
                    {
                        desc += " (needs tending)";
                    }
                }
            }

            return desc;
        }

        private static int CalculateTendTick(Hediff hediff)
        {
            int currentTick = Find.TickManager.TicksGame;

            if (hediff is HediffWithComps hediffWithComps)
            {
                var tendComp = hediffWithComps.TryGetComp<HediffComp_TendDuration>();
                if (tendComp != null && tendComp.tendTicksLeft > 0)
                {
                    return currentTick + tendComp.tendTicksLeft;
                }
            }

            return currentTick;
        }

        public override void OpenEditDialog()
        {
            Find.WindowStack.Add(new Dialog_EditTendReminder(this));
        }

        public override float GetProgressPercent()
        {
            Pawn pawn = FindPawn();
            Hediff hediff = FindHediff(pawn);

            if (pawn == null || hediff == null) return 1f;

            if (hediff is HediffWithComps hediffWithComps)
            {
                var tendComp = hediffWithComps.TryGetComp<HediffComp_TendDuration>();
                if (tendComp != null && tendComp.IsTended && lastMaxTendTicks > 0f)
                {
                    float remainingDuration = tendComp.tendTicksLeft;
                    return 1f - (remainingDuration / lastMaxTendTicks);
                }
            }

            return 0f;
        }

        private bool alerted = false;
        private float lastTendQuality = 0f;
        private float lastMaxTendTicks = 0f;
    }
}
