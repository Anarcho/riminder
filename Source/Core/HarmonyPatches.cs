using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace Riminder
{
    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        private static readonly FieldInfo pawnField = typeof(Pawn_HealthTracker).GetField("pawn", BindingFlags.NonPublic | BindingFlags.Instance);

        private static Pawn GetPawn(Pawn_HealthTracker healthTracker)
        {
            return (Pawn)pawnField.GetValue(healthTracker);
        }

        [HarmonyPatch(typeof(FloatMenuMakerMap), "AddHumanlikeOrders")]
        public static class FloatMenuMakerMap_AddHumanlikeOrders_Patch
        {
            public static void Postfix(Vector3 clickPos, Pawn pawn, List<FloatMenuOption> opts)
            {
                if (pawn == null)
                {
                    IntVec3 clickedCell = clickPos.ToIntVec3();
                    if (!GenGrid.InBounds(clickedCell, Find.CurrentMap)) return;

                    List<Thing> things = Find.CurrentMap.thingGrid.ThingsListAt(clickedCell);
                    foreach (Thing thing in things)
                    {
                        if (thing is Pawn p && p.IsColonist)
                        {
                            pawn = p;
                            break;
                        }
                    }
                }

                if (pawn == null || !pawn.IsColonist) return;

                IntVec3 targetCell = clickPos.ToIntVec3();
                if (!GenGrid.InBounds(targetCell, pawn.Map) || !GenGrid.InBounds(pawn.Position, pawn.Map)) return;
                if (targetCell.DistanceTo(pawn.Position) > 1) return;

                bool hasTendableHediffs = false;
                foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
                {
                    if (hediff.def.tendable && NeedsTending(hediff))
                    {
                        hasTendableHediffs = true;
                        break;
                    }
                }

                if (hasTendableHediffs)
                {
                    string optionLabel = $"Create tend reminder for {pawn.LabelShort}";

                    FloatMenuOption option = new FloatMenuOption(
                        optionLabel,
                        () => Find.WindowStack.Add(new Dialog_CreateTendReminder(pawn)),
                        MenuOptionPriority.Default,
                        null,
                        null,
                        0f,
                        null,
                        null);

                    opts.Add(option);
                }
            }

            private static bool NeedsTending(Hediff hediff)
            {
                if (!hediff.def.tendable) return false;
                if (hediff.IsPermanent()) return false;
                if (hediff.def.defName.Contains("Removed") || hediff.Label.Contains("removed")) return false;

                if (hediff is HediffWithComps hwc)
                {
                    var tendComp = hwc.TryGetComp<HediffComp_TendDuration>();
                    if (tendComp != null) return true;
                }

                if (hediff is Hediff_Injury injury)
                {
                    return injury.Severity > 0;
                }

                return false;
            }
        }

        [HarmonyPatch(typeof(ITab_Pawn_Health), "FillTab")]
        public static class ITab_Pawn_Health_FillTab_Patch
        {
            private static bool buttonsInitialized = false;
            private static MethodInfo getPawnMethod;

            [HarmonyPostfix]
            public static void Postfix(ITab_Pawn_Health __instance)
            {
                if (!buttonsInitialized)
                {
                    getPawnMethod = AccessTools.Method(typeof(ITab_Pawn_Health), "get_PawnForHealth");
                    buttonsInitialized = true;
                }

                Pawn pawn = (Pawn)getPawnMethod.Invoke(__instance, null);
                if (pawn == null || !pawn.IsColonistPlayerControlled) return;

                bool hasTendableHediffs = pawn.health.hediffSet.hediffs.Any(h => h.def.tendable);
                if (hasTendableHediffs)
                {
                    float buttonWidth = 180f;
                    float buttonHeight = 28f;
                    Rect buttonRect = new Rect(30f, 580f, buttonWidth, buttonHeight);
                    if (Widgets.ButtonText(buttonRect, "Set tend reminder"))
                    {
                        Find.WindowStack.Add(new Dialog_CreateTendReminder(pawn));
                    }
                }
            }
        }

        [HarmonyPatch(typeof(HediffComp_TendDuration), "CompPostTick")]
        public static class HediffComp_TendDuration_CompPostTick_Patch
        {
            
            private static Dictionary<string, int> lastUpdateTicks = new Dictionary<string, int>();
            private static readonly int MIN_UPDATE_INTERVAL = 30; 

            public static void Postfix(HediffComp_TendDuration __instance)
            {
                try
                {
                    if (__instance?.Pawn == null || !__instance.Pawn.IsColonist) return;
                    if (__instance.parent == null) return;

                    string pawnId = __instance.Pawn.ThingID;

                    
                    int currentTick = Find.TickManager.TicksGame;
                    if (lastUpdateTicks.TryGetValue(pawnId, out int lastUpdate))
                    {
                        if (currentTick - lastUpdate < MIN_UPDATE_INTERVAL)
                        {
                            return; 
                        }
                    }

                    var tendReminders = RiminderManager.GetActiveReminders()
                        ?.OfType<TendReminder>()
                        ?.Where(r => r?.FindPawn()?.ThingID == pawnId)
                        ?.ToList();

                    if (tendReminders == null || !tendReminders.Any()) return;

                    
                    bool isDisease = __instance.parent?.def?.HasComp(typeof(HediffComp_Immunizable)) == true
                        || __instance.parent is HediffWithComps hwc && hwc.TryGetComp<HediffComp_Immunizable>() != null;

                    bool isChronic = __instance.parent?.def?.chronic ?? false;
                    bool isUrgent = __instance.tendTicksLeft <= GenDate.TicksPerHour || !__instance.IsTended;
                    
                    
                    float tendPriority = __instance.parent?.TendPriority ?? 0f;
                    
                    
                    int checkInterval;
                    if (tendPriority >= 1.0f)
                        checkInterval = 30; 
                    else if (tendPriority >= 0.5f)
                        checkInterval = 60; 
                    else if (tendPriority >= 0.1f)
                        checkInterval = 120; 
                    else if (isChronic)
                        checkInterval = 240; 
                    else
                        checkInterval = 180; 

                    if (Find.TickManager.TicksGame % checkInterval == 0)
                    {
                        foreach (var reminder in tendReminders)
                        {
                            try
                            {
                                
                                if (__instance.IsTended)
                                {
                                    
                                    if (reminder.totalTendDuration <= 0)
                                    {
                                        
                                        reminder.totalTendDuration = GenDate.TicksPerDay;
                                    }
                                    
                                    
                                    reminder.tendProgress = TendReminderDataProvider.CalculateHediffProgress(__instance.parent, __instance);
                                    
                                    
                                    reminder.actualTendTicksLeft = __instance.tendTicksLeft;
                                    
                                    
                                    lastUpdateTicks[pawnId] = currentTick;
                                    
                                    
                                    reminder?.Trigger();
                                    
                                    
                                    reminder.ForceProgressUpdate();
                                }
                                else if (reminder.totalTendDuration <= 0 && __instance.tendTicksLeft > 0)
                                {
                                    reminder.totalTendDuration = __instance.tendTicksLeft;
                                }

                                if (reminder.actualTendTicksLeft == -1 ||
                                    (__instance.tendTicksLeft > 0 && __instance.tendTicksLeft < reminder.actualTendTicksLeft))
                                {
                                    reminder.actualTendTicksLeft = __instance.tendTicksLeft;
                                    
                                    lastUpdateTicks[pawnId] = currentTick;
                                    
                                    reminder?.Trigger();
                                }
                            }
                            catch (Exception ex)
                            {
                                if (Prefs.DevMode)
                                {
                                    Log.Error($"[Riminder] Error triggering reminder: {ex}");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex_main)
                {
                    if (Prefs.DevMode)
                    {
                        Log.Error($"[Riminder] Error in tend comp post tick patch: {ex_main}");
                    }
                }
            }
        }

        [HarmonyPatch]
        public static class Pawn_HealthTracker_HealthTick_Patch
        {
            public static MethodBase TargetMethod()
            {
                return AccessTools.Method(typeof(Pawn_HealthTracker), "HealthTick");
            }

            public static void Postfix(Pawn_HealthTracker __instance)
            {
                var pawn = GetPawn(__instance);
                if (pawn == null || !pawn.IsColonist) return;

                var reminders = RiminderManager.GetActiveTendReminders();
                foreach (var reminder in reminders)
                {
                    
                    if (!(reminder is TendReminder tendReminder))
                        continue;
                        
                    
                    var reminderPawn = tendReminder.FindPawn();
                    if (reminderPawn != pawn)
                        continue;
                        
                    
                    if (tendReminder.TendData?.removeOnImmunity == true)
                    {
                        
                        tendReminder.Trigger();
                    }
                }
            }
        }

        [HarmonyPatch]
        public static class Pawn_HealthTracker_ShouldBeDead_Patch
        {
            public static MethodBase TargetMethod()
            {
                return AccessTools.Method(typeof(Pawn_HealthTracker), "ShouldBeDead");
            }

            public static void Postfix(Pawn_HealthTracker __instance, ref bool __result)
            {
                if (__result)
                {
                    var pawn = GetPawn(__instance);
                    if (pawn != null && pawn.IsColonist)
                    {
                        var reminders = RiminderManager.GetActiveTendReminders();
                        foreach (var reminder in reminders)
                        {
                            if (reminder is TendReminder tendReminder && 
                                tendReminder.FindPawn() == pawn)
                            {
                                RiminderManager.RemoveReminder(reminder.id);
                            }
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Pawn_HealthTracker), "AddHediff")]
        [HarmonyPatch(new Type[] {
            typeof(Hediff),
            typeof(BodyPartRecord),
            typeof(DamageInfo?),
            typeof(DamageWorker.DamageResult)
        })]
        public static class Pawn_HealthTracker_AddHediff_Patch
        {
            private static HashSet<string> processedHediffs = new HashSet<string>();


            public static bool IsProcessed(string key)
            {
                return processedHediffs.Contains(key);
            }


            public static void MarkAsProcessed(string key)
            {
                processedHediffs.Add(key);
            }

            [HarmonyPatch(typeof(Game), "LoadGame")]
            public static class Game_LoadGame_Patch
            {
                public static void Postfix() => processedHediffs.Clear();
            }

            [HarmonyPatch(typeof(Game), "InitNewGame")]
            public static class Game_InitNewGame_Patch
            {
                public static void Postfix() => processedHediffs.Clear();
            }

            private static bool IsDisease(Hediff hediff)
            {
                if (hediff == null) return false;


                if (hediff.def.HasComp(typeof(HediffComp_Immunizable)))
                    return true;


                if (hediff is HediffWithComps hwc && hwc.TryGetComp<HediffComp_Immunizable>() != null)
                    return true;

                return false;
            }

            private static bool IsActuallyTendable(Pawn pawn, Hediff hediff)
            {
                if (hediff == null || pawn == null) return false;

                if (hediff.IsPermanent() || !hediff.def.tendable) return false;

                if (hediff.def.defName == "StoppingBlood") return false;

                if (hediff.def.defName.Contains("Missing") ||
                    hediff.def.defName.Contains("Removed") ||
                    hediff.def.defName.Contains("Scar") ||
                    hediff.Label.ToLower().Contains("missing") ||
                    hediff.Label.ToLower().Contains("removed") ||
                    hediff.Label.ToLower().Contains("scar"))
                    return false;

                if (hediff.def.chronic) return false;

                if (hediff.Part != null)
                {
                    bool partIsMissingOrDestroyed = false;

                    foreach (var otherHediff in pawn.health.hediffSet.hediffs)
                    {
                        if (otherHediff == hediff) continue;

                        if (otherHediff.Part == hediff.Part &&
                            (otherHediff.def.defName.Contains("Missing") ||
                             otherHediff.def.defName.Contains("Removed") ||
                             otherHediff.def.defName == "SurgicalCut" ||
                             otherHediff.def.defName == "Stump"))
                        {
                            partIsMissingOrDestroyed = true;
                            break;
                        }

                        BodyPartRecord parentPart = hediff.Part.parent;
                        while (parentPart != null)
                        {
                            if (otherHediff.Part == parentPart &&
                                (otherHediff.def.defName.Contains("Missing") ||
                                 otherHediff.def.defName.Contains("Removed")))
                            {
                                partIsMissingOrDestroyed = true;
                                break;
                            }
                            parentPart = parentPart.parent;
                        }

                        if (partIsMissingOrDestroyed) break;
                    }

                    if (partIsMissingOrDestroyed) return false;
                }

                if (hediff is Hediff_Injury injury)
                {
                    return injury.Severity > 0 &&
                           !injury.IsPermanent() &&
                           injury.TendableNow();
                }

                if (hediff is HediffWithComps hwc)
                {
                    return hwc.TryGetComp<HediffComp_TendDuration>() != null;
                }

                return false;
            }

            public static void Postfix(Pawn_HealthTracker __instance, Hediff hediff, DamageInfo? dinfo)
            {
                try
                {
                    if (hediff == null) return;

                    var pawn = GetPawn(__instance);
                    if (pawn == null || !pawn.IsColonist) return;

                    if (!RiminderMod.Settings.autoCreateTendReminders) return;

                    bool isDiseaseHediff = IsDisease(hediff);
                    bool isTendable = IsActuallyTendable(pawn, hediff);

                    if (!isTendable && !isDiseaseHediff) return;

                    string hediffKey = $"{pawn.ThingID}|{hediff.def.defName}|{hediff.Part?.def.defName ?? "null"}";

                    if (!isDiseaseHediff && IsProcessed(hediffKey)) return;

                    bool hasExistingReminder = RiminderManager.GetActiveTendReminders()
                        .Any(r => r is TendReminder tendReminder && 
                                 tendReminder.FindPawn() == pawn);
                    
                    if (!hasExistingReminder)
                    {
                        var reminder = ReminderFactory.CreateTendReminder(
                            pawn,
                            hediff,
                            hediff is HediffWithComps hwc2 && hwc2.TryGetComp<HediffComp_Immunizable>() != null
                        );
                        
                        if (reminder != null)
                        {
                            RiminderManager.AddReminder(reminder);
                        }
                    }

                    MarkAsProcessed(hediffKey);
                }
                catch (Exception ex)
                {
                    if (Prefs.DevMode)
                    {
                        Log.Error($"[Riminder] Error in AddHediff patch: {ex}");
                    }
                }
            }
        }

        [HarmonyPatch]
        public static class Pawn_HealthTracker_RemoveHediff_Patch
        {
            public static MethodBase TargetMethod()
            {
                return AccessTools.Method(typeof(Pawn_HealthTracker), "RemoveHediff", new[] { typeof(Hediff) });
            }

            public static void Postfix(Pawn_HealthTracker __instance, Hediff hediff)
            {
                try
                {
                    var pawn = GetPawn(__instance);
                    if (pawn == null || !pawn.IsColonist || hediff == null) return;

                    
                    bool hasTendableConditions = pawn.health.hediffSet.hediffs
                        .Any(h => h.def.tendable && !h.IsPermanent() && 
                              h is HediffWithComps hwc && 
                              hwc.TryGetComp<HediffComp_TendDuration>() != null);
                    
                    if (!hasTendableConditions)
                    {
                        
                        var remindersToRemove = RiminderManager.GetActiveTendReminders()
                            .Where(r => r is TendReminder tendReminder && 
                                   tendReminder.FindPawn() == pawn)
                            .ToList();
                            
                        foreach (var reminder in remindersToRemove)
                        {
                            if (Prefs.DevMode)
                            {
                                Log.Message($"[Riminder] Removing reminder for {pawn.LabelShort} - no more tendable conditions");
                            }
                            RiminderManager.RemoveReminder(reminder.id);
                        }
                    }
                    
                    else
                    {
                        RiminderManager.UpdateTendRemindersForPawn(pawn);
                    }
                }
                catch (Exception ex)
                {
                    if (Prefs.DevMode)
                    {
                        Log.Error($"[Riminder] Error in RemoveHediff patch: {ex}");
                    }
                }
            }
        }

        [HarmonyPatch]
        public static class Pawn_HealthTracker_Notify_HediffChanged_Patch
        {
            public static MethodBase TargetMethod()
            {
                return AccessTools.Method(typeof(Pawn_HealthTracker), "Notify_HediffChanged", new[] { typeof(Hediff) });
            }

            public static void Postfix(Pawn_HealthTracker __instance, Hediff hediff)
            {
                try
                {
                    var pawn = GetPawn(__instance);
                    if (pawn == null || !pawn.IsColonist || hediff == null) return;
                    if (hediff.IsPermanent()) return;


                    RiminderManager.UpdateTendRemindersForPawn(pawn);
                }
                catch (Exception ex)
                {
                    if (Prefs.DevMode)
                    {
                        Log.Error($"[Riminder] Error in Notify_HediffChanged: {ex}");
                    }
                }
            }
        }

        [HarmonyPatch(typeof(TendUtility), "DoTend")]
        public static class TendUtility_DoTend_Patch
        {
            public static void Postfix(Pawn doctor, Pawn patient, Medicine medicine)
            {
                try
                {
                    if (patient == null || !patient.IsColonist) return;

                    if (Prefs.DevMode)
                    {
                        Log.Message($"[Riminder] DoTend performed on {patient.LabelShort}");
                    }

                    
                    var reminders = RiminderManager.GetActiveTendReminders();
                    
                    foreach (var reminder in reminders)
                    {
                        if (reminder is TendReminder tendReminder && 
                            tendReminder.FindPawn() == patient)
                        {
                            
                            tendReminder.dataProvider?.Refresh();
                            
                            
                            tendReminder.Trigger();
                            
                            
                            tendReminder.ForceProgressUpdate();
                            
                            if (Prefs.DevMode)
                            {
                                Log.Message($"[Riminder] After DoTend for {patient.LabelShort}, updated reminder status");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (Prefs.DevMode)
                    {
                        Log.Error($"[Riminder] Error in TendUtility.DoTend patch: {ex}");
                    }
                }
            }
        }
    }
}

