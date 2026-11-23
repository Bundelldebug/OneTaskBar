using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace OneTaskBar
{
    // The Dex look for the taskbar: a thin status row along the top and a full-width bar along the bottom, with the search
    // pill, the start button and the apps centered as one group and task view / show desktop on the right. It stays a
    // taskbar: Start, Windows search, task view and the desktop remain what they are. Sizes come from the owner's recording.
    internal static class Dex
    {
        internal static bool Enabled {get{return ReleaseFeatures.GlassUI&&Preferences.Current.DexDesktop;}}
        internal const double BarHeight=42,Pitch=41,GroupGap=21,DividerGap=10,IconSize=27;
        internal const double PillWidth=120,PillHeight=28,StartLeft=130,GridLeft=166,AppsStart=206,TrailingPad=4,NavReserve=150;
        internal const double PanelRadius=24,DrawerWidth=652,DrawerHeight=598,DrawerGap=26,FinderWidth=532,FinderHeight=447,FinderGap=9,QuickHeight=712;
        // How much faster than the Fluid arrival the panels open here: the recording settles in about a third of a second.
        internal const double MotionRate=1.75;
        internal static Brush BarTint(bool dark,bool blurred)
        {
            if(!blurred)return Paint.Brush(dark?"#FF23282E":"#FFDDE5EC");
            return Paint.Brush(dark?"#4414181E":"#32FFFFFF");
        }
        internal static Brush BarTopEdge(bool dark){return Paint.Brush(dark?"#20FFFFFF":"#42FFFFFF");}
        // One UI 9.5 Dex uses bright frosted light glass for Calendar, App Drawer, Finder and notification cards in Light mode.
        internal static bool LightSheet {get{return Enabled&&!Glass95.Dark&&!SystemParameters.HighContrast;}}
        static readonly Brush sheetLightGlass=Paint.Brush("#D6F2F6FA"),sheetLightSolid=Paint.Brush("#FFF2F5F9");
        static readonly Brush finderLightGlass=Paint.Brush("#CCECF1F6"),finderLightSolid=Paint.Brush("#FFEBF0F5");
        static readonly Brush sheetDarkGlass=Paint.Brush("#CC1A1F26"),sheetDarkSolid=Paint.Brush("#FF181C22");
        static readonly Brush sheetLightRim=Paint.Brush("#7AFFFFFF"),sheetDarkRim=Paint.Brush("#2CFFFFFF");
        static readonly Brush sheetLightCard=Paint.Brush("#EAFFFFFF"),sheetDarkCard=Paint.Brush("#24FFFFFF");
        static readonly Brush sheetLightChip=Paint.Brush("#14000000"),sheetDarkChip=Paint.Brush("#22FFFFFF");
        static readonly Brush sheetLightInk=Paint.Brush("#FF1B2128"),sheetLightMuted=Paint.Brush("#FF5D6772");
        static readonly Brush noticeLightCard=Paint.Brush("#E8F5F9FC"),noticeDarkCard=Paint.Brush("#D8232931");
        static readonly Brush noticeLightTitle=Paint.Brush("#FF171E24"),noticeLightBody=Paint.Brush("#FF2D3640"),noticeLightMuted=Paint.Brush("#FF606B75");
        static readonly Brush noticeFootDark=Paint.Brush("#68222B35"),noticeDndDark=Paint.Brush("#5E222B35");
        internal static Brush SheetBackground(bool blurred,bool finder)
        {
            if(SystemParameters.HighContrast)return SystemColors.WindowBrush;
            if(!LightSheet)return blurred?sheetDarkGlass:sheetDarkSolid;
            return finder?(blurred?finderLightGlass:finderLightSolid):(blurred?sheetLightGlass:sheetLightSolid);
        }
        internal static Brush SheetRim {get{return SystemParameters.HighContrast?SystemColors.WindowFrameBrush:LightSheet?sheetLightRim:sheetDarkRim;}}
        internal static Brush SheetCard {get{return SystemParameters.HighContrast?SystemColors.WindowBrush:LightSheet?sheetLightCard:sheetDarkCard;}}
        internal static Brush SheetChip {get{return SystemParameters.HighContrast?SystemColors.ControlBrush:LightSheet?sheetLightChip:sheetDarkChip;}}
        internal static Brush SheetInk {get{return SystemParameters.HighContrast?SystemColors.WindowTextBrush:LightSheet?sheetLightInk:Paint.ControlWhite;}}
        internal static Brush SheetMuted {get{return SystemParameters.HighContrast?SystemColors.GrayTextBrush:LightSheet?sheetLightMuted:PanelMaterial.DarkMuted;}}
        internal static Brush NoticeCard {get{return SystemParameters.HighContrast?SystemColors.WindowBrush:LightSheet?noticeLightCard:noticeDarkCard;}}
        internal static Brush NoticeTitle {get{return SystemParameters.HighContrast?SystemColors.WindowTextBrush:LightSheet?noticeLightTitle:Paint.ControlWhite;}}
        internal static Brush NoticeBody {get{return SystemParameters.HighContrast?SystemColors.WindowTextBrush:LightSheet?noticeLightBody:PanelMaterial.DarkMuted;}}
        internal static Brush NoticeMuted {get{return SystemParameters.HighContrast?SystemColors.GrayTextBrush:LightSheet?noticeLightMuted:PanelMaterial.DarkMuted;}}
        internal static Brush NoticeFootButton {get{return noticeFootDark;}}
        internal static Brush NoticeDndPill {get{return noticeDndDark;}}
        internal static readonly Brush SearchAccent=Paint.Brush("#FF2F7FE0");
        static readonly System.Windows.Media.Effects.DropShadowEffect softShadow=BuildSoftShadow();
        static System.Windows.Media.Effects.DropShadowEffect BuildSoftShadow()
        {
            var shadow=new System.Windows.Media.Effects.DropShadowEffect {Color=Colors.Black,BlurRadius=14,ShadowDepth=3,Direction=270,Opacity=.16};
            shadow.Freeze();
            return shadow;
        }
        internal static System.Windows.Media.Effects.Effect SoftShadow {get{return softShadow;}}
        static double Available(double tall){return tall-IslandSafeArea.Height-DockSizing.BarSpace*DockSizing.Zoom;}
        internal static Rect DrawerRect(double wide,double tall)
        {
            double width=Math.Min(DrawerWidth,Math.Max(280,wide-32)),height=Math.Min(DrawerHeight,Math.Max(240,Available(tall)-DrawerGap-12));
            return new Rect((wide-width)/2,tall-DockSizing.BarSpace*DockSizing.Zoom-DrawerGap-height,width,height);
        }
        // The finder stands over its pill, a little to the left of it, and stays on the screen.
        internal static Rect FinderRect(double wide,double tall,double pillCenter)
        {
            double width=Math.Min(FinderWidth,Math.Max(280,wide-32)),height=Math.Min(FinderHeight,Math.Max(220,Available(tall)-FinderGap-12));
            double x=Math.Max(16,Math.Min(wide-width-16,pillCenter-width/2-44));
            return new Rect(x,tall-DockSizing.BarSpace*DockSizing.Zoom-FinderGap-height,width,height);
        }
    }

    internal sealed partial class DockHost
    {
        readonly bool dex=Dex.Enabled;
        Button dexApps,dexStartButton;Glyph dexStart;
        // The start button follows the same option as on the other styles.
        void RefreshDexStart()
        {
            bool native=Preferences.Current.NativeStartButton;
            dexStart.Color=native?Paint.Brush("#FF1975DD"):AppsInk;
            dexStart.InvalidateVisual();
            dexStartButton.ToolTip=native?"Windows 시작":"시작";
        }
        internal bool DexBar {get{return dex;}}
        internal Button DexAppsButton {get{return dexApps;}}
        internal Button DexStartButton {get{return dexStartButton;}}
        // The start key lives inside the bar here, so a press on it is the key's own toggle, not a click outside Start.
        internal bool DexStartContains(Native.PointI point)
        {
            if(!dex||dexStartButton==null||!Apps.IsVisible)return false;
            var rect=DeviceRect(dexStartButton);
            return rect.HasValue&&point.X>=rect.Value.Left&&point.X<rect.Value.Right&&point.Y>=rect.Value.Top&&point.Y<rect.Value.Bottom;
        }
        double IconSize {get{return dex?Dex.IconSize:30;}}
        internal double DexPillCenter
        {
            get
            {
                if(!dex||PresentationSource.FromVisual(SearchButton)==null)return Screen.Bounds.Width/Scale/2;
                return SearchButton.PointToScreen(new Point(SearchButton.ActualWidth/2,0)).X/Scale-Screen.Bounds.Left/Scale;
            }
        }
        Button NavKey(string kind,Action press,string tip)
        {
            var glyph=new Glyph(kind,20){Color=Glass95.DockForeground};
            var button=Paint.Button(glyph,press,tip);
            button.Width=38;button.Height=Dex.BarHeight;button.Padding=new Thickness(0);button.Background=Brushes.Transparent;
            Motion.Dock(button,glyph);
            return button;
        }
        void BuildDexBar()
        {
            Apps.DexBar=true;Apps.Radius=0;Apps.Surface.CornerRadius=new CornerRadius(0);
            // The search button becomes the finder pill; it keeps its own colours on any wallpaper.
            var words=new StackPanel {Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(11,0,0,0)};
            words.Children.Add(new Glyph("search",15){Color=Paint.Brush("#FF2F7FE0"),Margin=new Thickness(0,0,7,0),VerticalAlignment=VerticalAlignment.Center});
            var label=Paint.TextBlock("파인더",12,Paint.Brush("#FF4E5A66"));label.VerticalAlignment=VerticalAlignment.Center;words.Children.Add(label);
            var pill=new Border {Tag=Glass95.FixedInk,Width=Dex.PillWidth,Height=Dex.PillHeight,CornerRadius=new CornerRadius(Dex.PillHeight/2),Background=Paint.Brush("#59FFFFFF"),Child=words};
            SearchButton.Content=pill;SearchButton.Width=Dex.PillWidth;SearchButton.Height=Dex.PillHeight;
            Canvas.SetLeft(SearchButton,0);Canvas.SetTop(SearchButton,(Dex.BarHeight-Dex.PillHeight)/2);
            Motion.Dock(SearchButton,pill);
            dexStart=new Glyph("windows-start",19){Color=Glass95.DockForeground};
            dexStartButton=Paint.Button(dexStart,()=>controller.ToggleStartButton(this),"시작");
            dexStartButton.Width=dexStartButton.Height=34;dexStartButton.Padding=new Thickness(0);dexStartButton.Background=Brushes.Transparent;
            Canvas.SetLeft(dexStartButton,Dex.StartLeft);Canvas.SetTop(dexStartButton,4);Motion.Dock(dexStartButton,dexStart);
            appCanvas.Children.Add(dexStartButton);
            var grid=new Glyph("apps",23){Color=Glass95.DockForeground};
            dexApps=Paint.Button(grid,()=>controller.ToggleDrawer(this),"앱 목록");
            dexApps.Width=dexApps.Height=34;dexApps.Padding=new Thickness(0);dexApps.Background=Brushes.Transparent;
            Canvas.SetLeft(dexApps,Dex.GridLeft);Canvas.SetTop(dexApps,4);Motion.Dock(dexApps,grid);
            appCanvas.Children.Add(dexApps);
            pinDivider.Height=18;Canvas.SetTop(pinDivider,12);
            appCanvas.Height=Dex.BarHeight;appCanvas.HorizontalAlignment=HorizontalAlignment.Left;
            var keys=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,0,12,0)};
            keys.Children.Add(NavKey("dock-desktops",controller.OpenTaskView,"데스크톱 전환 · 작업 보기"));
            keys.Children.Add(NavKey("dex-desktop",controller.DexShowDesktop,"바탕화면 보기"));
            var root=new Grid();
            Apps.Surface.Child=null;
            root.Children.Add(appCanvas);root.Children.Add(keys);
            Apps.Surface.Child=root;
        }
        void PlaceDexBar(bool show)
        {
            double y=currentY+(1-reveal)*8;
            appCanvas.Width=currentWidth;
            appCanvas.Margin=new Thickness(currentX,0,0,0);
            PlaceDock(Apps,new Rect(0,y,Screen.Bounds.Width/ContentScale,Dex.BarHeight),show);
        }
        static Native.RectI? DeviceRect(FrameworkElement part)
        {
            if(part==null||part.Visibility!=Visibility.Visible||part.ActualWidth<4||part.ActualHeight<4||PresentationSource.FromVisual(part)==null)return null;
            var a=part.PointToScreen(new Point(0,0));var b=part.PointToScreen(new Point(part.ActualWidth,part.ActualHeight));
            return new Native.RectI {Left=(int)Math.Round(Math.Min(a.X,b.X)),Top=(int)Math.Round(Math.Min(a.Y,b.Y)),Right=(int)Math.Round(Math.Max(a.X,b.X)),Bottom=(int)Math.Round(Math.Max(a.Y,b.Y))};
        }
        // In the recording a panel grows out of the key that opened it, and that key stays where it is.
        MorphPlan DexPlan(GlassWindow panel)
        {
            FrameworkElement part=panel is StartWindow?dexStartButton:panel is DrawerWindow?dexApps:panel is SearchWindow?(FrameworkElement)SearchButton:null;
            if(part==null)return controller.DexTopPlan(panel);
            var source=Apps.IsVisible?DeviceRect(part):null;
            return source.HasValue?new MorphPlan {Piece=Apps,Source=source.Value,Keep=true}:null;
        }
    }

    internal sealed partial class ShellController
    {
        bool lastDex=Dex.Enabled;
        // The output list opened out of the media card under the status row.
        internal void ShowOutputFromCard(TopStatusBar bar,PanelMorph.Shot card,double corner,Forms.Screen screen)
        {
            CloseTrayOverflow();CloseDesktopOverview();Previews.Close();
            foreach(var panel in new GlassWindow[]{StartMenu,Finder,Drawer,Quick,Center})if(panel!=null)panel.HideImmediately();
            if(Detail!=null){Detail.Close();Detail=null;}
            Detail=new DetailWindow(this,"sound") {OutputCard=bar,PopFromTop=true};
            double scale=MonitorScale(screen,Detail),w=Math.Min(380,screen.Bounds.Width/scale-24),h=Math.Min(Dex.QuickHeight,screen.Bounds.Height/scale-100);
            double middle=(card.Bounds.Left+card.Bounds.Right)/2.0/scale-screen.Bounds.Left/scale;
            double x=Math.Max(8,Math.Min(screen.Bounds.Width/scale-w-8,middle-w/2));
            Detail.ShowAt(screen.Bounds,x,IslandSafeArea.Height+4,w,h);
            PanelMorph.OutputOpen(Detail,card,corner);
            Detail.Activate();
        }
        internal void DexShowDesktop()
        {
            CloseDesktopOverview();
            foreach(var panel in new GlassWindow[]{Quick,Center,Detail,Drawer,Finder,StartMenu})if(panel!=null&&panel.IsVisible)panel.Hide();
            System.Threading.Tasks.Task.Run(()=>SystemPanels.ShowDesktop());
        }
        internal MorphPlan DexTopPlan(GlassWindow panel)
        {
            if(topRight==null||!topRight.IsVisible)return null;
            bool notices=panel is NotificationWindow;
            if(!notices&&!(panel is QuickWindow)&&!(panel is DetailWindow))return null;
            var source=topRight.DexAnchor(notices);
            return source.HasValue?new MorphPlan {Piece=topRight,Source=source.Value,Keep=true}:null;
        }
        // Windows built for the other layout are dropped when the style changes.
        void RetireDexPanels()
        {
            if(Drawer!=null&&Drawer.DexStyle!=Dex.Enabled){Drawer.Close();Drawer=null;}
            if(Finder!=null&&Finder.DexStyle!=Dex.Enabled){Finder.Close();Finder=null;}
        }
        Rect DexDrawerRect(System.Windows.Forms.Screen screen,double scale){return Dex.DrawerRect(screen.Bounds.Width/scale,screen.Bounds.Height/scale);}
        Rect DexFinderRect(System.Windows.Forms.Screen screen,double scale,DockHost dock)
        {
            var host=dock??DockFor(screen);
            return Dex.FinderRect(screen.Bounds.Width/scale,screen.Bounds.Height/scale,host!=null?host.DexPillCenter:screen.Bounds.Width/scale/2);
        }
    }

    // The Dex status row: only the clock on the left; the media pill, capture, input language, notification icons and the
    // status capsule stand together on the right.
    internal sealed partial class TopStatusBar
    {
        static readonly Brush LightCapsuleTint=Paint.Brush("#34FFFFFF"),DarkCapsuleTint=Paint.Brush("#24121C26");
        Border noticePill,statusPill;
        const double CardCorner=26;
        Native.RectI CardPlace()
        {
            var a=cardSurface.PointToScreen(new Point(0,drop));var b=cardSurface.PointToScreen(new Point(CardWidth,drop+cardHeight));
            return new Native.RectI {Left=(int)Math.Round(a.X),Top=(int)Math.Round(a.Y),Right=(int)Math.Round(b.X),Bottom=(int)Math.Round(b.Y)};
        }
        // The output key of the media card: the card becomes the output list.
        void CardToOutput()
        {
            if(!Motion.Enabled||card==null||!card.IsOpen||closing){CloseCard();shell.ShowDetail("sound");return;}
            var place=CardPlace();PanelMorph.Shot shot=null;
            try
            {
                var whole=PanelMorph.Card(cardSurface) as System.Windows.Media.Imaging.BitmapSource;
                double unit=whole.PixelWidth/CardWidth;
                var part=new System.Windows.Media.Imaging.CroppedBitmap(whole,new Int32Rect(0,(int)Math.Round(drop*unit),whole.PixelWidth,Math.Min(whole.PixelHeight-(int)Math.Round(drop*unit),(int)Math.Round(cardHeight*unit))));
                part.Freeze();
                shot=new PanelMorph.Shot {Bounds=place,Image=part,Content=new Rect(0,0,CardWidth,cardHeight)};
            }
            catch(Exception error){AppLog.Once("dex-output-card",error.GetType().Name);}
            StopOutside();closing=false;closeTimer.Stop();cardClip.BeginAnimation(RectangleGeometry.RectProperty,null);FinishClose();
            if(shot==null){shell.ShowDetail("sound");return;}
            shell.ShowOutputFromCard(this,shot,CardCorner,Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle));
        }
        // Back from the output list: the card stands in place at once and the glass closes around it.
        internal bool ReturnFromOutput(DetailWindow page)
        {
            var media=shell.Services.Media;
            if(closed||!IsVisible||media==null||!media.Available||!media.Active)return false;
            if(card==null||!card.IsOpen){cardAtOnce=true;try{ToggleCard();}finally{cardAtOnce=false;}}
            if(card==null||!card.IsOpen)return false;
            cardSurface.UpdateLayout();
            page.LeaveToward(CardPlace());
            return true;
        }
        bool cardAtOnce;
        void CardInPlace()
        {
            var full=new Rect(0,drop,CardWidth,cardHeight);
            cardClip.BeginAnimation(RectangleGeometry.RectProperty,null);cardClip.Rect=full;
            cardClip.BeginAnimation(RectangleGeometry.RadiusXProperty,null);cardClip.BeginAnimation(RectangleGeometry.RadiusYProperty,null);cardClip.RadiusX=cardClip.RadiusY=CardCorner;
            player.BeginAnimation(UIElement.OpacityProperty,null);player.Opacity=1;
            miniHost.BeginAnimation(UIElement.OpacityProperty,null);miniHost.Opacity=0;
            chip.BeginAnimation(UIElement.OpacityProperty,null);chip.Opacity=0;
            cardSurface.BeginAnimation(UIElement.OpacityProperty,new System.Windows.Media.Animation.DoubleAnimation(0,1,TimeSpan.FromMilliseconds(220)){EasingFunction=new System.Windows.Media.Animation.QuadraticEase {EasingMode=System.Windows.Media.Animation.EasingMode.EaseIn},FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop});
        }
        DexHint hint;readonly DispatcherTimer hintDelay=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(260)};Func<string> hintText;Button hintButton;bool hintWired;
        // Resting on a capsule names it in a white label; the label stays while the panel it opens is coming up.
        void Named(Button button,Func<string> text)
        {
            button.ToolTip=null;
            if(!hintWired){hintWired=true;hintDelay.Tick+=(s,e)=>ShowHint();Closed+=(s,e)=>{hintDelay.Stop();if(hint!=null)hint.Close();};}
            button.MouseEnter+=(s,e)=>{hintButton=button;hintText=text;hintDelay.Stop();hintDelay.Start();};
            button.MouseLeave+=(s,e)=>{hintDelay.Stop();if(hint!=null)hint.Dismiss();};
        }
        void ShowHint()
        {
            hintDelay.Stop();
            if(closed||hintButton==null||!hintButton.IsMouseOver)return;
            var a=hintButton.PointToScreen(new Point());var b=hintButton.PointToScreen(new Point(hintButton.ActualWidth,hintButton.ActualHeight));
            if(hint==null)hint=new DexHint();
            hint.Present(hintText(),new Native.RectI {Left=(int)a.X,Top=(int)a.Y,Right=(int)b.X,Bottom=(int)b.Y});
        }
        readonly Glyph captureGlyph=new Glyph("dex-capture",15){VerticalAlignment=VerticalAlignment.Center};
        readonly TextBlock language=Paint.TextBlock("",11,Paint.Text);
        readonly Glyph noticeBell=new Glyph("notification",12);
        DateTime languageAt=DateTime.MinValue;
        static Button Flat(Button button,double side)
        {
            button.Background=Hit;button.Padding=new Thickness(side,0,side,0);button.VerticalAlignment=VerticalAlignment.Stretch;
            return button;
        }
        Border Capsule(UIElement child,Thickness padding){return new Border {Height=20,CornerRadius=new CornerRadius(10),Padding=padding,Background=DarkCapsuleTint,VerticalAlignment=VerticalAlignment.Center,Child=child};}
        void BuildDexRight(FrameworkElement status)
        {
            wifi.Width=wifi.Height=15;volume.Width=volume.Height=14;wifi.Margin=new Thickness(0,0,5,0);
            row.HorizontalAlignment=HorizontalAlignment.Right;row.VerticalAlignment=VerticalAlignment.Top;
            row.Children.Add(BuildChip());
            var captureBtn=Flat(Paint.Button(captureGlyph,()=>shell.OpenScreenCapture(),"화면 캡처"),9);
            Named(captureBtn,()=>"화면 캡처");
            row.Children.Add(captureBtn);
            language.VerticalAlignment=VerticalAlignment.Center;language.Background=Hit;
            var langBtn=Flat(Paint.Button(language,()=>System.Threading.Tasks.Task.Run(()=>SystemPanels.InputSwitcher()),"입력 언어"),8);
            Named(langBtn,()=>"입력 언어");
            row.Children.Add(langBtn);
            icons.Margin=new Thickness(0);
            // The capsule stays when nothing is waiting, so the notification center can always be reached.
            noticeBell.VerticalAlignment=VerticalAlignment.Center;
            var noticeContent=new StackPanel {Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};
            noticeContent.Children.Add(noticeBell);noticeContent.Children.Add(icons);
            noticePill=Capsule(noticeContent,new Thickness(7,0,7,0));
            var notices=Flat(Paint.Button(noticePill,()=>shell.ToggleNotifications(Dock(),true),"알림센터"),3);notices.RenderTransform=iconShift;
            Named(notices,()=>{int count=shell.Notifications.Items.Count;return count>0?"알림 "+count+"개":"알림";});
            row.Children.Add(notices);
            trayGlyph.RenderTransformOrigin=new Point(.5,.5);trayGlyph.RenderTransform=new RotateTransform(90);trayGlyph.Width=trayGlyph.Height=11;
            trayButton=Flat(Paint.Button(trayGlyph,OpenTray,"숨겨진 아이콘"),2);
            Named(trayButton,()=>"숨겨진 아이콘");
            row.Children.Add(trayButton);
            statusPill=Capsule(status,new Thickness(8,0,4,0));
            var quick=Flat(Paint.Button(statusPill,()=>shell.ToggleQuick(Dock(),false,true),"빠른 설정"),3);
            Named(quick,()=>"빠른 설정창");
            row.Children.Add(quick);
            BuildWindowKeys();
        }
        internal void InvalidateLanguage(){languageAt=DateTime.MinValue;}
        void RefreshDexTools(Brush ink,System.Windows.Media.Effects.Effect effect)
        {
            var pillBg=ink==lightInk?LightCapsuleTint:DarkCapsuleTint;
            if(noticePill!=null&&noticePill.Background!=pillBg)noticePill.Background=pillBg;
            if(statusPill!=null&&statusPill.Background!=pillBg)statusPill.Background=pillBg;
            if(captureGlyph.Color!=ink){captureGlyph.Color=ink;captureGlyph.InvalidateVisual();}
            captureGlyph.Effect=language.Effect=effect;language.Foreground=ink;
            var now=DateTime.UtcNow;
            if(now>=languageAt)
            {
                languageAt=now.AddMilliseconds(RuntimeCadence.OnBattery?1500:700);
                string next=InputLanguage.Current();
                if(next!=language.Text){language.Text=next;layoutDirty=true;}
            }
            bool hasIcons=icons.Children.Count>0;
            var wanted=hasIcons?Visibility.Collapsed:Visibility.Visible;
            if(noticeBell.Visibility!=wanted)
            {
                noticeBell.Visibility=wanted;
                if(noticePill!=null)noticePill.Padding=hasIcons?new Thickness(7,0,4,0):new Thickness(7,0,7,0);
                layoutDirty=true;
            }
            if(hasIcons)
            {
                var last=icons.Children[icons.Children.Count-1] as System.Windows.Shapes.Ellipse;
                if(last!=null&&"overflow-dot".Equals(last.Tag)&&last.Fill!=ink)last.Fill=ink;
            }
            if(noticeBell.Color!=ink){noticeBell.Color=ink;noticeBell.InvalidateVisual();}
            noticeBell.Effect=effect;
            if(keyGlyphs!=null)foreach(var glyph in keyGlyphs)if(glyph.Color!=ink){glyph.Color=ink;glyph.InvalidateVisual();}
            if(windowKeys!=null)foreach(UIElement item in windowKeys.Children){var btn=item as Button;if(btn!=null&&btn.Background!=pillBg)btn.Background=pillBg;}
        }
        // The capsule a panel grows out of, in device pixels.
        internal Native.RectI? DexAnchor(bool notices)
        {
            FrameworkElement part=notices&&noticePill!=null&&noticePill.Visibility==Visibility.Visible?noticePill:statusPill;
            if(part==null||part.ActualWidth<4||PresentationSource.FromVisual(part)==null)return null;
            var a=part.PointToScreen(new Point(0,0));var b=part.PointToScreen(new Point(part.ActualWidth,part.ActualHeight));
            return new Native.RectI {Left=(int)Math.Round(a.X),Top=(int)Math.Round(a.Y),Right=(int)Math.Round(b.X),Bottom=(int)Math.Round(b.Y)};
        }
    }

    // The label of the keyboard layout the foreground window is typing with; for Korean, whether Hangul input is on.
    internal static class InputLanguage
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr window,IntPtr process);
        [System.Runtime.InteropServices.DllImport("user32.dll")]static extern IntPtr GetKeyboardLayout(uint thread);
        [System.Runtime.InteropServices.DllImport("imm32.dll")]static extern IntPtr ImmGetDefaultIMEWnd(IntPtr window);
        [System.Runtime.InteropServices.DllImport("user32.dll")]static extern IntPtr SendMessageTimeout(IntPtr window,uint message,IntPtr wparam,IntPtr lparam,uint flags,uint timeout,out IntPtr result);
        internal static string Current()
        {
            try
            {
                var window=Native.GetForegroundWindow();
                int id=GetKeyboardLayout(window==IntPtr.Zero?0:GetWindowThreadProcessId(window,IntPtr.Zero)).ToInt32()&0xffff;
                if(id==0x412)
                {
                    IntPtr open=IntPtr.Zero,ime=window==IntPtr.Zero?IntPtr.Zero:ImmGetDefaultIMEWnd(window);
                    // WM_IME_CONTROL / IMC_GETOPENSTATUS, abandoned at once when the window does not answer.
                    if(ime!=IntPtr.Zero)SendMessageTimeout(ime,0x283,new IntPtr(5),IntPtr.Zero,2,20,out open);
                    return open!=IntPtr.Zero?"한":"EN";
                }
                return new System.Globalization.CultureInfo(id).TwoLetterISOLanguageName.ToUpperInvariant();
            }
            catch(Exception){return "EN";}
        }
    }
}
