using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace Riminder
{
    public class AutoTendReminderManager : GameComponent
    {
        private static AutoTendReminderManager instance;
        private HashSet<string> processedHediffs = new HashSet<string>();

        public AutoTendReminderManager(Game game) : base()
        {
            instance = this;
        }

        public static void Initialize()
        {
            if (instance == null && Current.Game != null)
            {
                instance = new AutoTendReminderManager(Current.Game);
                Current.Game.components.Add(instance);
            }
        }

        public override void GameComponentTick()
        {
            // Only check every 60 ticks (about 1 second)
            if (Find.TickManager.TicksGame % 60 != 0) return;

            // Check if auto-create is enabled
            if (!RiminderMod.Settings.autoCreateTendReminders) return;

            // Check all pawns
            foreach (Pawn pawn in PawnsFinder.AllMapsCaravansAndTravelingTransportPods_Alive_Colonists)
            {
                if (pawn.health == null || pawn.health.hediffSet == null) continue;

                foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
                {
                    if (hediff == null) continue;

                    // Skip if we've already processed this hediff
                    string hediffId = GetHediffIdentifier(pawn, hediff);
                    if (processedHediffs.Contains(hediffId)) continue;

                    // Check if this hediff needs tending
                    if (NeedsTending(hediff))
                    {
                        // Check if we already have a reminder for this
                        if (!HasExistingReminder(pawn, hediff))
                        {
                            // Create a new reminder
                            CreateTendReminder(pawn, hediff);
                        }
                        processedHediffs.Add(hediffId);
                    }
                }
            }
        }

        private bool NeedsTending(Hediff hediff)
        {
            // Skip permanent hediffs like scars
            if (hediff.IsPermanent()) return false;

            // Skip removed hediffs
            if (hediff.def.defName.Contains("Removed") || hediff.Label.Contains("removed")) return false;

            // Check if the hediff needs tending
            if (hediff is HediffWithComps hediffWithComps)
            {
                foreach (HediffComp comp in hediffWithComps.comps)
                {
                    if (comp is HediffComp_TendDuration tendComp)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private string GetHediffIdentifier(Pawn pawn, Hediff hediff)
        {
            return $"{pawn.ThingID}_{hediff.def.defName}";
        }

        private bool HasExistingReminder(Pawn pawn, Hediff hediff)
        {
            foreach (Reminder reminder in RiminderManager.GetActiveReminders())
            {
                if (reminder is PawnTendReminder tendReminder)
                {
                    if (tendReminder.pawnId == pawn.ThingID && 
                        tendReminder.hediffId == GetHediffIdentifier(pawn, hediff))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void CreateTendReminder(Pawn pawn, Hediff hediff)
        {
            bool removeOnImmunity = hediff is HediffWithComps hwc && 
                                  hwc.TryGetComp<HediffComp_Immunizable>() != null;

            PawnTendReminder reminder = new PawnTendReminder(
                pawn,
                hediff,
                removeOnImmunity
            );

            RiminderManager.AddReminder(reminder);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref processedHediffs, "processedHediffs", LookMode.Value);
        }
    }
} 