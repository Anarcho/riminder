using System;
using UnityEngine;
using Verse;
using RimWorld;
using System.Linq;

namespace Riminder
{
    public class Dialog_EditRitualReminder : Window
    {
        private readonly RitualReminder reminder;
        private readonly Precept_Ritual ritual;
        private float calculatedHeight;

        public Dialog_EditRitualReminder(RitualReminder reminder)
        {
            this.reminder = reminder ?? throw new ArgumentNullException(nameof(reminder));
            this.ritual = this.reminder.RitualData?.GetRitual();
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
            float contentWidth = inRect.width - (RiminderUIHelper.LeftMargin * 2);
            float currentY = 10f;
            RiminderUIHelper.DrawSectionHeader(0, currentY, inRect.width, "Ritual Reminder Details");
            currentY += RiminderUIHelper.RowHeight + RiminderUIHelper.SectionSpacing;
            if (ritual != null)
            {
                Rect ritualLabelRect = new Rect(RiminderUIHelper.LeftMargin, currentY, contentWidth, RiminderUIHelper.RowHeight);
                Widgets.Label(ritualLabelRect, $"Ritual: {ritual.LabelCap}");
                currentY += RiminderUIHelper.RowHeight + RiminderUIHelper.Gap;
                Rect descRect = new Rect(RiminderUIHelper.LeftMargin, currentY, contentWidth, 80f);
                Widgets.Label(descRect, ritual.ritualExplanation ?? ritual.def.description);
                currentY += 90f;
                Rect cooldownRect = new Rect(RiminderUIHelper.LeftMargin, currentY, contentWidth, RiminderUIHelper.RowHeight);
                string cooldown = ritual.isAnytime && ritual.def.useRepeatPenalty ? $"Cooldown: {ritual.RepeatPenaltyTimeLeft}" : "";
                Widgets.Label(cooldownRect, cooldown);
                currentY += RiminderUIHelper.RowHeight + RiminderUIHelper.Gap;
            }
            else
            {
                Rect errorRect = new Rect(RiminderUIHelper.LeftMargin, currentY, contentWidth, RiminderUIHelper.RowHeight);
                GUI.color = Color.red;
                Widgets.Label(errorRect, "Error: Ritual not found");
                GUI.color = Color.white;
            }
            float buttonWidth = 100f;
            float spacing = 10f;
            float totalButtonWidth = buttonWidth + spacing;
            float startX = (inRect.width - totalButtonWidth) / 2;
            float buttonY = inRect.height - RiminderUIHelper.RowHeight - RiminderUIHelper.Gap * 2;
            if (Widgets.ButtonText(new Rect(startX, buttonY, buttonWidth, RiminderUIHelper.RowHeight), "Close"))
            {
                Close();
                Find.WindowStack.Add(new Dialog_ViewReminders());
            }
        }
    }
}
