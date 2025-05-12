using RimWorld;
using Verse;

namespace Riminder
{
    [RimWorld.DefOf]
    public static class ReminderDefOf
    {
        public static ReminderDef TendReminder;
        public static KeyBindingDef Riminder_OpenReminders;
        
        static ReminderDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(ReminderDefOf));
        }
    }
} 