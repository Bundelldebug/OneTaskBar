using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Forms=System.Windows.Forms;
namespace OneTaskBar
{
    internal sealed class WindowSwitcherManager : IDisposable
    {
        [DllImport("user32.dll")]static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")]static extern bool PostMessage(IntPtr window,int message,IntPtr wp,IntPtr lp);
        readonly ShellController shell;readonly Func<List<SwitchItem>> fixtureRead;readonly Action<IntPtr> activate;readonly bool fixture;readonly DispatcherTimer pulse=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(120)};
        readonly List<int> commands=new List<int>();List<SwitchItem> snapshot=new List<SwitchItem>();AppEntry[] metadata=new AppEntry[0];SwitchSession session;WindowSwitcherView view;IntPtr original,lastSource;bool pending,queued,disposed,preferNative=Preferences.Current.NativeAltTab;bool? finish;IDisposable outside;System.Drawing.Point pointer;
        internal bool Active {get{return pending||session!=null;}}
        internal WindowSwitcherView View {get{return view;}}
        internal int Selected {get{return session==null?-1:session.Selected;}}
        internal bool TimerActive {get{return pulse.IsEnabled;}}
        bool Enabled {get{return !disposed&&(fixture||shell.Desktop)&&!Preferences.Current.NativeAltTab;}}
        internal WindowSwitcherManager(ShellController controller,Func<List<SwitchItem>> read=null,Action<IntPtr> activation=null)
        {
            shell=controller;fixture=read!=null;if(fixture&&!Program.Inspectable)throw new InvalidOperationException("Switcher fixture requires inspectable mode");fixtureRead=read;activate=activation??WindowActivation.Activate;pulse.Tick+=(s,e)=>Pulse();
        }
        internal void Update(IEnumerable<AppEntry> apps){if(disposed)return;metadata=apps.ToArray();if(Preferences.Current.NativeAltTab||!fixture&&!shell.Desktop){snapshot.Clear();return;}try{snapshot=fixture?fixtureRead():SwitchWindows.Read(metadata);}catch(Exception error){AppLog.Once("switcher-list",error.GetType().Name);snapshot.Clear();}}
        internal void PreferenceChanged(){bool next=Preferences.Current.NativeAltTab;if(next==preferNative)return;preferNative=next;if(next){Cancel();snapshot.Clear();}else Update(metadata);}
        internal void AltReleased(){var source=original!=IntPtr.Zero?original:lastSource;Application.Current.Dispatcher.BeginInvoke(new Action(()=>{if(source!=IntPtr.Zero&&Native.IsWindow(source))PostMessage(source,0x1F,IntPtr.Zero,IntPtr.Zero);}),DispatcherPriority.Input);}
        internal bool Cycle(bool reverse)
        {
            if(!Enabled)return false;if(!Active&&snapshot.Count==0)return false;if(!Active){pending=true;original=Native.GetForegroundWindow();pointer=Forms.Cursor.Position;}commands.Add(reverse?-1:1);Queue();return true;
        }
        internal void Navigate(int key){if(!Active)return;commands.Add(key==0x24?Int32.MinValue:key==0x23?Int32.MaxValue:key==0x25||key==0x26?-1:1);Queue();}
        internal void Finish(bool commit){if(!Active)return;finish=commit;Queue();}
        void Queue(){if(queued)return;queued=true;Application.Current.Dispatcher.BeginInvoke(new Action(Drain),DispatcherPriority.Input);}
        void Drain()
        {
            queued=false;if(!Enabled){Cancel();return;}if(finish==false){End(false);return;}
            try{
                if(pending){var items=fixture?fixtureRead():SwitchWindows.Read(metadata);if(items.Count==0){End(false);return;}int first=commands.Count==0?1:commands[0];if(first!=1&&first!=-1)first=1;session=new SwitchSession(items,original,first);if(commands.Count>0)commands.RemoveAt(0);pending=false;shell.PrepareWindowSwitcher();}
                if(session==null)return;var previous=session.Current;foreach(int command in commands){if(command==Int32.MinValue)session.Selected=0;else if(command==Int32.MaxValue)session.Selected=session.Items.Count-1;else session.Move(command);}commands.Clear();bool pruned=session.Prune(Native.IsWindow);if(session.Items.Count==0){End(false);return;}if(finish.HasValue){End(finish.Value);return;}if(view==null||pruned||session.Current!=previous)Render();pulse.Start();
            }catch(Exception error){AppLog.Write("switcher",error.GetType().Name+": "+error.Message);End(false);}
        }
        void Render()
        {
            if(session==null||session.Current==null)return;if(view==null)view=new WindowSwitcherView(Select,Navigate);var screen=original==IntPtr.Zero?Forms.Screen.PrimaryScreen:Forms.Screen.FromHandle(original);double dpi=Native.PrepareMonitor(view,screen.Bounds);var fit=SwitchLayout.Fit(screen.Bounds,Native.WorkArea(screen),dpi,session.Items.Count);view.Present(session,fit,screen.Bounds);if(outside==null)outside=MenuDismiss.Watch(Contains,()=>Finish(false));
        }
        bool Contains(Native.PointI point){if(view==null||!view.IsVisible)return false;Native.RectI bounds;return Native.GetWindowRect(new WindowInteropHelper(view).Handle,out bounds)&&point.X>=bounds.Left&&point.X<bounds.Right&&point.Y>=bounds.Top&&point.Y<bounds.Bottom;}
        void Select(int index,bool commit)
        {
            if(session==null||index<0||index>=session.Items.Count)return;var now=Forms.Cursor.Position;if(!commit&&now==pointer)return;pointer=now;if(!commit&&session.Selected==index)return;session.Selected=index;if(commit)End(true);else Render();
        }
        void Pulse()
        {
            if(!Enabled||view==null||view.IsClosed){End(false);return;}if(!fixture&&Native.GetForegroundWindow()==IntPtr.Zero){End(false);return;}if(!fixture&&(GetAsyncKeyState(0x12)&0x8000)==0){Finish(true);return;}if(session!=null&&session.Prune(Native.IsWindow)){if(session.Items.Count==0)End(false);else Render();}
        }
        void End(bool commit)
        {
            var selected=session==null?null:session.Current;var source=original;if(source!=IntPtr.Zero)lastSource=source;commands.Clear();pending=false;session=null;finish=null;original=IntPtr.Zero;pulse.Stop();if(outside!=null){outside.Dispose();outside=null;}if(view!=null){var closing=view;view=null;if(!closing.IsClosed)closing.Close();}if(source!=IntPtr.Zero&&Native.IsWindow(source))PostMessage(source,0x1F,IntPtr.Zero,IntPtr.Zero);if(commit&&selected!=null&&Native.IsWindow(selected.Window))activate(selected.Window);
        }
        internal void Cancel(){End(false);}
        public void Dispose(){if(disposed)return;disposed=true;End(false);snapshot.Clear();metadata=new AppEntry[0];}
    }
}
