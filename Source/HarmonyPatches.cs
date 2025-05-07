using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace Riminder
{
    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        static HarmonyPatches()
        {
            Harmony harmony = new Harmony("User.Riminder");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            
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
    }
} 