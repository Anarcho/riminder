using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;

namespace Riminder
{
    public class Dialog_EditReminder : Window
    {
        private string label;
        private string description;
        private int days = 0;
        private int hours = 0;
        private ReminderFrequency selectedFrequency;
        
        // Complex recurrence values
        private int recurrenceHours = 0;
        private int recurrenceDays = 0;
        private int recurrenceQuadrums = 0;
        private int recurrenceYears = 0;
        
        // Use the same controls for one-time reminders
        private int oneTimeHours = 0;
        private int oneTimeDays = 0;
        private int oneTimeQuadrums = 0;
        private int oneTimeYears = 0;

        private BaseReminder originalReminder;

        private const float LeftMargin = 20f;
        private const float ControlHeight = 30f;
        private const float SmallButtonSize = 25f;
        private const float ButtonGap = 5f;

        public Dialog_EditReminder(BaseReminder reminder)
        {
            this.originalReminder = reminder;
            this.label = reminder.GetLabel();
            this.description = reminder.GetDescription();
            this.selectedFrequency = reminder.frequency;

            forcePause = false;
            doCloseX = true;
            doCloseButton = false;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = false;
            preventCameraMotion = false;

            // Calculate days and hours from tick difference if it's a one-time reminder
            if (reminder.frequency != ReminderFrequency.Custom)
            {
                int ticksRemaining = reminder.triggerTick - Find.TickManager.TicksGame;
                if (ticksRemaining > 0)
                {
                    // Parse the time into our multi-unit controls
                    ParseOneTimeFromTicks(ticksRemaining);
                }
            }
            else
            {
                // For custom reminders, extract interval from recurrenceInterval
                int intervalInTicks = reminder.recurrenceInterval;
                
                // If recurrenceInterval is 0, try using createdTick for backward compatibility
                if (intervalInTicks <= 0 && reminder.frequency == ReminderFrequency.Custom)
                {
                    intervalInTicks = reminder.createdTick;
                }
                
                // Check if we have a stored description to parse
                if (reminder is Reminder reminderObj && 
                    reminderObj.metadata != null && 
                    reminderObj.metadata.TryGetValue("recurrenceDescription", out string description))
                {
                    // Parse description to set values
                    ParseRecurrenceDescription(description);
                }
                else
                {
                    // Otherwise calculate from ticks
                    ParseRecurrenceFromTicks(intervalInTicks);
                }
            }
        }
        
        private void ParseRecurrenceDescription(string description)
        {
            // Reset all values
            recurrenceYears = 0;
            recurrenceQuadrums = 0;
            recurrenceDays = 0;
            recurrenceHours = 0;
            
            // Parse the description (e.g., "1 year, 2 quadrums, and 3 days")
            if (description.Contains("year"))
            {
                int index = description.IndexOf("year");
                int startIndex = index;
                while (startIndex > 0 && (char.IsDigit(description[startIndex - 1]) || char.IsWhiteSpace(description[startIndex - 1])))
                {
                    startIndex--;
                }
                if (int.TryParse(description.Substring(startIndex, index - startIndex).Trim(), out int years))
                {
                    recurrenceYears = years;
                }
            }
            
            if (description.Contains("quadrum"))
            {
                int index = description.IndexOf("quadrum");
                int startIndex = index;
                while (startIndex > 0 && (char.IsDigit(description[startIndex - 1]) || char.IsWhiteSpace(description[startIndex - 1])))
                {
                    startIndex--;
                }
                if (int.TryParse(description.Substring(startIndex, index - startIndex).Trim(), out int quadrums))
                {
                    recurrenceQuadrums = quadrums;
                }
            }
            
            if (description.Contains("day"))
            {
                int index = description.IndexOf("day");
                int startIndex = index;
                while (startIndex > 0 && (char.IsDigit(description[startIndex - 1]) || char.IsWhiteSpace(description[startIndex - 1])))
                {
                    startIndex--;
                }
                if (int.TryParse(description.Substring(startIndex, index - startIndex).Trim(), out int days))
                {
                    recurrenceDays = days;
                }
            }
            
            if (description.Contains("hour"))
            {
                int index = description.IndexOf("hour");
                int startIndex = index;
                while (startIndex > 0 && (char.IsDigit(description[startIndex - 1]) || char.IsWhiteSpace(description[startIndex - 1])))
                {
                    startIndex--;
                }
                if (int.TryParse(description.Substring(startIndex, index - startIndex).Trim(), out int hours))
                {
                    recurrenceHours = hours;
                }
            }
        }
        
