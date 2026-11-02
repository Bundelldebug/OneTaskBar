using System;
using System.Collections.Generic;

namespace OneTaskBar
{
    internal static class LogonTaskIdentityChecks
    {
        internal static void Validate(List<string> checks)
        {
            const string previous = @"C:\Downloads\One TaskBar\OneTaskBar.exe";
            const string current = @"C:\Apps\One TaskBar\OneTaskBar.exe";
            const string sid = "S-1-5-21-1234";
            string xml = LogonStartup.Definition(previous, sid);
            Require(LogonTaskIdentity.Executable(xml, sid) == previous, "recognizes a moved portable app's owned task");
            Require(!LogonStartup.Owned(xml, current, sid), "a stale path is not reported as current startup");
            Require(LogonStartup.Owned(LogonStartup.Definition(current, sid), current, sid), "replacement targets the current path");
            using(var identity=System.Security.Principal.WindowsIdentity.GetCurrent())
            {
                string own=LogonStartup.Definition(previous,identity.User.Value);
                string normalized=own.Replace("<UserId>"+identity.User.Value+"</UserId><Delay>",
                    "<UserId>"+System.Security.SecurityElement.Escape(identity.Name)+"</UserId><Delay>");
                Require(LogonTaskIdentity.Executable(normalized,identity.User.Value)==previous,
                    "scheduler-normalized account names retain the same owner");
            }
            Require(LogonTaskIdentity.Executable(xml, "S-1-5-21-other") == null, "another user is excluded");
            foreach (var invalid in new[] {
                xml.Replace("OneTaskBar.Logon.v1|", "AnotherApp|"),
                xml.Replace("LeastPrivilege", "HighestAvailable"),
                xml.Replace("InteractiveToken", "Password"),
                xml.Replace("--desktop --logon", "--settings"),
                xml.Replace("OneTaskBar.exe", "other.exe"),
                xml.Replace("</Actions>", "<Exec><Command>other.exe</Command></Exec></Actions>"),
                xml.Replace("</Triggers>", "<BootTrigger /></Triggers>")})
                Require(LogonTaskIdentity.Executable(invalid, sid) == null, "foreign or expanded task definitions are excluded");
            checks.Add("portable-logon-task-relocation-retains-user-ownership-and-least-privilege");
        }

        static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("Portable startup: " + message);
        }
    }
}
