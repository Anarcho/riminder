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

        static HarmonyPatches()
        {
            var harmony = new Harmony("com.riminder.patches");
            harmony.PatchAll();
            
            // Log that patches were applied
            Log.Message("[Riminder] Harmony patches applied");
        }
        
        // Create a KeyPressHandler class that will register keyboard events properly
        public class KeyPressHandler : MonoBehaviour
        {
            private void Update()
            {
                if (Current.Game == null) return;
                
                if (RiminderHotKeyDefOf.OpenRiminderDialog != null && 
                    RiminderHotKeyDefOf.OpenRiminderDialog.KeyDownEvent &&
                    Find.WindowStack != null)
                {
                    Log.Message("[Riminder] Hotkey detected, opening dialog");
                    Find.WindowStack.Add(new Dialog_ViewReminders());
                }
            }
        }
        
        // Patch UIRoot_Play to add our key handler
        [HarmonyPatch(typeof(UIRoot_Play), "UIRootOnGUI")]
        public static class UIRoot_Play_UIRootOnGUI_Patch
        {
            private static bool initialized = false;
            
            [HarmonyPostfix]
            public static void Postfix()
            {
                if (!initialized && Current.Game != null)
                {
                    var gameObject = new GameObject("RiminderKeyHandler");
                    gameObject.AddComponent<KeyPressHandler>();
                    UnityEngine.Object.DontDestroyOnLoad(gameObject);
                    initialized = true;
                    Log.Message("[Riminder] Key handler initialized");
                }
            }
        }
        
        // Patch for adding option to the pawn's float menu
        [HarmonyPatch(typeof(FloatMenuMakerMap), "AddHumanlikeOrders")]
        public static class FloatMenuMakerMap_AddHumanlikeOrders_Patch
        {
            public static void Postfix(Vector3 clickPos, Pawn pawn, List<FloatMenuOption> opts)
            {
                // Get the pawn at the clicked position if none was provided
                if (pawn == null)
                {
                    IntVec3 clickedCell = clickPos.ToIntVec3();
                    if (!GenGrid.InBounds(clickedCell, Find.CurrentMap)) return;
                    
                    // Find all pawns at the clicked position
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
                
                // Only show for colonists
                if (pawn == null || !pawn.IsColonist) return;
                
                // Convert click position to IntVec3 and check if it's within 1 cell of the pawn
                IntVec3 targetCell = clickPos.ToIntVec3();
                if (!GenGrid.InBounds(targetCell, pawn.Map) || !GenGrid.InBounds(pawn.Position, pawn.Map)) return;
                
                // Check if click is within 1 cell of pawn
                if (targetCell.DistanceTo(pawn.Position) > 1) return;
                
                // Check if pawn has any tendable hediffs that actually need tending
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
                
                // Scars and permanent conditions don't need tending
                if (hediff.IsPermanent()) return false;
                
                // Skip "removed" hediffs
                if (hediff.def.defName.Contains("Removed") || hediff.Label.Contains("removed")) return false;
                
                // Check if it has a tend duration component
                if (hediff is HediffWithComps hwc)
                {
                    var tendComp = hwc.TryGetComp<HediffComp_TendDuration>();
                    if (tendComp != null)
                    {
                        // Allow creating reminders for ANY condition with a tend component,
                        // whether it's currently tended or not, as eventually it will need tending again
                        return true;
                    }
                }
                
                // For injuries, check if they're still healing
                if (hediff is Hediff_Injury injury)
                {
                    // Allow any injury with severity > 0, even if currently tended
                    return injury.Severity > 0;
                }
                
                return false;
            }
        }
        
        // Patch for adding option to the health tab
        [HarmonyPatch(typeof(ITab_Pawn_Health), "FillTab")]
        public static class ITab_Pawn_Health_FillTab_Patch
        {
            private static bool buttonsInitialized = false;
            private static MethodInfo getPawnMethod;
            
            [HarmonyPostfix]
            public static void Postfix(ITab_Pawn_Health __instance)
            {
                // Find the GetPawn method if we haven't already
                if (!buttonsInitialized)
                {
                    getPawnMethod = AccessTools.Method(typeof(ITab_Pawn_Health), "get_PawnForHealth");
                    buttonsInitialized = true;
                }
                
                // Get the pawn
                Pawn pawn = (Pawn)getPawnMethod.Invoke(__instance, null);
                if (pawn == null || !pawn.IsColonistPlayerControlled) return;
                
                // Check if pawn has any tendable hediffs
                bool hasTendableHediffs = false;
                foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
                {
                    if (hediff.def.tendable)
                    {
                        hasTendableHediffs = true;
                        break;
                    }
                }
                
                if (hasTendableHediffs)
                {
                    // Position the button in the health tab
                    float buttonWidth = 180f;
                    float buttonHeight = 28f;
                    Rect buttonRect = new Rect(
                        30f,
                        580f,
                        buttonWidth,
                        buttonHeight);
                    
                    // Draw the button
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
            public static void Postfix(HediffComp_TendDuration __instance)
            {
                try
                {
                    if (__instance?.Pawn == null || !__instance.Pawn.IsColonist) return;
                    if (__instance.parent == null) return;

                    // Check if this hediff has been tended
                    if (HediffUtility.IsTended(__instance.parent))
                    {
                        // Find any existing reminders for this hediff using both ID formats
                        string loadIdStr = __instance.parent.loadID.ToString();
                        string alternateId = $"{__instance.Pawn.ThingID}_{__instance.parent.def.defName}";
                        
                        var reminders = RiminderManager.GetActiveReminders()
                            .OfType<PawnTendReminder>()
                            .Where(r => r.pawnId == __instance.Pawn.ThingID && 
                                      (r.hediffId == loadIdStr || r.hediffId == alternateId ||
                                       r.hediffId.Contains(__instance.parent.def.defName)));

                        foreach (var reminder in reminders)
                        {
                            // Trigger the reminder to update its tend time, instead of removing it
                            reminder.Trigger();
                        }
                    }
                }
                catch (Exception)
                {
                    // Silently fail
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

                // Check for fully healed conditions
                var reminders = RiminderManager.GetActiveReminders()
                    .OfType<PawnTendReminder>()
                    .Where(r => r.pawnId == pawn.ThingID);

                foreach (var reminder in reminders)
                {
                    if (reminder.removeOnImmunity)
                    {
                        var hediff = reminder.FindHediff(pawn);
                        if (hediff == null || hediff.Severity <= 0)
                        {
                            RiminderManager.RemoveReminder(reminder.id);
                        }
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
                        // Remove all reminders for the dead pawn
                        var reminders = RiminderManager.GetActiveReminders()
                            .OfType<PawnTendReminder>()
                            .Where(r => r.pawnId == pawn.ThingID);

                        foreach (var reminder in reminders)
                        {
                            RiminderManager.RemoveReminder(reminder.id);
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Pawn_HealthTracker))]
        [HarmonyPatch("AddHediff")]
        [HarmonyPatch(new Type[] { 
            typeof(Hediff),
            typeof(BodyPartRecord),
            typeof(DamageInfo?),
            typeof(DamageWorker.DamageResult)
        })]
        public static class Pawn_HealthTracker_AddHediff_Patch
        {
            // Static set to track processed hediffs during the current game session
            private static HashSet<string> processedHediffs = new HashSet<string>();
            
            // Clear the processed hediffs set when the game is loaded
            [HarmonyPatch(typeof(Game), "LoadGame")]
            public static class Game_LoadGame_Patch
            {
                public static void Postfix()
                {
                    processedHediffs.Clear();
                }
            }

            // Also clear processed hediffs when a new game is started
            [HarmonyPatch(typeof(Game), "InitNewGame")]
            public static class Game_InitNewGame_Patch
            {
                public static void Postfix()
                {
                    processedHediffs.Clear();
                }
            }

            public static void Postfix(Pawn_HealthTracker __instance, Hediff hediff, DamageInfo? dinfo)
            {
                try
                {
                    if (hediff == null) return;
                    
                    var pawn = GetPawn(__instance);
                    if (pawn == null || !pawn.IsColonist) return;
                    
                    // Skip permanent conditions like scars
                    if (hediff.IsPermanent()) return;

                    // Skip if it's not tendable at all
                    if (!hediff.def.tendable) return;
                    
                    // Skip "removed" hediffs 
                    if (hediff.def.defName.Contains("Removed") || hediff.Label.Contains("removed")) return;
                    
                    // *** Check if auto-create is enabled - if not, don't create reminders automatically ***
                    if (!RiminderMod.Settings.autoCreateTendReminders) return;

                    // Create a comprehensive unique identifier for this hediff
                    string hediffKey = $"{pawn.ThingID}|{hediff.def.defName}|{hediff.Part?.def.defName ?? "null"}";
                    
                    // Skip if we've already processed a hediff with this exact characteristics recently
                    // This prevents duplicates when the same disease/injury is applied multiple times
                    if (processedHediffs.Contains(hediffKey)) return;
                    
                    // Check if it's a disease or injury that needs tending
                    bool shouldCreateReminder = false;
                    
                    if (hediff is HediffWithComps hwc)
                    {
                        var tendComp = hwc.TryGetComp<HediffComp_TendDuration>();
                        if (tendComp != null)
                        {
                            // Allow creating reminders for any condition with a tend component,
                            // even if currently tended, as it will need tending again eventually
                            shouldCreateReminder = true;
                        }
                    }
                    
                    // Check if it's an injury that needs tending
                    if (hediff is Hediff_Injury injury)
                    {
                        // Allow any injury with severity > 0, even if currently tended
                        shouldCreateReminder = injury.Severity > 0;
                    }
                    
                    if (shouldCreateReminder)
                    {
                        // Comprehensive check for existing reminders:
                        // 1. Check by pawnId + hediff label (e.g., "plague")
                        // 2. Check by pawnId + hediff def name
                        // 3. Check by loadID if available
                        bool hasExistingReminder = false;
                        
                        var allReminders = RiminderManager.GetActiveReminders();
                        foreach (var reminder in allReminders)
                        {
                            if (reminder is PawnTendReminder tendReminder)
                            {
                                // Check if this reminder is for the same pawn
                                if (tendReminder.pawnId != pawn.ThingID) continue;
                                
                                // Check if it's for the same condition
                                if (tendReminder.hediffLabel == hediff.def.label || 
                                    tendReminder.hediffId.Contains(hediff.def.defName))
                                {
                                    hasExistingReminder = true;
                                    break;
                                }
                                
                                // Check by load ID if possible
                                if (hediff.loadID > 0 && tendReminder.hediffId == hediff.loadID.ToString())
                                {
                                    hasExistingReminder = true;
                                    break;
                                }
                            }
                        }
                        
                        if (!hasExistingReminder)
                        {
                            // Create the reminder only if we don't have an existing one
                            var reminder = new PawnTendReminder(
                                pawn,
                                hediff,
                                hediff is HediffWithComps hwc2 && hwc2.TryGetComp<HediffComp_Immunizable>() != null
                            );
                            RiminderManager.AddReminder(reminder);
                            
                            // Mark this hediff as processed
                            processedHediffs.Add(hediffKey);
                        }
                    }
                }
                catch (Exception)
                {
                    // Silently fail - don't log errors to avoid stack trace issues
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

                    // Remove any reminders for this hediff using both ID formats
                    string loadIdStr = hediff.loadID.ToString();
                    string alternateId = $"{pawn.ThingID}_{hediff.def.defName}";
                    
                    var reminders = RiminderManager.GetActiveReminders()
                        .OfType<PawnTendReminder>()
                        .Where(r => r.pawnId == pawn.ThingID && 
                                  (r.hediffId == loadIdStr || r.hediffId == alternateId ||
                                   r.hediffLabel == hediff.def.label))
                        .ToList(); // Create a copy to avoid modification during enumeration

                    foreach (var reminder in reminders)
                    {
                        RiminderManager.RemoveReminder(reminder.id);
                    }
                }
                catch (Exception)
                {
                    // Silently fail
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
                    // We'll let AddHediff handle creating new reminders
                    // This patch should only handle updating existing reminders
                    var pawn = GetPawn(__instance);
                    if (pawn == null || !pawn.IsColonist || hediff == null) return;

                    // Skip permanent conditions like scars
                    if (hediff.IsPermanent()) return;

                    // Check if this hediff needs tending
                    if (hediff is HediffWithComps hediffWithComps)
                    {
                        var tendComp = hediffWithComps.TryGetComp<HediffComp_TendDuration>();
                        if (tendComp != null)
                        {
                            // Create the two possible ID formats that might be used by the reminder
                            string loadIdStr = hediff.loadID.ToString();
                            string alternateId = $"{pawn.ThingID}_{hediff.def.defName}";
                            
                            // Find all possible matching reminders to update
                            var existingReminders = RiminderManager.GetActiveReminders()
                                .OfType<PawnTendReminder>()
                                .Where(r => r.pawnId == pawn.ThingID && 
                                       (r.hediffId == loadIdStr || 
                                        r.hediffId == alternateId ||
                                        r.hediffId.Contains(hediff.def.defName) || 
                                        r.hediffLabel == hediff.def.label ||
                                        hediff.def.defName.Contains(r.hediffLabel) ||
                                        hediff.def.label == r.hediffLabel))
                                .ToList();

                            foreach (var reminder in existingReminders)
                            {
                                // The reminder will update itself in its Trigger method
                                reminder.Trigger();
                            }
                            
                            if (existingReminders.Count > 0)
                            {
                                // Also trigger a refresh of the view dialog if it's open
                                var openDialogs = Find.WindowStack?.Windows?.OfType<Dialog_ViewReminders>();
                                if (openDialogs != null && openDialogs.Any())
                                {
                                    foreach (var dialog in openDialogs)
                                    {
                                        dialog.RefreshTendReminders();
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log errors in development mode
                    if (Prefs.DevMode)
                    {
                        Log.Error($"[Riminder] Error in Notify_HediffChanged: {ex}");
                    }
                }
            }
        }
    }
} 