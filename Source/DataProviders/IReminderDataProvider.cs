using System;
using Verse;

namespace Riminder
{
    public interface IReminderDataProvider
    {
        string GetLabel();
        string GetDescription();
        string GetTimeLeftString();
        float GetProgress();
        bool NeedsAttention();
        void Refresh();
        
        
        void ExposeData();
    }
} 