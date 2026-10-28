using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
namespace OneTaskBar
{
    internal sealed class SwitchItem
    {
        internal IntPtr Window;internal string Title;internal AppEntry App;
    }
    internal sealed class SwitchSession
    {
        internal readonly List<SwitchItem> Items;internal int Selected;
        internal SwitchSession(IEnumerable<SwitchItem> items,IntPtr foreground,int steps)
        {
            Items=items.GroupBy(i=>i.Window).Select(g=>g.First()).ToList();
            // UWP/hosted apps can expose their CoreWindow as foreground while
            // the switch list contains the outer ApplicationFrameWindow.
            var root=Native.GetAncestor(foreground,2);if(root==IntPtr.Zero)root=foreground;
            int current=Items.FindIndex(i=>i.Window==foreground||i.Window==root);
            Selected=Wrap(current<0?(steps>0?steps-1:steps):current+steps,Items.Count);
        }
        internal static int Wrap(int index,int count){return count<=0?0:(index%count+count)%count;}
        internal void Move(int delta){Selected=Wrap(Selected+delta,Items.Count);}
        internal SwitchItem Current {get{return Items.Count==0?null:Items[Math.Min(Selected,Items.Count-1)];}}
        internal bool Prune(Func<IntPtr,bool> exists){var selected=Current;int before=Items.Count;Items.RemoveAll(i=>!exists(i.Window));int found=selected==null?-1:Items.FindIndex(i=>i.Window==selected.Window);Selected=found>=0?found:Math.Max(0,Math.Min(Selected,Items.Count-1));return before!=Items.Count;}
    }
    internal sealed class SwitchLayout
    {
        internal double X,Y,Width,Height,CardWidth,PreviewHeight;internal int Count;
        internal static SwitchLayout Fit(System.Drawing.Rectangle screen,Native.RectI work,double dpi,int count)
        {
            if(dpi<=0||Double.IsNaN(dpi)||Double.IsInfinity(dpi))dpi=1;double width=Math.Max(1,(work.Right-work.Left)/dpi),height=Math.Max(1,(work.Bottom-work.Top)/dpi),edge=Math.Min(16,width/8),top=Math.Min(28,height/12),room=Math.Max(1,width-edge*2);int visible=Math.Max(1,Math.Min(Math.Min(5,Math.Max(1,count)),(int)Math.Max(1,Math.Floor(room/160))));
            double panel=Math.Min(room,Math.Min(1000,Math.Max(visible*160+32,Math.Min(width*.68,visible*250+32)))),card=Math.Max(1,(panel-32-Math.Max(0,visible-1)*10)/visible),preview=Math.Max(20,Math.Min(128,Math.Min((card-16)*.70,height-top*2-100))),panelHeight=Math.Max(1,Math.Min(height-top*2,preview+100));
            return new SwitchLayout {X=(work.Left-screen.Left)/dpi+(width-panel)/2,Y=(work.Top-screen.Top)/dpi+top,Width=panel,Height=panelHeight,CardWidth=card,PreviewHeight=preview,Count=visible};
        }
    }
    internal sealed class WindowSwitchGesture
    {
        internal Func<bool,bool> Cycle;internal Action<int> Navigate;internal Action<bool> Finish;internal Action AltReleased;bool active,used;readonly HashSet<int> consumed=new HashSet<int>();
        internal bool Active {get{return active;}}
        internal void Reset(){active=false;used=false;consumed.Clear();}
        internal bool Observe(int key,bool down,bool enabled,bool alt,bool ctrl,bool shift,bool win,bool injected)
        {
            if(injected)return false;if(!down&&consumed.Remove(key))return true;
            if(!down&&(key==0x12||key==0xA4||key==0xA5)&&used){bool commit=active;active=false;used=false;if(commit&&Finish!=null)Finish(true);if(AltReleased!=null)AltReleased();return false;}
            if(!enabled||ctrl||win){if(active){active=false;if(Finish!=null)Finish(false);}return false;}
            if(down&&key==9&&alt&&Cycle!=null&&Cycle(shift)){active=used=true;consumed.Add(key);return true;}
            if(active&&down&&(key==0x1B||key==0x0D)){active=false;consumed.Add(key);if(Finish!=null)Finish(key==0x0D);return true;}
            if(active&&down&&(key==0x25||key==0x26||key==0x27||key==0x28||key==0x24||key==0x23)){consumed.Add(key);if(Navigate!=null)Navigate(key);return true;}
            if(active&&down&&key!=0x10&&key!=0xA0&&key!=0xA1&&key!=0x12&&key!=0xA4&&key!=0xA5){active=false;if(Finish!=null)Finish(false);}return false;
        }
        internal static void Check()
        {
            foreach(int count in new[]{0,1,2,3,17,100})for(int i=-300;i<300;i++){int index=SwitchSession.Wrap(i,count);if(index<0||count>0&&index>=count)throw new InvalidOperationException("Window switch wrap");}
            var moves=new List<int>();var finishes=new List<bool>();var gesture=new WindowSwitchGesture {Cycle=reverse=>{moves.Add(reverse?-1:1);return true;},Finish=finishes.Add};if(!gesture.Observe(9,true,true,true,false,false,false,false)||!gesture.Observe(9,false,true,true,false,false,false,false)||!gesture.Observe(9,true,true,true,false,true,false,false))throw new InvalidOperationException("Alt Tab cycle");gesture.Observe(0xA4,false,true,false,false,false,false,false);if(gesture.Active||!moves.SequenceEqual(new[]{1,-1})||!finishes.SequenceEqual(new[]{true}))throw new InvalidOperationException("Alt release commit");
            foreach(bool enabled in new[]{false,true})foreach(bool ctrl in new[]{false,true})foreach(bool win in new[]{false,true}){gesture.Reset();if(gesture.Observe(9,true,enabled,true,ctrl,false,win,false)!=(enabled&&!ctrl&&!win))throw new InvalidOperationException("Native chord fallback");}
            gesture.Reset();if(gesture.Observe(9,true,true,true,false,false,false,true))throw new InvalidOperationException("Injected shortcut must pass through");
            int releases=0;gesture.AltReleased=()=>releases++;gesture.Observe(9,true,true,true,false,false,false,false);gesture.Observe(9,false,true,true,false,false,false,false);gesture.Observe(0x1B,true,true,true,false,false,false,false);if(!gesture.Observe(0x1B,false,true,true,false,false,false,false)||gesture.Active)throw new InvalidOperationException("Escape pair cancellation");gesture.Observe(0xA4,false,true,false,false,false,false,false);if(releases!=1||finishes.Last())throw new InvalidOperationException("Cancelled Alt release cleanup");
            gesture.Reset();gesture.Cycle=reverse=>false;if(gesture.Observe(9,true,true,true,false,false,false,false)||gesture.Active)throw new InvalidOperationException("Unavailable switcher must not consume Tab");
            var items=Enumerable.Range(1,4).Select(i=>new SwitchItem {Window=new IntPtr(i),Title="Window "+i}).ToList();var session=new SwitchSession(items.Concat(items),new IntPtr(1),1);if(session.Items.Count!=4||session.Current.Window!=new IntPtr(2))throw new InvalidOperationException("Distinct MRU windows");session.Prune(h=>h!=new IntPtr(1));if(session.Current.Window!=new IntPtr(2))throw new InvalidOperationException("Pruning preserves selected HWND");session.Prune(h=>h!=new IntPtr(2));if(session.Current.Window!=new IntPtr(3))throw new InvalidOperationException("Closed selection advances safely");session.Prune(h=>false);if(session.Current!=null)throw new InvalidOperationException("Empty session selection");
            if(!SwitchWindows.Eligible(true,false,false,0,"Application",true)||!SwitchWindows.Eligible(true,false,false,0x40000,"Application",false))throw new InvalidOperationException("Native application eligibility");
            if(SwitchWindows.Eligible(false,false,false,0,"Application",true)||SwitchWindows.Eligible(true,true,false,0,"Application",true)||SwitchWindows.Eligible(true,false,true,0,"Application",true)||SwitchWindows.Eligible(true,false,false,0x80,"Application",true)||SwitchWindows.Eligible(true,false,false,0x08000000,"Application",true)||SwitchWindows.Eligible(true,false,false,0,"Application",false))throw new InvalidOperationException("Hidden, own, tool, cloaked and inactive-owner windows are excluded");foreach(string type in new[]{"Progman","WorkerW","Shell_TrayWnd","Shell_SecondaryTrayWnd"})if(SwitchWindows.Eligible(true,false,false,0,type,true))throw new InvalidOperationException("Explorer shell exclusion");
            var serializer=new System.Web.Script.Serialization.JavaScriptSerializer();if(serializer.Deserialize<PreferenceData>("{}").NativeAltTab||!serializer.Deserialize<PreferenceData>(serializer.Serialize(new PreferenceData {NativeAltTab=true})).NativeAltTab)throw new InvalidOperationException("Native Alt Tab choice migration and persistence");
            foreach(double scale in new[]{1.0,1.25,1.5,1.75,2,2.5})foreach(int count in new[]{0,1,2,3,7,40})foreach(var size in new[]{new System.Drawing.Size(640,480),new System.Drawing.Size(1366,768),new System.Drawing.Size(2560,1440)}){var screen=new System.Drawing.Rectangle(-2560,80,size.Width,size.Height);var work=new Native.RectI {Left=screen.Left+10,Top=screen.Top+20,Right=screen.Right,Bottom=screen.Bottom-70};var fit=SwitchLayout.Fit(screen,work,scale,count);if(fit.Count<1||fit.Count>5||fit.Width<=0||fit.Height<=0||screen.Left+fit.X*scale<work.Left-.1||screen.Top+fit.Y*scale<work.Top-.1||screen.Left+(fit.X+fit.Width)*scale>work.Right+.1||screen.Top+(fit.Y+fit.Height)*scale>work.Bottom+.1)throw new InvalidOperationException("Switcher monitor bounds");}
        }
    }
    internal static class SwitchWindows
    {
        static readonly int ownPid=Process.GetCurrentProcess().Id;
        [DllImport("user32.dll")]static extern IntPtr GetLastActivePopup(IntPtr window);
        internal static bool Eligible(bool visible,bool own,bool cloaked,int extended,string windowClass,bool isOwnerPopup){return visible&&!own&&!cloaked&&(extended&0x08000000)==0&&((extended&0x40000)!=0||((extended&0x80)==0&&isOwnerPopup))&&windowClass!="Progman"&&windowClass!="WorkerW"&&windowClass!="Shell_TrayWnd"&&windowClass!="Shell_SecondaryTrayWnd";}
        internal static List<SwitchItem> Read(IEnumerable<AppEntry> apps,bool allDesktops=false,bool resolveUnknown=true)
        {
            var map=new Dictionary<IntPtr,AppEntry>();foreach(var app in apps)foreach(var window in app.Windows)if(!map.ContainsKey(window))map[window]=app;int own=ownPid;var result=new List<SwitchItem>();
            Native.EnumWindows((window,state)=>{try{
                if(!Native.IsWindowVisible(window))return true;uint pid;Native.GetWindowThreadProcessId(window,out pid);if(pid==own)return true;int cloak;if(Native.DwmGetWindowAttribute(window,14,out cloak,4)!=0)cloak=0;var type=new StringBuilder(128);Native.GetClassName(window,type,type.Capacity);var root=Native.GetAncestor(window,3);var popup=root;for(int i=0;i<12;i++){var next=GetLastActivePopup(popup);if(next==IntPtr.Zero||next==popup)break;popup=next;if(Native.IsWindowVisible(popup))break;}if(!Eligible(true,false,allDesktops?(cloak&1)!=0:cloak!=0,Native.GetWindowLong(window,-20),type.ToString(),popup==window))return true;
                var title=new StringBuilder(512);Native.GetWindowText(window,title,title.Capacity);if(String.IsNullOrWhiteSpace(title.ToString()))return true;AppEntry entry;if(!map.TryGetValue(window,out entry)){string target=null;if(resolveUnknown)try{using(var process=Process.GetProcessById((int)pid))target=process.MainModule.FileName;}catch{}entry=new AppEntry {Name=title.ToString(),Target=target};}result.Add(new SwitchItem {Window=window,Title=title.ToString(),App=entry});
            }catch{}return result.Count<256;},IntPtr.Zero);return result;
        }
    }
}
