using System;
using RimWorld;
using Verse;
using System.Linq;

namespace Riminder
{
    public class RitualReminderDataProvider : IExposable, IReminderDataProvider
    {
        public string ritualDefName;
        public string ideoDefName;
        public int lastFinishedTick;
        public bool isAnytime;
        public bool isDateTriggered;
        public string label;
        public string description;
        public float progress;
        public bool needsAttention;

        private Precept_Ritual cachedRitual;

        public RitualReminderDataProvider() { }

        public RitualReminderDataProvider(Precept_Ritual ritual)
        {
            if (ritual == null) throw new ArgumentNullException(nameof(ritual));
            ritualDefName = ritual.def.defName;
            ideoDefName = ritual.ideo.GetUniqueLoadID();
            lastFinishedTick = ritual.lastFinishedTick;
            isAnytime = ritual.isAnytime;
            isDateTriggered = ritual.IsDateTriggered;
            Refresh();
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref ritualDefName, "ritualDefName");
            Scribe_Values.Look(ref ideoDefName, "ideoDefName");
            Scribe_Values.Look(ref lastFinishedTick, "lastFinishedTick", -1);
            Scribe_Values.Look(ref isAnytime, "isAnytime", false);
            Scribe_Values.Look(ref isDateTriggered, "isDateTriggered", false);
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref description, "description");
            Scribe_Values.Look(ref progress, "progress", 0f);
            Scribe_Values.Look(ref needsAttention, "needsAttention", false);
        }

        public string GetLabel() => label;
        public string GetDescription() => description;
        public string GetTimeLeftString() => isAnytime ? "Anytime" : "Scheduled";
        public float GetProgress() => progress;
        public bool NeedsAttention() => needsAttention;

        public void Refresh()
        {
            var ritual = GetRitual();
            if (ritual == null)
            {
                label = "Invalid Ritual";
                description = "Ritual not found or unavailable.";
                progress = 0f;
                needsAttention = false;
                return;
            }

            label = ritual.LabelCap;
            description = ritual.ritualExplanation ?? ritual.def.description;
            lastFinishedTick = ritual.lastFinishedTick;
            isAnytime = ritual.isAnytime;
            isDateTriggered = ritual.IsDateTriggered;

            
            progress = 0f;
            needsAttention = false;

            
            Map map = Find.CurrentMap;
            TargetInfo target = map != null ? new TargetInfo(map.Center, map) : TargetInfo.Invalid;
            string canStartReason = ritual.behavior?.CanStartRitualNow(target, ritual);

            if (canStartReason == null)
            {
                
                progress = 1f;
                needsAttention = true;
                if (isAnytime)
                {
                    description = "Anytime\n" + description;
                }
                else if (isDateTriggered)
                {
                    description = "Available now\n" + description;
                }
                else
                {
                    description = "Available\n" + description;
                }
            }
            else
            {
                
                description = $"Not available: {canStartReason}\n" + description;

                
                if (isAnytime && ritual.def.useRepeatPenalty && ritual.RepeatPenaltyActive)
                {
                    float penaltyProgress = ritual.RepeatPenaltyProgress;
                    progress = Math.Max(0f, Math.Min(1f, penaltyProgress));
                }
                
                else if (isDateTriggered)
                {
                    var dateTrigger = ritual.obligationTriggers?.OfType<RitualObligationTrigger_Date>().FirstOrDefault();
                    if (dateTrigger != null)
                    {
                        int currentTick = Find.TickManager.TicksGame;
                        int nextOccurrence = dateTrigger.OccursOnTick();
                        
                        
                        if (currentTick > nextOccurrence)
                        {
                            nextOccurrence += 3600000; 
                        }
                        
                        int ticksUntil = nextOccurrence - currentTick;
                        int totalInterval = 3600000; 
                        if (ticksUntil > 0)
                        {
                            progress = 1f - Math.Max(0f, Math.Min(1f, (float)ticksUntil / totalInterval));
                        }
                    }
                }
            }
        }

        public Precept_Ritual GetRitual()
        {
            if (cachedRitual != null) return cachedRitual;
            var ideo = Faction.OfPlayer.ideos?.PrimaryIdeo;
            if (ideo == null) return null;
            cachedRitual = ideo.PreceptsListForReading.OfType<Precept_Ritual>()
                .FirstOrDefault(r => r.def.defName == ritualDefName);
            return cachedRitual;
        }
    }
}
