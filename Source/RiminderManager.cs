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

            // Check reminders only once every 60 ticks (1 in-game second)
            if (Find.TickManager.TicksGame % 60 != 0) return;

            int currentTick = Find.TickManager.TicksGame;
            
            // Check for triggered reminders
            for (int i = reminders.Count - 1; i >= 0; i--)
            {
                Reminder reminder = reminders[i];
                
                // Immediately remove completed or dismissed reminders
                if (reminder.completed || reminder.dismissed)
                {
                    reminders.RemoveAt(i);
                    continue;
                }
                
                // Check if it's time to trigger
                if (currentTick >= reminder.triggerTick)
                {
                    // Trigger the reminder
                    reminder.Trigger();
                    
                    // If it's a one-time reminder, it will be marked as completed
                    // Remove it immediately if it's now completed
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
            
            instance.reminders.Add(reminder);
        }

        public static List<Reminder> GetActiveReminders()
        {
            if (instance == null) return new List<Reminder>();
            
            // Filter out completed and dismissed reminders
            return instance.reminders
                .Where(r => !r.completed && !r.dismissed)
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
            
            // Initialize list if null
            if (Scribe.mode == LoadSaveMode.LoadingVars && reminders == null)
            {
                reminders = new List<Reminder>();
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                instance = this;
                
                // Remove any completed or dismissed reminders on load
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