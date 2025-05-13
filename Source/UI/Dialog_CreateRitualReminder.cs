using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;

namespace Riminder
{
    public class Dialog_CreateRitualReminder : Window
    {
        private Precept_Ritual selectedRitual;
        private Vector2 scrollPosition = Vector2.zero;

        public Dialog_CreateRitualReminder()
        {
            forcePause = false;
            doCloseX = true;
            doCloseButton = false;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = false;
            preventCameraMotion = false;
        }

        public override Vector2 InitialSize => new Vector2(450f, 400f);

        public override void DoWindowContents(Rect inRect)
        {
            float contentWidth = inRect.width - (RiminderUIHelper.LeftMargin * 2);
            float currentY = 10f;

            RiminderUIHelper.DrawSectionHeader(0, currentY, inRect.width, "Create Ritual Reminder");
            currentY += RiminderUIHelper.RowHeight + RiminderUIHelper.SectionSpacing;

            var rituals = GetAvailableRituals();
            if (rituals.Any())
            {
                Rect listRect = new Rect(RiminderUIHelper.LeftMargin, currentY, contentWidth, 200f);
                Rect viewRect = new Rect(0f, 0f, contentWidth - 16f, rituals.Count * 30f);
                Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
                float listY = 0f;
                foreach (var ritual in rituals)
                {
                    Rect rowRect = new Rect(0f, listY, viewRect.width, 24f);
                    if (Widgets.RadioButtonLabeled(rowRect, ritual.LabelCap, selectedRitual == ritual))
                    {
                        selectedRitual = ritual;
                    }
                    listY += 26f;
                }
                Widgets.EndScrollView();
                currentY += 210f;
            }
            else
            {
                Rect noRitualsRect = new Rect(RiminderUIHelper.LeftMargin, currentY, contentWidth, RiminderUIHelper.RowHeight);
                GUI.color = Color.gray;
                Widgets.Label(noRitualsRect, "No eligible rituals found for your ideology.");
                GUI.color = Color.white;
                currentY += RiminderUIHelper.RowHeight + RiminderUIHelper.Gap;
            }

            RiminderUIHelper.DrawBottomButtons(inRect, "Cancel", "Create", out bool cancelClicked, out bool createClicked, 100f, RiminderUIHelper.RowHeight);
            if (cancelClicked)
            {
                Close();
            }
            if (createClicked)
            {
                TryCreateReminder();
            }
        }

        private List<Precept_Ritual> GetAvailableRituals()
        {
            var ideo = Faction.OfPlayer.ideos?.PrimaryIdeo;
            if (ideo == null) return new List<Precept_Ritual>();
            return ideo.PreceptsListForReading
                .OfType<Precept_Ritual>()
                .Where(r => !IsFuneral(r) && IsColonistRitual(r))
                .ToList();
        }

        private bool IsFuneral(Precept_Ritual ritual)
        {
            return ritual.def.ritualPatternBase?.defName.ToLower().Contains("funeral") == true
                || ritual.def.label.ToLower().Contains("funeral");
        }

        private bool IsColonistRitual(Precept_Ritual ritual)
        {
            return ritual.ritualOnlyForIdeoMembers;
        }

        private void TryCreateReminder()
        {
            if (selectedRitual == null)
            {
                Messages.Message("Please select a ritual", MessageTypeDefOf.RejectInput, false);
                return;
            }
            var reminder = ReminderFactory.CreateRitualReminder(selectedRitual);
            RiminderManager.AddReminder(reminder);
            Messages.Message("Ritual reminder created", MessageTypeDefOf.TaskCompletion, false);
            Close();
        }
    }
}
