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
        private int days = 0;
        private int hours = 0;
        private ReminderFrequency selectedFrequency = ReminderFrequency.OneTime;
        
        private const float LeftMargin = 20f;
        private const float ControlHeight = 30f;
        private const float SmallButtonSize = 25f;
        private const float ButtonGap = 5f;
        
        public Dialog_CreateReminder()
        {
            forcePause = false;
            doCloseX = true;
            doCloseButton = false;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = false;
            preventCameraMotion = false;
        }

        public override Vector2 InitialSize => new Vector2(500f, 600f);

        public override void DoWindowContents(Rect inRect)
        {
            bool originalWordWrap = Text.WordWrap;
            
            try
            {
                float contentWidth = inRect.width - 40f;
                float currentY = 10f;
                
                Text.Font = GameFont.Medium;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(new Rect(0, currentY, inRect.width, ControlHeight), "Create Reminder");
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
                
                float radioSpacing = contentWidth / 3;
                
                float row1Y = currentY;
                
                Rect oneTimeRect = new Rect(LeftMargin, row1Y, 80f, ControlHeight);
                Widgets.Label(oneTimeRect, "One Time");
                Rect oneTimeRadioRect = new Rect(oneTimeRect.xMax + 5f, row1Y, 24f, 24f);
                bool oneTimeSelected = selectedFrequency == ReminderFrequency.OneTime;
                Widgets.RadioButton(oneTimeRadioRect.position, oneTimeSelected);
                if (Widgets.ButtonInvisible(new Rect(LeftMargin, row1Y, oneTimeRadioRect.xMax - LeftMargin + 24f, ControlHeight), true) || 
                    Mouse.IsOver(oneTimeRadioRect) && Input.GetMouseButtonDown(0))
                {
                    selectedFrequency = ReminderFrequency.OneTime;
                }
                
                Rect daysRect = new Rect(LeftMargin + radioSpacing, row1Y, 80f, ControlHeight);
                Widgets.Label(daysRect, "Days");
                Rect daysRadioRect = new Rect(daysRect.xMax + 5f, row1Y, 24f, 24f);
                bool daysSelected = selectedFrequency == ReminderFrequency.Days;
                Widgets.RadioButton(daysRadioRect.position, daysSelected);
                if (Widgets.ButtonInvisible(new Rect(LeftMargin + radioSpacing, row1Y, daysRadioRect.xMax - (LeftMargin + radioSpacing) + 24f, ControlHeight), true) || 
                    Mouse.IsOver(daysRadioRect) && Input.GetMouseButtonDown(0))
                {
                    selectedFrequency = ReminderFrequency.Days;
                }
                
                Rect quadrumsRect = new Rect(LeftMargin + 2 * radioSpacing, row1Y, 80f, ControlHeight);
                Widgets.Label(quadrumsRect, "Quadrums");
                Rect quadrumsRadioRect = new Rect(quadrumsRect.xMax + 5f, row1Y, 24f, 24f);
                bool quadrumsSelected = selectedFrequency == ReminderFrequency.Quadrums;
                Widgets.RadioButton(quadrumsRadioRect.position, quadrumsSelected);

                if (Widgets.ButtonInvisible(new Rect(LeftMargin + 2 * radioSpacing, row1Y, quadrumsRadioRect.xMax - (LeftMargin + 2 * radioSpacing) + 24f, ControlHeight), true) || 
                    Mouse.IsOver(quadrumsRadioRect) && Input.GetMouseButtonDown(0))
                {
                    selectedFrequency = ReminderFrequency.Quadrums;
                }
                
                currentY += ControlHeight + 10f;
                
                float row2Y = currentY;
                
                Rect yearsRect = new Rect(LeftMargin, row2Y, 80f, ControlHeight);
                Widgets.Label(yearsRect, "Years");
                Rect yearsRadioRect = new Rect(yearsRect.xMax + 5f, row2Y, 24f, 24f);
                bool yearsSelected = selectedFrequency == ReminderFrequency.Years;
                Widgets.RadioButton(yearsRadioRect.position, yearsSelected);
                
                if (Widgets.ButtonInvisible(new Rect(LeftMargin, row2Y, yearsRadioRect.xMax - LeftMargin + 24f, ControlHeight), true) || 
                    Mouse.IsOver(yearsRadioRect) && Input.GetMouseButtonDown(0))
                {
                    selectedFrequency = ReminderFrequency.Years;
                }
                
                Rect repeatedRect = new Rect(LeftMargin + radioSpacing, row2Y, 80f, ControlHeight);
                Widgets.Label(repeatedRect, "Repeated");
                Rect repeatedRadioRect = new Rect(repeatedRect.xMax + 5f, row2Y, 24f, 24f);
                bool repeatedSelected = selectedFrequency == ReminderFrequency.Custom;
                Widgets.RadioButton(repeatedRadioRect.position, repeatedSelected);
                if (Widgets.ButtonInvisible(new Rect(LeftMargin + radioSpacing, row2Y, repeatedRadioRect.xMax - (LeftMargin + radioSpacing) + 24f, ControlHeight), true) || 
                    Mouse.IsOver(repeatedRadioRect) && Input.GetMouseButtonDown(0))
                {
                    selectedFrequency = ReminderFrequency.Custom;
                }
                
                currentY += ControlHeight + 25f;
                
                if (selectedFrequency == ReminderFrequency.OneTime)
                {
                    Text.Font = GameFont.Medium;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(new Rect(LeftMargin, currentY, contentWidth, ControlHeight), "Trigger Time");
                    Text.Anchor = TextAnchor.UpperLeft;
                    Text.Font = GameFont.Small;
                    currentY += ControlHeight + 15f;
                }
                else if (selectedFrequency == ReminderFrequency.Custom)
                {
                    Text.Font = GameFont.Medium;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(new Rect(LeftMargin, currentY, contentWidth, ControlHeight), "Repeat Interval");
                    Text.Anchor = TextAnchor.UpperLeft;
                    Text.Font = GameFont.Small;
                    currentY += ControlHeight + 15f;
                }
                else
                {
                    Text.Font = GameFont.Medium;
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(new Rect(LeftMargin, currentY, contentWidth, ControlHeight), "Initial Offset");
                    Text.Anchor = TextAnchor.UpperLeft;
                    Text.Font = GameFont.Small;
                    currentY += ControlHeight + 15f;
                }
                
                Rect daysLabelRect = new Rect(LeftMargin, currentY + 2f, 60f, ControlHeight);
                
                if (selectedFrequency == ReminderFrequency.Custom)
                {
                    Widgets.Label(daysLabelRect, "Every:");
                }
                else
                {
                    Widgets.Label(daysLabelRect, "Days:");
                }
                
                if (Widgets.ButtonText(new Rect(LeftMargin + 70f, currentY, SmallButtonSize, ControlHeight), "-"))
                {
                    days = Math.Max(0, days - 1);
                }
                
                Rect daysValueRect = new Rect(LeftMargin + 70f + SmallButtonSize + 8f, currentY + 2f, 30f, ControlHeight);
                Widgets.Label(daysValueRect, days.ToString());
                
                if (Widgets.ButtonText(new Rect(daysValueRect.xMax + 8f, currentY, SmallButtonSize, ControlHeight), "+"))
                {
                    days += 1;
                }
                
                Rect hoursLabelRect = new Rect(LeftMargin + contentWidth/2, currentY + 2f, 60f, ControlHeight);
                Widgets.Label(hoursLabelRect, "Hours:");
                
                if (Widgets.ButtonText(new Rect(hoursLabelRect.xMax + 10f, currentY, SmallButtonSize, ControlHeight), "-"))
                {
                    hours = Math.Max(0, hours - 1);
                }
                
                Rect hoursValueRect = new Rect(hoursLabelRect.xMax + 10f + SmallButtonSize + 8f, currentY + 2f, 30f, ControlHeight);
                Widgets.Label(hoursValueRect, hours.ToString());
                
                if (Widgets.ButtonText(new Rect(hoursValueRect.xMax + 8f, currentY, SmallButtonSize, ControlHeight), "+"))
                {
                    hours = (hours + 1) % 24;
                }
                
                currentY += ControlHeight + 20f;
                
                float bottomY = inRect.height - 45f;
                float buttonWidth = 120f;
                float buttonHeight = 40f;
                
                if (Widgets.ButtonText(new Rect(inRect.width / 2 - buttonWidth - 10f, bottomY, buttonWidth, buttonHeight), "Cancel"))
                {
                    Close();
                }
                
                if (Widgets.ButtonText(new Rect(inRect.width / 2 + 10f, bottomY, buttonWidth, buttonHeight), "Create"))
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

            if (selectedFrequency == ReminderFrequency.Custom && days == 0 && hours == 0)
            {
                Messages.Message("Custom frequency requires at least some time interval", MessageTypeDefOf.RejectInput, false);
                return;
            }

            int triggerTick = Find.TickManager.TicksGame;
            
            if (days == 0 && hours == 0 && selectedFrequency != ReminderFrequency.Custom)
            {
                switch (selectedFrequency)
                {
                    case ReminderFrequency.OneTime:
                        triggerTick += GenDate.TicksPerHour;
                        break;
                    case ReminderFrequency.Days:
                        triggerTick += GenDate.TicksPerDay;
                        break;
                    case ReminderFrequency.Quadrums:
                        triggerTick += GenDate.TicksPerQuadrum;
                        break;
                    case ReminderFrequency.Years:
                        triggerTick += GenDate.TicksPerYear;
                        break;
                }
            }
            else
            {
                triggerTick += days * GenDate.TicksPerDay;
                triggerTick += hours * GenDate.TicksPerHour;
            }

            int minOffset = GenDate.TicksPerHour; 
            if (triggerTick - Find.TickManager.TicksGame < minOffset)
            {
                triggerTick = Find.TickManager.TicksGame + minOffset;
            }

            var reminder = new Reminder(label, description, triggerTick, selectedFrequency);
            RiminderManager.AddReminder(reminder);

            Messages.Message("Reminder created: " + label, MessageTypeDefOf.TaskCompletion, false);

            Close();
        }
    }
}