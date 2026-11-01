using System;

namespace OneTaskBar
{
    // Spatial sampling is noisy near a contrast boundary. Require a sustained
    // change rather than reversing the palette for a single video frame.
    internal sealed class BackdropInkTone
    {
        internal bool HasSample {get;private set;}
        internal bool Dark {get;private set;}
        internal bool Pending {get{return candidate.HasValue;}}
        bool? candidate;
        double candidateAt;

        internal bool Observe(double luminance,double milliseconds)
        {
            if(Double.IsNaN(luminance)||Double.IsInfinity(luminance)||luminance<0||luminance>1)return false;
            bool next=ChooseDark(luminance,HasSample?(bool?)Dark:null);
            if(!HasSample)
            {
                HasSample=true;
                Dark=next;
                candidate=null;
                return true;
            }
            if(next==Dark)
            {
                candidate=null;
                return false;
            }
            if(candidate!=next)
            {
                candidate=next;
                candidateAt=milliseconds;
                return false;
            }
            if(milliseconds-candidateAt<180)return false;
            Dark=next;
            candidate=null;
            return true;
        }
        internal static bool ChooseDark(double luminance,bool? previous)
        {
            return previous.HasValue?(previous.Value?luminance>.17:luminance>.24):luminance>.20;
        }
        internal void CancelPending(){candidate=null;}
        internal void Reset(){HasSample=false;candidate=null;}
    }
}
