using System;
using System.Collections.Generic;
using Verse;
using RimWorld;
using System.Linq;

namespace Riminder
{
    public static class ReminderFactory
    {
        public static BaseReminder CreateReminder(ReminderDef def)
        {
            if (def == null) return null;
            
            try
            {
                // Create the reminder instance
                BaseReminder reminder = (BaseReminder)Activator.CreateInstance(def.reminderClass);
                if (reminder == null) return null;
                
                // Set the def
                reminder.def = def;
                
                // Create and set the data provider
                if (def.dataProviderClass != null)
                {
                    reminder.SetDataProvider((IReminderDataProvider)Activator.CreateInstance(def.dataProviderClass));
                }
                
                return reminder;
            }
            catch (Exception ex)
            {
                if (Prefs.DevMode)
                {
                    Log.Error($"[Riminder] Failed to create reminder of type {def.defName}: {ex}");
                }
                return null;
            }
        }
        
        public static TendReminder CreateTendReminder(Pawn pawn, Hediff hediff, bool removeOnImmunity)
        {
            if (pawn == null || hediff == null) return null;
            
            try
            {
                // First check if a reminder already exists for this pawn
                var existingReminders = RiminderManager.GetActiveTendReminders()
                    .Where(r => r is TendReminder tendReminder && 
                           tendReminder.FindPawn() == pawn)
                    .ToList();
                    
                if (existingReminders.Any())
                {
                    if (Prefs.DevMode)
                    {
                        Log.Message($"[Riminder] Not creating new reminder for {pawn.LabelShort} - reminder already exists");
                    }
                    
                    // Instead of creating a new one, refresh the existing one
                    foreach (var existing in existingReminders)
                    {
                        if (existing is TendReminder tendReminder)
                        {
                            tendReminder.dataProvider?.Refresh();
                            tendReminder.Trigger();
                        }
                    }
                    
                    // Return the first existing reminder
                    return existingReminders.FirstOrDefault() as TendReminder;
                }
                
                // Create a new reminder only if one doesn't exist
                var reminder = new TendReminder(pawn, hediff, removeOnImmunity);
                reminder.def = ReminderDefOf.TendReminder;
                return reminder;
            }
            catch (Exception ex)
            {
                if (Prefs.DevMode)
                {
                    Log.Error($"[Riminder] Failed to create tend reminder: {ex}");
                }
                return null;
            }
        }
    }
    
    // Extension method for BaseReminder
    public static class BaseReminderExtensions
    {
        public static void SetDataProvider(this BaseReminder reminder, IReminderDataProvider provider)
        {
            var field = reminder.GetType().GetField("dataProvider", 
                System.Reflection.BindingFlags.Instance | 
                System.Reflection.BindingFlags.NonPublic);
                
            if (field != null)
            {
                field.SetValue(reminder, provider);
            }
        }
    }
} 