        private void ParseRecurrenceFromTicks(int intervalInTicks)
        {
            // Extract years
            recurrenceYears = intervalInTicks / GenDate.TicksPerYear;
            intervalInTicks -= recurrenceYears * GenDate.TicksPerYear;
            
            // Extract quadrums
            recurrenceQuadrums = intervalInTicks / GenDate.TicksPerQuadrum;
            intervalInTicks -= recurrenceQuadrums * GenDate.TicksPerQuadrum;
            
            // Extract days
            recurrenceDays = intervalInTicks / GenDate.TicksPerDay;
            intervalInTicks -= recurrenceDays * GenDate.TicksPerDay;
            
            // Extract hours
            recurrenceHours = intervalInTicks / GenDate.TicksPerHour;
        }

        private void ParseOneTimeFromTicks(int remainingTicks)
        {
            // Extract years
            oneTimeYears = remainingTicks / GenDate.TicksPerYear;
            remainingTicks -= oneTimeYears * GenDate.TicksPerYear;
            
            // Extract quadrums
            oneTimeQuadrums = remainingTicks / GenDate.TicksPerQuadrum;
            remainingTicks -= oneTimeQuadrums * GenDate.TicksPerQuadrum;
            
            // Extract days
            oneTimeDays = remainingTicks / GenDate.TicksPerDay;
            remainingTicks -= oneTimeDays * GenDate.TicksPerDay;
            
            // Extract hours
            oneTimeHours = remainingTicks / GenDate.TicksPerHour;
            
            // Also set the regular days/hours for backward compatibility
            days = remainingTicks / GenDate.TicksPerDay;
            hours = (remainingTicks % GenDate.TicksPerDay) / GenDate.TicksPerHour;
        }

        public override Vector2 InitialSize => new Vector2(500f, 700f);

