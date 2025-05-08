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
        private List<Reminder> reminders;

        public RiminderManager(Game game) : base()
        {
            instance = this;
            reminders = new List<Reminder>();
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

        public override void GameComponentTick()
        {
            try
            {
                base.GameComponentTick();

                if (reminders == null)
                {
                    reminders = new List<Reminder>();
                    return;
                }

                int currentTick = Find.TickManager.TicksGame;

                bool shouldRefreshDescriptions = currentTick % 60 == 0;

                for (int i = reminders.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        if (i >= reminders.Count || reminders[i] == null)
                        {
                            continue;
                        }

                        Reminder reminder = reminders[i];

                        if (reminder.completed || reminder.dismissed)
                        {
                            reminders.RemoveAt(i);
                            continue;
                        }

                        if (shouldRefreshDescriptions && reminder is PawnTendReminder tendReminder)
                        {
                            Pawn pawn = tendReminder.FindPawn();
                            if (pawn != null)
                            {
                                tendReminder.UpdateLabelAndDescription(pawn);
                            }
                        }

                        if (currentTick >= reminder.triggerTick)
                        {
                            reminder.Trigger();

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

                if (shouldRefreshDescriptions)
                {
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
            }
            catch (Exception ex)
            {
                if (Prefs.DevMode)
                {
                    Log.Error($"[Riminder] Error in GameComponentTick: {ex}");
                }
            }
        }

        public static void AddReminder(Reminder reminder)
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
                    instance.reminders = new List<Reminder>();
                }

                if (reminder == null)
                {
                    Log.Error("[Riminder] Attempted to add null reminder");
                    return;
                }

                if (reminder is PawnTendReminder tendReminder)
                {
                    try
                    {
                        var existingReminders = instance.reminders
                            .Where(r => r != null && !r.completed && !r.dismissed)
                            .OfType<PawnTendReminder>()
                            .Where(r => r.pawnId == tendReminder.pawnId)
                            .ToList();

                        if (existingReminders.Any())
                        {
                            var existingReminder = existingReminders.First();
                            existingReminder.label = tendReminder.label;
                            existingReminder.description = tendReminder.description;
                            existingReminder.triggerTick = tendReminder.triggerTick;
                            existingReminder.removeOnImmunity = tendReminder.removeOnImmunity;
                            existingReminder.hediffId = tendReminder.hediffId;
                            existingReminder.hediffLabel = tendReminder.hediffLabel;
                            
                            try
                            {
                                var existingDialogs = Find.WindowStack?.Windows?.OfType<Dialog_ViewReminders>().ToList();
                                if (existingDialogs != null && existingDialogs.Any())
                                {
                                    foreach (var dialog in existingDialogs)
                                    {
                                        if (dialog != null)
                                        {
                                            dialog.RefreshAndRedraw();
                                        }
                                    }
                                }
                            }
                            catch (Exception) { }
                            
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[Riminder] Error checking existing tend reminders: {ex}");
                    }
                }

                instance.reminders.Add(reminder);

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

        public static List<Reminder> GetActiveReminders()
        {
            if (Current.Game == null || Find.CurrentMap == null)
            {
                return new List<Reminder>();
            }
            
            try
            {
                if (instance == null) return new List<Reminder>();
                if (instance.reminders == null) return new List<Reminder>();

                return instance.reminders
                    .Where(r => r != null && !r.completed && !r.dismissed)
                    .ToList();
            }
            catch (Exception ex)
            {
                Log.Error($"[Riminder] Error getting active reminders: {ex}");
                return new List<Reminder>();
            }
        }

        public static List<PawnTendReminder> GetActiveTendReminders()
        {
            if (Current.Game == null || Find.CurrentMap == null)
            {
                return new List<PawnTendReminder>();
            }
            
            try
            {
                if (instance == null) return new List<PawnTendReminder>();
                if (instance.reminders == null) return new List<PawnTendReminder>();

                return instance.reminders
                    .Where(r => r != null && r is PawnTendReminder && !r.completed && !r.dismissed)
                    .Cast<PawnTendReminder>()
                    .ToList();
            }
            catch (Exception ex)
            {
                Log.Error($"[Riminder] Error getting active tend reminders: {ex}");
                return new List<PawnTendReminder>();
            }
        }

        public static void RemoveReminder(string id)
        {
            try
            {
                if (instance == null) return;
                if (instance.reminders == null) return;
                if (string.IsNullOrEmpty(id)) return;

                instance.reminders.RemoveAll(r => r != null && r.id == id);

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

        public override void ExposeData()
        {
            try
            {
                base.ExposeData();
                Scribe_Collections.Look(ref reminders, "reminders", LookMode.Deep);

                if (Scribe.mode == LoadSaveMode.LoadingVars && reminders == null)
                {
                    reminders = new List<Reminder>();
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
                    }
                    else
                    {
                        reminders = new List<Reminder>();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[Riminder] Error in ExposeData: {ex}");
                reminders = new List<Reminder>();
            }
        }
    }
}
