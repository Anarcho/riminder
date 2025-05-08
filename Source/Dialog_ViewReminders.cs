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

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(Padding, currentY, inRect.width - (Padding * 2), 42f), "Manage Reminders");
            Text.Font = GameFont.Small;
            currentY += 42f;

            float filterWidth = usableWidth - ButtonWidth - Padding;

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

            Widgets.Label(new Rect(Padding + 110f, currentY + 5f, 60f, LineHeight), "Search:");
            filterText = Widgets.TextField(
                new Rect(Padding + 170f, currentY + 5f, filterWidth - 170f, LineHeight), 
                filterText
            );

            if (Widgets.ButtonText(new Rect(inRect.width - ButtonWidth - Padding, currentY, ButtonWidth, ButtonHeight), "Add New"))
            {
                Find.WindowStack.Add(new Dialog_CreateReminder());
                Close();
            }

            currentY += LineHeight + Padding;

            RefreshTendReminders();

            if (reminders.Count == 0)
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(Padding, currentY, usableWidth, LineHeight), "No active reminders");
                GUI.color = Color.white;
                DrawCloseButton(inRect);
                return;
            }

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

            Rect viewRect = new Rect(Padding, currentY, usableWidth, inRect.height - currentY - ButtonHeight - (Padding * 3));
            float totalHeight = filteredReminders.Count * (ReminderHeight + ReminderSpacing);
            Rect scrollRect = new Rect(0, 0, viewRect.width - 16f, totalHeight);

            Widgets.BeginScrollView(viewRect, ref scrollPosition, scrollRect, true);
            float scrollY = 0f;

            foreach (Reminder reminder in filteredReminders)
            {
                if (scrollY > 0)
                {
                    scrollY += ReminderSpacing;
                }

                Rect reminderRect = new Rect(0, scrollY, scrollRect.width, ReminderHeight);
                Widgets.DrawBoxSolid(reminderRect, new Color(0.2f, 0.2f, 0.2f, 0.6f));

                float progress = reminder.GetProgressPercent();
                Rect progressRect = reminderRect;
                progressRect.width = progressRect.width * progress;
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
                
                if (reminder is PawnTendReminder tendReminder)
                {
                    Widgets.Label(descRect, tendReminder.description);
                    
                    string deathTimeInfo = tendReminder.GetTimeUntilDeathInfo();
                    if (!string.IsNullOrEmpty(deathTimeInfo))
                    {
                        Rect deathTimeRect = new Rect(infoRect.x, infoRect.y + LineHeight * 2.5f, infoRect.width, LineHeight);
                        GUI.color = new Color(1f, 0.3f, 0.3f);
                        Widgets.Label(deathTimeRect, deathTimeInfo);
                        GUI.color = Color.white;
                    }
                }
                else
                {
                    Widgets.Label(descRect, reminder.description);
                }
                
                Text.WordWrap = prevWordWrap;

                Rect freqRect = new Rect(infoRect.x, infoRect.y + LineHeight * 3, infoRect.width / 2, LineHeight);
                Widgets.Label(freqRect, "Frequency: " + reminder.GetFrequencyDisplayString());

                float buttonRowY = infoRect.y + LineHeight * 3;
                float buttonWidth = 100f;
                float buttonSpacing = 10f;
                bool isPawnTendReminder = reminder is PawnTendReminder;
                float buttonX = infoRect.x + infoRect.width;

                buttonX -= buttonWidth;
                Rect deleteRect = new Rect(buttonX, buttonRowY, buttonWidth, LineHeight);
                if (Widgets.ButtonText(deleteRect, "Delete"))
                {
                    RiminderManager.RemoveReminder(reminder.id);
                    reminders.Remove(reminder);
                    break;
                }

                buttonX -= buttonWidth + buttonSpacing;
                Rect editRect = new Rect(buttonX, buttonRowY, buttonWidth, LineHeight);
                if (Widgets.ButtonText(editRect, "Edit"))
                {
                    reminder.OpenEditDialog();
                    Close();
                    break;
                }

                if (isPawnTendReminder)
                {
                    PawnTendReminder pawnTendReminder = (PawnTendReminder)reminder;
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

                scrollY += ReminderHeight;
            }

            Widgets.EndScrollView();
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

        public void RefreshTendReminders()
        {
            foreach (var reminder in reminders)
            {
                if (reminder is PawnTendReminder tendReminder)
                {
                    Pawn pawn = tendReminder.FindPawn();
                    if (pawn == null) continue;
                    
                    Hediff hediff = tendReminder.FindHediff(pawn);
                    if (hediff == null) continue;

                    if (hediff is HediffWithComps hwc)
                    {
                        string desc = $"Reminder to tend to {pawn.LabelShort}'s {hediff.Label} ({hediff.def.label})";
                        
                        var immunityComp = hwc.TryGetComp<HediffComp_Immunizable>();
                        if (immunityComp != null)
                        {
                            desc += $"\nImmunity Progress: {immunityComp.Immunity:P0}";
                            
                            if (immunityComp.Immunity >= 0.8f)
                            {
                                desc += " (Almost immune)";
                            }
                            else if (immunityComp.Immunity >= 0.5f)
                            {
                                desc += " (Good progress)";
                            }
                        }
                        
                        if (hediff.CurStage != null && hediff.CurStage.lifeThreatening && hediff.def.lethalSeverity > 0)
                        {
                            float severityPerHour = 0f;
                            HediffComp_SeverityPerDay severityComp = hediff.TryGetComp<HediffComp_SeverityPerDay>();
                            if (severityComp != null)
                            {
                                severityPerHour = severityComp.SeverityChangePerDay() / 24f;
                            }

                            if (severityPerHour > 0)
                            {
                                float hoursUntilDeath = (hediff.def.lethalSeverity - hediff.Severity) / severityPerHour;
                                if (hoursUntilDeath > 0)
                                {
                                    if (hoursUntilDeath > 24)
                                    {
                                        desc += $"\nLethal in: {hoursUntilDeath/24f:F1} days";
                                    }
                                    else
                                    {
                                        desc += $"\nLethal in: {hoursUntilDeath:F1} hours";
                                    }
                                    
                                    if (immunityComp != null && tendReminder is PawnTendReminder pawnTendReminder)
                                    {
                                        float immunityGainPerDay = 0f;
                                        var tendCompForDisease = hwc.TryGetComp<HediffComp_TendDuration>();
                                        
                                        if (tendCompForDisease != null && hwc.def.CompProps<HediffCompProperties_Immunizable>() != null)
                                        {
                                            float baseImmunityGain = hwc.def.CompProps<HediffCompProperties_Immunizable>().immunityPerDaySick;
                                            
                                            baseImmunityGain *= pawn.GetStatValue(StatDefOf.ImmunityGainSpeed);
                                            
                                            if (tendCompForDisease.IsTended)
                                            {
                                                float tendBonus = tendCompForDisease.tendQuality * 0.3f;
                                                baseImmunityGain += tendBonus;
                                            }
                                            
                                            immunityGainPerDay = baseImmunityGain;
                                            
                                            float daysUntilImmune = (1f - immunityComp.Immunity) / immunityGainPerDay;
                                            
                                            if (daysUntilImmune * 24 < hoursUntilDeath)
                                            {
                                                desc += " (Will recover with current care)";
                                            }
                                            else
                                            {
                                                desc += " (Critical - improve tend quality)";
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        var tendComp = hwc.TryGetComp<HediffComp_TendDuration>();
                        if (tendComp != null)
                        {
                            if (tendComp.IsTended)
                            {
                                desc += $"\nTend Quality: {tendComp.tendQuality:P0}";
                                float hoursLeft = tendComp.tendTicksLeft / (float)GenDate.TicksPerHour;
                                if (hoursLeft > 0)
                                {
                                    desc += $", next tend in {hoursLeft:F1}h";
                                }
                                else
                                {
                                    desc += ", needs tending now";
                                }
                            }
                            else
                            {
                                desc += "\nNeeds tending now!";
                            }
                        }

                        tendReminder.description = desc;
                    }
                }
            }
        }

        public void RefreshAndRedraw()
        {
            RefreshTendReminders();
            reminders = RiminderManager.GetActiveReminders();
        }
    }
}
