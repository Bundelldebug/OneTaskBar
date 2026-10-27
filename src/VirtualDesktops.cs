using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace OneTaskBar
{
    internal sealed class DesktopPage
    {
        internal Guid Id;
        internal string Name;
        internal readonly List<SwitchItem> Windows=new List<SwitchItem>();
    }
    internal sealed class DesktopSnapshot
    {
        internal readonly List<DesktopPage> Pages=new List<DesktopPage>();
        internal Guid Current;
        internal int CurrentIndex {get{return Math.Max(0,Pages.FindIndex(p=>p.Id==Current));}}
    }
    // Public COM methods identify a window's desktop. Explorer's persisted order
    // is read defensively; no private, build-dependent COM vtable is invoked.
    internal static class VirtualDesktops
    {
        const string Root=@"Software\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops";
        [ComImport,Guid("A5CD92FF-29BE-454C-8D04-D82879FB3F1B"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IManager
        {
            [PreserveSig]int IsWindowOnCurrentVirtualDesktop(IntPtr window,[MarshalAs(UnmanagedType.Bool)]out bool current);
            [PreserveSig]int GetWindowDesktopId(IntPtr window,out Guid desktop);
            [PreserveSig]int MoveWindowToDesktop(IntPtr window,ref Guid desktop);
        }
        internal static List<Guid> Decode(byte[] data)
        {
            var result=new List<Guid>();
            if(data==null||data.Length>16*256||data.Length%16!=0)return result;
            for(int offset=0;offset<data.Length;offset+=16){var bytes=new byte[16];Array.Copy(data,offset,bytes,0,16);var id=new Guid(bytes);if(id!=Guid.Empty&&!result.Contains(id))result.Add(id);}
            return result;
        }
        static Guid ReadId(RegistryKey key,string name){var values=key==null?new List<Guid>():Decode(key.GetValue(name) as byte[]);return values.Count==1?values[0]:Guid.Empty;}
        internal static Guid CurrentId(){try{using(var key=Registry.CurrentUser.OpenSubKey(Root))return ReadId(key,"CurrentVirtualDesktop");}catch{return Guid.Empty;}}
        internal static bool Distance(IList<Guid> order,Guid current,Guid target,out int steps)
        {
            steps=0;int from=order.IndexOf(current),to=order.IndexOf(target);
            if(current==Guid.Empty||target==Guid.Empty||from<0||to<0)return false;
            steps=to-from;return true;
        }
        internal static bool CurrentDistance(Guid target,out int steps)
        {
            steps=0;
            try{using(var key=Registry.CurrentUser.OpenSubKey(Root))return key!=null&&Distance(Decode(key.GetValue("VirtualDesktopIDs") as byte[]),ReadId(key,"CurrentVirtualDesktop"),target,out steps);}
            catch{return false;}
        }
        internal static DesktopSnapshot Read(IEnumerable<AppEntry> apps,IntPtr foreground)
        {
            var result=new DesktopSnapshot();IManager manager=null;
            try {
                using(var key=Registry.CurrentUser.OpenSubKey(Root)){
                    if(key!=null){result.Current=ReadId(key,"CurrentVirtualDesktop");foreach(var id in Decode(key.GetValue("VirtualDesktopIDs") as byte[])){
                        string name=null;using(var item=key.OpenSubKey(@"Desktops\"+id.ToString("B")))if(item!=null)name=item.GetValue("Name") as string;
                        result.Pages.Add(new DesktopPage {Id=id,Name=String.IsNullOrWhiteSpace(name)?"데스크톱 "+(result.Pages.Count+1):name});
                    }}
                }
                manager=(IManager)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("AA509086-5CA9-4C25-8F95-589D3C07B48A")));
                Guid active;if(result.Current==Guid.Empty&&foreground!=IntPtr.Zero&&manager.GetWindowDesktopId(foreground,out active)>=0)result.Current=active;
                if(result.Pages.Count==0)result.Pages.Add(new DesktopPage {Id=result.Current,Name="데스크톱 1"});
                foreach(var item in SwitchWindows.Read(apps,true)){
                    Guid id;if(manager.GetWindowDesktopId(item.Window,out id)<0||id==Guid.Empty)continue;
                    var page=result.Pages.FirstOrDefault(p=>p.Id==id);
                    if(page==null){page=new DesktopPage {Id=id,Name="데스크톱 "+(result.Pages.Count+1)};result.Pages.Add(page);}
                    page.Windows.Add(item);
                }
            }catch(Exception error){AppLog.Once("desktop-list",error.GetType().Name);}
            finally{if(manager!=null&&Marshal.IsComObject(manager))Marshal.ReleaseComObject(manager);}
            if(result.Pages.Count==0)result.Pages.Add(new DesktopPage {Id=result.Current,Name="데스크톱 1"});
            return result;
        }
    }
}