        public override void DoWindowContents(Rect inRect)
        {
            bool originalWordWrap = Text.WordWrap;

            try
            {
                float contentWidth = inRect.width - 40f;
                float currentY = 10f;
                
                Text.Font = GameFont.Medium;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(new Rect(0, currentY, inRect.width, ControlHeight), "Edit Reminder");
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
                currentY += ControlHeight + 15f;
                
                Rect titleLabelRect = new Rect(LeftMargin, currentY, 60f, ControlHeight);
                Widgets.Label(titleLabelRect, "Title:");
                
                Rect titleRect = new Rect(LeftMargin + 60f, currentY, contentWidth - 60f, ControlHeight);
                label = Widgets.TextField(titleRect, label);
                currentY += ControlHeight + 15f;
                
                Rect descLabelRect = new Rect(LeftMargin, currentY, 60f, ControlHeight);
                Widgets.Label(descLabelRect, "Desc:");
                
                Rect descRect = new Rect(LeftMargin + 60f, currentY, contentWidth - 60f, 60f);
                description = Widgets.TextArea(descRect, description);
                currentY += 60f + 15f;
                
                Text.Font = GameFont.Medium;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(new Rect(LeftMargin, currentY, contentWidth, ControlHeight), "Frequency");
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
                currentY += ControlHeight + 15f;
                
                // Simplified frequency options
                float row1Y = currentY;
                
                // One Time Option (Left)
                Rect oneTimeRect = new Rect(inRect.width / 4 - 80f, row1Y, 80f, ControlHeight);
                Widgets.Label(oneTimeRect, "One Time");
                Rect oneTimeRadioRect = new Rect(oneTimeRect.xMax + 5f, row1Y, 24f, 24f);
                bool oneTimeSelected = selectedFrequency == ReminderFrequency.OneTime;
                Widgets.RadioButton(oneTimeRadioRect.position, oneTimeSelected);
                if (Widgets.ButtonInvisible(new Rect(oneTimeRect.x, row1Y, oneTimeRadioRect.xMax - oneTimeRect.x, ControlHeight), true) || 
                    Mouse.IsOver(oneTimeRadioRect) && Input.GetMouseButtonDown(0))
                {
                    selectedFrequency = ReminderFrequency.OneTime;
                }
                
                // Repeated Option (Right)
                Rect repeatedRect = new Rect(inRect.width * 3/4 - 80f, row1Y, 80f, ControlHeight);
                Widgets.Label(repeatedRect, "Repeated");
                Rect repeatedRadioRect = new Rect(repeatedRect.xMax + 5f, row1Y, 24f, 24f);
                bool repeatedSelected = selectedFrequency == ReminderFrequency.Custom;
                Widgets.RadioButton(repeatedRadioRect.position, repeatedSelected);
                if (Widgets.ButtonInvisible(new Rect(repeatedRect.x, row1Y, repeatedRadioRect.xMax - repeatedRect.x, ControlHeight), true) || 
                    Mouse.IsOver(repeatedRadioRect) && Input.GetMouseButtonDown(0))
                {
                    selectedFrequency = ReminderFrequency.Custom;
                }
                
                currentY += ControlHeight + 25f;
                
                // Draw the title for time options section
                Text.Font = GameFont.Medium;
                Text.Anchor = TextAnchor.MiddleCenter;
                
                if (selectedFrequency == ReminderFrequency.OneTime)
                {
                    Widgets.Label(new Rect(LeftMargin, currentY, contentWidth, ControlHeight), "Trigger Time");
                }
                else if (selectedFrequency == ReminderFrequency.Custom)
                {
                    Widgets.Label(new Rect(LeftMargin, currentY, contentWidth, ControlHeight), "Repeat Every");
                }
                
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
                currentY += ControlHeight + 15f;
                
                if (selectedFrequency == ReminderFrequency.OneTime)
                {
                    // Use the same multi-unit controls for one-time reminders
                    DrawMultiUnitTimeControls(currentY, contentWidth, true);
                    currentY += ControlHeight * 2 + 30f;
                    
                    // Visual feedback for one-time reminder
                    Text.Font = GameFont.Small;
                    string timeString = FormatOneTimeInterval();
                    Rect intervalRect = new Rect(LeftMargin, currentY, contentWidth, ControlHeight);
                    
                    // Make this stand out visually
                    Widgets.DrawBoxSolid(intervalRect, new Color(0.2f, 0.2f, 0.2f, 0.3f));
                    Text.Anchor = TextAnchor.MiddleCenter;
                    GUI.color = Color.yellow;
                    Widgets.Label(intervalRect, "Triggers in " + timeString);
                    GUI.color = Color.white;
                    Text.Anchor = TextAnchor.UpperLeft;
                    
                    currentY += ControlHeight + 20f;
                }
                else if (selectedFrequency == ReminderFrequency.Custom)
                {
                    // Multi-unit recurrence controls
                    DrawMultiUnitTimeControls(currentY, contentWidth, false);
                    currentY += ControlHeight * 2 + 30f;
                    
                    // Visual feedback for custom frequency - moved down after all controls
                    Text.Font = GameFont.Small;
                    string intervalString = FormatRecurrenceInterval();
                    Rect intervalRect = new Rect(LeftMargin, currentY, contentWidth, ControlHeight);
                    
                    // Make this stand out visually
                    Widgets.DrawBoxSolid(intervalRect, new Color(0.2f, 0.2f, 0.2f, 0.3f));
                    Text.Anchor = TextAnchor.MiddleCenter;
                    GUI.color = Color.green;
                    Widgets.Label(intervalRect, "Repeats every " + intervalString);
                    GUI.color = Color.white;
                    Text.Anchor = TextAnchor.UpperLeft;
                    
                    currentY += ControlHeight + 20f;
                }

                float bottomY = inRect.height - 45f;
                float buttonWidth = 120f;
                float buttonHeight = 40f;

                if (Widgets.ButtonText(new Rect(inRect.width / 2 - buttonWidth - 10f, bottomY, buttonWidth, buttonHeight), "Cancel"))
                {
                    Close();
                    Find.WindowStack.Add(new Dialog_ViewReminders());
                }

                if (Widgets.ButtonText(new Rect(inRect.width / 2 + 10f, bottomY, buttonWidth, buttonHeight), "Save"))
                {
                    if (TrySaveReminder())
                    {
                        Close();
                        Find.WindowStack.Add(new Dialog_ViewReminders());
                    }
                }
            }
            finally
            {
                Text.WordWrap = originalWordWrap;
                Text.Anchor = TextAnchor.UpperLeft;
            }
        }

