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
        private Vector2 scrollPosition = Vector2.zero;
        
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

        public override Vector2 InitialSize => new Vector2(450f, 750f);

        public override void DoWindowContents(Rect inRect)
        {
            bool originalWordWrap = Text.WordWrap;
            
            try
            {
                float contentWidth = inRect.width - 40f; 
                
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(LeftMargin, 10f, contentWidth, ControlHeight), "Create Reminder");
                Text.Font = GameFont.Small;
                
                float currentY = 50f;
                
                Widgets.Label(new Rect(LeftMargin, currentY, 100f, ControlHeight), "Title:");
                currentY += ControlHeight;
                Rect titleRect = new Rect(LeftMargin, currentY, contentWidth, ControlHeight);
                label = Widgets.TextField(titleRect, label);
                currentY += ControlHeight + 15f; 
                
                Widgets.Label(new Rect(LeftMargin, currentY, 100f, ControlHeight), "Description:");
                currentY += ControlHeight;
                Rect descRect = new Rect(LeftMargin, currentY, contentWidth, 80f);
                description = Widgets.TextArea(descRect, description);
                currentY += 80f + 15f; 
                
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(LeftMargin, currentY, contentWidth, ControlHeight), "Trigger after:");
                Text.Font = GameFont.Small;
                currentY += ControlHeight + 5f;
                
                float labelWidth = 70f;
                float valueWidth = 30f;
                
                Rect daysLabelRect = new Rect(LeftMargin, currentY, labelWidth, ControlHeight);
                Widgets.Label(daysLabelRect, "Days:");
                
                Rect daysValueRect = new Rect(LeftMargin + labelWidth + 5f, currentY, valueWidth, ControlHeight);
                Widgets.Label(daysValueRect, days.ToString());
                
                if (Widgets.ButtonText(new Rect(LeftMargin + labelWidth + valueWidth + 10f, currentY, SmallButtonSize, ControlHeight), "-"))
                {
                    days = Math.Max(0, days - 1);
                }
                if (Widgets.ButtonText(new Rect(LeftMargin + labelWidth + valueWidth + 10f + SmallButtonSize + ButtonGap, currentY, SmallButtonSize, ControlHeight), "+"))
                {
                    days = Math.Min(6, days + 1); 
                }
                currentY += ControlHeight + 10f;
                
                Rect hoursLabelRect = new Rect(LeftMargin, currentY, labelWidth, ControlHeight);
                Widgets.Label(hoursLabelRect, "Hours:");
                
                Rect hoursValueRect = new Rect(LeftMargin + labelWidth + 5f, currentY, valueWidth, ControlHeight);
                Widgets.Label(hoursValueRect, hours.ToString());
                
                if (Widgets.ButtonText(new Rect(LeftMargin + labelWidth + valueWidth + 10f, currentY, SmallButtonSize, ControlHeight), "-"))
                {
                    hours = Math.Max(0, hours - 1);
                }
                if (Widgets.ButtonText(new Rect(LeftMargin + labelWidth + valueWidth + 10f + SmallButtonSize + ButtonGap, currentY, SmallButtonSize, ControlHeight), "+"))
                {
                    hours = Math.Min(23, hours + 1);
                }
                currentY += ControlHeight + 20f;
                
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(LeftMargin, currentY, contentWidth, ControlHeight), "Frequency:");
                Text.Font = GameFont.Small;
                currentY += ControlHeight + 10f;
                
                float optionSpacing = 8f;
                float frequencyHeight = 6 * (ControlHeight + optionSpacing);
                
                Rect frequencyBg = new Rect(LeftMargin, currentY, contentWidth, frequencyHeight);
                Widgets.DrawLightHighlight(frequencyBg);
                
                float radioX = LeftMargin + 10f;
                float radioButtonX = LeftMargin + contentWidth - 30f;
                
                ReminderFrequency[] frequencies = new ReminderFrequency[]
                {
                    ReminderFrequency.OneTime,
                    ReminderFrequency.Daily,
                    ReminderFrequency.Weekly,
                    ReminderFrequency.Monthly,
                    ReminderFrequency.Quarterly,
                    ReminderFrequency.Yearly
                };
                
                for (int i = 0; i < frequencies.Length; i++)
                {
                    ReminderFrequency freq = frequencies[i];
                    Rect radioRect = new Rect(radioX, currentY + 5f, contentWidth - 20f, ControlHeight);
                    bool isSelected = selectedFrequency == freq;
                    
                    if (Widgets.RadioButtonLabeled(radioRect, freq.ToString(), isSelected))
                    {
                        selectedFrequency = freq;
                    }
                    
                    currentY += ControlHeight + optionSpacing;
                }
                
                currentY += 15f;
                
                float bottomY = inRect.height - 50f;
                float buttonWidth = 100f;
                
                if (Widgets.ButtonText(new Rect(inRect.width / 2 - buttonWidth - 5f, bottomY, buttonWidth, ControlHeight), "Cancel"))
                {
                    Close();
                }
                
                if (Widgets.ButtonText(new Rect(inRect.width / 2 + 5f, bottomY, buttonWidth, ControlHeight), "Create"))
                {
                    TryCreateReminder();
                }
            }
            finally
            {
                Text.WordWrap = originalWordWrap;
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

            int triggerTick = Find.TickManager.TicksGame;
            
            if (days == 0 && hours == 0)
            {
                switch (selectedFrequency)
                {
                    case ReminderFrequency.OneTime:
                        triggerTick += GenDate.TicksPerHour;
                        break;
                    case ReminderFrequency.Daily:
                        triggerTick += GenDate.TicksPerDay;
                        break;
                    case ReminderFrequency.Weekly:
                        triggerTick += GenDate.TicksPerDay * 7;
                        break;
                    case ReminderFrequency.Monthly:
                        triggerTick += GenDate.TicksPerDay * 30;
                        break;
                    case ReminderFrequency.Quarterly:
                        triggerTick += GenDate.TicksPerDay * 90;
                        break;
                    case ReminderFrequency.Yearly:
                        triggerTick += GenDate.TicksPerDay * 60;
                        break;
                }
            }
            else
            {
                triggerTick += days * GenDate.TicksPerDay;
                triggerTick += hours * GenDate.TicksPerHour;
            }

            int minOffset = GenDate.TicksPerHour; // Minimum 1 hour
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