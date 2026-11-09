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
        static readonly string[] desktopStyleNotes={"기본 하단 작업표시줄","다이나믹 아일랜드 + 하단바 · 나우바 끔 · 듀오 아이콘","상단 안드로이드 바"};
        internal int DesktopStyleCount {get{return desktopStyleNames.Length;}}
        // Each style is a preset of several existing options and is shown as chosen only while every one of them matches.
        // Any option changed on its own leaves no preset matching, and the "custom desktop" card in front is chosen instead.
        internal const int CustomStyle=-1;
        internal static int CurrentDesktopStyle()
        {
            var p=Preferences.Current;
            if(!p.DynamicIsland&&!p.TopStatusBar&&!p.DuoStatusIcon&&p.ShowNowBar)return 0;
            if(p.DynamicIsland&&!p.TopStatusBar&&p.DuoStatusIcon&&p.LiquidGlass&&p.Blur&&!p.ShowNowBar)return 1;
            if(p.TopStatusBar&&!p.DynamicIsland&&!p.DuoStatusIcon&&p.ShowNowBar)return 2;
            return CustomStyle;
        }
        internal void ChooseDesktopStyle(int style)
        {
            // Mac turns the liquid material on too. LiquidGlass.Enabled still holds it back on a weak PC,
            // so the material follows the hardware without a second switch.
            // A style is applied as a preset: entering Mac remembers the options it changes, and leaving it puts them back.
            int from=CurrentDesktopStyle();
            Preferences.Set(p=>
            {
                p.DynamicIsland=style==1;p.TopStatusBar=style==2;
                if(style==1)
                {
                    if(from!=1||String.IsNullOrEmpty(p.DesktopStyleBackup))p.DesktopStyleBackup=(p.LiquidGlass?"1":"0")+","+(p.Blur?"1":"0")+","+(p.ShowNowBar?"1":"0");
                    p.LiquidGlass=true;p.Blur=true;p.ShowNowBar=false;p.DuoStatusIcon=true;
                    return;
                }
                p.DuoStatusIcon=false;p.ShowNowBar=true;
                if(from==1)
                {
                    var saved=(p.DesktopStyleBackup??"").Split(',');
                    bool known=saved.Length==3;
                    p.LiquidGlass=known?saved[0]=="1":false;
                    p.Blur=known?saved[1]=="1":p.Blur;
                    p.ShowNowBar=known?saved[2]=="1":true;
                }
                p.ShowNowBar=true;p.DesktopStyleBackup=null;
            });
        }
        // Three miniature desktops side by side; the chosen one gets an accent frame.
        void DesktopStyleSetting(StackPanel rows)
        {
            var box=new StackPanel {Margin=new Thickness(15,14,15,14)};
            var title=Paint.TextBlock("데스크톱 스타일",15,AppSettingsPalette.Text);title.FontWeight=FontWeights.SemiBold;box.Children.Add(title);
            var detail=Paint.TextBlock("각 스타일은 여러 설정을 한 번에 맞추는 프리셋입니다. 설정을 하나라도 따로 바꾸면 '커스텀 데스크톱'이 선택됩니다",13,AppSettingsPalette.Muted);detail.TextWrapping=TextWrapping.Wrap;detail.Margin=new Thickness(0,4,0,10);box.Children.Add(detail);
            var grid=new Grid();
            // The custom desktop leads; the presets follow in their usual order.
            const int cards=4;
            var frames=new Border[cards];
            Action refresh=()=>
            {
                for(int i=0;i<frames.Length;i++)
                {
                    bool chosen=CurrentDesktopStyle()==i-1;
                    frames[i].BorderBrush=chosen?AppSettingsPalette.Accent:Brushes.Transparent;
                    AutomationProperties.SetHelpText(frames[i],chosen?"선택됨":"");
                }
            };
            for(int i=0;i<cards;i++)
            {
                int style=i-1;
                string label=style<0?"커스텀 데스크톱":desktopStyleNames[style],words=style<0?"직접 바꾼 설정 그대로":desktopStyleNotes[style];
                grid.ColumnDefinitions.Add(new ColumnDefinition());
                var cell=new StackPanel {Margin=new Thickness(i==0?0:4,0,i==cards-1?0:4,0),Background=Brushes.Transparent,Cursor=Cursors.Hand};
                var frame=new Border {BorderThickness=new Thickness(2.5),CornerRadius=new CornerRadius(14),Padding=new Thickness(2),Child=DesktopStylePreview(style)};
                frames[i]=frame;
                cell.Children.Add(frame);
                var name=Paint.TextBlock(label,14,AppSettingsPalette.Text);name.HorizontalAlignment=HorizontalAlignment.Center;name.Margin=new Thickness(0,8,0,0);name.FontWeight=FontWeights.SemiBold;cell.Children.Add(name);
                var note=Paint.TextBlock(words,12,AppSettingsPalette.Muted);note.HorizontalAlignment=HorizontalAlignment.Center;note.TextWrapping=TextWrapping.Wrap;note.TextAlignment=TextAlignment.Center;note.Margin=new Thickness(0,2,0,0);cell.Children.Add(note);
                AutomationProperties.SetName(frame,"데스크톱 스타일 · "+label);
                cell.MouseLeftButtonUp+=(s,e)=>
                {
                    // The custom desktop is what the options already say; there is nothing to apply to become it.
                    if(style<0||CurrentDesktopStyle()==style)return;
                    ChooseDesktopStyle(style);
                    refresh();
                };
                Grid.SetColumn(cell,i);grid.Children.Add(cell);
            }
            Preferences.Changed+=refresh;
            Unloaded+=(s,e)=>Preferences.Changed-=refresh;
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
            Piece(canvas,46,style<=0?16:24,98,52,7,"#D9FFFFFF");
            // bottom bar: launcher dot and four apps, status on the right
            Piece(canvas,w/2-52,h-20,104,16,4,"#CC1B1C22");
            Piece(canvas,w/2-45,h-16,8,8,4,"#FFE9EDF5");
            string[] colors={"#FFEF741D","#FF55AF39","#FF7868CF","#FFDA9B20"};
            for(int i=0;i<4;i++)Piece(canvas,w/2-30+i*13,h-16,8,8,2.5,colors[i]);
            Piece(canvas,w-42,h-16,30,8,4,"#99FFFFFF");
            // The custom desktop is a plain desktop with a few sliders drawn on it: settings set one by one.
            if(style<0)
            {
                Piece(canvas,58,24,74,4,2,"#B3FFFFFF");Piece(canvas,58,36,74,4,2,"#B3FFFFFF");Piece(canvas,58,48,74,4,2,"#B3FFFFFF");
                Piece(canvas,84,21,10,10,5,"#FF3478FD");Piece(canvas,68,33,10,10,5,"#FF3478FD");Piece(canvas,108,45,10,10,5,"#FF3478FD");
            }
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
