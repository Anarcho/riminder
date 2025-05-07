# Riminder
A RimWorld 1.5 mod that helps you set and manage reminders while playing.

## Features

- Create one-time or recurring reminders (daily, weekly, monthly, quarterly, yearly)
- Get notified via the game's alert system when a reminder triggers
- Option to pause the game when a reminder triggers
- Easy management of all reminders through a dedicated UI
- Filter reminders by text search
- Visual progress indicator for each reminder
- Hotkey support (Alt+R) for quick acces - TODO: Fix this as it is not working
- **NEW: Set automatic reminders to tend to colonists with medical conditions**

## Usage

1. Click on the "Reminders" button in the bottom menu bar (or press Alt+R)
2. Click "Add New" to create a new reminder
3. Fill in the title, description, time period, and frequency
4. Click "Create" to save the reminder

You can view, edit, and delete your reminders from the main Reminders screen at any time.

### Tending Reminders

To set a reminder for tending to a colonist with a medical condition:
1. Select a colonist with a tendable medical condition
2. Right-click on them and choose "Create tend reminder for [Name]" option
3. Or open their health tab and click the "Set tend reminder" button
4. Select the specific condition from the list
5. Adjust the reminder interval if needed
6. Click "Create" to save the reminder

The mod will automatically notify you when it's time to tend to the colonist again. The reminder will trigger based on the condition's tend duration and severity.

## Settings

In the mod settings panel, you can configure:
- Whether to pause the game when a reminder triggers (default: off)
TODO: Add more settings if required.

## Compatibility
- Compatible with existing saves - no need to start a new colony
- Requires Harmony
- Works with RimWorld 1.5

## Todo:
- Fix keymap for opening reminders
- Add more relevant settings
- ~~Add additional functionality to create reminders from various sources - i.e pawn with hediff -> trigger reminder to tend.~~
- verify performance on large quantities of reminder.

## License

MIT License 