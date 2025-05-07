using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Riminder
{
    public class Alert_Reminder : Alert
    {
        private List<Reminder> dueReminders = new List<Reminder>();

        public Alert_Reminder()
        {
            defaultLabel = "Reminder Due";
            defaultPriority = AlertPriority.Medium;
        }

        public override string GetLabel()
        {
            if (dueReminders.Count == 1)
            {
                return "Reminder Due: " + dueReminders[0].label;
            }
            return "Multiple Reminders Due";
        }

        public override TaggedString GetExplanation()
        {
            if (dueReminders.Count == 0) return "";
            
            if (dueReminders.Count == 1)
            {
                Reminder reminder = dueReminders[0];
                return $"Reminder: {reminder.label}\n\n{reminder.description}";
            }
            
            string result = "Multiple reminders are due:\n";
            foreach (Reminder reminder in dueReminders)
            {
                result += $"\n- {reminder.label}";
            }
            return result;
        }

        public override AlertReport GetReport()
        {
            if (!ModsConfig.IsActive("User.Riminder")) return false;
            
            dueReminders.Clear();
            List<Reminder> activeReminders = RiminderManager.GetActiveReminders();
            
            int currentTick = Find.TickManager.TicksGame;
            dueReminders = activeReminders
                .Where(r => !r.dismissed && !r.completed && r.triggerTick <= currentTick)
                .ToList();
            
            return dueReminders.Count > 0;
        }
    }
} 