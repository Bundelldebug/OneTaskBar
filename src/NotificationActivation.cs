using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.Win32;
using Windows.UI.Notifications;

namespace OneTaskBar
{
    internal sealed class NotificationPayload
    {
        internal string Arguments="",Type="foreground",TargetPackage="";
        internal static string TextKey(IEnumerable<string> values) {return Regex.Replace(String.Concat(values),@"\s+","");}
        internal static NotificationPayload Parse(string xml,IEnumerable<string> text)
        {
            var document=new XmlDocument {XmlResolver=null};document.LoadXml(xml);var toast=document.DocumentElement;if(toast==null||toast.LocalName!="toast")return null;
            var expected=text.ToArray();var binding=toast.SelectSingleNode("visual/binding[@template='ToastGeneric']");if(binding==null)return null;
            var actual=binding.SelectNodes("text").Cast<XmlNode>().Take(expected.Length).Select(n=>n.InnerText);
            if(TextKey(actual)!=TextKey(expected))return null;
            return new NotificationPayload {Arguments=toast.GetAttribute("launch"),Type=String.IsNullOrEmpty(toast.GetAttribute("activationType"))?"foreground":toast.GetAttribute("activationType"),TargetPackage=toast.GetAttribute("protocolActivationTargetApplicationPfn")};
        }
        internal static NotificationPayload Read(string appId,string[] text)
        {
            try {
                var matches=new List<NotificationPayload>();
                foreach(var toast in ToastNotificationManager.GetDefault().History.GetHistory(appId)) {try {var payload=Parse(toast.Content.GetXml(),text);if(payload!=null)matches.Add(payload);}catch{}}
                var unique=matches.GroupBy(p=>p.Type+"\0"+p.Arguments+"\0"+p.TargetPackage).Select(g=>g.First()).ToList();
                return unique.Count==1?unique[0]:null;
            }catch(Exception e){AppLog.Once("notification-payload",e.Message);return null;}
        }
    }
    [ComImport,Guid("53E31837-6600-4A81-9395-75CFFE746F94"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface INotificationCallback
    {
        void Activate([MarshalAs(UnmanagedType.LPWStr)]string appId,[MarshalAs(UnmanagedType.LPWStr)]string arguments,IntPtr input,uint count);
    }
    [ComImport,Guid("2E941141-7F97-4756-BA1D-9DECDE894A3D"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IApplicationActivation
    {
        void ActivateApplication([MarshalAs(UnmanagedType.LPWStr)]string appId,[MarshalAs(UnmanagedType.LPWStr)]string arguments,uint options,out uint pid);
        void ActivateForFile([MarshalAs(UnmanagedType.LPWStr)]string appId,IntPtr items,[MarshalAs(UnmanagedType.LPWStr)]string verb,out uint pid);
        void ActivateForProtocol([MarshalAs(UnmanagedType.LPWStr)]string appId,IntPtr items,out uint pid);
    }
    internal sealed class NotificationOpenResult {public bool Invoked,Exact,Payload;public string Method;}
    internal static class NotificationActivation
    {
        [DllImport("ole32.dll")]static extern int CoCreateInstance(ref Guid clsid,IntPtr outer,uint context,ref Guid iid,out IntPtr instance);
        [DllImport("user32.dll")]static extern bool AllowSetForegroundWindow(uint pid);
        static object Create(Guid clsid,Type contract)
        {
            IntPtr pointer;var iid=contract.GUID;Marshal.ThrowExceptionForHR(CoCreateInstance(ref clsid,IntPtr.Zero,4,ref iid,out pointer));
            try{return Marshal.GetObjectForIUnknown(pointer);}finally{Marshal.Release(pointer);}
        }
        internal static Guid Callback(string appId)
        {
            if(String.IsNullOrEmpty(appId))return Guid.Empty;
            foreach(var hive in new[]{Registry.CurrentUser,Registry.LocalMachine})try {
                using(var key=hive.OpenSubKey(@"Software\Classes\AppUserModelId\"+appId)) {Guid id;if(key!=null&&Guid.TryParse(key.GetValue("CustomActivator") as string,out id))return id;}
            }catch{}
            if(appId.Contains("!"))try {
                var info=Windows.ApplicationModel.AppInfo.GetFromAppUserModelId(appId);var package=info.Package;
                var document=new XmlDocument {XmlResolver=null};document.Load(Path.Combine(package.InstalledPath,"AppxManifest.xml"));string applicationId=appId.Substring(appId.IndexOf('!')+1);
                foreach(XmlElement app in document.GetElementsByTagName("Application"))if(app.GetAttribute("Id")==applicationId)
                    foreach(XmlElement element in app.GetElementsByTagName("*"))if(element.LocalName=="ToastNotificationActivation") {Guid id;if(Guid.TryParse(element.GetAttribute("ToastActivatorCLSID"),out id))return id;}
            }catch{}
            return Guid.Empty;
        }
        internal static NotificationOpenResult Open(NotificationEntry item,bool inspectOnly)
        {
            var result=new NotificationOpenResult {Payload=item.Payload!=null,Method="app"};var payload=item.Payload;
            bool protocol=payload!=null&&payload.Type=="protocol";
            if(protocol){Uri uri;if(!Uri.TryCreate(payload.Arguments,UriKind.Absolute,out uri))return result;result.Exact=true;result.Method="protocol";
                if(!inspectOnly){var options=new Windows.System.LauncherOptions();if(!String.IsNullOrEmpty(payload.TargetPackage))options.TargetApplicationPackageFamilyName=payload.TargetPackage;result.Invoked=SystemServices.Wait(Windows.System.Launcher.LaunchUriAsync(uri,options)).GetAwaiter().GetResult();}return result;}
            Guid callback=item.Activator;
            if(payload!=null&&callback!=Guid.Empty){result.Exact=true;result.Method="toast-callback";if(inspectOnly)return result;
                object instance=null;try {AllowSetForegroundWindow(UInt32.MaxValue);instance=Create(callback,typeof(INotificationCallback));((INotificationCallback)instance).Activate(item.AppId,payload.Arguments,IntPtr.Zero,0);result.Invoked=true;return result;}
                catch(Exception e){AppLog.Once("notification-callback",e.Message);result.Exact=false;}finally{if(instance!=null&&Marshal.IsComObject(instance))Marshal.ReleaseComObject(instance);}}
            if(inspectOnly)return result;
            if(String.IsNullOrEmpty(item.AppId))return result;
            object activation=null;
            try {AllowSetForegroundWindow(UInt32.MaxValue);activation=Create(new Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C"),typeof(IApplicationActivation));uint pid;((IApplicationActivation)activation).ActivateApplication(item.AppId,payload!=null?payload.Arguments:"",0,out pid);result.Invoked=true;}
            catch {try {Process.Start(new ProcessStartInfo("explorer.exe","\"shell:AppsFolder\\"+item.AppId+"\""){UseShellExecute=true});result.Invoked=true;}catch(Exception e){AppLog.Once("notification-launch",e.Message);}}
            finally {if(activation!=null&&Marshal.IsComObject(activation))Marshal.ReleaseComObject(activation);}
            return result;
        }
    }
}
