using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Xml;
using Windows.Foundation;
using Windows.Management.Deployment;
namespace OneTaskBar
{
    internal sealed class IdentityResult {public bool Registered,AdministratorRequired;public string AppId,Error;public int ErrorCode;}
    internal static class NotificationIdentity
    {
        internal const string Name="OneTaskBar.Desktop",Publisher="CN=OneTaskBar, OID.2.25.311729368913984317654407730594956997722=1";
        internal static bool IsCurrent {get{try{var id=Windows.ApplicationModel.Package.Current.Id;return id.Name==Name&&String.Equals(id.Publisher,Publisher,StringComparison.OrdinalIgnoreCase);}catch{return false;}}}
        internal static IdentityResult Find()
        {
            try {var package=new PackageManager().FindPackagesForUser("",Name,Publisher).FirstOrDefault(p=>p.Id.Name==Name&&String.Equals(p.Id.Publisher,Publisher,StringComparison.OrdinalIgnoreCase)&&String.Equals(p.EffectiveExternalPath.TrimEnd(Path.DirectorySeparatorChar),Path.GetDirectoryName(PhysicalFile(Path.Combine(AppLog.Root,"OneTaskBar.exe"))),StringComparison.OrdinalIgnoreCase));return package==null?new IdentityResult():new IdentityResult {Registered=true,AppId=package.Id.FamilyName+"!OneTaskBar"};}
            catch(Exception error){return new IdentityResult {Error=error.Message,ErrorCode=error.HResult};}
        }
        internal static bool Bootstrap(string[] args)
        {
            if(IsCurrent||args.Contains("--identity-activation"))return false;
            try{var registration=Find();if(!registration.Registered)return false;var allowed=new[]{"--desktop","--logon","--settings","--connect-notifications","--qa","--review-ui"};string forwarded=String.Join(" ",args.Where(a=>allowed.Contains(a)).Concat(new[]{"--identity-activation"}));object instance=Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")));try{uint pid;((IApplicationActivation)instance).ActivateApplication(registration.AppId,forwarded,0,out pid);}finally{Marshal.ReleaseComObject(instance);}return true;}
            catch(Exception error){AppLog.Once("identity-startup",error.Message);return false;}
        }
        static string ResultFile(string nonce){return Path.Combine(AppLog.Data,"notification-registration-"+nonce+".json");}
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern uint GetFinalPathNameByHandle(Microsoft.Win32.SafeHandles.SafeFileHandle file,System.Text.StringBuilder path,uint length,uint flags);
        internal static string PhysicalFile(string path){using(var file=File.Open(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)){var text=new System.Text.StringBuilder(32768);uint length=GetFinalPathNameByHandle(file.SafeFileHandle,text,(uint)text.Capacity,0);if(length==0||length>=text.Capacity)throw new System.ComponentModel.Win32Exception();string value=text.ToString();return value.StartsWith(@"\\?\UNC\",StringComparison.OrdinalIgnoreCase)?@"\\"+value.Substring(8):value.StartsWith(@"\\?\",StringComparison.Ordinal)?value.Substring(4):value;}}
        // The .NET Framework WinRT URI marshaler uses OriginalString. Normalize it to
        // file:/// explicitly so a Windows path cannot become an empty relative path.
        internal static Uri FileUri(string path){return new Uri(new Uri(Path.GetFullPath(path)).AbsoluteUri,UriKind.Absolute);}
        internal static string Manifest()
        {
            var xml=new XmlDocument();using(var stream=typeof(NotificationIdentity).Assembly.GetManifestResourceStream("OneTaskBar.IdentityManifest")){if(stream==null)throw new InvalidOperationException("알림 연결 매니페스트가 없습니다.");xml.Load(stream);}var ns=new XmlNamespaceManager(xml.NameTable);ns.AddNamespace("p","http://schemas.microsoft.com/appx/manifest/foundation/windows10");var identity=(XmlElement)xml.SelectSingleNode("/p:Package/p:Identity",ns);identity.SetAttribute("Version",typeof(NotificationIdentity).Assembly.GetName().Version.ToString());return xml.OuterXml;
        }
        internal static int RegisterHelper(string nonce,string userSid)
        {
            Guid parsed;if(!Guid.TryParseExact(nonce,"N",out parsed))return 2;var result=new IdentityResult();int exit=1;
            try {
                using(var identity=WindowsIdentity.GetCurrent())if(identity.User.Value!=userSid)return 23;
                var existing=Find();if(existing.Registered){result=existing;exit=0;}else{
                    string folder=Path.Combine(AppLog.Data,"notification-identity");Directory.CreateDirectory(folder);string manifest=Path.Combine(folder,"AppxManifest.xml");File.WriteAllText(manifest,Manifest(),new System.Text.UTF8Encoding(false));
                    string package=IdentityPackage.Build(manifest,folder),external=Path.GetDirectoryName(PhysicalFile(Path.Combine(AppLog.Root,"OneTaskBar.exe")));var options=new AddPackageOptions {AllowUnsigned=true,ExternalLocationUri=FileUri(external+Path.DirectorySeparatorChar)};var uri=FileUri(PhysicalFile(package));AppLog.Write("notification-register-path",uri.AbsoluteUri+" | "+options.ExternalLocationUri.AbsoluteUri);var operation=new PackageManager().AddPackageByUriAsync(uri,options);var watch=Stopwatch.StartNew();while(operation.Status==AsyncStatus.Started&&watch.Elapsed.TotalSeconds<90)Thread.Sleep(40);
                    if(operation.Status==AsyncStatus.Started){operation.Cancel();throw new TimeoutException("Windows 앱 등록이 시간 안에 끝나지 않았습니다.");}if(operation.Status==AsyncStatus.Error)throw operation.ErrorCode;if(operation.Status==AsyncStatus.Canceled)throw new OperationCanceledException();var deployment=operation.GetResults();if(deployment.ExtendedErrorCode!=null)throw new InvalidOperationException(deployment.ErrorText,deployment.ExtendedErrorCode);
                    result=Find();if(!result.Registered)throw new InvalidOperationException("Windows 앱 등록을 확인하지 못했습니다.");exit=0;
                }
            }catch(Exception error){result.Error=error.Message;result.ErrorCode=error.HResult;result.AdministratorRequired=error.HResult==unchecked((int)0x80070005)||error.HResult==unchecked((int)0x80073D2B)||error is UnauthorizedAccessException;exit=result.AdministratorRequired?5:1;AppLog.Write("notification-register",error.ToString());}
            AppLog.Json(Path.GetFileName(ResultFile(nonce)),result);return exit;
        }
        internal static async Task<IdentityResult> Register(bool elevate)
        {
            string nonce=Guid.NewGuid().ToString("N"),sid;using(var identity=WindowsIdentity.GetCurrent())sid=identity.User.Value;
            var info=new ProcessStartInfo(PhysicalFile(Path.Combine(AppLog.Root,"OneTaskBar.exe")),"--register-notifications "+nonce+" "+sid) {UseShellExecute=elevate,WindowStyle=ProcessWindowStyle.Hidden,CreateNoWindow=!elevate};if(elevate)info.Verb="runas";
            try {using(var process=Process.Start(info)){bool ended=await Task.Run(()=>process.WaitForExit(120000));if(!ended)return new IdentityResult {Error="Windows 승인 또는 앱 등록이 아직 끝나지 않았습니다. 잠시 후 다시 확인해 주세요."};if(process.ExitCode==23)return new IdentityResult {Error="현재 로그인한 Windows 계정으로 등록해야 합니다. 다른 관리자 계정으로는 연결할 수 없습니다."};string path=ResultFile(nonce);if(File.Exists(path)){var result=new JavaScriptSerializer().Deserialize<IdentityResult>(File.ReadAllText(path));File.Delete(path);return result;}return new IdentityResult {Error="Windows가 앱 등록을 완료하지 못했습니다.",ErrorCode=process.ExitCode};}}
            catch(System.ComponentModel.Win32Exception error){return new IdentityResult {Error=error.NativeErrorCode==1223?"Windows 관리자 승인이 취소되었습니다.":error.Message,ErrorCode=error.HResult};}
        }
        internal static void RestartWithIdentity(string appId)
        {
            var expected=Find();if(!expected.Registered||expected.AppId!=appId)throw new InvalidOperationException("알림 연결 앱 ID가 등록된 One TaskBar와 다릅니다.");using(var process=Process.GetCurrentProcess()){Process.Start(new ProcessStartInfo(Path.Combine(AppLog.Root,"OneTaskBar.exe"),"--notification-restart "+process.Id+" "+process.StartTime.ToUniversalTime().Ticks+" "+appId){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});}System.Windows.Application.Current.Shutdown();
        }
        internal static int RestartHelper(string[] args)
        {
            try {int pid;long ticks;if(args.Length!=4||!Int32.TryParse(args[1],out pid)||!Int64.TryParse(args[2],out ticks))return 2;var expected=Find();if(!expected.Registered||expected.AppId!=args[3])return 2;
                try{using(var process=Process.GetProcessById(pid)){if(process.StartTime.ToUniversalTime().Ticks!=ticks||!String.Equals(process.MainModule.FileName,Path.Combine(AppLog.Root,"OneTaskBar.exe"),StringComparison.OrdinalIgnoreCase))return 2;if(!process.WaitForExit(20000))return 3;}}catch(ArgumentException){}
                object instance=Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")));try{uint started;((IApplicationActivation)instance).ActivateApplication(expected.AppId,"--desktop --connect-notifications",0,out started);}finally{Marshal.ReleaseComObject(instance);}return 0;
            }catch(Exception error){AppLog.Write("notification-restart",error.ToString());Process.Start(new ProcessStartInfo(Path.Combine(AppLog.Root,"OneTaskBar.exe"),"--desktop"){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});return 1;}
        }
    }
}
