using System;
using UnityEngine;
using Verse;
using RimWorld;

namespace Riminder
{
    public class Dialog_EditTendReminder : Window
    {
        private readonly PawnTendReminder reminder;
        private bool removeOnImmunity;

        private const float LeftMargin = 20f;
        private const float ControlHeight = 30f;

        public Dialog_EditTendReminder(PawnTendReminder reminder)
        {
            this.reminder = reminder ?? throw new ArgumentNullException(nameof(reminder));
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
            float contentWidth = inRect.width - (LeftMargin * 2);
            float currentY = 10f;

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(LeftMargin, currentY, contentWidth, ControlHeight), "Edit Tend Reminder");
            Text.Font = GameFont.Small;
            currentY += ControlHeight + 20f;

            Pawn pawn = reminder.FindPawn();
            Hediff hediff = reminder.FindHediff(pawn);

            if (pawn != null && hediff != null)
            {
                Widgets.Label(new Rect(LeftMargin, currentY, contentWidth, ControlHeight), $"Tending reminder for {pawn.LabelShort}'s {hediff.Label}");
                currentY += ControlHeight + 10f;

                if (hediff is HediffWithComps hwc)
                {
                    var tendComp = hwc.TryGetComp<HediffComp_TendDuration>();
                    if (tendComp != null)
                    {
                        Widgets.Label(new Rect(LeftMargin, currentY, contentWidth, ControlHeight), $"Current tend quality: {tendComp.tendQuality:P0}");
                        currentY += ControlHeight + 10f;

                        float hoursLeft = tendComp.tendTicksLeft / (float)GenDate.TicksPerHour;
                        Widgets.Label(new Rect(LeftMargin, currentY, contentWidth, ControlHeight), $"Time until next tending needed: {hoursLeft:F1} hours");
                        currentY += ControlHeight + 10f;
                    }
                }

                if (hediff is HediffWithComps diseaseHediff && diseaseHediff.TryGetComp<HediffComp_Immunizable>() != null)
                {
                    Rect checkboxRect = new Rect(LeftMargin, currentY, contentWidth, ControlHeight);
                    Widgets.CheckboxLabeled(checkboxRect, "Remove reminder when immunity is reached", ref removeOnImmunity);
                    currentY += ControlHeight + 10f;
                }
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
                reminder.removeOnImmunity = removeOnImmunity;
                Messages.Message("Tend reminder updated", MessageTypeDefOf.TaskCompletion, false);
                Close();
                Find.WindowStack.Add(new Dialog_ViewReminders());
            }
        }
    }
}
