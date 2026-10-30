using System;
using System.Collections.Generic;
using System.Windows;

namespace OneTaskBar
{
    // Presentation only. The island shares the shell's existing audio, power and media services.
    internal sealed class DynamicIslandState
    {
        internal bool Expanded;
        internal string FeedbackKind;
        internal double Value;
        internal bool Muted;
        internal DateTime FeedbackUntil;
        internal bool FeedbackVisible(DateTime now){return FeedbackKind!=null&&now<FeedbackUntil;}
        internal void Feedback(string kind,double value,bool muted,DateTime now)
        {
            FeedbackKind=kind;
            Value=Math.Max(0,Math.Min(100,value));
            Muted=muted;
            FeedbackUntil=now.AddSeconds(kind=="charge"?3:1.6);
        }
        internal Size Size(bool media,DateTime now)
        {
            if(Expanded&&media)return new Size(360,184);
            if(FeedbackVisible(now))return new Size(240,28);
            return media?new Size(112,28):new Size(56,18);
        }
        internal void Reset(){Expanded=false;FeedbackKind=null;FeedbackUntil=DateTime.MinValue;}
    }
    internal static class DynamicIslandLayout
    {
        internal const double MaximumWidth=360,MaximumHeight=184,TopInset=2;
        internal static Rect Host(System.Drawing.Rectangle screen,System.Drawing.Rectangle work,double scale)
        {
            scale=Math.Max(1,scale);
            double width=Math.Min(MaximumWidth,Math.Max(1,work.Width/scale-24));
            double height=Math.Min(MaximumHeight,Math.Max(1,work.Height/scale-16));
            return new Rect((work.Left-screen.Left)/scale+(work.Width/scale-width)/2,
                (work.Top-screen.Top)/scale+TopInset,width,height);
        }
        internal static Rect Capsule(Size size,double hostWidth)
        {
            double width=Math.Min(size.Width,hostWidth);
            return new Rect((hostWidth-width)/2,0,width,size.Height);
        }
        internal static bool Clear(System.Drawing.Rectangle screen,System.Drawing.Rectangle work,double scale,IEnumerable<FullscreenCandidate> windows)
        {
            if(work.Width/scale<300||work.Height/scale<240||FullscreenVisibility.IsFullScreen(screen,windows))return false;
            return true;
        }
    }
}
