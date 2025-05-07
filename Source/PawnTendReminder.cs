using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace Riminder
{
    public class PawnTendReminder : Reminder
    {
        // Pawn reference for the reminder
        public string pawnId;

        // Hediff reference for the reminder
        public string hediffId;
        
        // Whether to remove the reminder when immunity is reached
        public bool removeOnImmunity;
        
        // Label for the pawn
        public string pawnLabel;
        
        // Label for the hediff
        public string hediffLabel;
        
        // Empty constructor for loading
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
            
            // Set the base reminder properties
            this.label = $"Tend {pawnLabel}'s {hediffLabel}";
            this.description = GetTendDescription(pawn, hediff);
            this.frequency = ReminderFrequency.WhenTendingRequired;
            this.createdTick = Find.TickManager.TicksGame;
            this.triggerTick = CalculateTendTick(hediff);
            this.id = Guid.NewGuid().ToString();
        }
        
        // Override ExposeData to save/load the additional fields
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
        
        // Override Trigger to handle pawn-specific behavior
        public override void Trigger()
        {
            try 
            {
                Pawn pawn = FindPawn();
                Hediff hediff = FindHediff(pawn);
                if (pawn == null)
                {
                    completed = true;
                    return;
                }
                if (hediff == null)
                {
                    completed = true;
                    return;
                }

                // Check if the hediff is fully healed
                if (hediff.Severity <= 0)
                {
                    completed = true;
                    return;
                }

                // Check for immunity if this is a disease and we should remove on immunity
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
                        
                        // Always update the description to reflect current tend quality
                        description = GetTendDescription(pawn, hediff);

                        // RESET ALERT STATE: If a new tend occurred with higher quality or was untended and now is tended
                        // This ensures we reset the alert state when the pawn gets tended
                        if (tendComp.IsTended && (currentQuality > lastTendQuality || (lastTendQuality == 0f && currentQuality > 0f)))
                        {
                            // New tending detected - reset alert state
                            alerted = false;
                            lastTendQuality = currentQuality;
                            lastMaxTendTicks = tendComp.tendTicksLeft;
                            
                            // Update label to reflect current state
                            label = $"Tend {pawnLabel}'s {hediffLabel}";
                            
                            // Schedule the next check for exactly when this tend expires
                            triggerTick = Find.TickManager.TicksGame + tendComp.tendTicksLeft;
                            return;
                        }

                        // ALERT CONDITION: Only alert if it ACTUALLY needs tending now and we haven't already alerted
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
                            
                            // Mark as alerted - won't alert again until tended
                            alerted = true;
                            
                            // Store the current quality (likely 0) to detect when it changes
                            lastTendQuality = currentQuality;
                            
                            // Check again in an hour in case they don't tend
                            triggerTick = Find.TickManager.TicksGame + GenDate.TicksPerHour;
                            return;
                        }

                        // ALREADY ALERTED: If we've already alerted and it hasn't been tended yet
                        if (alerted)
                        {
                            // Don't alert again, check back in an hour
                            triggerTick = Find.TickManager.TicksGame + GenDate.TicksPerHour;
                            return;
                        }
                        
                        // CURRENTLY TENDED: If it's tended and not due for tending yet
                        if (tendComp.IsTended && tendComp.tendTicksLeft > 0)
                        {
                            // Check exactly when the current tend expires
                            triggerTick = Find.TickManager.TicksGame + tendComp.tendTicksLeft;
                            return;
                        }
                        
                        // DEFAULT: If it's not tended but also not tendable right now for some reason
                        // Check again in 15 minutes
                        triggerTick = Find.TickManager.TicksGame + (GenDate.TicksPerHour / 4);
                    }
                }
            }
            catch (Exception)
            {
                // Silently fail
            }
        }
        
        // Find the pawn based on stored ID
        public Pawn FindPawn()
        {
            return Find.CurrentMap?.mapPawns?.AllPawns
                .FirstOrDefault(p => p.ThingID == pawnId);
        }
        
        // Find the hediff on the pawn based on stored ID
        public Hediff FindHediff(Pawn pawn)
        {
            if (pawn == null) return null;
            
            // Try to find by loadID first
            if (long.TryParse(hediffId, out long loadID) && loadID != 0)
            {
                return pawn.health.hediffSet.hediffs
                    .FirstOrDefault(h => h.loadID == loadID);
            }
            
            // Otherwise try to find by our custom identifier
            string[] parts = hediffId.Split('_');
            if (parts.Length != 2) 
            {
                Log.Warning($"[Riminder] Invalid hediff ID format: {hediffId}");
                return null;
            }
            
            string pawnID = parts[0];
            string defName = parts[1];
            
            // Find matching hediff
            var matchingHediffs = pawn.health.hediffSet.hediffs
                .Where(h => h.def.defName == defName)
                .ToList();

            if (matchingHediffs.Count == 0)
            {
                return null;
            }

            // Return the first matching hediff
            return matchingHediffs.FirstOrDefault();
        }
        
        // Generate a description of the tend reminder
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
                        
                        // Add time left until next tend is needed
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
        
        // Calculate when the next tend is needed based on the hediff
        private static int CalculateTendTick(Hediff hediff)
        {
            int currentTick = Find.TickManager.TicksGame;
            
            // If the hediff has a tendDuration component, use that to calculate when tending is needed
            if (hediff is HediffWithComps hediffWithComps)
            {
                var tendComp = hediffWithComps.TryGetComp<HediffComp_TendDuration>();
                if (tendComp != null && tendComp.tendTicksLeft > 0)
                {
                    // Set the reminder for when the current tend expires
                    return currentTick + tendComp.tendTicksLeft;
                }
            }
            
            // If no tend duration comp found or needs tending now, set reminder for immediate tending
            return currentTick;
        }

        public override void OpenEditDialog()
        {
            Find.WindowStack.Add(new Dialog_EditTendReminder(this));
        }

        // Override GetProgressPercent to show tend duration progress
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
            
            // If not tended or no tend component, show empty progress
            return 0f;
        }

        // Add a field to track if we've already alerted
        private bool alerted = false;
        private float lastTendQuality = 0f;
        private float lastMaxTendTicks = 0f;
    }
} 