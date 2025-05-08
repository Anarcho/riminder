using System;
using UnityEngine;
using Verse;
using RimWorld;

namespace Riminder
{
    public class Dialog_EditTendReminder : Window
    {
        private readonly PawnTendReminder reminder;

        private const float LeftMargin = 20f;
        private const float ControlHeight = 30f;

        public Dialog_EditTendReminder(PawnTendReminder reminder)
        {
            this.reminder = reminder ?? throw new ArgumentNullException(nameof(reminder));

            forcePause = false;
            doCloseX = true;
            doCloseButton = false;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = false;
            preventCameraMotion = false;
        }

        public override Vector2 InitialSize => new Vector2(450f, 300f);

        public override void DoWindowContents(Rect inRect)
        {
            float contentWidth = inRect.width - (LeftMargin * 2);
            float currentY = 10f;

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(LeftMargin, currentY, contentWidth, ControlHeight), "Edit Tend Reminder");
            Text.Font = GameFont.Small;
            currentY += ControlHeight + 20f;

            Pawn pawn = reminder.FindPawn();
            Hediff hediff = reminder.FindHediff(pawn);

            if (pawn != null)
            {
                Widgets.Label(new Rect(LeftMargin, currentY, contentWidth, ControlHeight), $"Tending reminder for {pawn.LabelShort}");
                currentY += ControlHeight + 10f;

                string healthInfo = "Current health conditions:";
                
                bool foundAnyTendableCondition = false;
                
                foreach (var h in pawn.health.hediffSet.hediffs)
                {
                    if (!h.def.tendable || h.IsPermanent()) continue;
                    
                    if (h is HediffWithComps hwc)
                    {
                        var tendComp = hwc.TryGetComp<HediffComp_TendDuration>();
                        if (tendComp != null)
                        {
                            healthInfo += $"\n- {h.Label}";
                            
                            if (tendComp.IsTended)
                            {
                                healthInfo += $" (Quality: {tendComp.tendQuality:P0})";
                                float hoursLeft = tendComp.tendTicksLeft / (float)GenDate.TicksPerHour;
                                if (hoursLeft > 0)
                                {
                                    healthInfo += $" - Next tend in: {hoursLeft:F1}h";
                                }
                                else
                                {
                                    healthInfo += " - Ready to tend";
                                }
                            }
                            else
                            {
                                healthInfo += " - Needs tending now!";
                            }
                            
                            foundAnyTendableCondition = true;
                        }
                    }
                }
                
                if (!foundAnyTendableCondition)
                {
                    healthInfo += "\nNo conditions currently require tending.";
                }
                
                float infoHeight = Text.CalcHeight(healthInfo, contentWidth);
                Rect infoRect = new Rect(LeftMargin, currentY, contentWidth, infoHeight);
                Widgets.Label(infoRect, healthInfo);
                
                currentY += infoHeight + 20f;
                
                string noteText = "Note: This reminder will automatically be removed when no conditions need tending.";
                Rect noteRect = new Rect(LeftMargin, currentY, contentWidth, ControlHeight);
                GUI.color = Color.yellow;
                Widgets.Label(noteRect, noteText);
                GUI.color = Color.white;
                
                currentY += ControlHeight + 10f;
            }
            else
            {
                Widgets.Label(new Rect(LeftMargin, currentY, contentWidth, ControlHeight), "Error: Pawn not found");
                currentY += ControlHeight + 10f;
            }

            float buttonY = inRect.height - ControlHeight - 10f;
            float buttonWidth = 100f;

            if (Widgets.ButtonText(new Rect(inRect.width / 2 - buttonWidth - 5f, buttonY, buttonWidth, ControlHeight), "Cancel"))
            {
                Close();
                Find.WindowStack.Add(new Dialog_ViewReminders());
            }

            if (Widgets.ButtonText(new Rect(inRect.width / 2 + 5f, buttonY, buttonWidth, ControlHeight), "Save"))
            {
                Messages.Message("Tend reminder updated", MessageTypeDefOf.TaskCompletion, false);
                Close();
                Find.WindowStack.Add(new Dialog_ViewReminders());
            }
        }
    }
}
