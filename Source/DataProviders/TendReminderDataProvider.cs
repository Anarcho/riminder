using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using Riminder;

namespace Riminder
{
    public class TendableHediffInfo
    {
        public Hediff Hediff;
        public int TendTicksLeft;
        public bool IsBleeding;
        public bool IsDisease;
    }

    public class TendReminderDataProvider : IExposable, IReminderDataProvider
    {
        // Data that is saved/loaded
        public string pawnId;
        public string pawnLabel;
        public List<string> trackedHediffIds = new List<string>();
        public bool removeOnImmunity;
        
        // Cached data from last refresh
        private string label;
        private string description;
        private string timeLeftString = "Now";
        private float progress;
        private bool needsAttention;
        
        // Internal calculations
        private int tendTicksLeft = -1;
        private int nextTendableInTicks = -1;
        private float tendQuality;
        private string hediffLabel;
        private string hediffDefLabel;
        private List<TendableHediffInfo> trackedHediffs = new List<TendableHediffInfo>();

        public TendReminderDataProvider()
        {
        }
        
        public TendReminderDataProvider(Pawn pawn, List<Hediff> hediffs, bool removeOnImmunity)
        {
            if (pawn == null) throw new ArgumentNullException(nameof(pawn));
            
            this.pawnId = pawn.ThingID;
            this.pawnLabel = pawn.LabelShort;
            this.removeOnImmunity = removeOnImmunity;
            
            // Track all tendable hediffs
            foreach (var hediff in hediffs)
            {
                if (hediff is HediffWithComps hwc && hwc.TryGetComp<HediffComp_TendDuration>() != null)
                {
                    string id = hediff.loadID > 0 ? 
                        hediff.loadID.ToString() : 
                        $"{pawn.ThingID}_{hediff.def.defName}_{(hediff.Part != null ? hediff.Part.def.defName : "null")}";
                    trackedHediffIds.Add(id);
                }
            }
            
            Refresh();
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref pawnId, "pawnId");
            Scribe_Values.Look(ref pawnLabel, "pawnLabel");
            Scribe_Values.Look(ref removeOnImmunity, "removeOnImmunity", false);
            Scribe_Collections.Look(ref trackedHediffIds, "trackedHediffIds", LookMode.Value);
            
            // Don't save cached data, it will be recalculated on load
        }

        public string GetLabel() => label;
        public string GetDescription() => description;
        public string GetTimeLeftString() => timeLeftString;
        public float GetProgress() => progress;
        public bool NeedsAttention() => needsAttention;

        public void Refresh()
        {
            Pawn pawn = FindPawn();
            if (pawn == null)
            {
                SetDefaults("Invalid Pawn");
                return;
            }
            trackedHediffs.Clear();
            foreach (string id in trackedHediffIds.ToList())
            {
                Hediff found = null;
                if (long.TryParse(id, out long loadId))
                {
                    found = pawn.health.hediffSet.hediffs.FirstOrDefault(h => h.loadID == loadId);
                }
                else
                {
                    string[] parts = id.Split('_');
                    if (parts.Length >= 2)
                    {
                        string defName = parts[1];
                        string partDefName = parts.Length > 2 ? parts[2] : null;
                        found = pawn.health.hediffSet.hediffs.FirstOrDefault(h => 
                            h.def.defName == defName && 
                            (partDefName == "null" || h.Part == null || 
                             (h.Part != null && h.Part.def.defName == partDefName)));
                    }
                }
                // Exclude missing limbs, non-tendable, permanent, and already-tended permanent hediffs
                bool shouldTrack = false;
                if (found != null && found.def.tendable && !found.IsPermanent() && !found.def.defName.Contains("Missing") && !found.Label.ToLower().Contains("missing") && !found.def.defName.Contains("Removed") && !found.Label.ToLower().Contains("removed"))
                {
                    if (found is HediffWithComps hwc)
                    {
                        var tendComp = hwc.TryGetComp<HediffComp_TendDuration>();
                        if (tendComp != null)
                        {
                            // If permanent-tend and already tended, skip
                            if (tendComp.TProps.TendIsPermanent && tendComp.IsTended)
                            {
                                shouldTrack = false;
                            }
                            else
                            {
                                shouldTrack = true;
                            }
                        }
                    }
                }
                if (shouldTrack)
                {
                    int tendTicks = int.MaxValue;
                    bool isBleeding = false;
                    bool isDisease = false;
                    if (found is HediffWithComps hwc)
                    {
                        var tendComp = hwc.TryGetComp<HediffComp_TendDuration>();
                        if (tendComp != null)
                            tendTicks = tendComp.tendTicksLeft;
                        if (hwc.TryGetComp<HediffComp_Immunizable>() != null)
                            isDisease = true;
                    }
                    if (found is Hediff_Injury injury && injury.Bleeding)
                        isBleeding = true;
                    trackedHediffs.Add(new TendableHediffInfo {
                        Hediff = found,
                        TendTicksLeft = tendTicks,
                        IsBleeding = isBleeding,
                        IsDisease = isDisease
                    });
                }
                else
                {
                    trackedHediffIds.Remove(id);
                }
            }
            if (trackedHediffs.Count == 0)
            {
                SetDefaults($"Monitor {pawnLabel}");
                return;
            }
            // Group by defName + partDefName and keep only the most urgent (lowest tendTicksLeft) for each unique instance
            var grouped = trackedHediffs
                .GroupBy(x => x.Hediff.def.defName + "_" + (x.Hediff.Part != null ? x.Hediff.Part.def.defName : "null"))
                .Select(g => g.OrderBy(x => x.TendTicksLeft).First())
                .ToList();
            if (grouped.Count == 0)
            {
                // No more valid tendable hediffs, mark reminder as complete
                RiminderManager.RemoveReminderForPawn(pawnId);
                SetDefaults($"Monitor {pawnLabel}");
                return;
            }
            // Prioritize: bleeding > disease > other, then by lowest tendTicksLeft
            var urgentInfo = grouped
                .OrderByDescending(x => x.IsBleeding)
                .ThenByDescending(x => x.IsDisease)
                .ThenBy(x => x.TendTicksLeft)
                .FirstOrDefault();
            Hediff urgent = urgentInfo?.Hediff;
            // Update tracked hediffs
            UpdateTrackedHediffs(pawn);
            // Calculate all the values in one place
            CalculateValues(pawn, urgent);
            // Format everything
            FormatOutputs(pawn, urgent);
        }
        
