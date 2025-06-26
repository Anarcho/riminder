using UnityEngine;
using RimWorld;
using Verse;

namespace Riminder
{
    [StaticConstructorOnStartup]
    public class MainButtonWorker_Riminder : MainButtonWorker
    {
        public override void Activate()
        {
            Find.WindowStack.Add(new Dialog_ViewReminders());
        }
    }
} 