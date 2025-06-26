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
        public bool IsChronic;
    }

    public class TendReminderDataProvider : IExposable, IReminderDataProvider
    {
        
        public string pawnId;
        public string pawnLabel;
        public List<string> trackedHediffIds = new List<string>();
        public bool removeOnImmunity;
        
        
        private string label;
        private string description;
        private string timeLeftString = "Now";
        private float progress;
        private bool needsAttention;
        
        
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
                
                bool shouldTrack = false;
                if (found != null && found.def.tendable && !found.IsPermanent() && !found.def.defName.Contains("Missing") && !found.Label.ToLower().Contains("missing") && !found.def.defName.Contains("Removed") && !found.Label.ToLower().Contains("removed"))
                {
                    if (found is HediffWithComps hwc)
                    {
                        var tendComp = hwc.TryGetComp<HediffComp_TendDuration>();
                        if (tendComp != null)
                        {
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
                    bool isChronic = false;
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
                    isChronic = found.def.chronic;
                    trackedHediffs.Add(new TendableHediffInfo {
                        Hediff = found,
                        TendTicksLeft = tendTicks,
                        IsBleeding = isBleeding,
                        IsDisease = isDisease,
                        IsChronic = isChronic
                    });
                }
                else
                {
                    trackedHediffIds.Remove(id);
                }
            }
            
            if (trackedHediffs.Count == 0)
            {
                RiminderManager.RemoveReminderForPawn(pawnId);
                return;
            }
            
            var grouped = trackedHediffs
                .GroupBy(x => x.Hediff.def.defName + "_" + (x.Hediff.Part != null ? x.Hediff.Part.def.defName : "null"))
                .Select(g => g.OrderBy(x => x.TendTicksLeft).First())
                .ToList();
            if (grouped.Count == 0)
            {
                RiminderManager.RemoveReminderForPawn(pawnId);
                return;
            }
            
            var urgentInfo = grouped
                .OrderByDescending(x => {
                    if (x.Hediff is HediffWithComps hwc)
                    {
                        var tendComp = hwc.TryGetComp<HediffComp_TendDuration>();
                        if (tendComp != null)
                        {
                            return tendComp.IsTended ? 
                                (tendComp.tendTicksLeft <= tendComp.TProps.TendTicksOverlap ? 2 : 0) : 
                                3; 
                        }
                    }
                    
                    if (x.IsBleeding) return 3;
                    return 0; 
                })
                .ThenByDescending(x => x.Hediff.TendPriority)
                .ThenByDescending(x => x.IsBleeding)
                .ThenByDescending(x => x.IsDisease && !x.IsChronic) 
                .ThenByDescending(x => x.IsDisease) 
                .ThenByDescending(x => !x.IsChronic) 
                .ThenBy(x => x.TendTicksLeft)
                .FirstOrDefault();
            Hediff urgent = urgentInfo?.Hediff;
            
            UpdateTrackedHediffs(pawn);
            
            CalculateValues(pawn, urgent);
            
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
            
            tendTicksLeft = -1;
            nextTendableInTicks = -1;
            tendQuality = 0f;
            needsAttention = false;
            
            
            hediffLabel = hediff.LabelCap;
            hediffDefLabel = hediff.def.label;
            
            
            if (hediff is Hediff_Injury injury && injury.Bleeding)
            {
                needsAttention = true;
                tendTicksLeft = -1;
                nextTendableInTicks = 0;
                progress = 1f;
                return;
            }
            
            
            if (hediff is HediffWithComps hwc)
            {
                var tendComp = hwc.TryGetComp<HediffComp_TendDuration>();
                if (tendComp != null)
                {
                    tendTicksLeft = tendComp.tendTicksLeft;
                    tendQuality = tendComp.tendQuality;
                    
                    
                    nextTendableInTicks = tendTicksLeft > 0 ? 
                        tendTicksLeft - tendComp.TProps.TendTicksOverlap : 0;
                        
                    
                    needsAttention = !tendComp.IsTended || nextTendableInTicks <= 0;
                    
                    
                    progress = CalculateHediffProgress(hediff, tendComp);
                }
            }
        }
        
        
        public static float CalculateHediffProgress(Hediff hediff, HediffComp_TendDuration tendComp)
        {
            
            if (tendComp == null || !tendComp.IsTended)
                return 1f;
            
            
            if (hediff is Hediff_Injury injury && injury.Bleeding)
                return 1f;
            
            
            int currentTick = Find.TickManager.TicksGame;
            
            
            int totalDuration = tendComp.TProps.TendTicksFull;
            
            
            if (tendComp.TProps.TendTicksOverlap > 0)
            {
                totalDuration = Math.Max(totalDuration - tendComp.TProps.TendTicksOverlap, GenDate.TicksPerHour * 4);
            }
            
            
            int timeRemaining = tendComp.tendTicksLeft;
            
            
            
            
            float progress = 1f - (Math.Max(0, timeRemaining) / (float)totalDuration);
            
            
            return Math.Max(0f, Math.Min(1f, progress));
        }
        
        private void FormatOutputs(Pawn pawn, Hediff hediff)
        {
            
            label = $"Tend {pawnLabel}'s {hediffLabel}";
            
            
            description = FormatDescription(pawn, hediff);
            
            
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
            
            
            if (hediff is HediffWithComps hwc)
            {
                
                if (hediff.def.chronic)
                {
                    bool severityDisplayed = false;
                    
                    
                    var severityComp = hwc.TryGetComp<HediffComp_SeverityPerDay>();
                    if (severityComp != null)
                    {
                        float severityChange = severityComp.SeverityChangePerDay();
                        string direction = severityChange < 0 ? "decreasing" : "increasing";
                        string color = severityChange < 0 ? "#88FF88" : "#FF8888";
                        desc += $"\n<color={color}>Severity/day: {Math.Abs(severityChange):F3} ({direction})</color>";
                        severityDisplayed = true;
                    }
                    
                    
                    if (!severityDisplayed)
                    {
                        var allComps = hwc.comps;
                        if (allComps != null)
                        {
                            foreach (var comp in allComps)
                            {
                                if (comp is HediffComp_SeverityModifierBase severityModComp)
                                {
                                    float severityChange = severityModComp.SeverityChangePerDay();
                                    string direction = severityChange < 0 ? "decreasing" : "increasing";
                                    string color = severityChange < 0 ? "#88FF88" : "#FF8888";
                                    desc += $"\n<color={color}>Severity/day: {Math.Abs(severityChange):F3} ({direction})</color>";
                                    severityDisplayed = true;
                                    break;
                                }
                            }
                        }
                    }
                }
                
                else
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
            }
            
            
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
            
            
            var trackedHediffs = new List<Hediff>();
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
                
                if (found != null)
                {
                    trackedHediffs.Add(found);
                }
                else
                {
                    
                    trackedHediffIds.Remove(id);
                }
            }
            
            if (trackedHediffs.Count == 0) return null;
            
            
            var bleeding = trackedHediffs.FirstOrDefault(h => h is Hediff_Injury injury && injury.Bleeding);
            if (bleeding != null) return bleeding;
            
            
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