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
                
                BaseReminder reminder = (BaseReminder)Activator.CreateInstance(def.reminderClass);
                if (reminder == null) return null;
                
                
                reminder.def = def;
                
                
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
                
                var existingReminders = RiminderManager.GetActiveTendReminders()
                    .Where(r => r is TendReminder tendReminder && 
                           tendReminder.FindPawn() == pawn)
                    .ToList();
                    
                if (existingReminders.Any())
                {
                    foreach (var existing in existingReminders)
                    {
                        if (existing is TendReminder tendReminder)
                        {
                            tendReminder.dataProvider?.Refresh();
                            tendReminder.Trigger();
                        }
                    }
                    
                    return existingReminders.FirstOrDefault() as TendReminder;
                }
                
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