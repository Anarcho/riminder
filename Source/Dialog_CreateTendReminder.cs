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
            
            // Find all tendable hediffs on the pawn
            tendableHediffs = pawn.health.hediffSet.hediffs
                .Where(h => NeedsTending(h) && !h.def.defName.Contains("Removed"))
                .ToList();
            
            // Default to the first tendable hediff if available
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
                
                // If no tendable hediffs found
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
                
                // Hediff selection section
                Widgets.Label(new Rect(LeftMargin, currentY, contentWidth, ControlHeight), "Select condition to monitor:");
                currentY += ControlHeight + 5f;
                
                // Fixed height list, but slightly smaller than before
                float hediffListHeight = 130f;
                Rect hediffListRect = new Rect(LeftMargin, currentY, contentWidth, hediffListHeight);
                Widgets.DrawBox(hediffListRect);
                
                // Create a scrollable view for the hediffs
                Rect scrollViewRect = hediffListRect.ContractedBy(1f);
                
                // Calculate the needed height for all hediffs without gaps
                float totalContentHeight = tendableHediffs.Count * ControlHeight;
                
                // Create the view rect - ensure it's exactly the right size for the content
                Rect viewRect = new Rect(0, 0, scrollViewRect.width - 16f, totalContentHeight);
                
                // Begin scrolling
                Widgets.BeginScrollView(scrollViewRect, ref scrollPosition, viewRect, true);
                
                for (int i = 0; i < tendableHediffs.Count; i++)
                {
                    Hediff hediff = tendableHediffs[i];
                    Rect rowRect = new Rect(0, i * ControlHeight, viewRect.width, ControlHeight);
                    
                    if (Mouse.IsOver(rowRect))
                    {
                        Widgets.DrawHighlight(rowRect);
                    }
                    
                    // Draw selection indicator
                    if (selectedHediff == hediff)
                    {
                        Widgets.DrawHighlightSelected(rowRect);
                    }
                    
                    // Get tend quality info if available
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
                    
                    // Label with severity and tend quality if available
                    string label = $"{hediff.Label} - {hediff.Severity:F2} severity{tendInfo}";
                    
                    // Draw the label with proper text size and clipping
                    Rect labelRect = rowRect.ContractedBy(5f);
                    
                    // Save current text settings
                    GameFont prevFont = Text.Font;
                    bool prevWordWrap = Text.WordWrap;
                    TextAnchor prevAnchor = Text.Anchor;
                    
                    // Apply settings for this label
                    Text.Font = GameFont.Small;
                    Text.WordWrap = false;
                    Text.Anchor = TextAnchor.MiddleLeft;
                    
                    // Measure text width and clip if needed
                    float width = Text.CalcSize(label).x;
                    if (width > labelRect.width)
                    {
                        int cutoff = (int)(label.Length * (labelRect.width / width)) - 3;
                        if (cutoff > 0 && cutoff < label.Length)
                        {
                            label = label.Substring(0, cutoff) + "...";
                        }
                    }
                    
                    // Draw the text
                    Widgets.Label(labelRect, label);
                    
                    // Restore text settings
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

                // Add immunity checkbox if the selected hediff is a disease
                if (selectedHediff != null && selectedHediff is HediffWithComps diseaseHediff && diseaseHediff.TryGetComp<HediffComp_Immunizable>() != null)
                {
                    Rect immunityRect = new Rect(LeftMargin, currentY, contentWidth, ControlHeight);
                    Widgets.CheckboxLabeled(immunityRect, "Remove reminder when immunity is reached", ref removeOnImmunity);
                    currentY += ControlHeight + 10f;
                }
                
                // Confirmation section
                string confirmationText = selectedHediff != null 
                    ? $"This will create a reminder that will automatically notify you when {pawn.LabelShort}'s {selectedHediff.Label} needs tending."
                    : "Please select a condition first.";
                
                // Allow for multi-line text with word wrap
                float textHeight = Text.CalcHeight(confirmationText, contentWidth);
                Rect confirmationRect = new Rect(LeftMargin, currentY, contentWidth, textHeight);
                Widgets.Label(confirmationRect, confirmationText);
                
                // Buttons at bottom of window
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

            Messages.Message($"Created tend reminder for {pawn.LabelShort}'s {selectedHediff.Label}", MessageTypeDefOf.TaskCompletion, false);
            Close();
        }

        private bool NeedsTending(Hediff hediff)
        {
            if (!hediff.def.tendable) return false;
            
            // Scars and permanent conditions don't need tending
            if (hediff.IsPermanent()) return false;
            
            // Skip "removed" hediffs
            if (hediff.def.defName.Contains("Removed") || hediff.Label.Contains("removed")) return false;
            
            // Check if it has a tend duration component
            if (hediff is HediffWithComps hwc)
            {
                var tendComp = hwc.TryGetComp<HediffComp_TendDuration>();
                if (tendComp != null)
                {
                    // Allow creating reminders for ANY condition with a tend component,
                    // whether it's currently tended or not, as eventually it will need tending again
                    return true;
                }
            }
            
            // For injuries, check if they're still healing and tendable
            if (hediff is Hediff_Injury injury)
            {
                // Allow any injury with severity > 0, even if currently tended
                return injury.Severity > 0;
            }
            
            return false;
        }
    }
} 