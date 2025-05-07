using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;

namespace Riminder
{
    public class Dialog_ViewReminders : Window
    {
        private Vector2 scrollPosition = Vector2.zero;
        private List<Reminder> reminders = new List<Reminder>();
        private string filterText = "";

        // Constants for consistent layout
        private const float Padding = 15f;
        private const float LineHeight = 30f;
        private const float ButtonHeight = 30f;
        private const float ButtonWidth = 100f; // Increase button width since we have fewer buttons
        private const float ReminderSpacing = 20f; // Gap between reminders
        private const float ReminderHeight = LineHeight * 3;
        
        public Dialog_ViewReminders()
        {
            forcePause = true;
            doCloseX = true;
            doCloseButton = false; // Remove default close button
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            
            // Get all active reminders
            reminders = RiminderManager.GetActiveReminders();
        }

        public override Vector2 InitialSize => new Vector2(600f, 550f);

        public override void DoWindowContents(Rect inRect)
        {
            float usableWidth = inRect.width - (Padding * 2);
            float currentY = Padding;

            // Title
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(Padding, currentY, inRect.width - (Padding * 2), 42f), "Manage Reminders");
            Text.Font = GameFont.Small;
            currentY += 42f;

            // Add new reminder button and filter on same row, properly aligned
            float filterWidth = usableWidth - ButtonWidth - Padding;
            
            // Filter input
            Widgets.Label(new Rect(Padding, currentY + 5f, 60f, LineHeight), "Filter:");
            filterText = Widgets.TextField(
                new Rect(Padding + 60f, currentY + 5f, filterWidth - 60f, LineHeight), 
                filterText
            );
            
            // Add new button
            if (Widgets.ButtonText(new Rect(inRect.width - ButtonWidth - Padding, currentY, ButtonWidth, ButtonHeight), "Add New"))
            {
                Find.WindowStack.Add(new Dialog_CreateReminder());
                Close();
            }
            
            currentY += LineHeight + Padding;

            // No reminders message
            if (reminders.Count == 0)
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(Padding, currentY, usableWidth, LineHeight), "No active reminders");
                GUI.color = Color.white;
                
                // Add close button at bottom
                DrawCloseButton(inRect);
                return;
            }

            // Apply filter
            List<Reminder> filteredReminders = reminders;
            if (!string.IsNullOrEmpty(filterText))
            {
                string filter = filterText.ToLower();
                filteredReminders = reminders.Where(r => 
                    r.label.ToLower().Contains(filter) || 
                    r.description.ToLower().Contains(filter)).ToList();
            }

            // Calculate view rect
            Rect viewRect = new Rect(Padding, currentY, usableWidth, inRect.height - currentY - ButtonHeight - (Padding * 3));
            
            // Calculate total height with spacing between reminders
            float totalHeight = filteredReminders.Count * (ReminderHeight + ReminderSpacing);
            Rect scrollRect = new Rect(0, 0, viewRect.width - 16f, totalHeight);

            // Begin scrolling
            Widgets.BeginScrollView(viewRect, ref scrollPosition, scrollRect, true);
            float scrollY = 0f;

            // Draw each reminder
            foreach (Reminder reminder in filteredReminders)
            {
                // Add spacing before each reminder (except first one)
                if (scrollY > 0)
                {
                    scrollY += ReminderSpacing;
                }
                
                Rect reminderRect = new Rect(0, scrollY, scrollRect.width, ReminderHeight);
                Widgets.DrawBoxSolid(reminderRect, new Color(0.2f, 0.2f, 0.2f, 0.6f));
                
                // Progress bar in background
                float progress = reminder.GetProgressPercent();
                Rect progressRect = reminderRect;
                progressRect.width = progressRect.width * progress;
                Widgets.DrawBoxSolid(progressRect, new Color(0.2f, 0.5f, 0.2f, 0.3f));
                
                Rect infoRect = reminderRect.ContractedBy(10f);
                
                // Title and time on same row
                float timeWidth = 180f; // Increased from 100f to accommodate longer time strings
                float titleWidth = infoRect.width - timeWidth;
                
                // Title
                Rect titleRect = new Rect(infoRect.x, infoRect.y, titleWidth, LineHeight);
                Text.Font = GameFont.Medium;
                Widgets.Label(titleRect, reminder.label);
                Text.Font = GameFont.Small;
                
                // Time left - right aligned
                Rect timeRect = new Rect(infoRect.x + titleWidth, infoRect.y, timeWidth, LineHeight);
                GUI.color = Color.yellow;
                Widgets.Label(timeRect, reminder.GetTimeLeftString());
                GUI.color = Color.white;
                
                // Description
                Rect descRect = new Rect(infoRect.x, infoRect.y + LineHeight, infoRect.width, LineHeight);
                Widgets.Label(descRect, reminder.description);
                
                // Frequency and buttons on same row
                Rect freqRect = new Rect(infoRect.x, infoRect.y + LineHeight * 2, infoRect.width / 2, LineHeight);
                Widgets.Label(freqRect, "Frequency: " + reminder.frequency.ToString());
                
                // Buttons with consistent spacing - swapped positions
                float buttonX = infoRect.x + infoRect.width;
                float buttonSpacing = 15f; // More space between buttons
                
                // Delete button - now first (right)
                buttonX -= ButtonWidth;
                Rect deleteRect = new Rect(buttonX, infoRect.y + LineHeight * 2, ButtonWidth, LineHeight);
                if (Widgets.ButtonText(deleteRect, "Delete"))
                {
                    RiminderManager.RemoveReminder(reminder.id);
                    reminders.Remove(reminder);
                    break;
                }
                
                // Edit button - now second (left)
                buttonX -= ButtonWidth + buttonSpacing;
                Rect editRect = new Rect(buttonX, infoRect.y + LineHeight * 2, ButtonWidth, LineHeight);
                if (Widgets.ButtonText(editRect, "Edit"))
                {
                    // Open the edit dialog and pass the reminder to edit
                    Find.WindowStack.Add(new Dialog_EditReminder(reminder));
                    Close();
                    break;
                }
                
                scrollY += ReminderHeight;
            }

            Widgets.EndScrollView();
            
            // Add close button at bottom
            DrawCloseButton(inRect);
        }
        
        private void DrawCloseButton(Rect inRect)
        {
            float buttonWidth = 120f;
            float bottomY = inRect.height - ButtonHeight - Padding;
            
            if (Widgets.ButtonText(new Rect((inRect.width - buttonWidth) / 2, bottomY, buttonWidth, ButtonHeight), "Close"))
            {
                Close();
            }
        }
    }
} 