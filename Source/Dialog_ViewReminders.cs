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

        private const float Padding = 15f;
        private const float LineHeight = 30f;
        private const float ButtonHeight = 30f;
        private const float ButtonWidth = 100f;
        private const float ReminderSpacing = 20f;
        private const float ReminderHeight = LineHeight * 4;

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
            float usableWidth = inRect.width - (Padding * 2);
            float currentY = Padding; 

            DrawHeader(inRect, ref currentY, usableWidth);
            RefreshTendReminders();

            if (reminders.Count == 0)
            {
                DrawNoRemindersLabel(currentY, usableWidth);
                DrawCloseButton(inRect);
                return;
            }

            var filteredReminders = GetFilteredReminders();
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

        private void DrawNoRemindersLabel(float currentY, float usableWidth)
        {
            GUI.color = Color.gray;
            Widgets.Label(new Rect(Padding, currentY, usableWidth, LineHeight), "No active reminders");
            GUI.color = Color.white;
        }

        private List<Reminder> GetFilteredReminders()
        {
            if (string.IsNullOrEmpty(filterText) && selectedFilter == "All")
                return reminders;
            string filter = filterText.ToLower();
            return reminders.Where(r =>
            {
                bool matchesText = string.IsNullOrEmpty(filter) || r.label.ToLower().Contains(filter) || r.description.ToLower().Contains(filter);
                bool matchesType = selectedFilter == "All" ||
                    (selectedFilter == "Tend" && r is PawnTendReminder) ||
                    (selectedFilter == "Other" && !(r is PawnTendReminder));
                return matchesText && matchesType;
            }).ToList();
        }

        private void DrawRemindersList(List<Reminder> filteredReminders, Rect inRect, float usableWidth, ref float currentY)
        {
            Rect viewRect = new Rect(Padding, currentY, usableWidth, inRect.height - currentY - ButtonHeight - (Padding * 3));
            float totalHeight = filteredReminders.Count * (ReminderHeight + ReminderSpacing);
            Rect scrollRect = new Rect(0, 0, viewRect.width - 16f, totalHeight);
            Widgets.BeginScrollView(viewRect, ref scrollPosition, scrollRect, true);
            float scrollY = 0f;
            foreach (Reminder reminder in filteredReminders)
            {
                if (scrollY > 0) scrollY += ReminderSpacing;
                DrawReminderCard(reminder, scrollRect, scrollY);
                scrollY += ReminderHeight;
            }
            Widgets.EndScrollView();
        }

        private void DrawReminderCard(Reminder reminder, Rect scrollRect, float scrollY)
        {
            Rect reminderRect = new Rect(0, scrollY, scrollRect.width, ReminderHeight);
            Widgets.DrawBoxSolid(reminderRect, new Color(0.2f, 0.2f, 0.2f, 0.6f));
            
            
            float progress = (reminder is PawnTendReminder tendRem) ? tendRem.tendProgress : reminder.GetProgressPercent();
            Rect progressRect = reminderRect; progressRect.width *= progress;
            Widgets.DrawBoxSolid(progressRect, new Color(0.2f, 0.5f, 0.2f, 0.3f));
            
            Rect infoRect = reminderRect.ContractedBy(10f);
            float timeWidth = 180f;
            float titleWidth = infoRect.width - timeWidth;
            
            
            Rect titleRect = new Rect(infoRect.x, infoRect.y, titleWidth, LineHeight);
            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, reminder.label);
            Text.Font = GameFont.Small;
            
            
            Rect timeRect = new Rect(infoRect.x + titleWidth, infoRect.y, timeWidth, LineHeight);
            GUI.color = Color.yellow;
            
            
            Widgets.Label(timeRect, reminder.GetTimeLeftString());
            
            GUI.color = Color.white;
            
            
            bool prevWordWrap = Text.WordWrap;
            Text.WordWrap = true;
            Rect descRect = new Rect(infoRect.x, infoRect.y + LineHeight, infoRect.width, LineHeight * 2);
            Widgets.Label(descRect, reminder.description);
            Text.WordWrap = prevWordWrap;
            
            DrawReminderButtons(reminder, infoRect);
        }

        private void DrawReminderButtons(Reminder reminder, Rect infoRect)
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
            if (Widgets.ButtonText(editRect, "Edit"))
            {
                reminder.OpenEditDialog();
                Close();
                return;
            }
            if (reminder is PawnTendReminder pawnTendReminder)
            {
                buttonX -= buttonWidth + buttonSpacing;
                Rect jumpRect = new Rect(buttonX, buttonRowY, buttonWidth, LineHeight);
                if (Widgets.ButtonText(jumpRect, "Jump to Pawn"))
                {
                    try
                    {
                        Pawn pawn = pawnTendReminder.FindPawn();
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

        public void RefreshTendReminders()
        {
            foreach (var reminder in reminders)
            {
                if (reminder is PawnTendReminder tendReminder)
                {
                    Pawn pawn = tendReminder.FindPawn();
                    if (pawn == null) continue;

                    
                    var candidates = pawn.health.hediffSet.hediffs
                        .OfType<HediffWithComps>()
                        .Select(hwc => new HediffWithTendComp(hwc, hwc.TryGetComp<HediffComp_TendDuration>()))
                        .Where(x => x.TendComp != null &&
                                    x.Hediff.def.tendable &&
                                    !x.Hediff.IsPermanent() &&
                                    !(x.Hediff.def.defName.Contains("Missing") || x.Hediff.def.defName.Contains("Removed")) &&
                                    !(x.Hediff is Hediff_Injury injury && !injury.Bleeding && injury.Severity <= 0) &&
                                    !(x.Hediff.Part != null && pawn.health.hediffSet.hediffs.Any(h => h.Part == x.Hediff.Part && h.def.defName.Contains("Missing"))) &&
                                    (!x.TendComp.TProps.TendIsPermanent || (x.Hediff is Hediff_Injury hediff_Injury && hediff_Injury.Bleeding))) 
                        .ToList();

                    
                    if (candidates.Count == 0)
                    {
                        RiminderManager.RemoveReminder(tendReminder.id);
                        continue;
                    }  

                    
                    var needTendNow = candidates.Where(x => x.TendComp.tendTicksLeft == -1).ToList();

                    HediffWithTendComp priority = null;
                    if (needTendNow.Count > 0)
                    {
                        
                        if (needTendNow.Count == 1)
                        {
                            priority = needTendNow[0];
                        }
                        else
                        {
                            
                            var bleeding = needTendNow.FirstOrDefault(x => x.Hediff is Hediff_Injury injury && injury.Bleeding);
                            if (bleeding != null)
                                priority = bleeding;
                            else
                            {
                                
                                var disease = needTendNow.FirstOrDefault(x => x.Hediff.def is HediffDef def && def.makesSickThought);
                                if (disease != null)
                                    priority = disease;
                                else
                                    priority = needTendNow[0]; 
                            }
                        }
                    }
                    else
                    {
                        
                        priority = candidates.OrderBy(x => x.TendComp.tendTicksLeft == -1 ? int.MinValue : x.TendComp.tendTicksLeft).FirstOrDefault();
                    }

                    if (priority == null) continue;

                    
                    float progress = 0f;
                    if (priority.TendComp.tendTicksLeft == -1)
                    {
                        progress = 1f;
                        tendReminder.totalTendDuration = -1;
                    }
                    else
                    {
                        
                        if (tendReminder.totalTendDuration <= 0 || tendReminder.totalTendDuration < priority.TendComp.tendTicksLeft)
                        {
                            tendReminder.totalTendDuration = priority.TendComp.tendTicksLeft;
                        }
                        if (tendReminder.totalTendDuration > 0)
                        {
                            progress = 1f - (priority.TendComp.tendTicksLeft / (float)tendReminder.totalTendDuration);
                        }
                        else
                        {
                            progress = 0f;
                        }
                    }

                    
                    string desc = $"{pawn.LabelShort}'s {priority.Hediff.LabelCap} ({priority.Hediff.def.label})";
                    if (priority.TendComp.tendTicksLeft == -1)
                    {
                        desc += "\nNeeds tending now!";
                    }
                    else
                    {
                        // Calculate the actual time when the hediff can be tended again
                        int nextTendableInTicks = priority.TendComp.tendTicksLeft - priority.TendComp.TProps.TendTicksOverlap;
                        if (nextTendableInTicks <= 0)
                        {
                            desc += "\nTended, can be tended again now.";
                        }
                        else
                        {
                            float hoursUntilTendable = nextTendableInTicks / (float)GenDate.TicksPerHour;
                            desc += $"\nTended, next tend in {hoursUntilTendable:F1}h.";
                        }
                    }
                    tendReminder.description = desc;
                    tendReminder.hediffId = priority.Hediff.loadID > 0 ? priority.Hediff.loadID.ToString() : $"{pawn.ThingID}_{priority.Hediff.def.defName}";
                    tendReminder.hediffLabel = priority.Hediff.def.label;
                    tendReminder.label = $"Tend {pawn.LabelShort}'s {priority.Hediff.LabelCap}";
                    tendReminder.tendProgress = progress;
                }
            }
        }

        
        private class HediffWithTendComp
        {
            public HediffWithComps Hediff { get; }
            public HediffComp_TendDuration TendComp { get; }

            public HediffWithTendComp(HediffWithComps hediff, HediffComp_TendDuration tendComp)
            {
                Hediff = hediff;
                TendComp = tendComp;
            }
        }

        public void RefreshAndRedraw()
        {
            try
            {
                
                reminders = RiminderManager.GetActiveReminders();
                
                
                if (reminders.Any(r => r is PawnTendReminder))
                {
                    RefreshTendReminders();
                }
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
