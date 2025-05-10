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
        private Pawn pawn;
        private Hediff selectedHediff;
        private List<Hediff> tendableHediffs = new List<Hediff>();
        private Vector2 scrollPosition = Vector2.zero;
        private bool removeOnImmunity = false;

        private const float LeftMargin = 20f;
        private const float ControlHeight = 30f;
        private const float SmallButtonSize = 25f;
        private const float ButtonGap = 5f;

        public Dialog_CreateTendReminder(Pawn pawn)
        {
            this.pawn = pawn;
            forcePause = false;
            doCloseX = true;
            doCloseButton = false;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = false;
            preventCameraMotion = false;

            tendableHediffs = pawn.health.hediffSet.hediffs
                .Where(h => NeedsTending(h) && !h.def.defName.Contains("Removed"))
                .ToList();

            if (tendableHediffs.Count > 0)
            {
                selectedHediff = tendableHediffs[0];
            }
        }

        public override Vector2 InitialSize => new Vector2(475f, 500f);

        public override void DoWindowContents(Rect inRect)
        {
            bool originalWordWrap = Text.WordWrap;
            Text.WordWrap = true;

            try
            {
                float contentWidth = inRect.width - (LeftMargin * 2);

                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(LeftMargin, 10f, contentWidth, ControlHeight), $"Create Tend Reminder for {pawn.LabelShort}");
                Text.Font = GameFont.Small;

                float currentY = 50f;

                if (tendableHediffs.Count == 0)
                {
                    Widgets.Label(new Rect(LeftMargin, currentY, contentWidth, ControlHeight),
                        $"{pawn.LabelShort} has no conditions that need tending.");

                    currentY += ControlHeight + 20f;

                    if (Widgets.ButtonText(new Rect(inRect.width / 2 - 50f, currentY, 100f, ControlHeight), "Close"))
                    {
                        Close();
                    }

                    return;
                }

                Widgets.Label(new Rect(LeftMargin, currentY, contentWidth, ControlHeight), "Select condition to monitor:");
                currentY += ControlHeight + 5f;

                float hediffListHeight = 130f;
                Rect hediffListRect = new Rect(LeftMargin, currentY, contentWidth, hediffListHeight);
                Widgets.DrawBox(hediffListRect);

                Rect scrollViewRect = hediffListRect.ContractedBy(1f);

                float totalContentHeight = tendableHediffs.Count * ControlHeight;

                Rect viewRect = new Rect(0, 0, scrollViewRect.width - 16f, totalContentHeight);

                Widgets.BeginScrollView(scrollViewRect, ref scrollPosition, viewRect, true);

                for (int i = 0; i < tendableHediffs.Count; i++)
                {
                    Hediff hediff = tendableHediffs[i];
                    Rect rowRect = new Rect(0, i * ControlHeight, viewRect.width, ControlHeight);

                    if (Mouse.IsOver(rowRect))
                    {
                        Widgets.DrawHighlight(rowRect);
                    }

                    if (selectedHediff == hediff)
                    {
                        Widgets.DrawHighlightSelected(rowRect);
                    }

                    string tendInfo = "";
                    float? hoursLeft = null;
                    if (hediff is HediffWithComps hwc)
                    {
                        var tendComp = hwc.TryGetComp<HediffComp_TendDuration>();
                        if (tendComp != null)
                        {
                            tendInfo = $" (Quality: {tendComp.tendQuality:P0}";
                            hoursLeft = tendComp.tendTicksLeft / (float)GenDate.TicksPerHour;
                            tendInfo += hoursLeft > 0 ? $", {hoursLeft:F1}h left)" : ", needs tending)";
                        }
                    }

                    string label = $"{hediff.Label} - {hediff.Severity:F2} severity{tendInfo}";

                    Rect labelRect = rowRect.ContractedBy(5f);

                    GameFont prevFont = Text.Font;
                    bool prevWordWrap = Text.WordWrap;
                    TextAnchor prevAnchor = Text.Anchor;

                    Text.Font = GameFont.Small;
                    Text.WordWrap = false;
                    Text.Anchor = TextAnchor.MiddleLeft;

                    float width = Text.CalcSize(label).x;
                    if (width > labelRect.width)
                    {
                        int cutoff = (int)(label.Length * (labelRect.width / width)) - 3;
                        if (cutoff > 0 && cutoff < label.Length)
                        {
                            label = label.Substring(0, cutoff) + "...";
                        }
                    }

                    Widgets.Label(labelRect, label);

                    Text.Font = prevFont;
                    Text.WordWrap = prevWordWrap;
                    Text.Anchor = prevAnchor;

                    if (Widgets.ButtonInvisible(rowRect))
                    {
                        selectedHediff = hediff;
                    }
                }

                Widgets.EndScrollView();

                currentY += hediffListHeight + 20f;

                if (selectedHediff != null && selectedHediff is HediffWithComps diseaseHediff && diseaseHediff.TryGetComp<HediffComp_Immunizable>() != null)
                {
                    Rect immunityRect = new Rect(LeftMargin, currentY, contentWidth, ControlHeight);
                    Widgets.CheckboxLabeled(immunityRect, "Remove reminder when immunity is reached", ref removeOnImmunity);
                    currentY += ControlHeight + 10f;
                }

                string confirmationText = selectedHediff != null
                    ? $"This will create a reminder that will automatically notify you when {pawn.LabelShort}'s {selectedHediff.Label} needs tending."
                    : "Please select a condition first.";

                float textHeight = Text.CalcHeight(confirmationText, contentWidth);
                Rect confirmationRect = new Rect(LeftMargin, currentY, contentWidth, textHeight);
                Widgets.Label(confirmationRect, confirmationText);

                float bottomY = inRect.height - 50f;
                float buttonWidth = 100f;

                if (Widgets.ButtonText(new Rect(inRect.width / 2 - buttonWidth - 5f, bottomY, buttonWidth, ControlHeight), "Cancel"))
                {
                    Close();
                }

                if (Widgets.ButtonText(new Rect(inRect.width / 2 + 5f, bottomY, buttonWidth, ControlHeight), "Create"))
                {
                    TryCreateTendReminder();
                }
            }
            finally
            {
                Text.WordWrap = originalWordWrap;
                Text.Anchor = TextAnchor.UpperLeft;
            }
        }

        private void TryCreateTendReminder()
        {
            if (selectedHediff == null)
            {
                Messages.Message("No condition selected", MessageTypeDefOf.RejectInput, false);
                return;
            }

            var reminder = new PawnTendReminder(pawn, selectedHediff, removeOnImmunity);
            RiminderManager.AddReminder(reminder);

            
            RiminderManager.UpdateTendRemindersForPawn(pawn);

            
            reminder.Trigger();

            Messages.Message($"Created tend reminder for {pawn.LabelShort}'s {selectedHediff.Label}", MessageTypeDefOf.TaskCompletion, false);
            Close();
        }

        private bool NeedsTending(Hediff hediff)
        {
            if (!hediff.def.tendable) return false;

            if (hediff.IsPermanent()) return false;

            if (hediff.def.defName.Contains("Removed") || hediff.Label.Contains("removed")) return false;

            if (hediff is HediffWithComps hwc)
            {
                var tendComp = hwc.TryGetComp<HediffComp_TendDuration>();
                if (tendComp != null)
                {
                    return true;
                }
            }

            if (hediff is Hediff_Injury injury)
            {
                return injury.Severity > 0;
            }

            return false;
        }
    }
}
