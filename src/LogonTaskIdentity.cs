using System;
using System.IO;
using System.Security.Principal;
using System.Xml;

namespace OneTaskBar
{
    internal static class LogonTaskIdentity
    {
        const string Namespace = "http://schemas.microsoft.com/windows/2004/02/mit/task";

        // Ownership is tied to this user's marked, least-privilege logon task.
        // Its executable path can legitimately change when a portable app moves.
        internal static string Executable(string xml, string userSid)
        {
            if (String.IsNullOrEmpty(xml) || xml.Length > 65536 || String.IsNullOrEmpty(userSid)) return null;
            try
            {
                var document = new XmlDocument {XmlResolver = null};
                var options = new XmlReaderSettings {DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null};
                using (var reader = XmlReader.Create(new StringReader(xml), options)) document.Load(reader);
                var ns = new XmlNamespaceManager(document.NameTable);
                ns.AddNamespace("t", Namespace);
                Func<string, string> value = path =>
                {
                    var node = document.SelectSingleNode("/t:Task/" + path, ns);
                    return node == null ? null : node.InnerText;
                };
                string level = value("t:Principals/t:Principal/t:RunLevel");
                if (value("t:RegistrationInfo/t:Source") != "OneTaskBar.Logon.v1|" + userSid ||
                    document.SelectNodes("/t:Task/t:Actions/*", ns).Count != 1 ||
                    document.SelectNodes("/t:Task/t:Principals/*", ns).Count != 1 ||
                    document.SelectNodes("/t:Task/t:Triggers/*", ns).Count != 1 ||
                    !SameUser(value("t:Triggers/t:LogonTrigger/t:UserId"),userSid) ||
                    value("t:Actions/t:Exec/t:Arguments") != LogonStartup.Arguments ||
                    value("t:Principals/t:Principal/t:UserId") != userSid ||
                    value("t:Principals/t:Principal/t:LogonType") != "InteractiveToken" ||
                    (level != null && level != "LeastPrivilege")) return null;
                string command = value("t:Actions/t:Exec/t:Command");
                if (!Path.IsPathRooted(command) ||
                    !String.Equals(Path.GetFileName(command), "OneTaskBar.exe", StringComparison.OrdinalIgnoreCase)) return null;
                return Path.GetFullPath(command);
            }
            catch { return null; }
        }

        static bool SameUser(string registered,string sid)
        {
            if(registered==sid)return true;
            // Task Scheduler normalizes a logon trigger's SID to DOMAIN\name.
            // Only accept the current account's equivalent, never another user.
            using(var identity=WindowsIdentity.GetCurrent())
                return identity.User!=null&&identity.User.Value==sid&&
                    String.Equals(registered,identity.Name,StringComparison.OrdinalIgnoreCase);
        }
    }
}
