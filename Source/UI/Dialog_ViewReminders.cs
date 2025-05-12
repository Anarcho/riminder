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
        private List<BaseReminder> reminders = new List<BaseReminder>();
        private string filterText = "";
        private string selectedFilter = "All";
        private readonly string[] filterOptions = new string[] { "All", "Tend", "Other" };
        
        // Add sorting options
        private enum SortBy
        {
            Name,
            Urgency,
            None
        }
        
        private SortBy currentSort = SortBy.None;
        private bool sortAscending = true;

        private const float Padding = 15f;
        private const float LineHeight = 30f;
        private const float ButtonHeight = 30f;
        private const float ButtonWidth = 100f;
        private const float ReminderSpacing = 20f;
        private const float ReminderHeight = LineHeight * 4;

        // Store the last update tick to ensure progress bars stay current
        private int lastUpdateTick = 0;

        public Dialog_ViewReminders()
        {
            forcePause = false;
            doCloseX = true;
            doCloseButton = false;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = false;
            preventCameraMotion = false;

            reminders = RiminderManager.GetActiveReminders();
        }

        public override Vector2 InitialSize => new Vector2(700f, 550f);

        public override void DoWindowContents(Rect inRect)
        {
            // Check if we should refresh based on time passed
            // Refresh more frequently for better UI responsiveness - reduce to 1 tick for near real-time updates
            if (Find.TickManager.TicksGame > lastUpdateTick + 1)
            {
                RefreshAllReminders();
                // Force redraw for immediate visual update
                lastUpdateTick = Find.TickManager.TicksGame;
            }
            
            float usableWidth = inRect.width - (Padding * 2);
            float currentY = Padding; 

            DrawHeader(inRect, ref currentY, usableWidth);
            DrawSortingControls(usableWidth, ref currentY);
            
            if (reminders.Count == 0)
            {
                DrawNoRemindersLabel(currentY, usableWidth);
                DrawCloseButton(inRect);
                return;
            }

            var filteredReminders = GetFilteredReminders();
            // Apply sorting
            filteredReminders = SortReminders(filteredReminders);
            
            DrawRemindersList(filteredReminders, inRect, usableWidth, ref currentY);
            DrawCloseButton(inRect);
        }

        private void DrawHeader(Rect inRect, ref float currentY, float usableWidth)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(Padding, currentY, inRect.width - (Padding * 2), 42f), "Manage Reminders");
            Text.Font = GameFont.Small;
            currentY += 42f;

            float filterWidth = usableWidth - ButtonWidth - Padding;
            Rect filterDropdownRect = new Rect(Padding, currentY + 5f, 100f, LineHeight);
            if (Widgets.ButtonText(filterDropdownRect, selectedFilter))
            {
                var options = filterOptions.Select(filter => new FloatMenuOption(filter, () => selectedFilter = filter)).ToList();
                Find.WindowStack.Add(new FloatMenu(options));
            }
            Widgets.Label(new Rect(Padding + 110f, currentY + 5f, 60f, LineHeight), "Search:");
            filterText = Widgets.TextField(new Rect(Padding + 170f, currentY + 5f, filterWidth - 170f, LineHeight), filterText);
            if (Widgets.ButtonText(new Rect(inRect.width - ButtonWidth - Padding, currentY, ButtonWidth, ButtonHeight), "Add New"))
            {
                Find.WindowStack.Add(new Dialog_CreateReminder());
                Close();
            }
            currentY += LineHeight + Padding;
        }
        
        private void DrawSortingControls(float usableWidth, ref float currentY)
        {
            Rect sortLabelRect = new Rect(Padding, currentY, 50f, LineHeight);
            Widgets.Label(sortLabelRect, "Sort by:");
            
            // Sort by Name button
            Rect sortNameRect = new Rect(Padding + 55f, currentY, 80f, LineHeight);
            if (Widgets.ButtonText(sortNameRect, "Name " + GetSortIndicator(SortBy.Name)))
            {
                if (currentSort == SortBy.Name)
                    sortAscending = !sortAscending;
                else
                {
                    currentSort = SortBy.Name;
                    sortAscending = true;
                }
            }
            
            // Sort by Urgency button
            Rect sortUrgencyRect = new Rect(Padding + 140f, currentY, 80f, LineHeight);
            if (Widgets.ButtonText(sortUrgencyRect, "Urgency " + GetSortIndicator(SortBy.Urgency)))
            {
                if (currentSort == SortBy.Urgency)
                    sortAscending = !sortAscending;
                else
                {
                    currentSort = SortBy.Urgency;
                    sortAscending = true;
                }
            }
            
            // Add clear sort button
            if (currentSort != SortBy.None)
            {
                Rect clearSortRect = new Rect(Padding + 225f, currentY, 80f, LineHeight);
                if (Widgets.ButtonText(clearSortRect, "Clear Sort"))
                {
                    currentSort = SortBy.None;
                    sortAscending = true;
                }
            }
            
            currentY += LineHeight + Padding;
        }
        
        private string GetSortIndicator(SortBy sortType)
        {
            if (currentSort != sortType)
                return "";
                
            return sortAscending ? "↑" : "↓";
        }
        
        private List<BaseReminder> SortReminders(List<BaseReminder> remindersToSort)
        {
            switch (currentSort)
            {
                case SortBy.Name:
                    return sortAscending 
                        ? remindersToSort.OrderBy(r => r.GetLabel()).ToList()
                        : remindersToSort.OrderByDescending(r => r.GetLabel()).ToList();
                
                case SortBy.Urgency:
                    // For urgency sorting, we want to sort by:
                    // 1. Overdue reminders first (current tick > trigger tick)
                    // 2. Then by time left until trigger
                    
                    int currentTick = Find.TickManager.TicksGame;
                    
                    if (sortAscending)
                    {
                        // Ascending = soonest/most overdue first
                        return remindersToSort
                            .OrderBy(r => currentTick >= r.triggerTick ? 0 : 1) // Overdue first
                            .ThenBy(r => r.triggerTick) // Then by trigger time
                            .ToList();
                    }
                    else
                    {
                        // Descending = furthest in future first
                        return remindersToSort
                            .OrderByDescending(r => currentTick >= r.triggerTick ? 0 : 1) // Future first
                            .ThenByDescending(r => r.triggerTick) // Then by trigger time
                            .ToList();
                    }
                
                default:
                    return remindersToSort;
            }
        }

        private void DrawNoRemindersLabel(float currentY, float usableWidth)
        {
            GUI.color = Color.gray;
            Widgets.Label(new Rect(Padding, currentY, usableWidth, LineHeight), "No active reminders");
            GUI.color = Color.white;
        }

        private List<BaseReminder> GetFilteredReminders()
        {
            if (string.IsNullOrEmpty(filterText) && selectedFilter == "All")
                return reminders.Where(r => !(r is TendReminder tr && tr.FindPawn() == null)).ToList();
            string filter = filterText.ToLower();
            return reminders.Where(r =>
            {
                if (r is TendReminder tr && tr.FindPawn() == null)
                    return false; // Filter out invalid tend reminders
                bool matchesText = string.IsNullOrEmpty(filter) || r.GetLabel().ToLower().Contains(filter) || r.GetDescription().ToLower().Contains(filter);
                bool matchesType = selectedFilter == "All" ||
                    (selectedFilter == "Tend" && (r is TendReminder || r.GetLabel().StartsWith("Tend "))) ||
                    (selectedFilter == "Other" && !(r is TendReminder) && !r.GetLabel().StartsWith("Tend "));
                return matchesText && matchesType;
            }).ToList();
        }

        private void DrawRemindersList(List<BaseReminder> filteredReminders, Rect inRect, float usableWidth, ref float currentY)
        {
            Rect viewRect = new Rect(Padding, currentY, usableWidth, inRect.height - currentY - ButtonHeight - (Padding * 3));
            float totalHeight = filteredReminders.Count * (ReminderHeight + ReminderSpacing);
            Rect scrollRect = new Rect(0, 0, viewRect.width - 16f, totalHeight);
            Widgets.BeginScrollView(viewRect, ref scrollPosition, scrollRect, true);
            float scrollY = 0f;
            foreach (BaseReminder reminder in filteredReminders)
            {
                if (scrollY > 0) scrollY += ReminderSpacing;
                DrawReminderCard(reminder, scrollRect, scrollY);
                scrollY += ReminderHeight;
            }
            Widgets.EndScrollView();
        }

        private void DrawReminderCard(BaseReminder reminder, Rect scrollRect, float scrollY)
        {
            Rect reminderRect = new Rect(0, scrollY, scrollRect.width, ReminderHeight);
            Widgets.DrawBoxSolid(reminderRect, new Color(0.2f, 0.2f, 0.2f, 0.6f));
            
            // Calculate progress based on reminder type
            float progress = 0f;
            if (reminder is TendReminder tendRem) 
            {
                // Force an update of the progress value
                tendRem.ForceProgressUpdate();
                progress = tendRem.tendProgress;
                
                // Diagnostic logging in dev mode
                if (Prefs.DevMode)
                {
                    Log.Message($"[Riminder] Drawing tend reminder card for {tendRem.GetLabel()}, progress: {progress}, totalDuration: {tendRem.totalTendDuration}, ticksLeft: {tendRem.actualTendTicksLeft}");
                }
            }
            else
            {
                // For standard reminders, get progress through the standard method
                // This ensures progress starts immediately after creation
                progress = CalculateReminderProgress(reminder);
                
                // Diagnostic logging in dev mode
                if (Prefs.DevMode)
                {
                    Log.Message($"[Riminder] Drawing standard reminder card for {reminder.GetLabel()}, progress: {progress}, triggered at: {reminder.triggerTick}");
                }
            }
            
            // Ensure progress is between 0 and 1
            progress = Math.Max(0f, Math.Min(1f, progress));
            
            // Draw progress bar background
            Rect progressBgRect = reminderRect;
            Widgets.DrawBoxSolid(progressBgRect, new Color(0.2f, 0.2f, 0.2f, 0.6f));
            
            // Draw the progress bar - ensure a minimum width for visibility when progress is very small
            Rect progressRect = reminderRect; 
            // Minimum width of 2 pixels to ensure it's visible immediately
            progressRect.width = Math.Max(2f, progressRect.width * progress);
            Widgets.DrawBoxSolid(progressRect, new Color(0.2f, 0.7f, 0.2f, 0.4f));
            
            Rect infoRect = reminderRect.ContractedBy(10f);
            
            // Adjust layout for reminder elements
            float timeWidth = 250f; 
            float titleWidth = infoRect.width - timeWidth;
            
            // Title on the left
            Rect titleRect = new Rect(infoRect.x, infoRect.y, titleWidth, LineHeight);
            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, reminder.GetLabel());
            Text.Font = GameFont.Small;
            
            // Time display on the right - aligned with title on the same line
            bool prevWordWrap = Text.WordWrap;
            TextAnchor prevAnchor = Text.Anchor;
            
            Text.WordWrap = true;
            Text.Anchor = TextAnchor.MiddleCenter; // Center the text horizontally and vertically
            
            // Move the time rectangle to the right of the title, keeping on same line
            float rightOffset = 40f; // Adjust this value to move it further right
            
            // Place time rect on same line as title, with equal height
            Rect timeRect = new Rect(infoRect.x + titleWidth + rightOffset, infoRect.y, timeWidth - rightOffset, LineHeight + 10f);
            GUI.color = Color.yellow;
            Widgets.Label(timeRect, reminder.GetTimeLeftString());
            GUI.color = Color.white;
            
            // Restore previous text settings
            Text.Anchor = prevAnchor;
            Text.WordWrap = prevWordWrap;
            
            // Description below title and time
            Text.WordWrap = true;
            Rect descRect = new Rect(infoRect.x, infoRect.y + LineHeight, infoRect.width, LineHeight * 2);
            Widgets.Label(descRect, reminder.GetDescription());
            Text.WordWrap = prevWordWrap;
            
            DrawReminderButtons(reminder, infoRect);
        }

        private void DrawReminderButtons(BaseReminder reminder, Rect infoRect)
        {
            // Position buttons at the bottom of the card
            float buttonRowY = infoRect.y + LineHeight * 3; // Adjusted for new layout
            float buttonWidth = 100f;
            float buttonSpacing = 10f;
            float buttonX = infoRect.x + infoRect.width;
            buttonX -= buttonWidth;
            Rect deleteRect = new Rect(buttonX, buttonRowY, buttonWidth, LineHeight);
            if (Widgets.ButtonText(deleteRect, "Delete"))
            {
                RiminderManager.RemoveReminder(reminder.id);
                reminders.Remove(reminder);
                return;
            }
            buttonX -= buttonWidth + buttonSpacing;
            Rect editRect = new Rect(buttonX, buttonRowY, buttonWidth, LineHeight);
            if (Widgets.ButtonText(editRect, "Edit"))
            {
                reminder.OpenEditDialog();
                Close();
                return;
            }
            
            // Try to determine if it's a tend reminder by checking the label
            if (reminder is TendReminder || reminder.GetLabel().StartsWith("Tend "))
            {
                buttonX -= buttonWidth + buttonSpacing;
                Rect jumpRect = new Rect(buttonX, buttonRowY, buttonWidth, LineHeight);
                if (Widgets.ButtonText(jumpRect, "Jump to Pawn"))
                {
                    try
                    {
                        Pawn pawn = null;
                        
                        // First try if it's a direct TendReminder
                        if (reminder is TendReminder tendReminder)
                        {
                            pawn = tendReminder.FindPawn();
                        }
                        
                        // Otherwise try to infer from the label
                        if (pawn == null && reminder.GetLabel().Contains("'s"))
                        {
                            string pawnName = reminder.GetLabel().Split('\'')[0].Replace("Tend ", "").Trim();
                            pawn = Find.CurrentMap?.mapPawns?.AllPawns
                                .Where(p => p.LabelShort == pawnName)
                                .FirstOrDefault();
                        }
                        
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
                    catch (Exception ex)
                    {
                        if (Prefs.DevMode)
                        {
                            Log.Error($"[Riminder] Error jumping to pawn: {ex}");
                        }
                        Messages.Message("Could not jump to pawn", MessageTypeDefOf.RejectInput, false);
                    }
                }
            }
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

        // Helper method to calculate progress for regular reminders
        private float CalculateReminderProgress(BaseReminder reminder)
        {
            int currentTick = Find.TickManager.TicksGame;
            
            // If deadline has passed, return 100% progress
            if (currentTick >= reminder.triggerTick) return 1f;
            
            // For all reminders, always calculate from lastTriggerTick to triggerTick
            int startTick = reminder.lastTriggerTick;
            int endTick = reminder.triggerTick;
            
            // Sanity check: If start is after current time, use current time
            if (startTick > currentTick)
            {
                startTick = currentTick;
            }
            
            // If start and end are the same, return 0 progress to avoid division by zero
            if (endTick <= startTick)
            {
                if (Prefs.DevMode)
                {
                    Log.Warning($"[Riminder] Invalid interval for {reminder.GetLabel()}: " +
                               $"startTick={startTick}, endTick={endTick}. Defaulting to 0% progress.");
                }
                return 0f;
            }
            
            // Calculate progress as percentage of time elapsed from start to now, relative to total duration
            float progress = (float)(currentTick - startTick) / (endTick - startTick);
            
            // Clamp progress between 0 and 1
            progress = Math.Max(0f, Math.Min(1f, progress));
            
            // Add debug logging when in dev mode
            if (Prefs.DevMode)
            {
                Log.Message($"[Riminder] Progress for {reminder.GetLabel()}: {progress:P0} " +
                            $"[freq={reminder.frequency}, current={currentTick}, " +
                            $"startTick={startTick}, endTick={endTick}, " +
                            $"elapsed={currentTick - startTick}, total={endTick - startTick}]");
            }
            
            return progress;
        }
        
        // Helper method to determine the correct start tick based on reminder frequency
        private int GetStartTickBasedOnFrequency(BaseReminder reminder)
        {
            // This method is no longer used for initial progress calculation
            // but kept for compatibility with other parts of the code
            int currentTick = Find.TickManager.TicksGame;

            // For one-time reminders, use the creation time
            if (reminder.frequency == ReminderFrequency.OneTime)
            {
                return reminder.createdTick;
            }

            // Calculate interval in ticks
            int interval;
            switch (reminder.frequency)
            {
                case ReminderFrequency.Days:
                    interval = GenDate.TicksPerDay;
                    break;
                case ReminderFrequency.Quadrums:
                    interval = GenDate.TicksPerQuadrum;
                    break;
                case ReminderFrequency.Years:
                    interval = GenDate.TicksPerYear;
                    break;
                case ReminderFrequency.Custom:
                    if (reminder is Reminder reminderObj &&
                        reminderObj.metadata != null &&
                        reminderObj.metadata.TryGetValue("intervalTicks", out string intervalStr) &&
                        int.TryParse(intervalStr, out int customInterval) &&
                        customInterval > 0)
                    {
                        interval = customInterval;
                    }
                    else
                    {
                        interval = reminder.triggerTick - reminder.createdTick;
                        if (interval <= 0)
                            interval = GenDate.TicksPerDay;
                    }
                    break;
                default:
                    interval = GenDate.TicksPerDay;
                    break;
            }

            // If the first reminder hasn't occurred yet, fill from creation to first trigger
            if (currentTick < reminder.triggerTick)
            {
                return reminder.createdTick;
            }

            // Otherwise, use the recurring interval logic
            int intervalsSinceCreation = Math.Max(0, (currentTick - reminder.createdTick) / interval);
            int startTick = reminder.createdTick + intervalsSinceCreation * interval;

            // Clamp to not go past triggerTick
            if (startTick > reminder.triggerTick)
                startTick = reminder.triggerTick - interval;

            // Clamp to not go before creation
            startTick = Math.Max(reminder.createdTick, startTick);

            return startTick;
        }

        public void RefreshTendReminders()
        {
            // Renamed for clarity but keeping method name for compatibility
            RefreshAllReminders();
        }
        
        public void RefreshAllReminders()
        {
            // Get fresh list of reminders to ensure we're working with the latest data
            reminders = RiminderManager.GetActiveReminders();
            
            // Force calculation on all reminders
            foreach (var reminder in reminders)
            {
                if (reminder == null) continue;
                
                if (reminder is TendReminder tendReminder)
                {
                    // Force progress updates for tend reminders
                    tendReminder.ForceProgressUpdate();
                    
                    // The TendReminder already handles its own refresh through the data provider
                    tendReminder.dataProvider?.Refresh();
                }
                else
                {
                    // For regular reminders, refresh the data provider if available
                    reminder.dataProvider?.Refresh();
                    
                    // Calculate progress directly using our updated method
                    // This ensures consistent progress calculation
                    float progress = CalculateReminderProgress(reminder);
                    
                    // Log progress calculation for debugging
                    if (Prefs.DevMode && reminder.GetLabel() != null)
                    {
                        Log.Message($"[Riminder] Refreshed regular reminder '{reminder.GetLabel()}' with progress {progress:P0}");
                    }
                }
            }
        }

        public void RefreshAndRedraw()
        {
            try
            {
                // Get fresh list of reminders
                reminders = RiminderManager.GetActiveReminders();
                
                // Refresh all reminders including calculation of progress values
                RefreshAllReminders();
                
                // Update the last refresh time to prevent immediate refresh in DoWindowContents
                lastUpdateTick = Find.TickManager.TicksGame;
            }
            catch (Exception ex)
            {
                if (Prefs.DevMode)
                {
                    Log.Error($"[Riminder] Error in RefreshAndRedraw: {ex}");
                }
            }
        }
    }
}
