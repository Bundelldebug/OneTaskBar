using System;
using System.Collections.Generic;
using System.Windows;

namespace OneTaskBar
{
    // Presentation only. The island shares the shell's existing audio, power and media services.
    internal sealed class DynamicIslandState
    {
        internal bool Expanded,Searching;internal int SearchRows;
        internal string FeedbackKind;
        internal double Value;
        internal bool Muted;
        internal DateTime FeedbackUntil;
        internal DateTime NotificationUntil;
        internal bool FeedbackVisible(DateTime now){return FeedbackKind!=null&&now<FeedbackUntil;}
        internal bool NotificationVisible(DateTime now){return now<NotificationUntil;}
        internal void Feedback(string kind,double value,bool muted,DateTime now)
        {
            FeedbackKind=kind;
            Value=Math.Max(0,Math.Min(100,value));
            Muted=muted;
            FeedbackUntil=now.AddSeconds(kind=="charge"?3:1.6);
        }
        internal Size Size(bool media,DateTime now)
        {
            if(Searching)return new Size(360,SearchRows==0?IslandSearchView.HeaderHeight:IslandSearchView.HeaderHeight+SearchRows*IslandSearchView.RowHeight+8);
            if(Expanded&&media)return new Size(360,204);
            if(NotificationVisible(now))return new Size(360,156);
            if(FeedbackVisible(now))return FeedbackKind=="charge"?new Size(DynamicIslandLayout.ChargeWidth,DynamicIslandLayout.ChargeHeight):new Size(286,88);
            // Same height with or without media: playing only widens the island, it never grows downward.
            return media?new Size(DynamicIslandLayout.MediaWidth,DynamicIslandLayout.CompactHeight):new Size(DynamicIslandLayout.IdleWidth,DynamicIslandLayout.CompactHeight);
        }
        internal void Reset(){Expanded=Searching=false;FeedbackKind=null;FeedbackUntil=NotificationUntil=DateTime.MinValue;}
    }
    internal static class DynamicIslandLayout
    {
        internal const double MaximumWidth=360,MaximumHeight=244,TopInset=2,CompactHeight=22,ChargeHeight=22,IdleWidth=44,MediaWidth=88,ChargeWidth=208;
        internal static Rect Host(System.Drawing.Rectangle screen,System.Drawing.Rectangle work,double scale,bool bottom=false)
        {
            scale=Math.Max(1,scale);
            double width=Math.Min(MaximumWidth,Math.Max(1,work.Width/scale-24));
            double height=Math.Min(MaximumHeight,Math.Max(1,work.Height/scale-16));
            return new Rect((work.Left-screen.Left)/scale+(work.Width/scale-width)/2,
                bottom?(work.Bottom-screen.Top)/scale-height-TopInset:(work.Top-screen.Top)/scale+TopInset,width,height);
        }
        internal static Rect Capsule(Size size,double hostWidth,double hostHeight=0,bool bottom=false)
        {
            double width=Math.Min(size.Width,hostWidth);
            return new Rect((hostWidth-width)/2,bottom?Math.Max(0,hostHeight-size.Height):0,width,size.Height);
        }
        internal static bool Clear(System.Drawing.Rectangle screen,System.Drawing.Rectangle work,double scale,IEnumerable<FullscreenCandidate> windows)
        {
            if(work.Width/scale<300||work.Height/scale<240||FullscreenVisibility.IsFullScreen(screen,windows))return false;
            return true;
        }
    }
}
