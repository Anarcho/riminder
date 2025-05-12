using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;

namespace Riminder
{
    public class Dialog_CreateTendReminder : Window
    {
        private readonly Pawn pawn;
        private HediffWithComps selectedHediff;
        private Vector2 scrollPosition = Vector2.zero;
        private bool removeOnImmunity = true;

        public Dialog_CreateTendReminder(Pawn pawn)
        {
            this.pawn = pawn ?? throw new ArgumentNullException(nameof(pawn));

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

            
            RiminderUIHelper.DrawSectionHeader(0, currentY, inRect.width, "Create Tend Reminder");
            currentY += RiminderUIHelper.RowHeight + RiminderUIHelper.SectionSpacing;

            
            Rect pawnInfoRect = new Rect(RiminderUIHelper.LeftMargin, currentY, contentWidth, RiminderUIHelper.RowHeight);
            Widgets.Label(pawnInfoRect, $"Create tending reminder for {pawn.LabelShort}");
            currentY += RiminderUIHelper.RowHeight + RiminderUIHelper.Gap;

            
            var tendableHediffs = pawn.health.hediffSet.hediffs
                .Where(h => h.def.tendable && !h.IsPermanent())
                .OfType<HediffWithComps>()
                .Where(h => h.TryGetComp<HediffComp_TendDuration>() != null)
                .ToList();

            if (tendableHediffs.Any())
            {
                Rect listRect = new Rect(RiminderUIHelper.LeftMargin, currentY, contentWidth, 200f);
                Rect viewRect = new Rect(0f, 0f, contentWidth - 16f, tendableHediffs.Count * 30f);

                Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
                float listY = 0f;

                foreach (var hediff in tendableHediffs)
                {
                    Rect rowRect = new Rect(0f, listY, viewRect.width, 24f);
                    if (Widgets.RadioButtonLabeled(rowRect, hediff.Label, selectedHediff == hediff))
                    {
                        selectedHediff = hediff;
                    }
                    listY += 26f;
                }

                Widgets.EndScrollView();
                currentY += 210f;

                
                if (selectedHediff?.TryGetComp<HediffComp_Immunizable>() != null)
                {
                    Rect immunityRect = new Rect(RiminderUIHelper.LeftMargin, currentY, contentWidth, RiminderUIHelper.RowHeight);
                    Widgets.CheckboxLabeled(immunityRect, "Remove reminder when immunity reached", ref removeOnImmunity);
                    currentY += RiminderUIHelper.RowHeight + RiminderUIHelper.Gap;
                }
            }
            else
            {
                Rect noConditionsRect = new Rect(RiminderUIHelper.LeftMargin, currentY, contentWidth, RiminderUIHelper.RowHeight);
                GUI.color = Color.gray;
                Widgets.Label(noConditionsRect, "No conditions currently require tending.");
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

        private void TryCreateReminder()
        {
            if (selectedHediff == null)
            {
                Messages.Message("Please select a condition", MessageTypeDefOf.RejectInput, false);
                return;
            }

            string label = $"Tend {pawn.LabelShort}'s {selectedHediff.Label}";
            string description = $"Tend {pawn.LabelShort}'s {selectedHediff.Label} condition";

            var reminder = new TendReminder(pawn, selectedHediff, removeOnImmunity);
            RiminderManager.AddReminder(reminder);

            Messages.Message("Tend reminder created", MessageTypeDefOf.TaskCompletion, false);
            Close();
        }
    }
}
