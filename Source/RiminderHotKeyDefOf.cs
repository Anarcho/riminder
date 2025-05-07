using RimWorld;
using Verse;

namespace Riminder
{
    [DefOf]
    public static class RiminderHotKeyDefOf
    {
        public static KeyBindingDef OpenRiminderDialog;

        static RiminderHotKeyDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(RiminderHotKeyDefOf));
        }
    }
} 