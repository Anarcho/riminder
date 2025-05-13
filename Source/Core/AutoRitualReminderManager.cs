using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace Riminder
{
    public class AutoRitualReminderManager : GameComponent
    {
        private static AutoRitualReminderManager instance;
        private HashSet<string> processedRituals = new HashSet<string>();

        public AutoRitualReminderManager(Game game) : base()
        {
            instance = this;
        }

        public static void Initialize()
        {
            if (instance == null && Current.Game != null)
            {
                instance = new AutoRitualReminderManager(Current.Game);
                Current.Game.components.Add(instance);
            }
        }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % 600 == 0) 
            {
                if (!RiminderMod.Settings.autoCreateRitualReminders) return;
                var ideo = Faction.OfPlayer.ideos?.PrimaryIdeo;
                if (ideo == null) return;
                foreach (var ritual in ideo.PreceptsListForReading.OfType<Precept_Ritual>())
                {
                    if (IsEligibleRitual(ritual))
                    {
                        string ritualKey = ritual.def.defName;
                        if (!processedRituals.Contains(ritualKey) && !HasExistingReminder(ritual))
                        {
                            var reminder = ReminderFactory.CreateRitualReminder(ritual);
                            if (reminder != null)
                            {
                                RiminderManager.AddReminder(reminder);
                                processedRituals.Add(ritualKey);
                            }
                        }
                    }
                }
            }
        }

        private bool IsEligibleRitual(Precept_Ritual ritual)
        {
            
            string defName = ritual.def.defName.ToLowerInvariant();
            string label = ritual.def.label.ToLowerInvariant();
            if (defName.Contains("trial") || label.Contains("trial"))
                return false;
            return ritual.ritualOnlyForIdeoMembers && !IsFuneral(ritual);
        }

        private bool IsFuneral(Precept_Ritual ritual)
        {
            return ritual.def.ritualPatternBase?.defName.ToLower().Contains("funeral") == true
                || ritual.def.label.ToLower().Contains("funeral");
        }

        private bool HasExistingReminder(Precept_Ritual ritual)
        {
            return RiminderManager.GetActiveReminders().OfType<RitualReminder>()
                .Any(r => r.RitualData?.ritualDefName == ritual.def.defName);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref processedRituals, "processedRituals", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                processedRituals.Clear();
            }
        }

        public void ClearProcessedRituals()
        {
            processedRituals.Clear();
        }
    }
}
