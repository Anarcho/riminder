using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using UnityEngine;

namespace Riminder
{
    public class RiminderManager : GameComponent
    {
        private static RiminderManager instance;
        private List<BaseReminder> reminders;
        private bool needsDuplicateRemoval = false;

        public RiminderManager(Game game) : base()
        {
            instance = this;
            reminders = new List<BaseReminder>();
            needsDuplicateRemoval = false;
        }

        public static void Initialize()
        {
            try
            {
                if (instance == null && Current.Game != null)
                {
                    instance = Current.Game.GetComponent<RiminderManager>();
                    if (instance == null)
                    {
                        instance = new RiminderManager(Current.Game);
                        Current.Game.components.Add(instance);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[Riminder] Failed to initialize RiminderManager: {ex}");
            }
        }

        private void RemoveDuplicateTendReminders()
        {
            if (!needsDuplicateRemoval) return;
            
            var tendReminders = reminders.OfType<TendReminder>().ToList();
            if (tendReminders.Count <= 1)
            {
                needsDuplicateRemoval = false;
                return;
            }
            
            var grouped = tendReminders.GroupBy(tr => tr.FindPawn()).Where(g => g.Key != null);
            bool removedAny = false;
            
            foreach (var group in grouped)
            {
                if (group.Count() <= 1) continue;
                
                var toKeep = group.OrderByDescending(tr => (tr.dataProvider as TendReminderDataProvider)?.trackedHediffIds?.Count ?? 0).First();
                foreach (var duplicate in group.Where(tr => tr != toKeep))
                {
                    reminders.Remove(duplicate);
                    removedAny = true;
                }
            }
            
            if (removedAny)
            {
                needsDuplicateRemoval = false;
            }
        }

        public override void GameComponentTick()
        {
            try
            {
                base.GameComponentTick();

                if (reminders == null)
                {
                    reminders = new List<BaseReminder>();
                    return;
                }

                int currentTick = Find.TickManager.TicksGame;

                // Only check for duplicates occasionally and when flagged
                if (needsDuplicateRemoval && currentTick % 300 == 0)
                {
                    RemoveDuplicateTendReminders();
                }

                if (currentTick % 300 != 0) return;

                for (int i = reminders.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        if (i >= reminders.Count || reminders[i] == null)
                        {
                            continue;
                        }

                        BaseReminder reminder = reminders[i];

                        if (reminder.completed || reminder.dismissed)
                        {
                            reminders.RemoveAt(i);
                            continue;
                        }

                        if (currentTick >= reminder.triggerTick)
                        {
                            try
                            {
                                reminder.Trigger();
                                
                                
                                
                                RefreshOpenDialogs();
                            }
                            catch (Exception ex)
                            {
                                if (Prefs.DevMode)
                                {
                                    Log.Error($"[Riminder] Error triggering reminder: {ex}");
                                }
                            }

                            if (reminder.completed)
                            {
                                reminders.RemoveAt(i);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (Prefs.DevMode)
                        {
                            Log.Error($"[Riminder] Error processing reminder at index {i}: {ex}");
                        }
                        
                        if (i < reminders.Count)
                        {
                            reminders.RemoveAt(i);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (Prefs.DevMode)
                {
                    Log.Error($"[Riminder] Error in GameComponentTick: {ex}");
                }
            }
        }

        public static void AddReminder(BaseReminder reminder)
        {
            try
            {
                if (instance == null)
                {
                    Log.Error("[Riminder] Cannot add reminder - RiminderManager not initialized");
                    return;
                }

                if (instance.reminders == null)
                {
                    instance.reminders = new List<BaseReminder>();
                }

                if (reminder == null)
                {
                    Log.Error("[Riminder] Attempted to add null reminder");
                    return;
                }

                instance.reminders.Add(reminder);

                // Flag for duplicate removal if this is a TendReminder
                if (reminder is TendReminder)
                {
                    instance.needsDuplicateRemoval = true;
                    // Invalidate harmony patch cache
                    InvalidateHarmonyCache();
                }

                try
                {
                    var openDialogs = Find.WindowStack?.Windows?.OfType<Dialog_ViewReminders>().ToList();
                    if (openDialogs != null && openDialogs.Any())
                    {
                        foreach (var dialog in openDialogs)
                        {
                            if (dialog != null)
                            {
                                dialog.RefreshAndRedraw();
                            }
                        }
                    }
                }
                catch (Exception) { }
            }
            catch (Exception ex)
            {
                Log.Error($"[Riminder] Error adding reminder: {ex}");
            }
        }

        private static void InvalidateHarmonyCache()
        {
            try
            {
                // Call the Harmony patch's cache invalidation method
                HarmonyPatches.HediffComp_TendDuration_CompPostTick_Patch.InvalidateCache();
            }
            catch (Exception ex)
            {
                if (Prefs.DevMode)
                {
                    Log.Error($"[Riminder] Error invalidating harmony cache: {ex}");
                }
            }
        }

        public static List<BaseReminder> GetActiveReminders()
        {
            if (Current.Game == null || Find.CurrentMap == null)
            {
                return new List<BaseReminder>();
            }
            
            try
            {
                if (instance == null) return new List<BaseReminder>();
                if (instance.reminders == null) return new List<BaseReminder>();

                return instance.reminders
                    .Where(r => r != null && !r.completed && !r.dismissed)
                    .Where(r => ShouldShowReminder(r))
                    .ToList();
            }
            catch (Exception ex)
            {
                Log.Error($"[Riminder] Error getting active reminders: {ex}");
                return new List<BaseReminder>();
            }
        }

        private static bool ShouldShowReminder(BaseReminder reminder)
        {
            
            bool isTendReminder = reminder is TendReminder || 
                                reminder.GetLabel().StartsWith("Tend ") || 
                                reminder.frequency == ReminderFrequency.WhenTendingRequired;
                                
            
            if (isTendReminder)
                return RiminderMod.Settings.enableTendReminders;
            else
                return RiminderMod.Settings.enableNormalReminders;
        }

        public static List<BaseReminder> GetActiveTendReminders()
        {
            if (Current.Game == null)
                return new List<BaseReminder>();
            
            if (instance == null) return new List<BaseReminder>();
            if (instance.reminders == null) return new List<BaseReminder>();
            
            try
            {
                
                if (!RiminderMod.Settings.enableTendReminders)
                    return new List<BaseReminder>();
                    
                return instance.reminders
                    .Where(r => r != null && (r is TendReminder || r.GetLabel().StartsWith("Tend ")) && !r.completed && !r.dismissed)
                    .ToList();
            }
            catch (Exception ex)
            {
                Log.Error($"[Riminder] Error getting tend reminders: {ex}");
                return new List<BaseReminder>();
            }
        }

        public static void RemoveReminder(string id)
         {
            try
            {
                if (instance == null) return;
                if (instance.reminders == null) return;
                if (string.IsNullOrEmpty(id)) return;

                // Check if we're removing a TendReminder
                bool removedTendReminder = instance.reminders.Any(r => r != null && r.id == id && r is TendReminder);
                
                instance.reminders.RemoveAll(r => r != null && r.id == id);

                // Invalidate cache if we removed a TendReminder
                if (removedTendReminder)
                {
                    InvalidateHarmonyCache();
                }

                try
                {
                    var openDialogs = Find.WindowStack?.Windows?.OfType<Dialog_ViewReminders>().ToList();
                    if (openDialogs != null && openDialogs.Any())
                    {
                        foreach (var dialog in openDialogs)
                        {
                            if (dialog != null)
                            {
                                dialog.RefreshAndRedraw();
                            }
                        }
                    }
                }
                catch (Exception) { }
            }
            catch (Exception ex)
            {
                Log.Error($"[Riminder] Error removing reminder: {ex}");
            }
        }

        public static void RemoveReminderForPawn(string pawnId)
        {
            if (instance?.reminders == null) return;
            
            bool removedAny = instance.reminders.RemoveAll(r =>
                r is TendReminder tr &&
                (tr.FindPawn()?.ThingID == pawnId)) > 0;
                
            // Invalidate cache if we removed any TendReminders
            if (removedAny)
            {
                InvalidateHarmonyCache();
            }
        }

        public override void ExposeData()
        {
            try
            {
                base.ExposeData();
                Scribe_Collections.Look(ref reminders, "reminders", LookMode.Deep);
                Scribe_Values.Look(ref needsDuplicateRemoval, "needsDuplicateRemoval", false);

                if (Scribe.mode == LoadSaveMode.LoadingVars && reminders == null)
                {
                    reminders = new List<BaseReminder>();
                    needsDuplicateRemoval = false;
                }

                if (Scribe.mode == LoadSaveMode.PostLoadInit)
                {
                    instance = this;

                    if (reminders != null)
                    {
                        reminders.RemoveAll(r => r == null);

                        for (int i = reminders.Count - 1; i >= 0; i--)
                        {
                            if (reminders[i].completed || reminders[i].dismissed)
                            {
                                reminders.RemoveAt(i);
                            }
                        }
                        
                        // Check if we have any TendReminders that might need deduplication
                        needsDuplicateRemoval = reminders.OfType<TendReminder>().Count() > 1;
                        RemoveDuplicateTendReminders();
                    }
                    else
                    {
                        reminders = new List<BaseReminder>();
                        needsDuplicateRemoval = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[Riminder] Error in ExposeData: {ex}");
                reminders = new List<BaseReminder>();
            }
        }

        public static void RefreshOpenDialogs()
        {
            try
            {
                var openDialogs = Find.WindowStack?.Windows?.OfType<Dialog_ViewReminders>().ToList();
                if (openDialogs != null && openDialogs.Count > 0)
                {
                    foreach (var dialog in openDialogs)
                    {
                        if (dialog != null)
                        {
                            dialog.RefreshAndRedraw();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (Prefs.DevMode)
                    Log.Error($"[Riminder] Error refreshing dialogs: {ex}");
            }
        }

        public static void UpdateReminder(BaseReminder reminder)
        {
            if (instance == null || instance.reminders == null) return;
            
            int index = instance.reminders.FindIndex(r => r.id == reminder.id);
            if (index >= 0)
            {
                instance.reminders[index] = reminder;
            }
        }
        
        public static void UpdateTendRemindersForPawn(Pawn pawn)
        {
            if (pawn == null || instance == null || instance.reminders == null) return;
            
            try
            {
                var tendReminders = GetActiveTendReminders()
                    .OfType<TendReminder>()
                    .Where(r => r.FindPawn() == pawn)
                    .ToList();
                
                foreach (var reminder in tendReminders)
                {
                    
                    reminder.dataProvider?.Refresh();
                    
                    
                    if (reminder.ShouldComplete())
                    {
                        reminder.completed = true;
                    }
                }
                
                RefreshOpenDialogs();
            }
            catch (Exception ex)
            {
                if (Prefs.DevMode)
                {
                    Log.Error($"[Riminder] Error updating tend reminders for pawn {pawn.LabelShort}: {ex}");
                }
            }
        }

        public static void ForceDuplicateRemoval()
        {
            if (instance != null)
            {
                instance.needsDuplicateRemoval = true;
                instance.RemoveDuplicateTendReminders();
            }
        }
        
        public static bool HasTendReminderForPawn(Pawn pawn)
        {
            if (pawn == null || instance == null || instance.reminders == null) return false;
            
            return instance.reminders.OfType<TendReminder>()
                .Any(tr => tr.FindPawn() == pawn && !tr.completed && !tr.dismissed);
        }
    }
}
