using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace OneTaskBar
{
    // The round minimize / maximize / close keys of the Dex style. Another program's title bar cannot be redrawn, so a small
    // window lies over the keys of every window on screen (not only the one in front, unless a window above hides them).
    // A maximized window in front has its keys moved to the status row by the battery, when that row is really showing.
    internal sealed class DexWindowKeys : IDisposable
    {
        delegate void EventCallback(IntPtr hook,uint kind,IntPtr window,int objectId,int childId,uint thread,uint time);
        delegate bool EnumCallback(IntPtr window,IntPtr state);
        [DllImport("user32.dll")]static extern IntPtr SetWinEventHook(uint first,uint last,IntPtr module,EventCallback callback,uint process,uint thread,uint flags);
        [DllImport("user32.dll")]static extern bool UnhookWinEvent(IntPtr hook);
        [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
        [DllImport("user32.dll")]static extern bool PostMessage(IntPtr window,int message,IntPtr wparam,IntPtr lparam);
        [DllImport("user32.dll")]static extern bool EnumWindows(EnumCallback callback,IntPtr state);
        [DllImport("user32.dll")]static extern IntPtr GetAncestor(IntPtr window,uint flags);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetClassName(IntPtr window,StringBuilder name,int size);
        [DllImport("dwmapi.dll",EntryPoint="DwmGetWindowAttribute")]static extern int DwmRect(IntPtr window,int attribute,out Native.RectI value,int size);
        [DllImport("dwmapi.dll",EntryPoint="DwmGetWindowAttribute")]static extern int DwmInt(IntPtr window,int attribute,out int value,int size);
        [DllImport("user32.dll")]static extern IntPtr SendMessageTimeout(IntPtr window,uint message,IntPtr wparam,IntPtr lparam,uint flags,uint timeout,out IntPtr result);
        [DllImport("user32.dll")]static extern IntPtr GetDC(IntPtr window);
        [DllImport("user32.dll")]static extern int ReleaseDC(IntPtr window,IntPtr dc);
        [DllImport("gdi32.dll")]static extern uint GetPixel(IntPtr dc,int x,int y);
        internal const int Minimize=0xF020,Maximize=0xF030,Restore=0xF120,Close=0xF060;
        const int MaxCovers=16;
        static readonly Brush lightInk=Paint.Brush("#FF2A2F35"),lightFill=Paint.Brush("#1F000000"),darkInk=Paint.Brush("#FFF4F6F8"),darkFill=Paint.Brush("#30FFFFFF");

        readonly ShellController shell;readonly EventCallback callback;readonly List<IntPtr> hooks=new List<IntPtr>();readonly uint ownProcess;
        readonly Dictionary<IntPtr,Slot> slots=new Dictionary<IntPtr,Slot>();
        readonly HashSet<IntPtr> covers=new HashSet<IntPtr>();
        readonly DispatcherTimer backstop=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(5000)};
        readonly DispatcherTimer later=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(50)};
        readonly Stopwatch since=Stopwatch.StartNew();
        IntPtr front,pinnedWindow;bool disposed,sweeping;
        bool lastKnownState,lastMaximized,lastCanMin,lastCanMax;
        // A fixture holds the keys on its own window, whatever the person at the PC brings to the front meanwhile.
        internal bool Pinned;
        // The window whose keys the status row shows when it is maximized: the one most recently in front.
        internal IntPtr Target {get{return front;}}
        internal bool CoverVisible {get{Slot slot;return slots.TryGetValue(front,out slot)&&slot.CoverVisible;}}
        internal bool KeysVisible {get{Slot slot;return slots.TryGetValue(front,out slot)&&slot.KeysVisible;}}
        internal int CoversShown {get{int count=0;foreach(var slot in slots.Values)if(slot.CoverVisible)count++;return count;}}
        internal bool CoverShownFor(IntPtr window){Slot slot;return slots.TryGetValue(window,out slot)&&slot.CoverVisible;}
        internal static Button Key(string kind,Action press,string tip,out Glyph glyph)
        {
            glyph=new Glyph(kind,12){Color=lightInk};
            var button=Paint.Button(glyph,press,tip);
            button.Width=button.Height=20;button.Padding=new Thickness(0);button.Margin=new Thickness(5,0,5,0);button.Background=lightFill;
            button.Resources["ButtonRadius"]=new CornerRadius(10);
            return button;
        }
        internal DexWindowKeys(ShellController controller)
        {
            shell=controller;callback=Event;ownProcess=(uint)Process.GetCurrentProcess().Id;
            backstop.Tick+=(s,e)=>Sweep();
            later.Tick+=(s,e)=>{later.Stop();Sweep();};
            foreach(var range in new[]{new uint[]{3,3},new uint[]{0x16,0x17},new uint[]{0x800B,0x800B},new uint[]{0x8001,0x8003},new uint[]{0x8017,0x8018}})
            {
                var hook=SetWinEventHook(range[0],range[1],IntPtr.Zero,callback,0,0,2);
                if(hook!=IntPtr.Zero)hooks.Add(hook);
            }
            backstop.Start();
            Sweep();
        }
        void Event(IntPtr hook,uint kind,IntPtr window,int objectId,int childId,uint thread,uint time)
        {
            if(disposed||objectId!=0||window==IntPtr.Zero)return;
            if(kind!=3&&GetAncestor(window,2)!=window)return;
            if(kind==3&&Pinned)return;
            // Ignore background window move/scroll storms; only the foreground window affects the top status bar keys.
            if(kind==0x800B&&window!=front&&window!=Native.GetForegroundWindow())return;
            Request(kind==3);
        }
        // Coalesce rapid window events to avoid unnecessary wake-ups, especially on battery.
        void Request(bool now)
        {
            int interval=RuntimeCadence.OnBattery?80:50;
            if(now||since.ElapsedMilliseconds>=interval){Sweep();return;}
            if(later.Interval.TotalMilliseconds!=interval)later.Interval=TimeSpan.FromMilliseconds(interval);
            if(!later.IsEnabled)later.Start();
        }
        static bool ShellClass(IntPtr window)
        {
            var name=new StringBuilder(64);GetClassName(window,name,name.Capacity);string text=name.ToString();
            return text=="Progman"||text=="WorkerW"||text=="Shell_TrayWnd"||text=="Shell_SecondaryTrayWnd"||text.StartsWith("Windows.UI.Core",StringComparison.Ordinal)||text=="XamlExplorerHostIslandWindow";
        }
        static bool Cloaked(IntPtr window){int value;return DwmInt(window,14,out value,4)==0&&value!=0;}
        static bool Live(IntPtr window){return window!=IntPtr.Zero&&Native.IsWindow(window)&&Native.IsWindowVisible(window)&&!Native.IsIconic(window)&&!Cloaked(window);}
        bool Eligible(IntPtr window)
        {
            if(window==IntPtr.Zero||!Native.IsWindow(window)||!Native.IsWindowVisible(window)||covers.Contains(window))return false;
            uint process;GetWindowThreadProcessId(window,out process);
            if(process==ownProcess&&!Program.Inspectable)return false;
            int style=Native.GetWindowLong(window,-16),extended=Native.GetWindowLong(window,-20);
            // Apps that draw their own title bar drop the system menu style but keep the minimize and maximize ones.
            return (style&0xC00000)==0xC00000&&(style&0xB0000)!=0&&(extended&0x80)==0&&!ShellClass(window);
        }
        // A fixture names its window itself because a test cannot always take the foreground.
        internal void Follow(IntPtr window)
        {
            if(disposed)return;
            if(Pinned)pinnedWindow=window;
            Sweep();
        }
        internal void Update(){Sweep();}
        // Windows above another one hide its keys; only real, visible windows count.
        bool Hides(IntPtr window,out Native.RectI frame)
        {
            frame=new Native.RectI();
            uint process;GetWindowThreadProcessId(window,out process);
            if(process==ownProcess)return false;
            if((Native.GetWindowLong(window,-20)&0x20)!=0||ShellClass(window))return false;
            if(DwmRect(window,9,out frame,16)!=0&&!Native.GetWindowRect(window,out frame))return false;
            return frame.Right>frame.Left&&frame.Bottom>frame.Top;
        }
        void Sweep()
        {
            if(disposed)return;
            if(!shell.DisplayAwake&&!Pinned)return;
            int backstopMs=RuntimeCadence.OnBattery?15000:5000;
            if(backstop.Interval.TotalMilliseconds!=backstopMs)backstop.Interval=TimeSpan.FromMilliseconds(backstopMs);
            if(sweeping){if(!later.IsEnabled)later.Start();return;}
            sweeping=true;since.Restart();
            try
            {
                var now=Native.GetForegroundWindow();
                if(Pinned)front=pinnedWindow;
                else if(Eligible(now)&&Live(now))front=now;
                bool frontLive=front!=IntPtr.Zero&&Live(front);
                if(!frontLive&&!Pinned)front=IntPtr.Zero;
                bool maximized=frontLive&&Native.IsZoomed(front);
                int frontStyle=frontLive?Native.GetWindowLong(front,-16):0;
                bool canMin=(frontStyle&0x20000)!=0,canMax=(frontStyle&0x10000)!=0;
                if(!lastKnownState||maximized!=lastMaximized||canMin!=lastCanMin||canMax!=lastCanMax)
                {
                    lastKnownState=true;lastMaximized=maximized;lastCanMin=canMin;lastCanMax=canMax;
                    shell.DexWindowKeysChanged(maximized,canMin,canMax);
                }
                // Keep native title-bar buttons uncovered; only the top status bar carries Dex window keys for maximized windows.
                if(slots.Count>0)
                {
                    foreach(var slot in slots.Values)slot.Dispose();
                    slots.Clear();covers.Clear();
                }
            }
            finally{sweeping=false;}
        }
        internal static void Send(IntPtr window,int command)
        {
            if(window==IntPtr.Zero||!Native.IsWindow(window))return;
            if(command==Maximize&&Native.IsZoomed(window))command=Restore;
            // A window running as administrator refuses messages from here; closing it has its own route.
            if(!PostMessage(window,0x112,new IntPtr(command),IntPtr.Zero)&&command==Close)ElevatedWindows.Close(window);
        }
        internal void Send(int command){Send(front,command);}
        // Resting on the maximize key opens the layouts.
        readonly DispatcherTimer snapDelay=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(450)};
        DexSnapMenu snap;IntPtr snapWindow;FrameworkElement snapKey;bool snapWired;
        internal void ArmSnap(IntPtr window,FrameworkElement key)
        {
            if(disposed||snap!=null)return;
            if(!snapWired){snapWired=true;snapDelay.Tick+=(s,e)=>OpenSnap();}
            snapWindow=window;snapKey=key;snapDelay.Stop();snapDelay.Start();
        }
        internal void ArmSnapFront(FrameworkElement key){ArmSnap(front,key);}
        internal void DisarmSnap(){snapDelay.Stop();}
        void OpenSnap()
        {
            snapDelay.Stop();
            if(disposed||snap!=null||snapKey==null||!snapKey.IsMouseOver||!Live(snapWindow))return;
            snap=new DexSnapMenu(this,snapWindow,snapKey);
            covers.Add(new WindowInteropHelper(snap).Handle);
            snap.Present();
        }
        internal void SnapClosed(DexSnapMenu menu){if(snap==menu){covers.Remove(new WindowInteropHelper(menu).Handle);snap=null;}}
        // The windows on screen, front first, with the given one leading.
        internal List<IntPtr> WindowsInOrder(int most,IntPtr first=default(IntPtr))
        {
            var result=new List<IntPtr>();
            if(first!=IntPtr.Zero)result.Add(first);
            EnumWindows((w,s)=>{if(result.Count<most&&w!=first&&Eligible(w)&&Live(w))result.Add(w);return result.Count<most;},IntPtr.Zero);
            return result;
        }
        internal static void PlaceWindow(IntPtr window,Rect part,System.Windows.Forms.Screen screen)
        {
            if(Native.IsZoomed(window)||Native.IsIconic(window))Native.ShowWindow(window,9);
            var work=Native.WorkArea(screen);int wide=work.Right-work.Left,tall=work.Bottom-work.Top;
            int left=work.Left+(int)Math.Round(part.X*wide),top=work.Top+(int)Math.Round(part.Y*tall),right=work.Left+(int)Math.Round(part.Right*wide),bottom=work.Top+(int)Math.Round(part.Bottom*tall);
            // The window rectangle includes an invisible frame; the visible edges are what must meet.
            Native.RectI outer,seen;int l=0,t=0,r=0,b=0;
            if(Native.GetWindowRect(window,out outer)&&DwmRect(window,9,out seen,16)==0){l=seen.Left-outer.Left;t=seen.Top-outer.Top;r=outer.Right-seen.Right;b=outer.Bottom-seen.Bottom;}
            Native.SetWindowPos(window,IntPtr.Zero,left-l,top-t,right-left+l+r,bottom-top+t+b,0x14);
        }
        public void Dispose()
        {
            if(disposed)return;if(snap!=null)snap.Dismiss();disposed=true;backstop.Stop();later.Stop();snapDelay.Stop();
            foreach(var hook in hooks)UnhookWinEvent(hook);
            hooks.Clear();
            foreach(var slot in slots.Values)slot.Dispose();
            slots.Clear();covers.Clear();
        }

        // The cover of one window.
        sealed class Slot : IDisposable
        {
            readonly DexWindowKeys owner;readonly IntPtr window;
            readonly Window cover;readonly Border face=new Border {CornerRadius=new CornerRadius(0,8,0,0)};
            readonly StackPanel keys=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,7,0)};
            readonly Button minimizeKey,maximizeKey,closeKey;readonly Glyph maximizeGlyph;
            // While Windows animates a maximize or restore the title bar is not there yet to take its colour from.
            readonly DispatcherTimer settle=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(320)};
            bool settling,zoomKnown,zoomed,wasHidden,disposed;uint shownColour=uint.MaxValue;
            int placeLeft,placeTop,placeWidth,placeHeight;
            internal readonly IntPtr Handle;
            internal bool CoverVisible {get{return cover.IsVisible;}}
            internal bool KeysVisible {get{return cover.IsVisible&&keys.Visibility==Visibility.Visible;}}
            internal Slot(DexWindowKeys parent,IntPtr target)
            {
                owner=parent;window=target;
                Glyph ignored;
                minimizeKey=Key("dex-min",()=>Send(window,Minimize),"최소화",out ignored);
                maximizeKey=Key("dex-max",()=>Send(window,Maximize),"최대화",out maximizeGlyph);
                closeKey=Key("close",()=>Send(window,Close),"닫기",out ignored);
                keys.Children.Add(minimizeKey);keys.Children.Add(maximizeKey);keys.Children.Add(closeKey);
                maximizeKey.MouseEnter+=(s,e)=>owner.ArmSnap(window,maximizeKey);maximizeKey.MouseLeave+=(s,e)=>owner.DisarmSnap();
                face.Child=keys;
                cover=new Window {WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,AllowsTransparency=true,Background=Brushes.Transparent,ShowInTaskbar=false,ShowActivated=false,Topmost=true,Title="One TaskBar · 창 버튼",Content=face,Width=120,Height=30};
                Handle=new WindowInteropHelper(cover).EnsureHandle();
                Native.SetWindowLong(Handle,-20,Native.GetWindowLong(Handle,-20)|0x80|0x08000000);
                HwndSource.FromHwnd(Handle).AddHook((IntPtr h,int message,IntPtr w,IntPtr l,ref bool handled)=>{if(message==0x21){handled=true;return new IntPtr(3);}return IntPtr.Zero;});
                settle.Tick+=(s,e)=>{settle.Stop();settling=false;shownColour=uint.MaxValue;owner.Sweep();};
            }
            // Windows reports the title-bar keys of the window. Apps that draw their own title bar (Electron, Chromium) report
            // only its height and right edge, with the keys at the standard 46 x 3 width against that edge.
            // The window itself is then asked where its keys are, so the round keys land on the same line as its toolbar.
            int areaWidth=-1,areaHeight=-1;Native.RectI areaKnown;bool areaExact;
            int HitTest(int x,int y)
            {
                IntPtr result;
                if(SendMessageTimeout(window,0x84,IntPtr.Zero,new IntPtr((y&0xffff)<<16|x&0xffff),2,20,out result)==IntPtr.Zero)return 0;
                return result.ToInt32();
            }
            bool KeyArea(Native.RectI frame,out Native.RectI area)
            {
                int width=frame.Right-frame.Left,height=frame.Bottom-frame.Top;
                if(width==areaWidth&&height==areaHeight){area=areaKnown;return true;}
                if(DwmRect(window,5,out area,16)!=0)return false;
                areaExact=false;
                double scale=Math.Max(96,Native.GetDpiForWindow(window))/96.0;
                if(area.Right-area.Left<30&&area.Bottom-area.Top>=12&&area.Right>0)
                {
                    area.Left=area.Right-(int)Math.Round(138*scale);
                    int x=frame.Left+area.Right-(int)Math.Round(23*scale),step=Math.Max(1,(int)Math.Round(2*scale)),first=-1,last=-1;
                    for(int y=0;y<100*scale;y+=step)
                    {
                        if(HitTest(x,frame.Top+y)!=20)continue;
                        if(first<0)first=y;
                        last=y;
                    }
                    if(first>=0)
                    {
                        int left=x,wide=Math.Max(1,(int)Math.Round(4*scale)),middle=frame.Top+(first+last)/2;
                        for(int probe=x;probe>x-200*scale;probe-=wide)
                        {
                            int hit=HitTest(probe,middle);
                            if(hit==8||hit==9||hit==20)left=probe;
                        }
                        area.Left=Math.Min(area.Left,left-frame.Left);area.Top=first;area.Bottom=last+step;areaExact=true;
                    }
                }
                // The first pixels are the border of the window, which stays visible.
                if(!Native.IsZoomed(window))area.Top=Math.Max(area.Top,(int)Math.Ceiling(scale));
                areaWidth=width;areaHeight=height;areaKnown=area;
                return true;
            }
            void HideCover(){if(cover.IsVisible)cover.Hide();placeWidth=0;}
            static bool Inside(List<Native.RectI> frames,int x,int y)
            {
                foreach(var frame in frames)if(x>=frame.Left&&x<frame.Right&&y>=frame.Top&&y<frame.Bottom)return true;
                return false;
            }
            internal void Apply(bool live,bool statusKeys,List<Native.RectI> above)
            {
                if(disposed)return;
                if(!live)
                {
                    wasHidden=true;settling=false;settle.Stop();HideCover();
                    return;
                }
                bool maximized=Native.IsZoomed(window);
                int style=Native.GetWindowLong(window,-16);
                if(wasHidden||zoomKnown&&zoomed!=maximized){settling=true;settle.Stop();settle.Start();}
                wasHidden=false;zoomKnown=true;zoomed=maximized;
                if(settling){HideCover();return;}
                Native.RectI frame,buttons;
                if(!Native.GetWindowRect(window,out frame)||!KeyArea(frame,out buttons)){HideCover();return;}
                int width=buttons.Right-buttons.Left,height=buttons.Bottom-buttons.Top;
                if(width<30||height<12){HideCover();return;}
                minimizeKey.Visibility=(style&0x20000)!=0?Visibility.Visible:Visibility.Collapsed;
                maximizeKey.Visibility=(style&0x10000)!=0?Visibility.Visible:Visibility.Collapsed;
                // The keys leave the cover only when the status row really shows them; otherwise the cover must not be blank.
                keys.Visibility=maximized&&statusKeys?Visibility.Collapsed:Visibility.Visible;
                face.CornerRadius=maximized?new CornerRadius(0):new CornerRadius(0,8,0,0);
                maximizeGlyph.Kind=maximized?"dex-restore":"dex-max";maximizeGlyph.InvalidateVisual();
                maximizeKey.ToolTip=maximized?"이전 크기로":"최대화";
                // A maximized window reaches under the status row; the cover stays below it.
                int top=frame.Top+buttons.Top,bottom=top+height;
                // Its frame hangs past the work area, so its keys sit that much lower than the reported box.
                var work=maximized?Native.WorkArea(System.Windows.Forms.Screen.FromHandle(window)):default(Native.RectI);
                if(maximized&&top<work.Top){if(!areaExact)bottom+=work.Top-top;top=work.Top;}
                if(bottom-top<4){HideCover();return;}
                int left=frame.Left+buttons.Left,right=left+width;
                if(maximized)right=Math.Max(right,work.Right);
                // A window above hides the keys under it, and the colour beside them: the cover then steps aside.
                int middle=(top+bottom)/2;
                if(Inside(above,left+4,middle)||Inside(above,(left+right)/2,middle)||Inside(above,right-4,middle)||Inside(above,left-4,middle)){HideCover();return;}
                Paintover(left-4,middle);
                bool same=cover.IsVisible&&placeLeft==left&&placeTop==top&&placeWidth==right-left&&placeHeight==bottom-top;
                if(same)return;
                placeLeft=left;placeTop=top;placeWidth=right-left;placeHeight=bottom-top;
                if(!cover.IsVisible)cover.Show();
                Native.SetWindowPos(Handle,new IntPtr(-1),left,top,right-left,bottom-top,0x10);
            }
            // The keys lie on the colour of the title bar beside them.
            void Paintover(int x,int y)
            {
                var dc=GetDC(IntPtr.Zero);if(dc==IntPtr.Zero)return;
                uint pixel=GetPixel(dc,x,y);
                ReleaseDC(IntPtr.Zero,dc);
                if(pixel==0xFFFFFFFF||pixel==shownColour)return;
                shownColour=pixel;
                byte r=(byte)pixel,g=(byte)(pixel>>8),b=(byte)(pixel>>16);
                var fill=new SolidColorBrush(Color.FromRgb(r,g,b));fill.Freeze();face.Background=fill;
                bool dark=r*.299+g*.587+b*.114<128;
                foreach(UIElement item in keys.Children)
                {
                    var button=(Button)item;button.Background=dark?darkFill:lightFill;
                    var glyph=(Glyph)button.Content;glyph.Color=dark?darkInk:lightInk;glyph.InvalidateVisual();
                }
            }
            public void Dispose()
            {
                if(disposed)return;disposed=true;settle.Stop();
                cover.Close();
            }
        }
    }

    internal sealed partial class ShellController
    {
        internal DexWindowKeys DexKeys;
        void EnsureDexKeys(){if(Dex.Enabled&&DexKeys==null)DexKeys=new DexWindowKeys(this);}
        void CloseDexKeys(){if(DexKeys!=null){DexKeys.Dispose();DexKeys=null;}}
        internal void DexWindowKeysChanged(bool maximized,bool minimize,bool maximize){if(topRight!=null)topRight.ShowWindowKeys(maximized,minimize,maximize);}
        internal bool DexStatusKeysUsable {get{return topRight!=null&&topRight.KeysUsable;}}
    }

    internal sealed partial class TopStatusBar
    {
        StackPanel windowKeys;Button keyMinimize,keyMaximize;bool keysShown,keyMin,keyMax;
        Glyph[] keyGlyphs;
        void BuildWindowKeys()
        {
            Glyph minimum,middle,close;
            keyMinimize=DexWindowKeys.Key("dex-min",()=>SendKey(DexWindowKeys.Minimize),"최소화",out minimum);
            keyMaximize=DexWindowKeys.Key("dex-restore",()=>SendKey(DexWindowKeys.Maximize),"이전 크기로",out middle);
            var closeKey=DexWindowKeys.Key("close",()=>SendKey(DexWindowKeys.Close),"닫기",out close);
            keyGlyphs=new[]{minimum,middle,close};
            keyMaximize.MouseEnter+=(s,e)=>{if(shell.DexKeys!=null)shell.DexKeys.ArmSnapFront(keyMaximize);};keyMaximize.MouseLeave+=(s,e)=>{if(shell.DexKeys!=null)shell.DexKeys.DisarmSnap();};
            windowKeys=new StackPanel {Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center,Visibility=Visibility.Collapsed,Margin=new Thickness(3,0,0,0)};
            foreach(var key in new[]{keyMinimize,keyMaximize,closeKey})
            {
                key.Width=key.Height=20;key.Margin=new Thickness(3,0,3,0);key.Background=DarkCapsuleTint;key.Resources["ButtonRadius"]=new CornerRadius(10);
                windowKeys.Children.Add(key);
            }
            row.Children.Add(windowKeys);
        }
        void SendKey(int command){if(shell.DexKeys!=null)shell.DexKeys.Send(command);}
        internal void ShowWindowKeys(bool show,bool minimize,bool maximize)
        {
            if(windowKeys==null||closed||keysShown==show&&keyMin==minimize&&keyMax==maximize)return;
            keysShown=show;keyMin=minimize;keyMax=maximize;
            windowKeys.Visibility=show?Visibility.Visible:Visibility.Collapsed;
            keyMinimize.Visibility=minimize?Visibility.Visible:Visibility.Collapsed;
            keyMaximize.Visibility=maximize?Visibility.Visible:Visibility.Collapsed;
            layoutDirty=true;
            if(IsVisible)Refresh();
        }
        internal bool WindowKeysShown {get{return keysShown;}}
        // The keys are only a stand-in for the window's own when the row is on screen and not tucked away.
        internal bool KeysUsable {get{return keysShown&&!closed&&IsVisible&&!concealed;}}
    }
}