        private void SetDefaults(string defaultLabel)
        {
            label = defaultLabel;
            description = "No conditions currently require tending.";
            timeLeftString = "N/A";
            progress = 0f;
            needsAttention = false;
        }
        
        private void CalculateValues(Pawn pawn, Hediff hediff)
        {
            // Reset values
            tendTicksLeft = -1;
            nextTendableInTicks = -1;
            tendQuality = 0f;
            needsAttention = false;
            
            // Get labels for later use
            hediffLabel = hediff.LabelCap;
            hediffDefLabel = hediff.def.label;
            
            // Handle bleeding injuries
            if (hediff is Hediff_Injury injury && injury.Bleeding)
            {
                needsAttention = true;
                tendTicksLeft = -1;
                nextTendableInTicks = 0;
                progress = 1f;
                return;
            }
            
            // Get tend info if available
            if (hediff is HediffWithComps hwc)
            {
                var tendComp = hwc.TryGetComp<HediffComp_TendDuration>();
                if (tendComp != null)
                {
                    tendTicksLeft = tendComp.tendTicksLeft;
                    tendQuality = tendComp.tendQuality;
                    
                    // Apply the same calculation used in the game
                    nextTendableInTicks = tendTicksLeft > 0 ? 
                        tendTicksLeft - tendComp.TProps.TendTicksOverlap : 0;
                        
                    // Determine if needs attention - FIX: Only set to true if it actually needs tending now
                    needsAttention = !tendComp.IsTended || nextTendableInTicks <= 0;
                    
                    // Calculate progress
                    if (tendComp.IsTended && tendTicksLeft > 0)
                    {
                        // Calculate the original maximum duration from the current tend quality
                        int baseTendDuration = 60000; // Default value if we can't determine
                        if (tendComp != null && tendComp.TProps != null)
                        {
                            // Use a reasonable default based on RimWorld's tend durations
                            // Most tend durations are 1 day + quality-based extension
                            baseTendDuration = GenDate.TicksPerDay;
                        }
                        progress = 1f - (float)tendTicksLeft / baseTendDuration;
                    }
                    else
                    {
                        progress = 1f; // Ready to tend
                    }
                }
            }
        }
        
        private void FormatOutputs(Pawn pawn, Hediff hediff)
        {
            // Format label
            label = $"Tend {pawnLabel}'s {hediffLabel}";
            
            // Format description
            description = FormatDescription(pawn, hediff);
            
            // Format time left
            if (needsAttention)
            {
                timeLeftString = "Now";
            }
            else if (nextTendableInTicks > 0)
            {
                timeLeftString = $"in {FormatTimeLeft(nextTendableInTicks)}";
            }
            else
            {
                timeLeftString = "ready soon";
            }
        }
        
