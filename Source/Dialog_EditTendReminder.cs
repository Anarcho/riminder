using System;
using UnityEngine;
using Verse;
using RimWorld;

namespace Riminder
{
    public class Dialog_EditTendReminder : Window
    {
        private PawnTendReminder reminder;
        private bool removeOnImmunity;
        
        private const float LeftMargin = 20f;
        private const float ControlHeight = 30f;
        
        public Dialog_EditTendReminder(PawnTendReminder reminder)
        {
            this.reminder = reminder;
            this.removeOnImmunity = reminder.removeOnImmunity;
            
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
            float contentWidth = inRect.width - 40f;
            float currentY = 10f;
            
            // Title
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(LeftMargin, currentY, contentWidth, ControlHeight), "Edit Tend Reminder");
            Text.Font = GameFont.Small;
            currentY += ControlHeight + 20f;
            
            // Show pawn and hediff info
            Pawn pawn = reminder.FindPawn();
            Hediff hediff = reminder.FindHediff(pawn);
            
            if (pawn != null && hediff != null)
            {
                string hediffInfo = $"Tending reminder for {pawn.LabelShort}'s {hediff.Label}";
                Widgets.Label(new Rect(LeftMargin, currentY, contentWidth, ControlHeight), hediffInfo);
                currentY += ControlHeight + 10f;
                
                // Show current tend quality and time left if available
                if (hediff is HediffWithComps hwc)
                {
                    var tendComp = hwc.TryGetComp<HediffComp_TendDuration>();
                    if (tendComp != null)
                    {
                        string qualityInfo = $"Current tend quality: {tendComp.tendQuality:P0}";
                        Widgets.Label(new Rect(LeftMargin, currentY, contentWidth, ControlHeight), qualityInfo);
                        currentY += ControlHeight + 10f;

                        float hoursLeft = tendComp.tendTicksLeft / (float)GenDate.TicksPerHour;
                        string timeInfo = $"Time until next tending needed: {hoursLeft:F1} hours";
                        Widgets.Label(new Rect(LeftMargin, currentY, contentWidth, ControlHeight), timeInfo);
                        currentY += ControlHeight + 10f;
                    }
                }

                // Add immunity checkbox if the hediff is a disease
                if (hediff is HediffWithComps diseaseHediff && diseaseHediff.TryGetComp<HediffComp_Immunizable>() != null)
                {
                    Rect immunityRect = new Rect(LeftMargin, currentY, contentWidth, ControlHeight);
                    Widgets.CheckboxLabeled(immunityRect, "Remove reminder when immunity is reached", ref removeOnImmunity);
                    currentY += ControlHeight + 10f;
                }
            }
            
            // Buttons
            float bottomY = inRect.height - 50f;
            float buttonWidth = 100f;
            
            if (Widgets.ButtonText(new Rect(inRect.width / 2 - buttonWidth - 5f, bottomY, buttonWidth, ControlHeight), "Cancel"))
            {
                Close();
                Find.WindowStack.Add(new Dialog_ViewReminders());
            }
            
            if (Widgets.ButtonText(new Rect(inRect.width / 2 + 5f, bottomY, buttonWidth, ControlHeight), "Save"))
            {
                reminder.removeOnImmunity = removeOnImmunity;
                Messages.Message("Tend reminder updated", MessageTypeDefOf.TaskCompletion, false);
                Close();
                Find.WindowStack.Add(new Dialog_ViewReminders());
            }
        }
    }
} 