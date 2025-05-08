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

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            if (Find.TickManager.TicksGame % 60 != 0) return;

            int currentTick = Find.TickManager.TicksGame;

            for (int i = reminders.Count - 1; i >= 0; i--)
            {
                Reminder reminder = reminders[i];

                if (reminder.completed || reminder.dismissed)
                {
                    reminders.RemoveAt(i);
                    continue;
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
        }

        public static void AddReminder(Reminder reminder)
        {
            if (instance == null)
            {
                Log.Error("Cannot add reminder - RiminderManager not initialized");
                return;
            }

            if (reminder is PawnTendReminder tendReminder)
            {
                // Find any existing tend reminders for this pawn
                var existingReminders = instance.reminders
                    .Where(r => !r.completed && !r.dismissed)
                    .OfType<PawnTendReminder>()
                    .Where(r => r.pawnId == tendReminder.pawnId)
                    .ToList();

                // If we already have a reminder for this specific condition, update it
                var exactMatch = existingReminders.FirstOrDefault(r => 
                    r.hediffId == tendReminder.hediffId || 
                    r.hediffLabel == tendReminder.hediffLabel);

                if (exactMatch != null)
                {
                    exactMatch.label = tendReminder.label;
                    exactMatch.description = tendReminder.description;
                    exactMatch.triggerTick = tendReminder.triggerTick;
                    exactMatch.removeOnImmunity = tendReminder.removeOnImmunity;
                    return;
                }

                // If we find a similar reminder (same pawn, similar condition name), update it
                var similarMatch = existingReminders.FirstOrDefault(r =>
                    r.hediffId.Contains(tendReminder.hediffId) ||
                    tendReminder.hediffId.Contains(r.hediffId) ||
                    r.hediffLabel.Contains(tendReminder.hediffLabel) ||
                    tendReminder.hediffLabel.Contains(r.hediffLabel));

                if (similarMatch != null)
                {
                    similarMatch.label = tendReminder.label;
                    similarMatch.description = tendReminder.description;
                    similarMatch.triggerTick = tendReminder.triggerTick;
                    similarMatch.removeOnImmunity = tendReminder.removeOnImmunity;
                    return;
                }
            }

            instance.reminders.Add(reminder);

            // Refresh any open reminder dialogs
            var openDialogs = Find.WindowStack?.Windows?.OfType<Dialog_ViewReminders>();
            if (openDialogs != null && openDialogs.Any())
            {
                foreach (var dialog in openDialogs)
                {
                    dialog.RefreshAndRedraw();
                }
            }
        }

        public static List<Reminder> GetActiveReminders()
        {
            if (instance == null) return new List<Reminder>();

            return instance.reminders
                .Where(r => !r.completed && !r.dismissed)
                .ToList();
        }

        public static List<PawnTendReminder> GetActiveTendReminders()
        {
            if (instance == null) return new List<PawnTendReminder>();

            return instance.reminders
                .Where(r => r is PawnTendReminder && !r.completed && !r.dismissed)
                .Cast<PawnTendReminder>()
                .ToList();
        }

        public static void RemoveReminder(string id)
        {
            if (instance == null) return;

            instance.reminders.RemoveAll(r => r.id == id);

            // Refresh any open reminder dialogs
            var openDialogs = Find.WindowStack?.Windows?.OfType<Dialog_ViewReminders>();
            if (openDialogs != null && openDialogs.Any())
            {
                foreach (var dialog in openDialogs)
                {
                    dialog.RefreshAndRedraw();
                }
            }
        }

        public override void ExposeData()
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

                for (int i = reminders.Count - 1; i >= 0; i--)
                {
                    if (reminders[i].completed || reminders[i].dismissed)
                    {
                        reminders.RemoveAt(i);
                    }
                }
            }
        }
    }
}
