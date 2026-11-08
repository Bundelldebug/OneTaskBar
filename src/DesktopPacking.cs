using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace OneTaskBar
{
    // Packs window cards into rows like a puzzle: every card keeps its window's shape, the biggest row height that
    // still fits is used, and a nearly full row is stretched so its edges line up.
    internal static class DesktopPacking
    {
        // Card chrome around the live preview: 9px padding on each side plus a 30px title row.
        internal const double SideChrome=18,VerticalChrome=48,Gap=18,Stretch=1.35;

        internal static Rect[] Pack(double[] aspects,double width,double height)
        {
            int count=aspects.Length;
            if(count==0)return new Rect[0];
            double widest=aspects.Max();
            double ceiling=Math.Min(560,Math.Max(40,(width-SideChrome)/Math.Max(.3,widest)));
            Rect[] best=null;
            for(double rowHeight=ceiling;rowHeight>=40;rowHeight-=4)
            {
                var plan=Place(aspects,width,rowHeight);
                best=plan.Item1;
                if(plan.Item2<=height)break;
            }
            // Centre the whole block vertically.
            double bottom=best.Max(r=>r.Bottom);
            double shift=Math.Max(0,(height-bottom)/2);
            for(int i=0;i<best.Length;i++)best[i].Offset(0,shift);
            return best;
        }

        static Tuple<Rect[],double> Place(double[] aspects,double width,double rowHeight)
        {
            var rows=new List<List<int>>();
            var current=new List<int>();
            double used=0;
            for(int i=0;i<aspects.Length;i++)
            {
                double w=rowHeight*aspects[i]+SideChrome;
                if(current.Count>0&&used+Gap+w>width)
                {
                    rows.Add(current);
                    current=new List<int>();
                    used=0;
                }
                used+=(current.Count>0?Gap:0)+w;
                current.Add(i);
            }
            rows.Add(current);
            var boxes=new Rect[aspects.Length];
            double y=0;
            for(int r=0;r<rows.Count;r++)
            {
                var row=rows[r];
                double sum=row.Sum(i=>rowHeight*aspects[i]+SideChrome)+Gap*(row.Count-1);
                double scale=1;
                bool last=r==rows.Count-1;
                double preview=row.Sum(i=>rowHeight*aspects[i]);
                if(rows.Count>1&&(!last||sum>=width*.6))scale=Math.Max(1,Math.Min(Stretch,(width-Gap*(row.Count-1)-SideChrome*row.Count)/preview));
                double rowWidth=preview*scale+SideChrome*row.Count+Gap*(row.Count-1);
                double cardHeight=rowHeight*scale+VerticalChrome;
                double x=Math.Max(0,(width-rowWidth)/2);
                foreach(int i in row)
                {
                    double w=rowHeight*aspects[i]*scale+SideChrome;
                    boxes[i]=new Rect(x,y,w,cardHeight);
                    x+=w+Gap;
                }
                y+=cardHeight+Gap;
            }
            return Tuple.Create(boxes,y-Gap);
        }
    }
}
