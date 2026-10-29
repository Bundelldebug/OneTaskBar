using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace OneTaskBar
{
    internal sealed class TrayIconItem
    {
        internal string Name;
        internal ImageSource Icon;
        internal AutomationElement Element;
        internal string Executable;
        internal string Fallback="window";
    }
    internal sealed class TrayIconSession : IDisposable
    {
        internal readonly List<TrayIconItem> Items;
        readonly IntPtr source;
        bool masked,disposed,keepNative;
        internal bool SourceVisible {get{return !disposed&&Native.IsWindow(source)&&Native.IsWindowVisible(source);}}
        internal TrayIconSession(IntPtr source,List<TrayIconItem> items){this.source=source;Items=items;}
        internal bool Mask()
        {
            if(disposed||source==IntPtr.Zero)return false;
            masked=TaskbarMask.Hide(source);
            return masked;
        }
        internal void LeaveNative(){keepNative=true;Restore(source);masked=false;if(source!=IntPtr.Zero&&Native.IsWindow(source)&&!Native.IsWindowVisible(source))Native.ShowWindow(source,8);}
        internal static void Restore(IntPtr window)
        {
            if(window!=IntPtr.Zero)TaskbarMask.Restore(window);
        }
        internal void Invoke(TrayIconItem item,bool context)
        {
            if(disposed||item==null||item.Element==null)return;
            try {
                if(context){
                    LeaveNative();
                    item.Element.SetFocus();SystemPanels.ContextMenu();
                }else{
                    TrayButtonAction.Invoke(item.Element);
                }
            }catch(Exception error){AppLog.Once("tray-action",error.GetType().Name);}
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            if(!keepNative&&source!=IntPtr.Zero)Native.ShowWindow(source,0);
            if(masked)Restore(source);
            Items.Clear();
        }
    }
    internal static class TrayIcons
    {
        sealed class SavedIcon {internal string Id,Name,Path;internal byte[] Image;}
        internal static ImageSource Decode(byte[] bytes)
        {
            if(bytes==null||bytes.Length<8||bytes.Length>1024*1024)return null;
            try{using(var stream=new MemoryStream(bytes,false)){var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.DecodePixelWidth=64;image.StreamSource=stream;image.EndInit();image.Freeze();return image;}}catch{return null;}
        }
        static string Normalize(string value){return new String((value??"").Where(Char.IsLetterOrDigit).Select(Char.ToLowerInvariant).ToArray());}
        static List<SavedIcon> ReadSaved()
        {
            var result=new List<SavedIcon>();
            using(var root=Registry.CurrentUser.OpenSubKey(@"Control Panel\NotifyIconSettings")){
                if(root==null)return result;
                foreach(var name in root.GetSubKeyNames().Take(1024))using(var key=root.OpenSubKey(name)){
                    if(key==null)continue;var path=key.GetValue("ExecutablePath") as string;
                    if(String.IsNullOrWhiteSpace(path))continue;
                    result.Add(new SavedIcon {Id=(key.GetValue("IconGuid") as string)??name,Path=Environment.ExpandEnvironmentVariables(path),Name=key.GetValue("InitialTooltip") as string,Image=key.GetValue("IconSnapshot") as byte[]});
                }
            }
            return result;
        }
        internal static TrayIconSession Open(TrayPlacement anchor,AppEntry[] apps)
        {
            if(!ExplorerBridge.TrayVisible()&&!ExplorerBridge.OpenTray(anchor))return null;
            var source=ExplorerBridge.OverflowHandle();if(source==IntPtr.Zero)return null;
            ExplorerBridge.AnchorTray(anchor);
            try{
                var saved=ReadSaved();var result=new List<TrayIconItem>();var diagnostics=Program.Inspectable?new List<object>():null;
                var root=AutomationElement.FromHandle(source);
                foreach(AutomationElement element in root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Button))){
                    if(result.Count>=128)break;
                    string name=element.Current.Name??"",id=element.Current.AutomationId??"";
                    if(String.IsNullOrWhiteSpace(name))continue;
                    if(!TrayButtonAction.CanInvoke(element))continue;
                    string normalized=Normalize(name);
                    var match=saved.OrderByDescending(s=>Score(normalized,id,s.Id,s.Name,s.Path)).FirstOrDefault();
                    if(match!=null&&Score(normalized,id,match.Id,match.Name,match.Path)==0)match=null;
                    ImageSource image=match==null?null:Decode(match.Image);
                    if(image==null&&match!=null&&File.Exists(match.Path))image=Native.AppIcon(match.Path);
                    if(image==null&&apps!=null){
                        var app=apps.OrderByDescending(a=>LabelScore(normalized,a.Name)).FirstOrDefault();
                        if(app!=null&&LabelScore(normalized,app.Name)>0)image=AppIcons.Get(app);
                    }
                    if(diagnostics!=null)diagnostics.Add(new {name=name,automationId=id,matched=match==null?null:match.Name,executable=match==null?null:Path.GetFileName(match.Path),snapshot=image!=null});
                    result.Add(new TrayIconItem {Name=name,Element=element,Icon=image,Executable=match==null?null:match.Path,Fallback=Fallback(normalized)});
                }
                if(diagnostics!=null)AppLog.Json("tray-provider-review.json",diagnostics);
                if(result.Count==0)return null;
                return new TrayIconSession(source,result);
            }catch(Exception error){AppLog.Once("tray-icons",error.GetType().Name);return null;}
        }
        internal static int Score(string name,string automationId,string id,string tooltip,string path)
        {
            if(String.IsNullOrEmpty(name))return 0;
            automationId=automationId??"";
            if(!String.IsNullOrWhiteSpace(id)&&automationId.IndexOf(id,StringComparison.OrdinalIgnoreCase)>=0)return 10000;
            var label=Normalize(tooltip);if(label.Length>1&&(name==label||name.StartsWith(label)||label.StartsWith(name)))return 1000+Math.Min(500,label.Length);
            if(label.Length>3&&(name.Contains(label)||label.Contains(name)))return 500+Math.Min(400,label.Length);
            var filename=Path.GetFileNameWithoutExtension(path??"");var file=Normalize(filename);
            if(file.Length>2&&name.Contains(file))return 100+file.Length;
            var prefix=Normalize(filename.Split('-','.','_',' ')[0]);
            return prefix.Length>3&&name.Contains(prefix)?50+prefix.Length:0;
        }
        static int LabelScore(string name,string label)
        {
            var value=Normalize(label);
            return value.Length>1&&(name==value||name.StartsWith(value))?value.Length:0;
        }
        static string Fallback(string name)
        {
            if(name.Contains("bluetooth"))return "bluetooth";
            if(name.Contains("빠른설정"))return "settings";
            if(name.Contains("전화")||name.Contains("휴대폰"))return "phone";
            if(name.Contains("주변기기"))return "devices";
            if(name.Contains("windows보안"))return "shield";
            return "window";
        }
    }
}
