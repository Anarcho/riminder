using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Riminder
{
    public class Alert_Reminder : Alert
    {
        private List<BaseReminder> dueReminders = new List<BaseReminder>();

        public Alert_Reminder()
        {
            defaultLabel = "Reminder Due";
            defaultPriority = AlertPriority.Medium;
        }

        public override string GetLabel()
        {
            if (dueReminders.Count == 1)
            {
                return "Reminder Due: " + dueReminders[0].GetLabel();
            }
            return "Multiple Reminders Due";
        }

        public override TaggedString GetExplanation()
        {
            if (dueReminders.Count == 0) return "";
            
            if (dueReminders.Count == 1)
            {
                BaseReminder reminder = dueReminders[0];
                return $"Reminder: {reminder.GetLabel()}\n\n{reminder.GetDescription()}";
            }
            
            string result = "Multiple reminders are due:\n";
            foreach (BaseReminder reminder in dueReminders)
            {
                result += $"\n- {reminder.GetLabel()}";
            }
            return result;
        }

        public override AlertReport GetReport()
        {
            if (!ModsConfig.IsActive("User.Riminder")) return false;
            
            dueReminders.Clear();
            List<BaseReminder> activeReminders = RiminderManager.GetActiveReminders();
            
            int currentTick = Find.TickManager.TicksGame;
            dueReminders = activeReminders
                .Where(r => !r.dismissed && !r.completed && r.triggerTick <= currentTick)
                .ToList();
            
            return dueReminders.Count > 0;
        }
    }
}