        private void DrawMultiUnitTimeControls(float yPos, float contentWidth, bool isOneTime)
        {
            float currentY = yPos;
            float controlWidth = contentWidth / 2 - 10f;
            
            // Years
            Rect yearsLabelRect = new Rect(LeftMargin, currentY + 2f, 80f, ControlHeight);
            Widgets.Label(yearsLabelRect, "Years:");
            
            if (Widgets.ButtonText(new Rect(LeftMargin + 90f, currentY, SmallButtonSize, ControlHeight), "-"))
            {
                if (isOneTime)
                    oneTimeYears = Math.Max(0, oneTimeYears - 1);
                else
                    recurrenceYears = Math.Max(0, recurrenceYears - 1);
            }
            
            Rect yearsValueRect = new Rect(LeftMargin + 90f + SmallButtonSize + 8f, currentY + 2f, 30f, ControlHeight);
            Widgets.Label(yearsValueRect, isOneTime ? oneTimeYears.ToString() : recurrenceYears.ToString());
            
            if (Widgets.ButtonText(new Rect(yearsValueRect.xMax + 8f, currentY, SmallButtonSize, ControlHeight), "+"))
            {
                if (isOneTime)
                    oneTimeYears += 1;
                else
                    recurrenceYears += 1;
            }
            
            // Quadrums
            Rect quadrumsLabelRect = new Rect(LeftMargin + contentWidth/2, currentY + 2f, 70f, ControlHeight);
            Widgets.Label(quadrumsLabelRect, "Quadrums:");
            
            if (Widgets.ButtonText(new Rect(quadrumsLabelRect.xMax + 10f, currentY, SmallButtonSize, ControlHeight), "-"))
            {
                if (isOneTime)
                    oneTimeQuadrums = Math.Max(0, oneTimeQuadrums - 1);
                else
                    recurrenceQuadrums = Math.Max(0, recurrenceQuadrums - 1);
            }
            
            Rect quadrumsValueRect = new Rect(quadrumsLabelRect.xMax + 10f + SmallButtonSize + 8f, currentY + 2f, 30f, ControlHeight);
            Widgets.Label(quadrumsValueRect, isOneTime ? oneTimeQuadrums.ToString() : recurrenceQuadrums.ToString());
            
            if (Widgets.ButtonText(new Rect(quadrumsValueRect.xMax + 8f, currentY, SmallButtonSize, ControlHeight), "+"))
            {
                if (isOneTime)
                    oneTimeQuadrums = (oneTimeQuadrums + 1) % 5;
                else
                    recurrenceQuadrums = (recurrenceQuadrums + 1) % 5;
            }
            
            currentY += ControlHeight + 10f;
            
            // Days
            Rect daysLabelRect = new Rect(LeftMargin, currentY + 2f, 80f, ControlHeight);
            Widgets.Label(daysLabelRect, "Days:");
            
            if (Widgets.ButtonText(new Rect(LeftMargin + 90f, currentY, SmallButtonSize, ControlHeight), "-"))
            {
                if (isOneTime)
                    oneTimeDays = Math.Max(0, oneTimeDays - 1);
                else
                    recurrenceDays = Math.Max(0, recurrenceDays - 1);
            }
            
            Rect daysValueRect = new Rect(LeftMargin + 90f + SmallButtonSize + 8f, currentY + 2f, 30f, ControlHeight);
            Widgets.Label(daysValueRect, isOneTime ? oneTimeDays.ToString() : recurrenceDays.ToString());
            
            if (Widgets.ButtonText(new Rect(daysValueRect.xMax + 8f, currentY, SmallButtonSize, ControlHeight), "+"))
            {
                if (isOneTime)
                    oneTimeDays = (oneTimeDays + 1) % 16; // Cap at 15 (one quadrum)
                else
                    recurrenceDays = (recurrenceDays + 1) % 16; // Cap at 15 (one quadrum)
            }
            
            // Hours
            Rect hoursLabelRect = new Rect(LeftMargin + contentWidth/2, currentY + 2f, 70f, ControlHeight);
            Widgets.Label(hoursLabelRect, "Hours:");
            
            if (Widgets.ButtonText(new Rect(hoursLabelRect.xMax + 10f, currentY, SmallButtonSize, ControlHeight), "-"))
            {
                if (isOneTime)
                    oneTimeHours = Math.Max(0, oneTimeHours - 1);
                else
                    recurrenceHours = Math.Max(0, recurrenceHours - 1);
            }
            
            Rect hoursValueRect = new Rect(hoursLabelRect.xMax + 10f + SmallButtonSize + 8f, currentY + 2f, 30f, ControlHeight);
            Widgets.Label(hoursValueRect, isOneTime ? oneTimeHours.ToString() : recurrenceHours.ToString());
            
            if (Widgets.ButtonText(new Rect(hoursValueRect.xMax + 8f, currentY, SmallButtonSize, ControlHeight), "+"))
            {
                if (isOneTime)
                    oneTimeHours = (oneTimeHours + 1) % 24;
                else
                    recurrenceHours = (recurrenceHours + 1) % 24;
            }
        }
        
