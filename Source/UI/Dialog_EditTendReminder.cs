using System;
using UnityEngine;
using Verse;
using RimWorld;
using System.Linq;

namespace Riminder
{
    public class Dialog_EditTendReminder : Window
    {
        private readonly BaseReminder reminder;
        private readonly Pawn pawn;
        private float calculatedHeight;

        public Dialog_EditTendReminder(BaseReminder reminder)
        {
            this.reminder = reminder ?? throw new ArgumentNullException(nameof(reminder));
            
            if (reminder.GetLabel().Contains("'s"))
            {
                string pawnName = reminder.GetLabel().Split('\'')[0].Replace("Tend ", "").Trim();
                this.pawn = Find.CurrentMap?.mapPawns?.AllPawns
                    .Where(p => p.LabelShort == pawnName)
                    .FirstOrDefault();
            }

            forcePause = false;
            doCloseX = true;
            doCloseButton = false;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = false;
            preventCameraMotion = false;
        }

        public override Vector2 InitialSize
        {
            get
            {
                float baseHeight = 120f;
                float heightPerCondition = RiminderUIHelper.RowHeight + RiminderUIHelper.Gap;
                float buttonAreaHeight = RiminderUIHelper.RowHeight + RiminderUIHelper.Gap * 4;
                float bottomPadding = 20f;
                
                int tendableConditions = 0;
                float maxTextWidth = 450f; 
                
                if (pawn != null)
                {
                    foreach (var h in pawn.health.hediffSet.hediffs)
                    {
                        if (!h.def.tendable || h.IsPermanent()) continue;
                        if (h.def.defName.Contains("Missing") || h.Label.ToLower().Contains("missing")) continue;
                        if (h.def.defName.Contains("Removed") || h.Label.ToLower().Contains("removed")) continue;
                        
                        if (h is HediffWithComps hwc && hwc.TryGetComp<HediffComp_TendDuration>() != null)
                        {
                            var tendComp = hwc.TryGetComp<HediffComp_TendDuration>();
                            if (tendComp != null && (!tendComp.TProps.TendIsPermanent || !tendComp.IsTended))
                            {
                                tendableConditions++;
                                
                                string tendInfo;
                                if (tendComp.IsTended)
                                {
                                    float hoursLeft = tendComp.tendTicksLeft / (float)GenDate.TicksPerHour;
                                    tendInfo = hoursLeft > 0
                                        ? $" - Quality: {tendComp.tendQuality:P0}, Next tend in: {hoursLeft:F1}h"
                                        : $" - Quality: {tendComp.tendQuality:P0}, Ready to tend";
                                }
                                else
                                {
                                    tendInfo = " - Needs tending now!";
                                }
                                
                                string fullText = $"{h.Label}{tendInfo}";
                                float textWidth = Text.CalcSize(fullText).x + 60f;
                                maxTextWidth = Math.Max(maxTextWidth, textWidth);
                            }
                        }
                    }
                }
                
                if (tendableConditions == 0) tendableConditions = 1;
                calculatedHeight = baseHeight + (tendableConditions * heightPerCondition) + buttonAreaHeight + bottomPadding;
                
                return new Vector2(maxTextWidth, calculatedHeight);
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            float contentWidth = inRect.width - (RiminderUIHelper.LeftMargin * 2);
            float currentY = 10f;

            
            RiminderUIHelper.DrawSectionHeader(0, currentY, inRect.width, "Tending Details");
            currentY += RiminderUIHelper.RowHeight + RiminderUIHelper.SectionSpacing;

            
            float contentBoxHeight = calculatedHeight - currentY - (RiminderUIHelper.RowHeight + RiminderUIHelper.Gap * 6);
            
            
            Rect contentBoxBg = new Rect(0, currentY, inRect.width, contentBoxHeight);
            Widgets.DrawBoxSolid(contentBoxBg, new Color(0.2f, 0.2f, 0.2f, 0.3f));

            if (pawn != null)
            {
                
                Text.Font = GameFont.Medium;
                Rect pawnInfoRect = new Rect(RiminderUIHelper.LeftMargin, currentY + 5f, contentWidth, RiminderUIHelper.RowHeight);
                Widgets.Label(pawnInfoRect, $"Tending status for {pawn.LabelShort}");
                Text.Font = GameFont.Small;
                currentY += RiminderUIHelper.RowHeight + RiminderUIHelper.Gap * 2;

                
                Text.Font = GameFont.Small;
                bool foundAnyTendableCondition = false;
                
                foreach (var h in pawn.health.hediffSet.hediffs)
                {
                    if (!h.def.tendable || h.IsPermanent()) continue;
                    if (h.def.defName.Contains("Missing") || h.Label.ToLower().Contains("missing")) continue;
                    if (h.def.defName.Contains("Removed") || h.Label.ToLower().Contains("removed")) continue;
                    
                    if (h is HediffWithComps hwc)
                    {
                        var tendComp = hwc.TryGetComp<HediffComp_TendDuration>();
                        if (tendComp != null && (!tendComp.TProps.TendIsPermanent || !tendComp.IsTended))
                        {
                            string tendInfo;
                            if (tendComp.IsTended)
                            {
                                float hoursLeft = tendComp.tendTicksLeft / (float)GenDate.TicksPerHour;
                                tendInfo = hoursLeft > 0
                                    ? $" - Quality: {tendComp.tendQuality:P0}, Next tend in: {hoursLeft:F1}h"
                                    : $" - Quality: {tendComp.tendQuality:P0}, Ready to tend";
                            }
                            else
                            {
                                tendInfo = " - Needs tending now!";
                            }

                            string fullText = $"{h.Label}{tendInfo}";
                            
                            GUI.color = tendComp.IsTended ? Color.yellow : Color.red;
                            Widgets.Label(new Rect(RiminderUIHelper.LeftMargin + 20f, currentY, contentWidth - 40f, RiminderUIHelper.RowHeight),
                                fullText);
                            GUI.color = Color.white;

                            currentY += RiminderUIHelper.RowHeight + RiminderUIHelper.Gap;
                            foundAnyTendableCondition = true;
                        }
                    }
                }
                
                if (!foundAnyTendableCondition)
                {
                    Rect noConditionsRect = new Rect(RiminderUIHelper.LeftMargin, currentY, contentWidth, RiminderUIHelper.RowHeight);
                    GUI.color = Color.gray;
                    Widgets.Label(noConditionsRect, "No conditions currently require tending.");
                    GUI.color = Color.white;
                }
            }
            else
            {
                
                Rect errorRect = new Rect(RiminderUIHelper.LeftMargin, currentY + RiminderUIHelper.RowHeight * 0.5f, 
                    contentWidth, RiminderUIHelper.RowHeight);
                GUI.color = Color.red;
                Widgets.Label(errorRect, "Error: Pawn not found");
                GUI.color = Color.white;
            }

            
            float buttonWidth = 100f;
            float spacing = 10f;
            float totalButtonWidth = (buttonWidth * 2) + spacing;
            float startX = (inRect.width - totalButtonWidth) / 2;
            float buttonY = inRect.height - RiminderUIHelper.RowHeight - RiminderUIHelper.Gap * 2;
            
            if (Widgets.ButtonText(new Rect(startX, buttonY, buttonWidth, RiminderUIHelper.RowHeight), "Close"))
            {
                Close();
                Find.WindowStack.Add(new Dialog_ViewReminders());
            }
                
            if (Widgets.ButtonText(new Rect(startX + buttonWidth + spacing, buttonY, buttonWidth, RiminderUIHelper.RowHeight), "Jump to Pawn") && pawn != null)
            {
                CameraJumper.TryJump(pawn);
                Close();
                Find.WindowStack.Add(new Dialog_ViewReminders());
            }
        }
    }
}
