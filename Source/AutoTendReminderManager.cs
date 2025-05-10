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
            if (Find.TickManager.TicksGame % 60 != 0) return;

            if (!RiminderMod.Settings.autoCreateTendReminders) return;

            foreach (Pawn pawn in PawnsFinder.AllMapsCaravansAndTravelingTransportPods_Alive_Colonists)
            {
                if (pawn.health == null || pawn.health.hediffSet == null) continue;

                foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
                {
                    if (hediff == null) continue;

                    string hediffId = GetHediffIdentifier(pawn, hediff);
                    if (processedHediffs.Contains(hediffId)) continue;

                    if (NeedsTending(hediff))
                    {
                        if (!HasExistingReminder(pawn, hediff))
                        {
                            CreateTendReminder(pawn, hediff);
                            if (Prefs.DevMode)
                            {
                                Log.Message($"[Riminder] AutoTendReminderManager created reminder for {pawn.LabelShort}'s {hediff.Label}");
                            }
                        }
                        processedHediffs.Add(hediffId);
                    }
                }
            }
        }

        private bool NeedsTending(Hediff hediff)
        {
            if (hediff.IsPermanent()) return false;

            if (hediff.def.defName.Contains("Removed") || 
                hediff.Label.Contains("removed") || 
                hediff.def.defName.Contains("Missing") || 
                hediff.Label.Contains("missing")) return false;
            
            if (hediff.Part != null && hediff.Part.def.tags.Contains(BodyPartTagDefOf.ConsciousnessSource)) return false;
            
            if (hediff.Part != null)
            {
                foreach (Hediff h in hediff.pawn.health.hediffSet.hediffs)
                {
                    if ((h.def.defName.Contains("Missing") || h.def.defName.Contains("Removed")) && 
                        h.Part == hediff.Part)
                    {
                        return false;
                    }
                }
            }

            if (hediff.def.chronic) return false;

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
            
            
            if (hediff is Hediff_Injury injury && injury.Bleeding)
            {
                return true;
            }
            
            return false;
        }

        private string GetHediffIdentifier(Pawn pawn, Hediff hediff)
        {
            
            return $"{pawn.ThingID}|{hediff.def.defName}|{hediff.Part?.def.defName ?? "null"}";
        }

        private bool HasExistingReminder(Pawn pawn, Hediff hediff)
        {
            
            foreach (Reminder reminder in RiminderManager.GetActiveReminders())
            {
                if (reminder is PawnTendReminder tendReminder)
                {
                    if (tendReminder.pawnId != pawn.ThingID) continue;
                    
                    
                    if (tendReminder.hediffLabel == hediff.def.label) return true;
                    
                    
                    if (tendReminder.hediffId != null && tendReminder.hediffId.Contains(hediff.def.defName)) return true;
                    
                    
                    if (hediff.loadID > 0 && tendReminder.hediffId == hediff.loadID.ToString()) return true;
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
            
            
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                processedHediffs.Clear();
            }
        }
        
        
        public void ClearProcessedHediffs()
        {
            processedHediffs.Clear();
        }
    }
}