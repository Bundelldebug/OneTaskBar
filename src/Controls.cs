using System;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Documents;
using System.Windows.Media.Animation;
using System.Threading.Tasks;

namespace OneTaskBar
{
    internal static class GrayPanel
    {
        // Neutral frosted glass retains live background color with readable dark content.
        internal static readonly Brush Glass=LightPanel.Tint("#8F858E87","#75747F77","#887F8981"),Card=Paint.Brush("#55ADB5AE"),Muted=LightPanel.Muted;
        internal static void Apply(DependencyObject root)
        {
            LightPanel.Apply(root);
        }
    }
    internal static class LightPanel
    {
        internal static readonly Brush Glass=Tint("#9BE5EBE7","#7BC5D0C9","#8FD8E2DB"),DockGlass=Tint("#AADFE7E2","#82C3D0C7","#96D5E1D9"),Muted=Paint.Brush("#FF4C5B52"),Card=Paint.Brush("#24FFFFFF");
        internal static Brush Tint(string top,string middle,string bottom)
        {
            var brush=new LinearGradientBrush();brush.StartPoint=new Point(0,0);brush.EndPoint=new Point(0,1);
            brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(top),0));brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(middle),.42));brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(bottom),1));brush.Freeze();return brush;
        }
        internal static void Apply(DependencyObject root)
        {
            var text=root as TextBlock;if(text!=null){if(text.Foreground==Paint.Text)text.Foreground=Paint.Ink;else if(text.Foreground==Paint.Muted)text.Foreground=Muted;}
            var control=root as Control;if(control!=null&&control.Foreground==Paint.Text)control.Foreground=Paint.Ink;
            var glyph=root as Glyph;if(glyph!=null){if(glyph.Color==Paint.Text)glyph.Color=Paint.Ink;else if(glyph.Color==Paint.Muted)glyph.Color=Muted;}
            foreach(object child in LogicalTreeHelper.GetChildren(root)){var element=child as DependencyObject;if(element!=null)Apply(element);}
        }
        internal static Border IconFrame(UIElement child,double size)
        {
            return new Border {Width=size,Height=size,CornerRadius=new CornerRadius(size/2),BorderBrush=Brushes.Transparent,BorderThickness=new Thickness(0),Background=Paint.Brush("#16FFFFFF"),Child=child};
        }
    }
    internal static class Paint
    {
        static ControlTemplate buttonTemplate;
        static readonly Geometry gear=Gear(),windowsGear=Gear(6,4.2);
        static Geometry Gear(int teeth=8,double hole=3.5)
        {
            var shape=new StreamGeometry {FillRule=FillRule.EvenOdd};using(var path=shape.Open()){
                for(int i=0;i<teeth*6;i++){double angle=i/(teeth*6.0)*2*Math.PI-Math.PI/2,radius=i%6==0||i%6==5?8.3:10.6;var point=new Point(12+radius*Math.Cos(angle),12+radius*Math.Sin(angle));if(i==0)path.BeginFigure(point,true,true);else path.LineTo(point,true,false);}
                path.BeginFigure(new Point(12+hole,12),true,true);path.ArcTo(new Point(12-hole,12),new Size(hole,hole),0,false,SweepDirection.Clockwise,true,false);path.ArcTo(new Point(12+hole,12),new Size(hole,hole),0,false,SweepDirection.Clockwise,true,false);
            }shape.Freeze();return shape;
        }
        internal static SolidColorBrush Brush(string value) { var b=(SolidColorBrush)new BrushConverter().ConvertFromString(value); b.Freeze(); return b; }
        internal static readonly Brush Text=Brush("#FFFFFFFF"), Muted=Brush("#FFDBE3DF"), Ink=Brush("#FF19221E");
        internal static void Label(DrawingContext dc,string text,double size,Brush color,Point point,FontWeight weight)
        {
            var f=new FormattedText(text,CultureInfo.GetCultureInfo("ko-KR"),FlowDirection.LeftToRight,
                new Typeface(SystemFonts.MessageFontFamily,FontStyles.Normal,weight,FontStretches.Normal),size,color,1.0);
            dc.DrawText(f,point);
        }
        internal static void Center(DrawingContext dc,string text,double size,Brush color,Rect bounds)
        {
            var f=new FormattedText(text,CultureInfo.GetCultureInfo("ko-KR"),FlowDirection.LeftToRight,
                new Typeface(SystemFonts.MessageFontFamily,FontStyles.Normal,FontWeights.SemiBold,FontStretches.Normal),size,color,1.0);
            dc.DrawText(f,new Point(bounds.X+(bounds.Width-f.Width)/2,bounds.Y+(bounds.Height-f.Height)/2));
        }
        internal static void Icon(DrawingContext dc,string kind,Brush brush,Point center,double size,int generation=0)
        {
            var pen=new Pen(brush,1.8) { StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round };
            dc.PushTransform(new TranslateTransform(center.X-size/2,center.Y-size/2)); dc.PushTransform(new ScaleTransform(size/24,size/24));
            Action<double,double,double,double> line=(x,y,a,b)=>dc.DrawLine(pen,new Point(x,y),new Point(a,b));
            if(kind=="brightness") {
                dc.DrawEllipse(brush,null,new Point(12,12),4.2,4.2);
                for(int i=0;i<8;i++) { double a=i*Math.PI/4; line(12+8*Math.Cos(a),12+8*Math.Sin(a),12+10.6*Math.Cos(a),12+10.6*Math.Sin(a)); }
            } else if(kind=="wifi"||kind=="wifi-off"||kind=="wifi-disconnected") {
                bool badge=kind=="wifi"&&generation>=4&&generation<=7;if(badge)dc.PushTransform(new ScaleTransform(.82,.82,0,20));
                var wifiPen=new Pen(brush,2.3){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};
                dc.DrawGeometry(null,wifiPen,Geometry.Parse("M2.8,8.4 Q12,-.4 21.2,8.4 M6.1,12 Q12,6.3 17.9,12 M9.4,15.5 Q12,13.1 14.6,15.5"));dc.DrawEllipse(brush,null,new Point(12,19.7),1.5,1.5);
                if(badge){dc.Pop();Label(dc,generation.ToString(CultureInfo.InvariantCulture),9,brush,new Point(17.6,.2),FontWeights.Bold);}
                if(kind=="wifi-off")line(3,3,21,21);else if(kind=="wifi-disconnected"){line(18,15,22,19);line(22,15,18,19);}
            } else if(kind=="bluetooth") {
                dc.DrawGeometry(null,pen,Geometry.Parse("M6,6 L18,17 12,22 12,2 18,7 6,18"));
            } else if(kind=="volume" || kind=="mute" || kind=="volume-low") {
                dc.DrawGeometry(brush,new Pen(brush,.6){LineJoin=PenLineJoin.Round},Geometry.Parse("M3,9 L7,9 12,5 12,19 7,15 3,15 Z"));
                if(kind=="mute") { line(16,9,22,15); line(22,9,16,15); }
                else {dc.DrawGeometry(null,pen,Geometry.Parse("M15.5,8 Q19.5,12 15.5,16"));if(kind!="volume-low")dc.DrawGeometry(null,pen,Geometry.Parse("M18.5,4.5 Q25,12 18.5,19.5"));}
            } else if(kind=="bolt") dc.DrawGeometry(brush,null,Geometry.Parse("M14,1 L4,14 11,14 9,23 21,9 14,9 Z"));
            else if(kind=="apps") { for(int i=0;i<9;i++) dc.DrawEllipse(brush,null,new Point(5+(i%3)*7,5+(i/3)*7),1.5,1.5); }
            else if(kind=="windows-start") {foreach(int x in new[]{2,13})foreach(int y in new[]{2,13})dc.DrawRoundedRectangle(brush,null,new Rect(x,y,9,9),.5,.5);}
            else if(kind=="grip") {for(int i=0;i<6;i++)dc.DrawEllipse(brush,null,new Point(8+(i%2)*8,5+(i/2)*7),1.6,1.6);}
            else if(kind=="edit") {dc.DrawGeometry(null,pen,Geometry.Parse("M4,16 L16,4 20,8 8,20 3,21 Z M14,6 L18,10"));}
            else if(kind=="search") { dc.DrawEllipse(null,pen,new Point(10,10),6.5,6.5); line(15,15,22,22); }
            else if(kind=="network"||kind=="network-off") {dc.DrawRoundedRectangle(null,pen,new Rect(3,3,18,12),2,2);line(12,15,12,21);line(7,21,17,21);if(kind=="network-off")line(3,22,22,2);}
            else if(kind=="chevron") { line(8,5,15,12); line(15,12,8,19); }
            else if(kind=="up") { line(5,15,12,8); line(12,8,19,15); }
            else if(kind=="close") {line(6,6,18,18);line(18,6,6,18);}
            else if(kind=="pin") {dc.DrawGeometry(null,pen,Geometry.Parse("M8,3 L16,3 15,10 19,14 5,14 9,10 Z M12,14 L12,22"));}
            else if(kind=="airplane") dc.DrawGeometry(brush,null,Geometry.Parse("M11,2 Q12,0 13,2 L14,9 23,15 23,17 14,14 14,20 17,22 17,24 12,22 7,24 7,22 10,20 10,14 1,17 1,15 10,9 Z"));
            else if(kind=="battery") { dc.DrawRoundedRectangle(null,pen,new Rect(1,6,20,12),3,3); line(23,10,23,14); dc.DrawGeometry(brush,null,Geometry.Parse("M12,7 L7,13 11,13 10,17 17,10 13,10 Z")); }
            else if(kind=="person") { dc.DrawEllipse(brush,null,new Point(12,4),2.5,2.5); line(3,9,21,9); line(12,7,12,15); line(12,15,7,23); line(12,15,17,23); }
            else if(kind=="settings") dc.DrawGeometry(brush,null,gear);
            else if(kind=="windows-settings") dc.DrawGeometry(Paint.Ink,null,windowsGear);
            else if(kind=="window") { dc.DrawRoundedRectangle(null,pen,new Rect(2,3,20,18),2,2); line(2,8,22,8); }
            else if(kind=="play") dc.DrawGeometry(brush,null,Geometry.Parse("M6,3 L22,12 6,21 Z"));
            else if(kind=="pause") {dc.DrawRoundedRectangle(brush,null,new Rect(5,3,5,18),1.5,1.5);dc.DrawRoundedRectangle(brush,null,new Rect(14,3,5,18),1.5,1.5);}
            else if(kind=="next"||kind=="previous") {if(kind=="previous")dc.PushTransform(new ScaleTransform(-1,1,12,12));dc.DrawGeometry(brush,null,Geometry.Parse("M4,4 L17,12 4,20 Z"));dc.DrawRoundedRectangle(brush,null,new Rect(18,4,3,16),1,1);if(kind=="previous")dc.Pop();}
            else if(kind=="moon") dc.DrawGeometry(brush,null,Geometry.Parse("M16,2 A10,10 0 1 0 22,17 A10,10 0 0 1 16,2 Z"));
            else if(kind=="lock") {dc.DrawRoundedRectangle(null,pen,new Rect(5,10,14,12),2,2);dc.DrawGeometry(null,pen,Geometry.Parse("M8,10 L8,6 A4,4 0 0 1 16,6 L16,10"));dc.DrawEllipse(brush,null,new Point(12,15),1.2,1.2);line(12,15,12,18);}
            else if(kind=="power") dc.DrawGeometry(null,pen,Geometry.Parse("M12,1 L12,12 M7,4 A9,9 0 1 0 17,4"));
            else if(kind=="rotation") {dc.DrawGeometry(null,pen,Geometry.Parse("M3,11 A9,9 0 0 1 20,6 M17,3 L21,6 17,9 M21,13 A9,9 0 0 1 4,18 M7,15 L3,18 7,21"));}
            else if(kind=="devices") {dc.DrawRoundedRectangle(null,pen,new Rect(7,3,10,18),2,2);line(10,18,14,18);dc.DrawGeometry(null,pen,Geometry.Parse("M3,8 L1,12 3,16 M21,8 L23,12 21,16"));}
            else if(kind=="focus") {dc.DrawEllipse(null,pen,new Point(12,12),10,10);dc.DrawGeometry(brush,null,Geometry.Parse("M12,3 L12,12 20,7 A9,9 0 0 0 12,3 Z"));}
            else { dc.DrawEllipse(null,pen,new Point(12,12),8,8); dc.DrawEllipse(brush,null,new Point(12,12),2,2); }
            dc.Pop(); dc.Pop();
        }
        internal static Button Button(UIElement content,Action action,string hint)
        {
            var b=new Button { Content=content,Foreground=Text,Background=Brush("#1CFFFFFF"),BorderThickness=new Thickness(0),
                Padding=new Thickness(12,8,12,8),Cursor=Cursors.Hand,ToolTip=hint,HorizontalContentAlignment=HorizontalAlignment.Center };
            b.Resources["ButtonRadius"]=new CornerRadius(18);
            if(buttonTemplate==null)buttonTemplate=(ControlTemplate)System.Windows.Markup.XamlReader.Parse(
                "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Border x:Name='p' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='{DynamicResource ButtonRadius}' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='Center'/></Border></ControlTemplate>");
            b.Template=buttonTemplate;AutomationProperties.SetName(b,hint); b.Click+=(s,e)=>action();Motion.Button(b);return b;
        }
        internal static TextBlock TextBlock(string text,double size,Brush color)
        {return new TextBlock {Text=text,FontFamily=SystemFonts.MessageFontFamily,FontSize=size,Foreground=color,VerticalAlignment=VerticalAlignment.Center};}
        internal static Button Action(string text,Action action) { return Button(TextBlock(text,13,Text),action,text); }
    }
    internal sealed class Glyph : FrameworkElement
    {
        public string Kind; public Brush Color=Paint.Text;public int Generation;
        public Glyph(string kind,double size) { Kind=kind; Width=Height=size; IsHitTestVisible=false; }
        protected override void OnRender(DrawingContext dc) { Paint.Icon(dc,Kind,Color,new Point(ActualWidth/2,ActualHeight/2),Math.Min(ActualHeight,ActualWidth)-2,Generation); }
    }
    internal sealed class PillSlider : FrameworkElement
    {
        public string Kind; double value; bool dragging;
        static readonly DependencyProperty DisplayValueProperty=DependencyProperty.Register("DisplayValue",typeof(double),typeof(PillSlider),new FrameworkPropertyMetadata(0.0,FrameworkPropertyMetadataOptions.AffectsRender));
        public bool Tablet=true,AnimateValue=true;
        public bool Light;
        public event Action<double> Changed;
        public double Value { get { return value; } set { double next=Clamp(value);if(this.value==next)return;this.value=next;double from=(double)GetValue(DisplayValueProperty);BeginAnimation(DisplayValueProperty,null);SetValue(DisplayValueProperty,next);if(AnimateValue&&!dragging&&Motion.Enabled&&IsVisible&&PresentationSource.FromVisual(this)!=null)BeginAnimation(DisplayValueProperty,Motion.Tween(from,next,160));InvalidateVisual(); } }
        public static double Clamp(double value) { return Double.IsNaN(value) || Double.IsInfinity(value) ? 0 : Math.Max(0,Math.Min(100,value)); }
        public static double FillWidth(double width,double value) { return Math.Max(0,width)*Clamp(value)/100; }
        public PillSlider(string kind) { Kind=kind; Height=44; MinWidth=90; Focusable=true; Cursor=Cursors.Hand; SnapsToDevicePixels=true;
            AutomationProperties.SetName(this,kind=="brightness" ? "밝기" : "음량"); }
        protected override AutomationPeer OnCreateAutomationPeer() { return new PillPeer(this); }
        protected override void OnRender(DrawingContext dc)
        {
            double w=ActualWidth,h=ActualHeight; if(w<=0 || h<=0) return;
            double shown=(double)GetValue(DisplayValueProperty);string iconKind=Kind.StartsWith("volume",StringComparison.Ordinal)?SoundIcon.For(value,false,IsEnabled):Kind;
            var track=new Rect(0,0,w,h); var fill=new RectangleGeometry(new Rect(0,0,FillWidth(w,shown),h),h/2,h/2);
            dc.DrawRoundedRectangle(Paint.Brush(IsEnabled ? "#3C77817F" : "#286E7775"),new Pen(Paint.Brush("#288B9692"),.7),track,h/2,h/2);
            if(shown>0) dc.DrawGeometry(Tablet?Paint.Brush("#FFF4F8F5"):Brushes.Black,null,fill);
            var c=new Point(h*.70,h/2); double icon=h*.44;
            Paint.Icon(dc,iconKind,Tablet&&!Light?Paint.Brush("#D8E8EDEA"):Paint.Brush("#FF303A36"),c,icon);
            if(shown>0) {dc.PushClip(fill);Paint.Icon(dc,iconKind,Tablet?Paint.Brush(!Light&&Kind=="brightness"?"#FFEC7D4E":"#FF35413B"):Paint.Brush("#FFF5F5F1"),c,icon);dc.Pop();}
            if(IsKeyboardFocused) dc.DrawRoundedRectangle(null,new Pen(Paint.Brush("#9ED4F0E5"),1.5),new Rect(1,1,w-2,h-2),h/2,h/2);
        }
        void Update(double x) { Value=Math.Round(Clamp(x/Math.Max(1,ActualWidth)*100)); if(Changed!=null) Changed(Value); }
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) { base.OnMouseLeftButtonDown(e); Focus(); dragging=true; CaptureMouse(); Update(e.GetPosition(this).X); e.Handled=true; }
        protected override void OnMouseMove(MouseEventArgs e) { if(dragging && e.LeftButton==MouseButtonState.Pressed) Update(e.GetPosition(this).X); }
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) { dragging=false; ReleaseMouseCapture(); e.Handled=true; }
        protected override void OnLostMouseCapture(MouseEventArgs e) { dragging=false; }
        protected override void OnMouseWheel(MouseWheelEventArgs e) { Value+=Math.Sign(e.Delta)*2; if(Changed!=null) Changed(Value); e.Handled=true; }
        protected override void OnKeyDown(KeyEventArgs e) {
            double n=Value;
            if(e.Key==Key.Home) n=0; else if(e.Key==Key.End) n=100; else if(e.Key==Key.Left || e.Key==Key.Down) n-=2;
            else if(e.Key==Key.Right || e.Key==Key.Up) n+=2; else { base.OnKeyDown(e); return; }
            Value=n; if(Changed!=null) Changed(Value); e.Handled=true;
        }
        sealed class PillPeer : FrameworkElementAutomationPeer, IRangeValueProvider
        {
            PillSlider Pill { get { return (PillSlider)Owner; } }
            public PillPeer(PillSlider owner):base(owner) { }
            protected override AutomationControlType GetAutomationControlTypeCore() { return AutomationControlType.Slider; }
            protected override string GetClassNameCore() { return "OneTaskBar.PillSlider"; }
            public override object GetPattern(PatternInterface pattern) { return pattern==PatternInterface.RangeValue ? this : base.GetPattern(pattern); }
            public bool IsReadOnly { get { return !Pill.IsEnabled; } } public double LargeChange { get { return 10; } } public double SmallChange { get { return 2; } }
            public double Maximum { get { return 100; } } public double Minimum { get { return 0; } } public double Value { get { return Pill.Value; } }
            public void SetValue(double value) { if(IsReadOnly) throw new ElementNotEnabledException(); Pill.Value=value; if(Pill.Changed!=null) Pill.Changed(Pill.Value); }
        }
    }
    internal class GlassWindow : Window
    {
        int motion;bool closed,hiding;Brush glassTint;TaskCompletionSource<bool> dismissal;
        public readonly Border Surface;
        public double Radius=28; public bool Dark=true; public bool HasBlur;internal bool SoftCorners=true;
        internal bool StableSurface;internal FrameworkElement TransitionContent;
        public GlassWindow()
        {
            MenuTheme.Install();
            WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.NoResize; AllowsTransparency=true;Background=Brushes.Transparent;
            ShowInTaskbar=false;Topmost=true;FontFamily=SystemFonts.MessageFontFamily;
            Surface=new Border { Background=Paint.Brush("#B32C3932"),BorderBrush=Paint.Brush("#38FFFFFF"),BorderThickness=new Thickness(.7),CornerRadius=new CornerRadius(Radius) };
            Content=new AdornerDecorator {Child=Surface};
            var touchScroll=new Style(typeof(ScrollViewer));
            touchScroll.Setters.Add(new Setter(ScrollViewer.PanningModeProperty,PanningMode.VerticalFirst));
            touchScroll.Setters.Add(new Setter(ScrollViewer.PanningDecelerationProperty,.001));
            touchScroll.Setters.Add(new Setter(ScrollViewer.PanningRatioProperty,1.0));
            touchScroll.Setters.Add(new Setter(ScrollViewer.CanContentScrollProperty,false));
            Resources.Add(typeof(ScrollViewer),touchScroll);
            Resources.Add(typeof(System.Windows.Controls.Primitives.ScrollBar),System.Windows.Markup.XamlReader.Parse(
                "<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ScrollBar'><Setter Property='Width' Value='4'/><Setter Property='Background' Value='Transparent'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ScrollBar'><Track x:Name='PART_Track' IsDirectionReversed='True' Orientation='{TemplateBinding Orientation}'><Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType='Thumb'><Border Background='#55D5DFD9' CornerRadius='2'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb></Track></ControlTemplate></Setter.Value></Setter></Style>"));
            SourceInitialized+=(s,e)=> {
                var hwnd=new WindowInteropHelper(this).Handle;Native.SetWindowLong(hwnd,-20,(Native.GetWindowLong(hwnd,-20)|0x80)&~0x40000);
                glassTint=Surface.Background;HasBlur=Native.Blur(this,Radius,Dark);ApplyMaterial();
                var source=HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);if(source!=null)source.AddHook((IntPtr h,int msg,IntPtr w,IntPtr l,ref bool handled)=> {
                    if(msg==0x5||msg==0x2E0)Dispatcher.BeginInvoke(new Action(()=> {if(!closed)Native.Round(this,Radius);}),System.Windows.Threading.DispatcherPriority.Render);return IntPtr.Zero;
                });
            };
            SizeChanged+=(s,e)=> { if(new WindowInteropHelper(this).Handle!=IntPtr.Zero) Native.Round(this,Radius); };
            Preferences.Changed+=ApplyMaterial;
            Closed+=(s,e)=> {closed=true;++motion;CompleteDismissal(false);Preferences.Changed-=ApplyMaterial;};
            PreviewKeyDown+=(s,e)=> { if(e.Key==Key.Escape&&!DragReorder.Active) { Hide(); e.Handled=true; } };
        }
        void ApplyMaterial()
        {
            if(glassTint==null)return;
            bool enabled=HasBlur&&Preferences.Current.Blur;Native.EnableBlur(this,enabled);Surface.Background=enabled?glassTint:Paint.Brush(Dark?"#FF344139":"#FFE0E5E2");
        }
        public void ShowAt(System.Drawing.Rectangle screen,double x,double y,double width,double height)
        {
            ++motion;Surface.BeginAnimation(UIElement.OpacityProperty,null);Surface.Opacity=1;
            Width=width; Height=height;
            new WindowInteropHelper(this).EnsureHandle(); Native.Place(this,screen,x,y,width,height); Show();
            Native.Place(this,screen,x,y,width,height); Native.Round(this,Radius);
        }
        public new void Show() {++motion;hiding=false;CompleteDismissal(false);Surface.IsHitTestVisible=true;Surface.BeginAnimation(UIElement.OpacityProperty,null);Surface.Opacity=1;Surface.RenderTransform=Transform.Identity;if(StableSurface&&TransitionContent!=null){TransitionContent.BeginAnimation(OpacityProperty,null);TransitionContent.Opacity=1;TransitionContent.RenderTransform=Transform.Identity;}base.Show();}
        public void Pop()
        {
            if(!SystemParameters.ClientAreaAnimation||!Preferences.Current.Animations)return;
            if(StableSurface&&TransitionContent!=null){++motion;Surface.RenderTransform=Transform.Identity;Surface.Opacity=1;TransitionContent.Opacity=1;var slide=new TranslateTransform(0,12);TransitionContent.RenderTransform=slide;slide.BeginAnimation(TranslateTransform.YProperty,Motion.Tween(12,0,220));TransitionContent.BeginAnimation(OpacityProperty,Motion.Tween(0,1,180));return;}
            ++motion;Surface.RenderTransformOrigin=new Point(.85,1);var scale=new ScaleTransform(.91,.91);var translate=new TranslateTransform(0,14);var group=new TransformGroup();group.Children.Add(scale);group.Children.Add(translate);Surface.RenderTransform=group;
            var frames=new DoubleAnimationUsingKeyFrames();frames.KeyFrames.Add(new SplineDoubleKeyFrame(1.008,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(245)),new KeySpline(.18,.8,.16,1)));frames.KeyFrames.Add(new SplineDoubleKeyFrame(1,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(355)),new KeySpline(.25,0,.3,1)));
            scale.BeginAnimation(ScaleTransform.ScaleXProperty,frames);scale.BeginAnimation(ScaleTransform.ScaleYProperty,frames);
            translate.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(14,0,TimeSpan.FromMilliseconds(310)) {EasingFunction=new CubicEase {EasingMode=EasingMode.EaseOut}});
            Surface.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(150)));
        }
        public new void Hide()
        {
            if(closed||!IsVisible){CompleteDismissal(!closed);return;}if(hiding)return;
            if(!Motion.Enabled){base.Hide();CompleteDismissal(true);return;}
            if(StableSurface&&TransitionContent!=null){hiding=true;Surface.IsHitTestVisible=false;int contentToken=++motion;var slide=new TranslateTransform();TransitionContent.RenderTransform=slide;slide.BeginAnimation(TranslateTransform.YProperty,Motion.Tween(0,7,130));var fade=Motion.Tween(TransitionContent.Opacity,0,130);TransitionContent.Opacity=0;fade.Completed+=(s,e)=> {if(!closed&&contentToken==motion){base.Hide();hiding=false;Surface.IsHitTestVisible=true;TransitionContent.BeginAnimation(OpacityProperty,null);TransitionContent.Opacity=1;TransitionContent.RenderTransform=Transform.Identity;CompleteDismissal(true);}};TransitionContent.BeginAnimation(OpacityProperty,fade);return;}
            hiding=true;Surface.IsHitTestVisible=false;int token=++motion;Surface.RenderTransformOrigin=new Point(.85,1);var scale=new ScaleTransform(1,1);var shift=new TranslateTransform();var group=new TransformGroup();group.Children.Add(scale);group.Children.Add(shift);Surface.RenderTransform=group;
            scale.BeginAnimation(ScaleTransform.ScaleXProperty,new DoubleAnimation(.97,TimeSpan.FromMilliseconds(180)));scale.BeginAnimation(ScaleTransform.ScaleYProperty,new DoubleAnimation(.97,TimeSpan.FromMilliseconds(180)));shift.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(9,TimeSpan.FromMilliseconds(180)));
            var animation=new DoubleAnimation(0,TimeSpan.FromMilliseconds(180)) {EasingFunction=new CubicEase {EasingMode=EasingMode.EaseIn}};
            animation.Completed+=(s,e)=> {if(!closed&&token==motion){base.Hide();hiding=false;Surface.IsHitTestVisible=true;Surface.BeginAnimation(UIElement.OpacityProperty,null);Surface.Opacity=1;Surface.RenderTransform=Transform.Identity;CompleteDismissal(true);}};Surface.BeginAnimation(UIElement.OpacityProperty,animation);
        }
        public Task<bool> DismissAsync()
        {
            if(closed)return Task.FromResult(false);if(!IsVisible)return Task.FromResult(true);
            if(dismissal==null)dismissal=new TaskCompletionSource<bool>();var pending=dismissal.Task;Hide();return pending;
        }
        internal void HideImmediately(){++motion;hiding=false;Surface.BeginAnimation(OpacityProperty,null);Surface.Opacity=1;Surface.RenderTransform=Transform.Identity;Surface.IsHitTestVisible=true;if(TransitionContent!=null){TransitionContent.BeginAnimation(OpacityProperty,null);TransitionContent.Opacity=1;TransitionContent.RenderTransform=Transform.Identity;}base.Hide();CompleteDismissal(true);}
        void CompleteDismissal(bool completed) {var pending=dismissal;dismissal=null;if(pending!=null)pending.TrySetResult(completed);}
    }
}
