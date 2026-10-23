using System;
using System.Windows;
namespace OneTaskBar
{
    internal enum WindowDropKind {None,Left,Right,Popup,Cancel}
    internal sealed class WindowDropPlan
    {
        internal WindowDropKind Kind;internal System.Drawing.Rectangle Work,Bounds;internal double Scale;
        internal bool Valid {get{return Kind==WindowDropKind.Left||Kind==WindowDropKind.Right||Kind==WindowDropKind.Popup;}}
        internal string Caption {get{return Kind==WindowDropKind.Popup?"팝업으로 열기":Kind==WindowDropKind.Left?"왼쪽 분할 화면으로 열기":"오른쪽 분할 화면으로 열기";}}
        internal static WindowDropPlan At(System.Drawing.Rectangle work,System.Drawing.Point point,double scale,WindowDropKind previous=WindowDropKind.None)
        {
            var plan=new WindowDropPlan {Work=work,Scale=scale};if(work.Width<200||work.Height<160||!work.Contains(point))return plan;
            double edge=Math.Min(work.Width*.23,180*scale),hysteresis=12*scale;double x=point.X-work.Left;
            if(point.Y>work.Bottom-54*scale){plan.Kind=WindowDropKind.Cancel;return plan;}
            plan.Kind=x<edge||previous==WindowDropKind.Left&&x<edge+hysteresis?WindowDropKind.Left:x>work.Width-edge||previous==WindowDropKind.Right&&x>work.Width-edge-hysteresis?WindowDropKind.Right:WindowDropKind.Popup;
            if(plan.Kind==WindowDropKind.Popup){int width=(int)Math.Round(Math.Min(900*scale,work.Width*.66)),height=(int)Math.Round(Math.Min(650*scale,work.Height*.72));plan.Bounds=new System.Drawing.Rectangle(work.Left+(work.Width-width)/2,work.Top+(work.Height-height)/2,width,height);}
            else {int middle=work.Left+work.Width/2;plan.Bounds=plan.Kind==WindowDropKind.Left?new System.Drawing.Rectangle(work.Left,work.Top,middle-work.Left,work.Height):new System.Drawing.Rectangle(middle,work.Top,work.Right-middle,work.Height);}
            return plan;
        }
        internal System.Drawing.Rectangle OtherHalf {get{int middle=Work.Left+Work.Width/2;return Kind==WindowDropKind.Left?new System.Drawing.Rectangle(middle,Work.Top,Work.Right-middle,Work.Height):new System.Drawing.Rectangle(Work.Left,Work.Top,middle-Work.Left,Work.Height);}}
        internal Rect Preview {get{double gap=8*Scale;return new Rect(Bounds.Left+gap,Bounds.Top+gap,Math.Max(1,Bounds.Width-2*gap),Math.Max(1,Bounds.Height-2*gap));}}
    }
}
