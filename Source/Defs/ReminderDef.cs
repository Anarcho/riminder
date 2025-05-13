using System;
using Verse;

namespace Riminder
{
    public class ReminderDef : Def
    {
        public Type reminderClass;
        public Type dataProviderClass;
        public bool canAutoCreate = false;
        public string iconPath;

        public override string ToString()
        {
            return $"{defName} (ReminderDef)";
        }
    }
}