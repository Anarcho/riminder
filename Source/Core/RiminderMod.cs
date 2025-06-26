using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using UnityEngine;
using HarmonyLib;

namespace Riminder
{
    public class RiminderMod : Mod
    {
        public static RiminderSettings Settings { get; private set; }

        public RiminderMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<RiminderSettings>();
            RiminderManager.Initialize();
            AutoTendReminderManager.Initialize();
            
            
            var harmony = new Harmony("riminder.keybinds");
            harmony.PatchAll();
        }

        public override string SettingsCategory()
        {
            return "Riminder";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DoWindowContents(inRect);
        }
    }
    
    [HarmonyPatch(typeof(Game))]
    [HarmonyPatch("UpdatePlay")]
    public static class Game_UpdatePlay_Patch
    {
        public static void Postfix()
        {
            if (Current.Game != null && ReminderDefOf.Riminder_OpenReminders != null && 
                ReminderDefOf.Riminder_OpenReminders.KeyDownEvent)
            {
                Find.WindowStack.Add(new Dialog_ViewReminders());
            }
        }
    }
}