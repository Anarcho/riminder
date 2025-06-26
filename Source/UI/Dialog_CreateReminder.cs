using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;

namespace Riminder
{
    public class Dialog_CreateReminder : Window
    {
        private string label = "";
        private string description = "";
        private ReminderFrequency selectedFrequency = ReminderFrequency.OneTime;
        
        
        private int recurrenceHours = 0;
        private int recurrenceDays = 0;
        private int recurrenceQuadrums = 0;
        private int recurrenceYears = 0;
        
        private int oneTimeHours = 0;
        private int oneTimeDays = 0;
        private int oneTimeQuadrums = 0;
        private int oneTimeYears = 0;
        
        
        private int offsetHours = 0;
        private int offsetDays = 0;
        
        
        private string hoursBuffer = "0";
        private string daysBuffer = "0";
        private string quadrumsBuffer = "0";
        private string yearsBuffer = "0";
        
        private string offsetHoursBuffer = "0";
        private string offsetDaysBuffer = "0";
        
        public Dialog_CreateReminder()
        {
            forcePause = false;
            doCloseX = true;
            doCloseButton = false;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = false;
            preventCameraMotion = false;
        }

        public override Vector2 InitialSize => new Vector2(600f, 650f);

        public override void DoWindowContents(Rect inRect)
        {
            bool originalWordWrap = Text.WordWrap;
            Text.WordWrap = true;
            
            try
            {
                float contentWidth = inRect.width - (RiminderUIHelper.LeftMargin * 2);
                float currentY = 10f;
                
                
                RiminderUIHelper.DrawSectionHeader(0, currentY, inRect.width, "Create Reminder");
                currentY += RiminderUIHelper.RowHeight + RiminderUIHelper.SectionSpacing;
                
                
                RiminderUIHelper.DrawLabeledTextField(RiminderUIHelper.LeftMargin, currentY, contentWidth, "Title:", ref label, labelWidthOverride: 140f, inputWidthOverride: contentWidth - 140f + 40f, inputXOffset: -70f);
                currentY += RiminderUIHelper.RowHeight + RiminderUIHelper.SectionSpacing;
                
                
                RiminderUIHelper.DrawLabeledTextArea(RiminderUIHelper.LeftMargin, currentY, contentWidth, "Desc:", ref description, 60f, labelWidthOverride: 140f, inputWidthOverride: contentWidth - 140f + 40f, inputXOffset: -70f);
                currentY += 60f + RiminderUIHelper.SectionSpacing;
                
                
                RiminderUIHelper.DrawSectionHeader(RiminderUIHelper.LeftMargin, currentY, contentWidth, "Frequency");
                currentY += RiminderUIHelper.RowHeight + RiminderUIHelper.SectionSpacing;
                
                RiminderUIHelper.DrawFrequencySelector(RiminderUIHelper.LeftMargin, currentY, contentWidth, ref selectedFrequency);
                currentY += RiminderUIHelper.RowHeight + RiminderUIHelper.SectionSpacing;
                
                
                string timeHeader = selectedFrequency == ReminderFrequency.OneTime ? "Trigger Time" : "Repeat Every";
                RiminderUIHelper.DrawSectionHeader(RiminderUIHelper.LeftMargin, currentY, contentWidth, timeHeader);
                currentY += RiminderUIHelper.RowHeight + RiminderUIHelper.SectionSpacing;
                
                DrawTimeControls(currentY, contentWidth);
                currentY += (RiminderUIHelper.RowHeight + RiminderUIHelper.Gap) * 2 + RiminderUIHelper.SectionSpacing;
                
                
                RiminderUIHelper.DrawSectionHeader(RiminderUIHelper.LeftMargin, currentY, contentWidth, "Starting Offset");
                currentY += RiminderUIHelper.RowHeight + RiminderUIHelper.SectionSpacing;
                
                DrawOffsetControls(currentY, contentWidth);
                currentY += RiminderUIHelper.RowHeight + RiminderUIHelper.SectionSpacing;
                
                
                string summaryText = selectedFrequency == ReminderFrequency.OneTime
                    ? "Triggers in " + FormatOneTimeInterval()
                    : "Repeats every " + FormatRecurrenceInterval();
                    
                Rect summaryRect = new Rect(RiminderUIHelper.LeftMargin, currentY, contentWidth, RiminderUIHelper.RowHeight);
                Widgets.DrawBoxSolid(summaryRect, new Color(0.2f, 0.2f, 0.2f, 0.3f));
                
                var prevAnchor = Text.Anchor;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = selectedFrequency == ReminderFrequency.OneTime ? Color.yellow : Color.green;
                Widgets.Label(summaryRect, summaryText);
                GUI.color = Color.white;
                Text.Anchor = prevAnchor;
                
                
                RiminderUIHelper.DrawBottomButtons(inRect, "Cancel", "Create", out bool cancelClicked, out bool createClicked);
                
                if (cancelClicked)
                {
                    Close();
                }
                
                if (createClicked)
                {
                    TryCreateReminder();
                }
            }
            finally
            {
                Text.WordWrap = originalWordWrap;
                Text.Anchor = TextAnchor.UpperLeft;
            }
        }

