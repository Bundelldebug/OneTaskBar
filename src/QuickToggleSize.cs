using System;
namespace OneTaskBar
{
    internal static class QuickToggleSize
    {
        internal const int MaximumColumns=4,MaximumRows=5;
        internal const double RowHeight=62;
        internal static int Columns(int value){return Math.Max(1,Math.Min(MaximumColumns,value));}
        internal static int Rows(int value){return Math.Max(1,Math.Min(MaximumRows,value));}
        internal static int Columns(PreferenceData value){return Columns(value.QuickToggleColumns>0?value.QuickToggleColumns:(int)Math.Round(value.QuickToggleWidthPercent/25.0,MidpointRounding.AwayFromZero));}
        internal static int Rows(PreferenceData value){return Rows(value.QuickToggleRows>0?value.QuickToggleRows:(int)Math.Round(value.QuickToggleCollapsedHeight/RowHeight,MidpointRounding.AwayFromZero));}
        internal static double Width(double available,int columns){return Math.Max(0,available)*Columns(columns)/MaximumColumns;}
        internal static double Height(int rows){return RowHeight*Rows(rows);}
        internal static int SnapColumns(double width,double available,int current){return Snap(width,Math.Max(0,available)/MaximumColumns,Columns(current),MaximumColumns);}
        internal static int SnapRows(double height,int current){return Snap(height,RowHeight,Rows(current),MaximumRows);}
        static int Snap(double length,double cell,int current,int maximum)
        {
            if(cell<=0||Double.IsNaN(length)||Double.IsInfinity(length))return current;
            // A small dead band prevents the grid from jumping back and forth at a boundary.
            while(current<maximum&&length>=(current+.6)*cell)current++;
            while(current>1&&length<=(current-.6)*cell)current--;
            return current;
        }
    }
}