        private string FormatDescription(Pawn pawn, Hediff hediff)
        {
            string desc = $"{pawnLabel}'s {hediffLabel} ({hediffDefLabel})";
            
            // Add tending info
            if (tendTicksLeft == -1)
            {
                desc += "\nNeeds tending now!";
            }
            else
            {
                if (nextTendableInTicks <= 0)
                {
                    desc += $"\nTended {tendQuality:P0}, can be tended again now.";
                }
                else
                {
                    float hoursUntilTendable = nextTendableInTicks / (float)GenDate.TicksPerHour;
                    desc += $"\nTended {tendQuality:P0}, next tend in {hoursUntilTendable:F1}h.";
                }
            }
            
            // Add immunity info if applicable
            if (hediff is HediffWithComps hwc)
            {
                var immComp = hwc.TryGetComp<HediffComp_Immunizable>();
                if (immComp != null)
                {
                    desc += $"\n<color=#FFFF00>Immunity: {immComp.Immunity:P0}";
                    if (immComp.Immunity >= 0.8f)
                        desc += " (Almost immune)";
                    else if (immComp.Immunity >= 0.5f)
                        desc += " (Good progress)";
                    desc += "</color>";
                }
            }
            
            // Add bleeding info if applicable
            if (hediff is Hediff_Injury injury && injury.Bleeding)
            {
                int ticksUntilDeath = HealthUtility.TicksUntilDeathDueToBloodLoss(pawn);
                if (ticksUntilDeath < int.MaxValue)
                {
                    desc += $"\n<color=#FF4444>Death from blood loss in {FormatTimeLeft(ticksUntilDeath)}</color>";
                }
            }
            
            return desc;
        }
        
        private Hediff GetMostUrgentTendableHediff(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return null;
            
            // First get all tracked hediffs
            var trackedHediffs = new List<Hediff>();
            foreach (string id in trackedHediffIds.ToList())
            {
                Hediff found = null;
                if (long.TryParse(id, out long loadId))
                {
                    // Find hediff by load ID directly instead of using the extension method
                    found = pawn.health.hediffSet.hediffs.FirstOrDefault(h => h.loadID == loadId);
                }
                else
                {
                    // Try parsing the composite ID format
                    string[] parts = id.Split('_');
                    if (parts.Length >= 2)
                    {
                        string defName = parts[1];
                        string partDefName = parts.Length > 2 ? parts[2] : null;
                        
                        found = pawn.health.hediffSet.hediffs.FirstOrDefault(h => 
                            h.def.defName == defName && 
                            (partDefName == "null" || h.Part == null || 
                             (h.Part != null && h.Part.def.defName == partDefName)));
                    }
                }
                
                if (found != null)
                {
                    trackedHediffs.Add(found);
                }
                else
                {
                    // Remove IDs that no longer exist
                    trackedHediffIds.Remove(id);
                }
            }
            
            if (trackedHediffs.Count == 0) return null;
            
            // First check for bleeding injuries
            var bleeding = trackedHediffs.FirstOrDefault(h => h is Hediff_Injury injury && injury.Bleeding);
            if (bleeding != null) return bleeding;
            
            // Then check for other tendable conditions
            return trackedHediffs
                .Where(h => h is HediffWithComps hwc && hwc.TryGetComp<HediffComp_TendDuration>() != null)
                .OrderBy(h => {
                    var tendComp = ((HediffWithComps)h).TryGetComp<HediffComp_TendDuration>();
                    return tendComp.tendTicksLeft == -1 ? int.MinValue : tendComp.tendTicksLeft;
                })
                .FirstOrDefault();
        }
        
        private void UpdateTrackedHediffs(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return;
            
            var current = new HashSet<string>(trackedHediffIds);
            foreach (var h in pawn.health.hediffSet.hediffs)
            {
                if (h is HediffWithComps hwc && hwc.TryGetComp<HediffComp_TendDuration>() != null)
                {
                    string id = h.loadID > 0 ? 
                        h.loadID.ToString() : 
                        $"{pawn.ThingID}_{h.def.defName}_{(h.Part != null ? h.Part.def.defName : "null")}";
                        
                    if (!current.Contains(id))
                    {
                        trackedHediffIds.Add(id);
                    }
                }
            }
        }
        
        private Pawn FindPawn()
        {
            return Find.CurrentMap?.mapPawns?.AllPawns.FirstOrDefault(p => p.ThingID == pawnId);
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
    }
}