        private void DrawTimeControls(float yPos, float contentWidth)
        {
            bool isOneTime = selectedFrequency == ReminderFrequency.OneTime;
            float currentY = yPos;
            
            
            float col1X = RiminderUIHelper.LeftMargin;
            float col2X = col1X + RiminderUIHelper.ControlGroupWidth + RiminderUIHelper.Gap * 4;
            
            
            RiminderUIHelper.DrawLabeledIntRow(
                col1X, currentY,
                "Hours:",
                ref (isOneTime ? ref oneTimeHours : ref recurrenceHours),
                ref hoursBuffer,
                0, 23);
                
            RiminderUIHelper.DrawLabeledIntRow(
                col2X, currentY,
                "Days:",
                ref (isOneTime ? ref oneTimeDays : ref recurrenceDays),
                ref daysBuffer);
                
            currentY += RiminderUIHelper.RowHeight + RiminderUIHelper.Gap;
            
            
            RiminderUIHelper.DrawLabeledIntRow(
                col1X, currentY,
                "Quadrums:",
                ref (isOneTime ? ref oneTimeQuadrums : ref recurrenceQuadrums),
                ref quadrumsBuffer);
                
            RiminderUIHelper.DrawLabeledIntRow(
                col2X, currentY,
                "Years:",
                ref (isOneTime ? ref oneTimeYears : ref recurrenceYears),
                ref yearsBuffer);
        }
        
        private void DrawOffsetControls(float yPos, float contentWidth)
        {
            float col1X = RiminderUIHelper.LeftMargin;
            float col2X = col1X + RiminderUIHelper.ControlGroupWidth + RiminderUIHelper.Gap * 4;
            
            RiminderUIHelper.DrawLabeledIntRow(
                col1X, yPos,
                "Offset Hours:",
                ref offsetHours,
                ref offsetHoursBuffer,
                0, 23);
                
            RiminderUIHelper.DrawLabeledIntRow(
                col2X, yPos,
                "Offset Days:",
                ref offsetDays,
                ref offsetDaysBuffer);
        }

