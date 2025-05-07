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
        private string selectedFilter = "All";
        private readonly string[] filterOptions = new string[] { "All", "Tend", "Other" };

        // Constants for consistent layout
        private const float Padding = 15f;
        private const float LineHeight = 30f;
        private const float ButtonHeight = 30f;
        private const float ButtonWidth = 100f; // Increase button width since we have fewer buttons
        private const float ReminderSpacing = 20f; // Gap between reminders
        private const float ReminderHeight = LineHeight * 3;
        
        public Dialog_ViewReminders()
        {
            forcePause = false;
            doCloseX = true;
            doCloseButton = false; // Remove default close button
            closeOnClickedOutside = true;
            absorbInputAroundWindow = false; // Allow key events to pass through
            preventCameraMotion = false; // Allow camera movement
            
            // Get all active reminders
            reminders = RiminderManager.GetActiveReminders();
        }

        public override Vector2 InitialSize => new Vector2(700f, 550f);

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
            
            // Filter dropdown
            Rect filterDropdownRect = new Rect(Padding, currentY + 5f, 100f, LineHeight);
            if (Widgets.ButtonText(filterDropdownRect, selectedFilter))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                foreach (string filter in filterOptions)
                {
                    options.Add(new FloatMenuOption(filter, () => selectedFilter = filter));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            
            // Filter input
            Widgets.Label(new Rect(Padding + 110f, currentY + 5f, 60f, LineHeight), "Search:");
            filterText = Widgets.TextField(
                new Rect(Padding + 170f, currentY + 5f, filterWidth - 170f, LineHeight), 
                filterText
            );
            
            // Add new button
            if (Widgets.ButtonText(new Rect(inRect.width - ButtonWidth - Padding, currentY, ButtonWidth, ButtonHeight), "Add New"))
            {
                Find.WindowStack.Add(new Dialog_CreateReminder());
                Close();
            }
            
            currentY += LineHeight + Padding;

            // Refresh all PawnTendReminders to ensure up-to-date info
            RefreshTendReminders();

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
            if (!string.IsNullOrEmpty(filterText) || selectedFilter != "All")
            {
                string filter = filterText.ToLower();
                filteredReminders = reminders.Where(r => 
                {
                    bool matchesText = string.IsNullOrEmpty(filter) || 
                        r.label.ToLower().Contains(filter) || 
                        r.description.ToLower().Contains(filter);
                        
                    bool matchesType = selectedFilter == "All" ||
                        (selectedFilter == "Tend" && r is PawnTendReminder) ||
                        (selectedFilter == "Other" && !(r is PawnTendReminder));
                        
                    return matchesText && matchesType;
                }).ToList();
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
                
                // Description - wrap text if too long
                bool prevWordWrap = Text.WordWrap;
                Text.WordWrap = true;
                Rect descRect = new Rect(infoRect.x, infoRect.y + LineHeight, infoRect.width, LineHeight);
                Widgets.Label(descRect, reminder.description);
                Text.WordWrap = prevWordWrap;
                
                // Frequency on its own line - shortened text
                Rect freqRect = new Rect(infoRect.x, infoRect.y + LineHeight * 2, infoRect.width / 2, LineHeight);
                Widgets.Label(freqRect, "Frequency: " + reminder.GetFrequencyDisplayString());
                
                // Put buttons on the right side of the same line as frequency
                float buttonRowY = infoRect.y + LineHeight * 2;
                float buttonWidth = 100f;
                float buttonSpacing = 10f;
                
                // Determine number of buttons and start from the right side of the info rect
                bool isPawnTendReminder = reminder is PawnTendReminder;
                float buttonX = infoRect.x + infoRect.width;
                
                // Delete button - rightmost
                buttonX -= buttonWidth;
                Rect deleteRect = new Rect(buttonX, buttonRowY, buttonWidth, LineHeight);
                if (Widgets.ButtonText(deleteRect, "Delete"))
                {
                    RiminderManager.RemoveReminder(reminder.id);
                    reminders.Remove(reminder);
                    break;
                }
                
                // Edit button
                buttonX -= buttonWidth + buttonSpacing;
                Rect editRect = new Rect(buttonX, buttonRowY, buttonWidth, LineHeight);
                if (Widgets.ButtonText(editRect, "Edit"))
                {
                    reminder.OpenEditDialog();
                    Close();
                    break;
                }

                // Jump to Pawn button - leftmost button
                if (isPawnTendReminder)
                {
                    PawnTendReminder tendReminder = (PawnTendReminder)reminder;
                    buttonX -= buttonWidth + buttonSpacing;
                    Rect jumpRect = new Rect(buttonX, buttonRowY, buttonWidth, LineHeight);
                    if (Widgets.ButtonText(jumpRect, "Jump to Pawn"))
                    {
                        Pawn pawn = tendReminder.FindPawn();
                        if (pawn != null)
                        {
                            CameraJumper.TryJump(pawn);
                            Close();
                        }
                        else
                        {
                            Messages.Message("Pawn no longer exists", MessageTypeDefOf.RejectInput, false);
                        }
                    }
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

        private void RefreshTendReminders()
        {
            foreach (var reminder in reminders)
            {
                if (reminder is PawnTendReminder tendReminder)
                {
                    // Find the pawn and hediff
                    Pawn pawn = tendReminder.FindPawn();
                    Hediff hediff = tendReminder.FindHediff(pawn);
                    
                    // Skip if either is null
                    if (pawn == null || hediff == null) continue;
                    
                    // Update the description based on current hediff state
                    if (hediff is HediffWithComps hwc)
                    {
                        var tendComp = hwc.TryGetComp<HediffComp_TendDuration>();
                        if (tendComp != null)
                        {
                            // Update description
                            string desc = $"Reminder to tend to {pawn.LabelShort}'s {hediff.Label} ({hediff.def.label})";
                            
                            if (tendComp.IsTended)
                            {
                                // Format the quality percentage with consistent spacing
                                desc = $"Reminder to tend to {pawn.LabelShort}'s {hediff.Label} ({hediff.def.label}) (Quality: {tendComp.tendQuality:P0})";
                                
                                // Add time left until next tend with proper formatting
                                float hoursLeft = tendComp.tendTicksLeft / (float)GenDate.TicksPerHour;
                                if (hoursLeft > 0)
                                {
                                    desc += $", next tend in {hoursLeft:F1}h";
                                }
                            }
                            else
                            {
                                desc += " (needs tending)";
                            }
                            
                            tendReminder.description = desc;
                        }
                    }
                }
            }
        }
    }
} 