        private string FormatRecurrenceInterval()
        {
            if (recurrenceYears == 0 && recurrenceQuadrums == 0 && recurrenceDays == 0 && recurrenceHours == 0)
            {
                return "1 day"; // Default to daily if nothing is set
            }
            
            List<string> parts = new List<string>();
            
            if (recurrenceYears > 0)
            {
                parts.Add(recurrenceYears == 1 ? "1 year" : $"{recurrenceYears} years");
            }
            
            if (recurrenceQuadrums > 0)
            {
                parts.Add(recurrenceQuadrums == 1 ? "1 quadrum" : $"{recurrenceQuadrums} quadrums");
            }
            
            if (recurrenceDays > 0)
            {
                parts.Add(recurrenceDays == 1 ? "1 day" : $"{recurrenceDays} days");
            }
            
            if (recurrenceHours > 0)
            {
                parts.Add(recurrenceHours == 1 ? "1 hour" : $"{recurrenceHours} hours");
            }
            
            // Format as "X, Y, and Z"
            if (parts.Count == 1)
            {
                return parts[0];
            }
            else if (parts.Count == 2)
            {
                return parts[0] + " and " + parts[1];
            }
            else
            {
                string result = "";
                for (int i = 0; i < parts.Count - 1; i++)
                {
                    result += parts[i] + ", ";
                }
                return result + "and " + parts[parts.Count - 1];
            }
        }
        
        private int GetTotalRecurrenceIntervalInTicks()
        {
            int totalTicks = 0;
            
            totalTicks += recurrenceYears * GenDate.TicksPerYear;
            totalTicks += recurrenceQuadrums * GenDate.TicksPerQuadrum;
            totalTicks += recurrenceDays * GenDate.TicksPerDay;
            totalTicks += recurrenceHours * GenDate.TicksPerHour;
            
            // Ensure a minimum interval of 1 hour
            if (totalTicks < GenDate.TicksPerHour)
            {
                totalTicks = GenDate.TicksPerHour;
            }
            
            return totalTicks;
        }

