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
        private List<Reminder> reminders = new List<Reminder>();

        public RiminderManager(Game game) : base()
        {
            instance = this;
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
                bool isDuplicate = instance.reminders.Any(r =>
                    r is PawnTendReminder tr &&
                    !r.completed && !r.dismissed &&
                    tr.pawnId == tendReminder.pawnId &&
                    tr.hediffLabel == tendReminder.hediffLabel);

                if (isDuplicate)
                {
                    return;
                }
            }

            instance.reminders.Add(reminder);
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