        private string FormatRecurrenceInterval()
        {
            if (recurrenceYears == 0 && recurrenceQuadrums == 0 && recurrenceDays == 0 && recurrenceHours == 0)
            {
                return "1 day"; 
            }
            
            List<string> parts = new List<string>();
            
            if (recurrenceHours > 0)
            {
                parts.Add(recurrenceHours == 1 ? "1 hour" : $"{recurrenceHours} hours");
            }
            
            if (recurrenceDays > 0)
            {
                parts.Add(recurrenceDays == 1 ? "1 day" : $"{recurrenceDays} days");
            }
            
            if (recurrenceQuadrums > 0)
            {
                parts.Add(recurrenceQuadrums == 1 ? "1 quadrum" : $"{recurrenceQuadrums} quadrums");
            }
            
            if (recurrenceYears > 0)
            {
                parts.Add(recurrenceYears == 1 ? "1 year" : $"{recurrenceYears} years");
            }
            
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

        private string FormatOneTimeInterval()
        {
            if (oneTimeYears == 0 && oneTimeQuadrums == 0 && oneTimeDays == 0 && oneTimeHours == 0)
            {
                return "1 hour"; 
            }
            
            List<string> parts = new List<string>();
            
            if (oneTimeHours > 0)
            {
                parts.Add(oneTimeHours == 1 ? "1 hour" : $"{oneTimeHours} hours");
            }
            
            if (oneTimeDays > 0)
            {
                parts.Add(oneTimeDays == 1 ? "1 day" : $"{oneTimeDays} days");
            }
            
            if (oneTimeQuadrums > 0)
            {
                parts.Add(oneTimeQuadrums == 1 ? "1 quadrum" : $"{oneTimeQuadrums} quadrums");
            }
            
            if (oneTimeYears > 0)
            {
                parts.Add(oneTimeYears == 1 ? "1 year" : $"{oneTimeYears} years");
            }
            
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

        private void TryCreateReminder()
        {
            if (string.IsNullOrEmpty(label))
            {
                Messages.Message("Title cannot be empty", MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (label.Length > 50)
            {
                Messages.Message("Title too long (max 50 characters)", MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (description.Length > 200)
            {
                Messages.Message("Description too long (max 200 characters)", MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (selectedFrequency == ReminderFrequency.Custom &&
                recurrenceYears == 0 && recurrenceQuadrums == 0 && recurrenceDays == 0 && recurrenceHours == 0)
            {
                Messages.Message("Please specify at least one time unit for the recurrence interval", MessageTypeDefOf.RejectInput, false);
                return;
            }

            int triggerTick = Find.TickManager.TicksGame;
            
            
            int offsetTicks = (offsetHours * GenDate.TicksPerHour) + (offsetDays * GenDate.TicksPerDay);
            triggerTick += offsetTicks;
            
            int customIntervalInTicks = 0;
            if (selectedFrequency == ReminderFrequency.Custom)
            {
                customIntervalInTicks = GetTotalRecurrenceIntervalInTicks();
                triggerTick += customIntervalInTicks;
            }
            else 
            {
                int oneTimeIntervalInTicks = GetTotalOneTimeIntervalInTicks();
                triggerTick += oneTimeIntervalInTicks;
            }

            int minOffset = GenDate.TicksPerHour; 
            if (triggerTick - Find.TickManager.TicksGame < minOffset)
            {
                triggerTick = Find.TickManager.TicksGame + minOffset;
            }

            var reminder = new Reminder(label, description, triggerTick, selectedFrequency);
            
            if (selectedFrequency == ReminderFrequency.Custom)
            {
                reminder.recurrenceInterval = customIntervalInTicks;
                
                if (reminder.metadata == null)
                    reminder.metadata = new Dictionary<string, string>();
                    
                reminder.metadata["recurrenceDescription"] = FormatRecurrenceInterval();
            }
            
            RiminderManager.AddReminder(reminder);

            Messages.Message("Reminder created: " + label, MessageTypeDefOf.TaskCompletion, false);

            Close();
        }

        private int GetTotalRecurrenceIntervalInTicks()
        {
            int totalTicks = 0;
            
            totalTicks += recurrenceYears * GenDate.TicksPerYear;
            totalTicks += recurrenceQuadrums * GenDate.TicksPerQuadrum;
            totalTicks += recurrenceDays * GenDate.TicksPerDay;
            totalTicks += recurrenceHours * GenDate.TicksPerHour;
            
            
            if (totalTicks < GenDate.TicksPerHour)
            {
                totalTicks = GenDate.TicksPerHour;
            }
            
            return totalTicks;
        }

        private int GetTotalOneTimeIntervalInTicks()
        {
            int totalTicks = 0;
            
            totalTicks += oneTimeYears * GenDate.TicksPerYear;
            totalTicks += oneTimeQuadrums * GenDate.TicksPerQuadrum;
            totalTicks += oneTimeDays * GenDate.TicksPerDay;
            totalTicks += oneTimeHours * GenDate.TicksPerHour;
            
            
            if (totalTicks < GenDate.TicksPerHour)
            {
                totalTicks = GenDate.TicksPerHour;
            }
            
            return totalTicks;
        }
    }
}