        private bool TrySaveReminder()
        {
            if (string.IsNullOrEmpty(label))
            {
                Messages.Message("Title cannot be empty", MessageTypeDefOf.RejectInput, false);
                return false;
            }

            if (label.Length > 50)
            {
                Messages.Message("Title too long (max 50 characters)", MessageTypeDefOf.RejectInput, false);
                return false;
            }

            if (description.Length > 200)
            {
                Messages.Message("Description too long (max 200 characters)", MessageTypeDefOf.RejectInput, false);
                return false;
            }

            if (selectedFrequency == ReminderFrequency.Custom &&
                recurrenceYears == 0 && recurrenceQuadrums == 0 && recurrenceDays == 0 && recurrenceHours == 0)
            {
                Messages.Message("Please specify at least one time unit for the recurrence interval", MessageTypeDefOf.RejectInput, false);
                return false;
            }

            if (selectedFrequency == ReminderFrequency.OneTime &&
                oneTimeYears == 0 && oneTimeQuadrums == 0 && oneTimeDays == 0 && oneTimeHours == 0)
            {
                Messages.Message("Please specify at least one time unit for the trigger time", MessageTypeDefOf.RejectInput, false);
                return false;
            }

            int triggerTick = Find.TickManager.TicksGame;

            if (selectedFrequency == ReminderFrequency.Custom)
            {
                // For custom frequency, we use the interval directly for the initial trigger
                int customIntervalInTicks = GetTotalRecurrenceIntervalInTicks();
                triggerTick += customIntervalInTicks;
                
                // Store the custom interval in the recurrenceInterval field
                originalReminder.recurrenceInterval = customIntervalInTicks;
                
                // Also store the formatted description in the reminder
                if (originalReminder is Reminder reminderObj)
                {
                    if (reminderObj.metadata == null)
                        reminderObj.metadata = new Dictionary<string, string>();
                        
                    reminderObj.metadata["recurrenceDescription"] = FormatRecurrenceInterval();
                }
            }
            else // One Time
            {
                // Use the multi-unit time control values
                int oneTimeIntervalInTicks = GetTotalOneTimeIntervalInTicks();
                
                // For one-time reminders, just add the interval to the current time
                triggerTick += oneTimeIntervalInTicks;
            }

            int minOffset = GenDate.TicksPerHour;
            if (triggerTick - Find.TickManager.TicksGame < minOffset)
            {
                triggerTick = Find.TickManager.TicksGame + minOffset;
            }

            if (originalReminder is Reminder reminder)
            {
                reminder.SetLabel(label);
                reminder.SetDescription(description);
            }
            originalReminder.triggerTick = triggerTick;
            originalReminder.frequency = selectedFrequency;

            Messages.Message("Reminder updated: " + label, MessageTypeDefOf.TaskCompletion, false);

            return true;
        }

        private int GetTotalOneTimeIntervalInTicks()
        {
            int totalTicks = 0;
            
            totalTicks += oneTimeYears * GenDate.TicksPerYear;
            totalTicks += oneTimeQuadrums * GenDate.TicksPerQuadrum;
            totalTicks += oneTimeDays * GenDate.TicksPerDay;
            totalTicks += oneTimeHours * GenDate.TicksPerHour;
            
            // Ensure a minimum interval of 1 hour
            if (totalTicks < GenDate.TicksPerHour)
            {
                totalTicks = GenDate.TicksPerHour;
            }
            
            return totalTicks;
        }

        private string FormatOneTimeInterval()
        {
            if (oneTimeYears == 0 && oneTimeQuadrums == 0 && oneTimeDays == 0 && oneTimeHours == 0)
            {
                return "1 hour"; // Default to 1 hour if nothing is set
            }
            
            List<string> parts = new List<string>();
            
            if (oneTimeYears > 0)
            {
                parts.Add(oneTimeYears == 1 ? "1 year" : $"{oneTimeYears} years");
            }
            
            if (oneTimeQuadrums > 0)
            {
                parts.Add(oneTimeQuadrums == 1 ? "1 quadrum" : $"{oneTimeQuadrums} quadrums");
            }
            
            if (oneTimeDays > 0)
            {
                parts.Add(oneTimeDays == 1 ? "1 day" : $"{oneTimeDays} days");
            }
            
            if (oneTimeHours > 0)
            {
                parts.Add(oneTimeHours == 1 ? "1 hour" : $"{oneTimeHours} hours");
            }
            
            // Format as "X, Y, and Z"
            if (parts.Count == 1)
            {
                return parts[0];
            }
            else if (parts.Count == 2)
            {
                return parts[0] + " and " + parts[1];
            }
            else
            {
                string result = "";
                for (int i = 0; i < parts.Count - 1; i++)
                {
                    result += parts[i] + ", ";
                }
                return result + "and " + parts[parts.Count - 1];
            }
        }
    }
}
