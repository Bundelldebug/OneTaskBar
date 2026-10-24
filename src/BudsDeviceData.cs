using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media;
using Windows.Management.Deployment;
namespace OneTaskBar
{
    // Reads Windows' already-reported PnP data. Does not connect to the headset.
    internal static class BudsDeviceData
    {
        [StructLayout(LayoutKind.Sequential)]struct Key {internal Guid Format;internal uint Id;internal Key(string format,uint id){Format=new Guid(format);Id=id;}}
        [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)]static extern uint CM_Get_Device_ID_List_SizeW(out uint size,string filter,uint flags);
        [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)]static extern uint CM_Get_Device_ID_ListW(string filter,[Out]char[] ids,uint size,uint flags);
        [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)]static extern uint CM_Locate_DevNodeW(out uint instance,string id,uint flags);
        [DllImport("cfgmgr32.dll",CharSet=CharSet.Unicode)]static extern uint CM_Get_DevNode_PropertyW(uint instance,ref Key key,out uint type,[Out]byte[] data,ref uint size,uint flags);
        sealed class Device {internal Guid Container;internal string Name;internal int? Battery;}
        static readonly object gate=new object();static readonly Dictionary<string,ImageSource> pictures=new Dictionary<string,ImageSource>();static string root;static DateTime rootUntil;
        static ImageSource shortcutIcon;static DateTime shortcutUntil;
        internal static ImageSource ShortcutIcon()
        {
            lock(gate){if(shortcutIcon!=null||DateTime.UtcNow<shortcutUntil)return shortcutIcon;shortcutUntil=DateTime.UtcNow.AddMinutes(5);
                try{var package=new PackageManager().FindPackagesForUser("").FirstOrDefault(p=>p.Id.Name.Equals("SAMSUNGELECTRONICSCO.LTD.GalaxyBuds",StringComparison.OrdinalIgnoreCase)&&!p.IsFramework);if(package==null)return null;
                    foreach(string name in new[]{"ic_buds4f_left_dark.png","ic_buds3_left_dark.png","ic_tws_left_dark.png"}){string file=Path.Combine(package.InstalledPath,"Assets","Battery",name);if(!File.Exists(file))continue;shortcutIcon=NativeToastAssets.Image("",new Uri(file).AbsoluteUri,false,96);if(shortcutIcon!=null)return shortcutIcon;}
                }catch(Exception error){AppLog.Once("buds-shortcut-icon",error.GetType().Name);}return null;
            }
        }
        internal static BudsInfo Enrich(BudsInfo info)
        {
            if(info==null)return null;info.Image=info.Image??Picture(info.Name);if(info.RichImage||!(String.IsNullOrWhiteSpace(info.Detail)||info.Detail=="연결됨"||info.Detail.StartsWith("연결됨 · 배터리 ")))return info;int? battery=Battery(info.Container,info.Name);
            if(battery.HasValue&&(String.IsNullOrWhiteSpace(info.Detail)||info.Detail=="연결됨"||info.Detail.StartsWith("연결됨 · 배터리 ")))info.Detail="연결됨 · 배터리 "+battery.Value+"%";
            return info;
        }
        internal static string Model(string name)
        {
            string value=BudsConnectionTracker.Normalize(name);foreach(string model in new[]{"Buds4Pro","Buds4","Buds3FE","Buds3Pro","Buds3","Buds2Pro","Buds2","BudsCore","BudsFE","BudsLive","BudsPro"})if(value.IndexOf(model.ToUpperInvariant(),StringComparison.Ordinal)>=0)return model;
            return value.Contains("GALAXYBUDS")?"Buds":null;
        }
        internal static ImageSource Picture(string name)
        {
            string model=Model(name);if(model==null)return null;lock(gate){ImageSource image;if(pictures.TryGetValue(model,out image))return image;
                if(DateTime.UtcNow>=rootUntil){rootUntil=DateTime.UtcNow.AddMinutes(5);try{root=new PackageManager().FindPackagesForUser("").Where(p=>p.Id.Name.IndexOf("SamsungSettings",StringComparison.OrdinalIgnoreCase)>=0&&!p.IsFramework).Select(p=>p.InstalledPath).FirstOrDefault(p=>Directory.Exists(Path.Combine(p,"EasyConnect2","Assets","Buds")));}catch{root=null;}}
                if(root==null)return null;string file=Path.Combine(root,"EasyConnect2","Assets","Buds",model+"_IMG_Connect.png");image=NativeToastAssets.Image("",new Uri(file).AbsoluteUri,false,192);if(image!=null){if(pictures.Count>=6)pictures.Clear();pictures[model]=image;}return image;
            }
        }
        static byte[] Property(uint instance,Key key)
        {
            uint type,size=1024;var data=new byte[size];if(CM_Get_DevNode_PropertyW(instance,ref key,out type,data,ref size,0)!=0||size>data.Length)return null;Array.Resize(ref data,(int)size);return data;
        }
        static string Text(uint instance,uint id){var data=Property(instance,new Key("A45C254E-DF1C-4EFD-8020-67D146A850E0",id));return data==null?"":Encoding.Unicode.GetString(data).TrimEnd('\0');}
        internal static int? Battery(Guid? container,string name)
        {
            try{
                var devices=new List<Device>();foreach(string filter in new[]{"BTHLE","BTHENUM"}){
                    uint size;if(CM_Get_Device_ID_List_SizeW(out size,filter,0x101)!=0||size<2||size>262144)continue;var buffer=new char[size];if(CM_Get_Device_ID_ListW(filter,buffer,size,0x101)!=0)continue;
                    foreach(string id in new string(buffer).Split(new[]{'\0'},StringSplitOptions.RemoveEmptyEntries).Take(256)){
                        uint instance;if(CM_Locate_DevNodeW(out instance,id,0)!=0)continue;var cid=Property(instance,new Key("8C7ED206-3F8A-4827-B3AB-AE9E1FAEFC6C",2));if(cid==null||cid.Length!=16)continue;
                        var row=new Device {Container=new Guid(cid),Name=Text(instance,14)};if(String.IsNullOrEmpty(row.Name))row.Name=Text(instance,2);
                        var value=Property(instance,new Key("104EA319-6EE2-4701-BD47-8DDBF425BBE5",2));if(value!=null&&value.Length>0){int percent=value.Length>=4?BitConverter.ToInt32(value,0):value[0];if(percent>=0&&percent<=100)row.Battery=percent;}devices.Add(row);
                    }
                }
                if(!container.HasValue){var matches=devices.Where(d=>BudsConnectionTracker.Normalize(d.Name)==BudsConnectionTracker.Normalize(name)).Select(d=>d.Container).Distinct().ToList();if(matches.Count==1)container=matches[0];}
                if(container.HasValue)return devices.Where(d=>d.Container==container&&d.Battery.HasValue).Select(d=>d.Battery).FirstOrDefault();
            }catch(Exception error){AppLog.Once("buds-battery",error.GetType().Name);}return null;
        }
    }
}
