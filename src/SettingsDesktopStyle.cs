using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace OneTaskBar
{
    internal sealed partial class WelcomeWindow
    {
        static readonly string[] desktopStyleNames={"Windows","Mac","GoogleBook"};
        static readonly string[] desktopStyleNotes={"기본 하단 작업표시줄","Dynamic Island + 하단바 · 나우바 끔 · 듀오 아이콘","상단 안드로이드 바"};
        internal int DesktopStyleCount {get{return desktopStyleNames.Length;}}
        // The style is a preset of two existing options, so it always reflects what those options say.
        internal static int CurrentDesktopStyle(){return Preferences.Current.TopStatusBar?2:Preferences.Current.DynamicIsland?1:0;}
        internal void ChooseDesktopStyle(int style)
        {
            // Mac turns the liquid material on too. LiquidGlass.Enabled still holds it back on a weak PC,
            // so the material follows the hardware without a second switch.
            Preferences.Set(p=>{p.DynamicIsland=style==1;p.TopStatusBar=style==2;if(style==1){p.LiquidGlass=true;p.Blur=true;p.ShowNowBar=false;p.DuoStatusIcon=true;}else p.DuoStatusIcon=false;});
        }
        // Three miniature desktops side by side; the chosen one gets an accent frame.
        void DesktopStyleSetting(StackPanel rows)
        {
            var box=new StackPanel {Margin=new Thickness(15,14,15,14)};
            var title=Paint.TextBlock("데스크톱 스타일",15,AppSettingsPalette.Text);title.FontWeight=FontWeights.SemiBold;box.Children.Add(title);
            var detail=Paint.TextBlock("Windows는 기본 하단 작업표시줄, Mac은 Dynamic Island를 켠 하단바, GoogleBook은 위쪽 안드로이드 바입니다",13,AppSettingsPalette.Muted);detail.TextWrapping=TextWrapping.Wrap;detail.Margin=new Thickness(0,4,0,10);box.Children.Add(detail);
            var grid=new Grid();
            var frames=new Border[3];
            Action refresh=()=>
            {
                for(int i=0;i<frames.Length;i++)
                {
                    bool chosen=CurrentDesktopStyle()==i;
                    frames[i].BorderBrush=chosen?AppSettingsPalette.Accent:Brushes.Transparent;
                    AutomationProperties.SetHelpText(frames[i],chosen?"선택됨":"");
                }
            };
            for(int i=0;i<3;i++)
            {
                int style=i;
                grid.ColumnDefinitions.Add(new ColumnDefinition());
                var cell=new StackPanel {Margin=new Thickness(i==0?0:5,0,i==2?0:5,0),Background=Brushes.Transparent,Cursor=Cursors.Hand};
                var frame=new Border {BorderThickness=new Thickness(2.5),CornerRadius=new CornerRadius(14),Padding=new Thickness(2),Child=DesktopStylePreview(i)};
                frames[i]=frame;
                cell.Children.Add(frame);
                var name=Paint.TextBlock(desktopStyleNames[i],14,AppSettingsPalette.Text);name.HorizontalAlignment=HorizontalAlignment.Center;name.Margin=new Thickness(0,8,0,0);name.FontWeight=FontWeights.SemiBold;cell.Children.Add(name);
                var note=Paint.TextBlock(desktopStyleNotes[i],12,AppSettingsPalette.Muted);note.HorizontalAlignment=HorizontalAlignment.Center;note.TextWrapping=TextWrapping.Wrap;note.TextAlignment=TextAlignment.Center;note.Margin=new Thickness(0,2,0,0);cell.Children.Add(note);
                AutomationProperties.SetName(frame,"데스크톱 스타일 · "+desktopStyleNames[i]);
                cell.MouseLeftButtonUp+=(s,e)=>
                {
                    if(CurrentDesktopStyle()==style)return;
                    ChooseDesktopStyle(style);
                    refresh();
                };
                Grid.SetColumn(cell,i);grid.Children.Add(cell);
            }
            refresh();
            box.Children.Add(grid);
            rows.Children.Add(box);
        }
        static void Piece(Canvas canvas,double x,double y,double width,double height,double radius,string fill)
        {
            var shape=new Rectangle {Width=width,Height=height,RadiusX=radius,RadiusY=radius,Fill=Paint.Brush(fill)};
            Canvas.SetLeft(shape,x);Canvas.SetTop(shape,y);canvas.Children.Add(shape);
        }
        static UIElement DesktopStylePreview(int style)
        {
            const double w=190,h=112;
            var canvas=new Canvas {Width=w,Height=h,ClipToBounds=true};
            var wall=new LinearGradientBrush((Color)ColorConverter.ConvertFromString("#FF6C8DF2"),(Color)ColorConverter.ConvertFromString("#FFB07CE8"),new Point(0,0),new Point(1,1));
            canvas.Children.Add(new Rectangle {Width=w,Height=h,Fill=wall});
            Piece(canvas,46,style==0?16:24,98,52,7,"#D9FFFFFF");
            // bottom bar: launcher dot and four apps, status on the right
            Piece(canvas,w/2-52,h-20,104,16,4,"#CC1B1C22");
            Piece(canvas,w/2-45,h-16,8,8,4,"#FFE9EDF5");
            string[] colors={"#FFEF741D","#FF55AF39","#FF7868CF","#FFDA9B20"};
            for(int i=0;i<4;i++)Piece(canvas,w/2-30+i*13,h-16,8,8,2.5,colors[i]);
            Piece(canvas,w-42,h-16,30,8,4,"#99FFFFFF");
            if(style==1)Piece(canvas,w/2-26,5,52,13,6.5,"#FF111114");
            if(style==2)
            {
                Piece(canvas,0,0,w,12,0,"#B3111114");
                Piece(canvas,8,4,22,4,2,"#E6FFFFFF");
                Piece(canvas,w/2-14,4,28,4,2,"#99FFFFFF");
                Piece(canvas,w-34,4,26,4,2,"#E6FFFFFF");
            }
            return new Border {CornerRadius=new CornerRadius(11),ClipToBounds=true,Child=canvas};
        }
    }
}
