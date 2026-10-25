using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Xml;
using Microsoft.Win32;
namespace OneTaskBar
{
    internal static class LogonStartup
    {
        internal const string TaskName="OneTaskBar.Startup",Arguments="--desktop --logon";
        static readonly object gate=new object();
        static DateTime statusUntil;static bool cachedStatus;
        const string RunKey=@"Software\Microsoft\Windows\CurrentVersion\Run",TaskNamespace="http://schemas.microsoft.com/windows/2004/02/mit/task";
        static string Exe {get{return NotificationIdentity.PhysicalFile(Path.Combine(AppLog.Root,"OneTaskBar.exe"));}}
        static string Sid {get{using(var identity=WindowsIdentity.GetCurrent())return identity.User.Value;}}
        static object Invoke(object target,string name,BindingFlags flags,params object[] args){return target.GetType().InvokeMember(name,flags|BindingFlags.OptionalParamBinding,null,target,args,CultureInfo.InvariantCulture);}
        sealed class Scheduler : IDisposable
        {
            readonly List<object> objects=new List<object>();internal readonly object Root;
            internal Scheduler(){try{var service=Keep(Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")));Invoke(service,"Connect",BindingFlags.InvokeMethod);Root=Keep(Invoke(service,"GetFolder",BindingFlags.InvokeMethod,"\\"));}catch{Dispose();throw;}}
            internal object Keep(object value){if(value!=null&&Marshal.IsComObject(value))objects.Add(value);return value;}
            internal object Find(string name){try{return Keep(Invoke(Root,"GetTask",BindingFlags.InvokeMethod,name));}catch(Exception error){var inner=error;while(inner.InnerException!=null)inner=inner.InnerException;if(inner.HResult==unchecked((int)0x80070002)||inner.HResult==unchecked((int)0x8004130F))return null;throw;}}
            public void Dispose(){for(int i=objects.Count-1;i>=0;i--)try{Marshal.FinalReleaseComObject(objects[i]);}catch{}objects.Clear();}
        }
        internal static string Definition(string executable,string userSid,bool enabled=true)
        {
            var text=new StringBuilder();using(var writer=XmlWriter.Create(text,new XmlWriterSettings {OmitXmlDeclaration=true})){
                writer.WriteStartElement("Task",TaskNamespace);writer.WriteAttributeString("version","1.2");
                writer.WriteStartElement("RegistrationInfo");writer.WriteElementString("Description","One TaskBar sign-in startup, managed by the app's automatic startup setting.");writer.WriteElementString("Source","OneTaskBar.Logon.v1|"+userSid);writer.WriteEndElement();
                writer.WriteStartElement("Triggers");writer.WriteStartElement("LogonTrigger");writer.WriteElementString("Enabled",enabled?"true":"false");writer.WriteElementString("UserId",userSid);writer.WriteElementString("Delay","PT0S");writer.WriteEndElement();writer.WriteEndElement();
                writer.WriteStartElement("Principals");writer.WriteStartElement("Principal");writer.WriteAttributeString("id","CurrentUser");writer.WriteElementString("UserId",userSid);writer.WriteElementString("LogonType","InteractiveToken");writer.WriteElementString("RunLevel","LeastPrivilege");writer.WriteEndElement();writer.WriteEndElement();
                writer.WriteStartElement("Settings");writer.WriteElementString("MultipleInstancesPolicy","IgnoreNew");writer.WriteElementString("DisallowStartIfOnBatteries","false");writer.WriteElementString("StopIfGoingOnBatteries","false");writer.WriteElementString("AllowHardTerminate","false");writer.WriteElementString("StartWhenAvailable","true");writer.WriteElementString("Enabled",enabled?"true":"false");writer.WriteElementString("ExecutionTimeLimit","PT0S");writer.WriteElementString("Priority","5");writer.WriteEndElement();
                writer.WriteStartElement("Actions");writer.WriteAttributeString("Context","CurrentUser");writer.WriteStartElement("Exec");writer.WriteElementString("Command",executable);writer.WriteElementString("Arguments",Arguments);writer.WriteElementString("WorkingDirectory",Path.GetDirectoryName(executable));writer.WriteEndElement();writer.WriteEndElement();writer.WriteEndElement();
            }return text.ToString();
        }
        internal static bool Owned(string xml,string executable,string userSid)
        {
            try{if(xml==null||xml.Length>65536)return false;var document=new XmlDocument {XmlResolver=null};using(var reader=XmlReader.Create(new StringReader(xml),new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null}))document.Load(reader);var ns=new XmlNamespaceManager(document.NameTable);ns.AddNamespace("t",TaskNamespace);Func<string,string> value=path=>{var node=document.SelectSingleNode("/t:Task/"+path,ns);return node==null?null:node.InnerText;};string level=value("t:Principals/t:Principal/t:RunLevel");return value("t:RegistrationInfo/t:Source")=="OneTaskBar.Logon.v1|"+userSid&&document.SelectNodes("/t:Task/t:Actions/*",ns).Count==1&&String.Equals(Path.GetFullPath(value("t:Actions/t:Exec/t:Command")),Path.GetFullPath(executable),StringComparison.OrdinalIgnoreCase)&&value("t:Actions/t:Exec/t:Arguments")==Arguments&&value("t:Principals/t:Principal/t:UserId")==userSid&&value("t:Principals/t:Principal/t:LogonType")=="InteractiveToken"&&(level==null||level=="LeastPrivilege");}catch{return false;}
        }
        static string Legacy(){using(var key=Registry.CurrentUser.OpenSubKey(RunKey))return key==null?null:key.GetValue("OneTaskBar",null) as string;}
        static bool OwnLegacy(string command,string executable)
        {
            if(String.IsNullOrEmpty(command))return false;if(String.Equals(command,"\""+executable+"\" "+Arguments,StringComparison.OrdinalIgnoreCase))return true;
            try{if(NotificationIdentity.IsCurrent){string family=Windows.ApplicationModel.Package.Current.Id.FamilyName;return String.Equals(command,"explorer.exe \"shell:AppsFolder\\"+family+"!OneTaskBar\"",StringComparison.OrdinalIgnoreCase);}}catch{}return false;
        }
        static bool LegacyDisabled()
        {
            using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run")){var state=key==null?null:key.GetValue("OneTaskBar",null) as byte[];return state!=null&&state.Length>=1&&(state[0]==3||state[0]==7);}
        }
        internal static bool Enabled
        {
            get{lock(gate){if(DateTime.UtcNow<statusUntil)return cachedStatus;cachedStatus=ReadStatus();statusUntil=DateTime.UtcNow.AddSeconds(3);return cachedStatus;}}
        }
        static bool ReadStatus(){try{string executable=Exe,user=Sid;using(var scheduler=new Scheduler()){var task=scheduler.Find(TaskName);if(task!=null&&Owned((string)Invoke(task,"Xml",BindingFlags.GetProperty),executable,user))return (bool)Invoke(task,"Enabled",BindingFlags.GetProperty);}}catch(Exception error){AppLog.Once("startup-read",error.GetType().Name);}try{return OwnLegacy(Legacy(),Exe)&&!LegacyDisabled();}catch{return false;}}
        internal static void Set(bool enabled)
        {
            lock(gate)SetCore(enabled);
        }
        static void SetCore(bool enabled)
        {
            string executable=Exe,user=Sid;using(var scheduler=new Scheduler()){
                var task=scheduler.Find(TaskName);if(task!=null&&!Owned((string)Invoke(task,"Xml",BindingFlags.GetProperty),executable,user))throw new InvalidOperationException("같은 이름의 다른 시작 작업이 있어 변경하지 않았습니다.");
                if(enabled){scheduler.Keep(Invoke(scheduler.Root,"RegisterTask",BindingFlags.InvokeMethod,TaskName,Definition(executable,user),task==null?2:6,null,null,3,null));var registered=scheduler.Find(TaskName);if(registered==null||!(bool)Invoke(registered,"Enabled",BindingFlags.GetProperty)||!Owned((string)Invoke(registered,"Xml",BindingFlags.GetProperty),executable,user))throw new InvalidOperationException("로그인 시작 작업을 확인하지 못했습니다.");}
                else if(task!=null)Invoke(scheduler.Root,"DeleteTask",BindingFlags.InvokeMethod,TaskName,0);
            }
            using(var key=Registry.CurrentUser.OpenSubKey(RunKey,true))if(key!=null&&OwnLegacy(key.GetValue("OneTaskBar",null) as string,executable))key.DeleteValue("OneTaskBar",false);
            cachedStatus=enabled;statusUntil=DateTime.UtcNow.AddSeconds(3);
        }
        internal static bool Migrate()
        {
            lock(gate)try{string executable=Exe;if(!OwnLegacy(Legacy(),executable)||LegacyDisabled())return false;using(var scheduler=new Scheduler())if(scheduler.Find(TaskName)!=null)return false;SetCore(true);AppLog.Write("startup-registration","Owned Run entry migrated to current-user zero-delay interactive logon task.");return true;}catch(Exception error){AppLog.Once("startup-registration","Retained existing startup registration: "+error.GetType().Name);return false;}
        }
        internal static int ProbeRegistration()
        {
            string name="OneTaskBar.Validation."+Guid.NewGuid().ToString("N"),executable=Exe,user=Sid;bool created=false;try{using(var scheduler=new Scheduler()){
                if(scheduler.Find(name)!=null)throw new InvalidOperationException("Fixture name collision");scheduler.Keep(Invoke(scheduler.Root,"RegisterTask",BindingFlags.InvokeMethod,name,Definition(executable,user,false),6,null,null,3,null));created=true;var task=scheduler.Find(name);string xml=task==null?null:(string)Invoke(task,"Xml",BindingFlags.GetProperty);bool enabled=task!=null&&(bool)Invoke(task,"Enabled",BindingFlags.GetProperty),owned=Owned(xml,executable,user);bool passed=task!=null&&!enabled&&owned;if(!passed&&xml!=null)File.WriteAllText(Path.Combine(AppLog.Data,"startup-registration-fixture.xml"),xml);Invoke(scheduler.Root,"DeleteTask",BindingFlags.InvokeMethod,name,0);created=false;AppLog.Json("startup-registration-probe.json",new {passed=passed,enabled=enabled,owned=owned,disabledTemporaryTaskOnly=true,currentUserInteractiveToken=true,leastPrivilege=true,taskNeverExecuted=true,temporaryTaskRemoved=true,realStartupUnchanged=true});return passed?0:1;
            }}catch(Exception error){AppLog.Json("startup-registration-probe.json",new {passed=false,error=error.ToString()});return 1;}finally{if(created)try{using(var scheduler=new Scheduler()){var task=scheduler.Find(name);if(task!=null&&Owned((string)Invoke(task,"Xml",BindingFlags.GetProperty),executable,user))Invoke(scheduler.Root,"DeleteTask",BindingFlags.InvokeMethod,name,0);}}catch{}}
        }
    }
}
