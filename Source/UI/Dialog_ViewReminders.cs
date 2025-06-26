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
            
            
            if (Find.TickManager.TicksGame > lastUpdateTick + 1)
            {
                RefreshAllReminders();
                
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
                    
                    
                    
                    
                    int currentTick = Find.TickManager.TicksGame;
                    
                    if (sortAscending)
                    {
                        
                        return remindersToSort
                            .OrderBy(r => currentTick >= r.triggerTick ? 0 : 1) 
                            .ThenBy(r => r.triggerTick) 
                            .ToList();
                    }
                    else
                    {
                        
                        return remindersToSort
                            .OrderByDescending(r => currentTick >= r.triggerTick ? 0 : 1) 
                            .ThenByDescending(r => r.triggerTick) 
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
                    return false; 
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
            
            
            float progress = 0f;
            if (reminder is TendReminder tendRem) 
            {
                
                tendRem.ForceProgressUpdate();
                progress = tendRem.tendProgress;
            }
            else
            {
                
                
                progress = CalculateReminderProgress(reminder);
            }
            
            
            progress = Math.Max(0f, Math.Min(1f, progress));
            
            
            Rect progressBgRect = reminderRect;
            Widgets.DrawBoxSolid(progressBgRect, new Color(0.2f, 0.2f, 0.2f, 0.6f));
            
            
            Rect progressRect = reminderRect; 
            
            progressRect.width = Math.Max(2f, progressRect.width * progress);
            Widgets.DrawBoxSolid(progressRect, new Color(0.2f, 0.7f, 0.2f, 0.4f));
            
            Rect infoRect = reminderRect.ContractedBy(10f);
            
            
            float timeWidth = 250f; 
            float titleWidth = infoRect.width - timeWidth;
            
            
            Rect titleRect = new Rect(infoRect.x, infoRect.y, titleWidth, LineHeight);
            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, reminder.GetLabel());
            Text.Font = GameFont.Small;
            
            
            bool prevWordWrap = Text.WordWrap;
            TextAnchor prevAnchor = Text.Anchor;
            
            Text.WordWrap = true;
            Text.Anchor = TextAnchor.MiddleCenter; 
            
            
            float rightOffset = 40f; 
            
            
            Rect timeRect = new Rect(infoRect.x + titleWidth + rightOffset, infoRect.y, timeWidth - rightOffset, LineHeight + 10f);
            GUI.color = Color.yellow;
            Widgets.Label(timeRect, reminder.GetTimeLeftString());
            GUI.color = Color.white;
            
            
            Text.Anchor = prevAnchor;
            Text.WordWrap = prevWordWrap;
            
            
            Text.WordWrap = true;
            Rect descRect = new Rect(infoRect.x, infoRect.y + LineHeight, infoRect.width, LineHeight * 2);
            Widgets.Label(descRect, reminder.GetDescription());
            Text.WordWrap = prevWordWrap;
            
            DrawReminderButtons(reminder, infoRect);
        }

        private void DrawReminderButtons(BaseReminder reminder, Rect infoRect)
        {
            
            float buttonRowY = infoRect.y + LineHeight * 3; 
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
            string buttonText = (reminder is TendReminder || reminder.GetLabel().StartsWith("Tend ")) ? "Show" : "Edit";
            if (Widgets.ButtonText(editRect, buttonText))
            {
                reminder.OpenEditDialog();
                Close();
                return;
            }
            
            
            if (reminder is TendReminder || reminder.GetLabel().StartsWith("Tend "))
            {
                buttonX -= buttonWidth + buttonSpacing;
                Rect jumpRect = new Rect(buttonX, buttonRowY, buttonWidth, LineHeight);
                if (Widgets.ButtonText(jumpRect, "Jump to Pawn"))
                {
                    try
                    {
                        Pawn pawn = null;
                        
                        if (reminder is TendReminder tendReminder)
                        {
                            pawn = tendReminder.FindPawn();
                        }
                        
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

        
        private float CalculateReminderProgress(BaseReminder reminder)
        {
            int currentTick = Find.TickManager.TicksGame;
            
            
            if (currentTick >= reminder.triggerTick) return 1f;
            
            
            int startTick = reminder.lastTriggerTick;
            int endTick = reminder.triggerTick;
            
            
            if (startTick > currentTick)
            {
                startTick = currentTick;
            }
            
            
            if (endTick <= startTick)
            {
                return 0f;
            }
            
            
            float progress = (float)(currentTick - startTick) / (endTick - startTick);
            
            
            progress = Math.Max(0f, Math.Min(1f, progress));
            
            
            return progress;
        }
        
        
        private int GetStartTickBasedOnFrequency(BaseReminder reminder)
        {
            
            
            int currentTick = Find.TickManager.TicksGame;

            
            if (reminder.frequency == ReminderFrequency.OneTime)
            {
                return reminder.createdTick;
            }

            
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

            
            if (currentTick < reminder.triggerTick)
            {
                return reminder.createdTick;
            }

            
            int intervalsSinceCreation = Math.Max(0, (currentTick - reminder.createdTick) / interval);
            int startTick = reminder.createdTick + intervalsSinceCreation * interval;

            
            if (startTick > reminder.triggerTick)
                startTick = reminder.triggerTick - interval;

            
            startTick = Math.Max(reminder.createdTick, startTick);

            return startTick;
        }

        public void RefreshTendReminders()
        {
            
            RefreshAllReminders();
        }
        
        public void RefreshAllReminders()
        {
            
            reminders = RiminderManager.GetActiveReminders();
            
            
            foreach (var reminder in reminders)
            {
                if (reminder == null) continue;
                
                if (reminder is TendReminder tendReminder)
                {
                    
                    tendReminder.ForceProgressUpdate();
                    
                    
                    tendReminder.dataProvider?.Refresh();
                }
                else
                {
                    
                    reminder.dataProvider?.Refresh();
                    
                    
                    float progress = CalculateReminderProgress(reminder);
                }
            }
        }

        public void RefreshAndRedraw()
        {
            try
            {
                
                reminders = RiminderManager.GetActiveReminders();
                
                
                RefreshAllReminders();
                
                
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
