using RimWorld;
using Verse;

namespace Riminder
{
    [DefOf]
    public static class ReminderDefOf
    {
        public static ReminderDef TendReminder;
        
        static ReminderDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(ReminderDefOf));
        }
    }
} 