using UnityEngine;
using Verse;
using RimWorld;

namespace Riminder
{
    public static class RiminderUIHelper
    {
        
        public const float LabelWidth = 90f;
        public const float InputWidth = 50f;
        public const float ButtonWidth = 25f;
        public const float RowHeight = 30f;
        public const float Gap = 5f;
        public const float LeftMargin = 20f;
        public const float SectionSpacing = 15f;
        public const float RadioButtonSize = 24f;
        
        
        public static float ControlGroupWidth => LabelWidth + (ButtonWidth * 2) + InputWidth + (Gap * 3);

        public static void DrawLabeledField(float x, float y, string label, Rect fieldRect, float labelWidth)
        {
            var prevAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect labelRect = new Rect(x, y, labelWidth, RowHeight);
            Widgets.Label(labelRect, label);
            Text.Anchor = prevAnchor;
        }

        public static void DrawLabeledTextField(float x, float y, float width, string label, ref string text, float? labelWidthOverride = null, float? inputWidthOverride = null, float inputXOffset = 0f)
        {
            float labelWidth = labelWidthOverride ?? LabelWidth;
            float inputWidth = inputWidthOverride ?? (width - labelWidth - Gap);
            DrawLabeledField(x, y, label, new Rect(x + labelWidth + Gap, y, inputWidth, RowHeight), labelWidth);
            text = Widgets.TextField(new Rect(x + labelWidth + Gap + inputXOffset, y, inputWidth, RowHeight), text);
        }

        public static void DrawLabeledTextArea(float x, float y, float width, string label, ref string text, float areaHeight = 60f, float? labelWidthOverride = null, float? inputWidthOverride = null, float inputXOffset = 0f)
        {
            float labelWidth = labelWidthOverride ?? LabelWidth;
            float inputWidth = inputWidthOverride ?? (width - labelWidth - Gap);
            DrawLabeledField(x, y, label, new Rect(x + labelWidth + Gap, y, inputWidth, areaHeight), labelWidth);
            text = Widgets.TextArea(new Rect(x + labelWidth + Gap + inputXOffset, y, inputWidth, areaHeight), text);
        }

        public static void DrawLabeledIntRow(
            float x, float y,
            string label,
            ref int value,
            ref string buffer,
            int min = 0,
            int max = 999,
            bool centerLabel = false)
        {
            var prevAnchor = Text.Anchor;
            
            Rect labelRect = new Rect(x, y, LabelWidth, RowHeight);
            if (centerLabel)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
            }
            Widgets.Label(labelRect, label);
            if (centerLabel)
            {
                Text.Anchor = prevAnchor;
            }

            float curX = x + LabelWidth + Gap;
            if (Widgets.ButtonText(new Rect(curX, y, ButtonWidth, RowHeight), "-"))
            {
                value = Mathf.Max(min, value - 1);
                buffer = value.ToString();
            }

            curX += ButtonWidth + Gap;
            Rect inputRect = new Rect(curX, y, InputWidth, RowHeight);
            buffer = Widgets.TextField(inputRect, buffer);
            if (int.TryParse(buffer, out int parsed))
            {
                parsed = Mathf.Clamp(parsed, min, max);
                value = parsed;
                buffer = parsed.ToString();
            }

            curX += InputWidth + Gap;
            if (Widgets.ButtonText(new Rect(curX, y, ButtonWidth, RowHeight), "+"))
            {
                value = Mathf.Min(max, value + 1);
                buffer = value.ToString();
            }
        }

        public static void DrawSectionHeader(float x, float y, float width, string text)
        {
            var prevFont = Text.Font;
            var prevAnchor = Text.Anchor;
            
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            
            Widgets.Label(new Rect(x, y, width, RowHeight), text);
            
            Text.Font = prevFont;
            Text.Anchor = prevAnchor;
        }

        public static void DrawFrequencySelector(
            float x, float y, float width,
            ref ReminderFrequency selectedFrequency)
        {
            float radioSize = RadioButtonSize;
            float labelWidth = 110f;
            float labelOffset = 25f;  
            
            
            Rect bgRect = new Rect(x, y, width, RowHeight);
            Widgets.DrawBoxSolid(bgRect, new Color(0.2f, 0.2f, 0.2f, 0.3f));
            
            
            float timeFieldWidth = ButtonWidth + InputWidth + ButtonWidth + (Gap * 2);
            float col1Center = x + LabelWidth + (timeFieldWidth / 2) + Gap;
            float col2Center = col1Center + timeFieldWidth + (Gap * 4) + LabelWidth;
            
            
            bool oneTimeSelected = selectedFrequency == ReminderFrequency.OneTime;
            float radioY = y + (RowHeight - radioSize) / 2;
            
            
            Rect oneTimeRadioRect = new Rect(col1Center - (radioSize / 2), radioY, radioSize, radioSize);
            
            Rect oneTimeRect = new Rect(col1Center - labelWidth - labelOffset, y, labelWidth, RowHeight);
            
            
            Rect oneTimeClickArea = new Rect(oneTimeRect.x, y, labelWidth + radioSize + labelOffset, RowHeight);
            if (Widgets.ButtonInvisible(oneTimeClickArea))
            {
                selectedFrequency = ReminderFrequency.OneTime;
            }
            
            
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(oneTimeRect, "One Time");
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.RadioButton(oneTimeRadioRect.position, oneTimeSelected);
            
            
            bool repeatedSelected = selectedFrequency == ReminderFrequency.Custom;
            
            
            Rect repeatedRadioRect = new Rect(col2Center - (radioSize / 2), radioY, radioSize, radioSize);
            
            Rect repeatedRect = new Rect(col2Center - labelWidth - labelOffset, y, labelWidth, RowHeight);
            
            
            Rect repeatedClickArea = new Rect(repeatedRect.x, y, labelWidth + radioSize + labelOffset, RowHeight);
            if (Widgets.ButtonInvisible(repeatedClickArea))
            {
                selectedFrequency = ReminderFrequency.Custom;
            }
            
            
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(repeatedRect, "Repeated");
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.RadioButton(repeatedRadioRect.position, repeatedSelected);
        }

        public static void DrawBottomButtons(
            Rect inRect,
            string leftButtonText,
            string rightButtonText,
            out bool leftClicked,
            out bool rightClicked,
            float buttonWidth = 120f,
            float buttonHeight = 40f)
        {
            float bottomY = inRect.height - buttonHeight - 15f;
            
            leftClicked = Widgets.ButtonText(
                new Rect(inRect.width / 2 - buttonWidth - 10f, bottomY, buttonWidth, buttonHeight),
                leftButtonText);
                
            rightClicked = Widgets.ButtonText(
                new Rect(inRect.width / 2 + 10f, bottomY, buttonWidth, buttonHeight),
                rightButtonText);
        }
    }
} 