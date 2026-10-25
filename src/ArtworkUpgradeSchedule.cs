using System;
namespace OneTaskBar
{
    internal sealed class ArtworkUpgradeSchedule
    {
        int attempts;DateTime next=DateTime.MinValue;
        internal bool Due(DateTime now){return attempts<3&&now>=next;}
        internal bool Pending(DateTime now){return attempts>0&&Due(now);}
        internal void Attempt(DateTime now){attempts++;next=attempts>=3?DateTime.MaxValue:now.AddSeconds(attempts==1?2:6);}
        internal void Complete(){attempts=3;next=DateTime.MaxValue;}
        internal void Clear(){attempts=0;next=DateTime.MinValue;}
    }
}
