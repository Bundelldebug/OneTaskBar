using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace OneTaskBar
{
    internal sealed class DesktopWidgetView : Grid
    {
        readonly ShellController shell;
        internal readonly DesktopWidgetSlot Slot;
        readonly Border card;
        readonly Grid body=new Grid(),editor=new Grid();
        readonly TextBlock primary,secondary;
        readonly BatteryPill battery;
        internal readonly QuickWidgetTile Toggle;
        internal readonly MediaPlayerView Player;
        readonly Action<DesktopWidgetSlot,System.Drawing.Point,System.Drawing.Point,bool,bool> gesture;
        readonly Thumb move;
        readonly TextBlock notice;
        System.Drawing.Point origin;
        string lastClock,lastDate,lastBattery;
        bool retired;
        readonly InfoTile tile;
        readonly Border backdrop=new Border {IsHitTestVisible=false};
        TileScheme scheme;
        bool pressed,moving,pressedControl,held;
        readonly Action<DesktopWidgetSlot> select;
        DispatcherTimer watch;
        DateTime pressedAt;
        const int HoldMilliseconds=450,WatchMilliseconds=50;
        // Replaced only by fixtures, which have no physical button to hold.
        internal Func<bool> ButtonDown=DesktopWidgetNative.LeftButtonDown;
        // Which corner handle is resizing: 1 lower right, 2 lower left, 3 upper right, 4 upper left.
        internal int ResizeCorner {get;private set;}
        internal DesktopWidgetView(ShellController controller,DesktopWidgetSlot slot,
            Action<DesktopWidgetSlot,System.Drawing.Point,System.Drawing.Point,bool,bool> drag,Action<DesktopWidgetSlot> choose=null,TileScheme tileScheme=null)
        {
            shell=controller;
            Slot=slot;
            gesture=drag;
            select=choose;
            ResizeCorner=1;
            PreviewMouseLeftButtonDown+=(s,e)=>
            {
                EndPress();
                if(editor.Visibility==Visibility.Visible)return;
                Press(OnControl(e.OriginalSource as DependencyObject),Forms.Cursor.Position);
            };
            PreviewMouseMove+=(s,e)=>
            {
                if(!pressed)return;
                if(e.LeftButton!=MouseButtonState.Pressed)
                {
                    EndPress();
                    return;
                }
                Move(Forms.Cursor.Position);
            };
            PreviewMouseLeftButtonUp+=(s,e)=>
            {
                if(!pressed)return;
                if(moving)
                {
                    EndPress();
                    e.Handled=true;
                    return;
                }
                e.Handled=Release();
            };
            // Without capture the release happens elsewhere and this widget never hears about it.
            MouseLeave+=(s,e)=>
            {
                if(pressed&&!moving&&!held)EndPress();
            };
            LostMouseCapture+=(s,e)=>
            {
                if(e.OriginalSource==this&&(moving||held))EndPress();
            };
            MouseRightButtonUp+=(s,e)=>
            {
                if(select==null)return;
                select(Slot);
                e.Handled=true;
            };
            UseLayoutRounding=true;
            AutomationProperties.SetName(this,"바탕화면 위젯 · "+DesktopWidgetCatalog.Title(slot.Key));
            card=new Border {CornerRadius=new CornerRadius(28),Background=Paint.Brush("#DC28323C"),BorderBrush=Paint.Brush("#46FFFFFF"),BorderThickness=new Thickness(1),Child=body};
            Children.Add(backdrop);
            Children.Add(card);
            if(InfoTileCatalog.Known(slot.Key))
            {
                scheme=tileScheme??TileScheme.Desktop();
                card.Background=scheme.Surface;
                card.BorderThickness=new Thickness(0);
                tile=InfoTileCatalog.Create(slot.Key,false,shell,scheme);
                body.Children.Add(new Viewbox {Stretch=Stretch.Uniform,Child=tile});
            }
            else if(slot.Key=="clock"||slot.Key=="battery")
            {
                var words=new StackPanel {VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center};
                if(slot.Key=="battery")
                {
                    battery=new BatteryPill {Width=100,Height=38,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,0,0,8)};
                    words.Children.Add(battery);
                }
                primary=Paint.TextBlock("",slot.Key=="clock"?42:16,Paint.ControlWhite);
                primary.FontWeight=FontWeights.SemiBold;
                primary.TextAlignment=TextAlignment.Center;
                secondary=Paint.TextBlock("",12,Paint.Brush("#FFDBE4ED"));
                secondary.TextAlignment=TextAlignment.Center;
                secondary.Margin=new Thickness(0,4,0,0);
                words.Children.Add(primary);
                words.Children.Add(secondary);
                body.Children.Add(new Viewbox {Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,Child=words,Margin=new Thickness(16)});
            }
            else if(slot.Key=="media")
            {
                Player=new MediaPlayerView(shell.Media,shell.SeekMedia,()=>shell.ShowDetail("sound"),condensed:true,openApp:shell.OpenMediaApplication,
                    sourceApp:id=>MediaApplication.Resolve(id,shell.Running.Concat(shell.Apps)));
                Player.VerticalAlignment=VerticalAlignment.Center;
                body.Children.Add(Player);
            }
            else
            {
                Toggle=new QuickWidgetTile(DesktopWidgetCatalog.Icon(slot.Key),DesktopWidgetCatalog.Title(slot.Key),Invoke);
                body.Children.Add(Toggle);
                Toggle.Margin=new Thickness(8);
            }
            notice=Paint.TextBlock("",11,Paint.ControlWhite);
            notice.TextWrapping=TextWrapping.Wrap;
            notice.Visibility=Visibility.Collapsed;
            notice.Background=Paint.Brush("#F0323B45");
            notice.VerticalAlignment=VerticalAlignment.Bottom;
            notice.Margin=new Thickness(8);
            body.Children.Add(notice);
            move=new Thumb {Cursor=Cursors.SizeAll,Background=Brushes.Transparent,Template=TransparentThumb()};
            AutomationProperties.SetName(move,"위젯 이동. 방향키로 이동, Shift+방향키로 크기 조절");
            editor.Children.Add(move);
            Children.Add(editor);
            Editing(false);
            Wire(move,false,1);
            Corner(HorizontalAlignment.Right,VerticalAlignment.Bottom,1,Cursors.SizeNWSE);
            Corner(HorizontalAlignment.Left,VerticalAlignment.Bottom,2,Cursors.SizeNESW);
            Corner(HorizontalAlignment.Right,VerticalAlignment.Top,3,Cursors.SizeNESW);
            Corner(HorizontalAlignment.Left,VerticalAlignment.Top,4,Cursors.SizeNWSE);
            SizeChanged+=(s,e)=>Shape();
        }
        static ControlTemplate TransparentThumb()
        {
            var factory=new FrameworkElementFactory(typeof(Border));
            factory.SetValue(Border.BackgroundProperty,Brushes.Transparent);
            return new ControlTemplate(typeof(Thumb)) {VisualTree=factory};
        }
        static ControlTemplate HandleThumb()
        {
            var factory=new FrameworkElementFactory(typeof(Border));
            factory.SetValue(Border.WidthProperty,14.0);
            factory.SetValue(Border.HeightProperty,14.0);
            factory.SetValue(Border.CornerRadiusProperty,new CornerRadius(7));
            factory.SetValue(Border.BackgroundProperty,Paint.ControlWhite);
            factory.SetValue(Border.BorderBrushProperty,Paint.Brush("#FF4F8BF0"));
            factory.SetValue(Border.BorderThicknessProperty,new Thickness(3));
            return new ControlTemplate(typeof(Thumb)) {VisualTree=factory};
        }
        void Corner(HorizontalAlignment across,VerticalAlignment down,int corner,Cursor cursor)
        {
            var handle=new Thumb {Width=28,Height=28,Cursor=cursor,HorizontalAlignment=across,VerticalAlignment=down,Template=HandleThumb(),Background=Brushes.Transparent};
            AutomationProperties.SetName(handle,"위젯 크기 조절");
            editor.Children.Add(handle);
            Wire(handle,true,corner);
        }
        // A plain click or a long press opens editing. A click on a control keeps its own
        // action, so only a long press there edits.
        static bool OnControl(DependencyObject source)
        {
            while(source is Visual)
            {
                if(source is ButtonBase||source is TextBoxBase||source is RangeBase)return true;
                source=VisualTreeHelper.GetParent(source);
            }
            return false;
        }
        internal void Press(bool onControl,System.Drawing.Point at)
        {
            pressed=true;
            moving=held=false;
            pressedControl=onControl;
            origin=at;
            pressedAt=DateTime.UtcNow;
            if(watch==null)
            {
                watch=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(WatchMilliseconds)};
                watch.Tick+=(s,e)=>WatchPress();
            }
            watch.Start();
        }
        // Runs only while the button is down. The mouse-up can go missing, so the button itself decides.
        void WatchPress()
        {
            if(!pressed||retired)
            {
                StopWatch();
                return;
            }
            if(!ButtonDown())
            {
                EndPress();
                return;
            }
            if(moving||held||(DateTime.UtcNow-pressedAt).TotalMilliseconds<HoldMilliseconds)return;
            held=true;
            CaptureMouse();
            if(select!=null)select(Slot);
        }
        void StopWatch()
        {
            if(watch!=null)watch.Stop();
        }
        internal void Move(System.Drawing.Point at)
        {
            if(!moving)
            {
                if(Math.Abs(at.X-origin.X)<8&&Math.Abs(at.Y-origin.Y)<8)return;
                moving=true;
                Opacity=.85;
                gesture(Slot,origin,origin,false,false);
                CaptureMouse();
                // Capture can be refused and lost at once, which has already ended this move.
                if(!moving)return;
            }
            gesture(Slot,origin,at,false,false);
        }
        // Ends a press however it stopped. A move in progress is committed where the pointer is.
        void EndPress()
        {
            StopWatch();
            bool wasMoving=moving;
            pressed=moving=held=false;
            if(wasMoving)Opacity=1;
            if(IsMouseCaptured)ReleaseMouseCapture();
            if(wasMoving)gesture(Slot,origin,Forms.Cursor.Position,false,true);
        }
        // Returns true when the release was used for editing and must not click a control.
        internal bool Release()
        {
            StopWatch();
            pressed=false;
            if(held)
            {
                held=false;
                if(IsMouseCaptured)ReleaseMouseCapture();
                return true;
            }
            if(pressedControl||select==null)return false;
            select(Slot);
            return true;
        }
        void Wire(Thumb thumb,bool sizing,int corner)
        {
            thumb.DragStarted+=(s,e)=>
            {
                origin=Forms.Cursor.Position;
                ResizeCorner=corner;
                gesture(Slot,origin,origin,sizing,false);
            };
            thumb.DragDelta+=(s,e)=>gesture(Slot,origin,Forms.Cursor.Position,sizing,false);
            thumb.DragCompleted+=(s,e)=>gesture(Slot,origin,e.Canceled?origin:Forms.Cursor.Position,sizing,true);
            thumb.KeyDown+=(s,e)=>
            {
                int x=e.Key==Key.Left?-8:e.Key==Key.Right?8:0,y=e.Key==Key.Up?-8:e.Key==Key.Down?8:0;
                if(x==0&&y==0)return;
                var center=PointToScreen(new Point(ActualWidth/2,ActualHeight/2));
                var point=new System.Drawing.Point((int)center.X,(int)center.Y);
                bool size=sizing||(Keyboard.Modifiers&ModifierKeys.Shift)!=0;
                ResizeCorner=sizing?corner:1;
                gesture(Slot,point,point,size,false);
                gesture(Slot,point,new System.Drawing.Point(point.X+x,point.Y+y),size,true);
                e.Handled=true;
            };
        }
        internal void Editing(bool value)
        {
            editor.Visibility=value?Visibility.Visible:Visibility.Collapsed;
            card.BorderThickness=new Thickness(value?2:tile!=null?0:1);
            card.BorderBrush=value?Paint.Brush("#FF4F8BF0"):Paint.Brush("#46FFFFFF");
        }
        internal void Material(Brush blurred,int opacity)
        {
            backdrop.Background=blurred;
            if(scheme!=null)card.Background=WidgetMaterial.Surface(scheme,opacity);
            else card.Opacity=Math.Max(.1,opacity/100.0);
        }
        void Shape()
        {
            bool circular=Toggle!=null&&ActualWidth<=110&&ActualHeight<=110;
            bool pill=Toggle!=null?ActualWidth>ActualHeight:InfoTileCatalog.Known(Slot.Key)&&InfoTile.Pill(InfoTileCatalog.Columns(Slot.Key),InfoTileCatalog.Rows(Slot.Key));
            double radius=circular||pill?Math.Min(ActualWidth,ActualHeight)/2:28;
            card.CornerRadius=backdrop.CornerRadius=new CornerRadius(radius);
            body.Clip=new RectangleGeometry(new Rect(0,0,Math.Max(0,ActualWidth-2),Math.Max(0,ActualHeight-2)),Math.Max(0,radius-1),Math.Max(0,radius-1));
            if(Toggle!=null)Toggle.Shape(new QuickWidgetSlot {Columns=circular?1:2,Rows=!circular&&ActualHeight>ActualWidth?2:1});
        }
        async void Invoke()
        {
            if(retired||shell.QuickActionPending(Slot.Key))return;
            notice.Visibility=Visibility.Collapsed;
            Toggle.State(false,false);
            try{await shell.InvokeQuickAction(Slot.Key);}
            catch(Exception e)
            {
                if(!retired)
                {
                    notice.Text=e.Message;
                    notice.Visibility=Visibility.Visible;
                }
            }
            finally
            {
                toggleAt=DateTime.MinValue;
                if(!retired)Refresh();
            }
        }
        internal void Refresh()
        {
            if(retired)return;
            if(tile!=null)tile.Refresh(shell,DateTime.Now);
            else if(Slot.Key=="clock")
            {
                var now=DateTime.Now;
                string clock=now.ToString("h:mm"),date=now.ToString("M월 d일 dddd");
                if(lastClock!=clock){lastClock=clock;primary.Text=clock;}
                if(lastDate!=date){lastDate=date;secondary.Text=date;}
            }
            else if(Slot.Key=="battery")
            {
                var power=shell.Power;
                battery.Update(power);
                string label=power.Known?power.Connected?"충전기 연결됨":"배터리 사용 중":"배터리 없음";
                if(lastBattery!=label){lastBattery=label;primary.Text=label;secondary.Text=power.Known?"PC 배터리":"전원에 연결된 PC";}
            }
            else if(Player!=null)
            {
                Player.Update(shell.Services.Media,shell.Services.OutputName);
                if(Player.MediaBackground!=null&&card.Background!=Player.MediaBackground)card.Background=Player.MediaBackground;
            }
            else if(DateTime.UtcNow>=toggleAt)
            {
                // Some toggle states read the registry or power settings; a few seconds' lag is invisible here.
                toggleAt=DateTime.UtcNow.AddSeconds(4);
                RefreshToggle();
            }
        }
        DateTime toggleAt=DateTime.MinValue;
        void RefreshToggle()
        {
            string key=Slot.Key,caption="";
            bool on=false,available=true;
            var setting=QuickSettingCatalog.Find(key);
            if(setting!=null)on=setting.Read(Preferences.Current);
            else switch(key)
            {
                case "wifi": on=shell.Services.WifiOn??shell.Wifi!=null;available=shell.Services.WifiPresent!=false;caption=shell.Wifi??(on?"연결 안 됨":"꺼짐");break;
                case "bluetooth": on=shell.Services.BluetoothOn??false;available=shell.Services.BluetoothPresent!=false;caption=on?shell.Buds.ConnectedCaption??"연결 안 됨":"꺼짐";break;
                case "mute": on=shell.Audio.Muted;available=shell.Audio.Available;break;
                case "theme": var theme=AppTheme.Read();on=theme.Dark;available=theme.Available;break;
                case "efficiency": var power=shell.Toggles.Power();on=power.Selected==QuickActions.EfficientMode;available=power.Available;break;
                case "dnd": var disturb=shell.Toggles.Disturb.State;on=disturb.Enabled;available=disturb.Available;break;
                case "awake":on=shell.Toggles.Awake;break;
                case "animations":on=Preferences.Current.Animations;break;
                case "startup":on=shell.Toggles.Startup;break;
                case "slider-scroll":on=Preferences.Current.QuickSliderScroll;break;
                case "buds":on=shell.Buds.HasConnectedBuds||BudsInfo.IsBuds(shell.Services.OutputName);caption=on?shell.Buds.BudsCaption??"연결됨":"연결 안 됨";available=QuickActionCatalog.Available(key,shell);break;
                default:available=QuickActionCatalog.Available(key,shell);break;
            }
            if(caption.Length==0&&!QuickActionCatalog.Commands.Contains(key))caption=on?"켜짐":"꺼짐";
            Toggle.Text(DesktopWidgetCatalog.Title(key),caption);
            Toggle.State(on,available&&!shell.QuickActionPending(key));
        }
        internal void Retire()
        {
            if(retired)return;
            retired=true;
            if(tile!=null)tile.Retire();
            StopWatch();
            pressed=moving=held=false;
            if(IsMouseCaptured)ReleaseMouseCapture();
            if(move.IsDragging)move.CancelDrag();
            Visibility=Visibility.Collapsed;
            body.Children.Clear();
        }
    }
}
