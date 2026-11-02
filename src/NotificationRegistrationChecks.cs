using System;
using System.Collections.Generic;
using System.Xml;

namespace OneTaskBar
{
    internal static class NotificationRegistrationChecks
    {
        internal static void Validate(List<string> checks)
        {
            var build = new Version(9, 5, 9, 0);
            Require(NotificationRegistration.ForLocation(build, null, false) == build, "first registration");
            Require(NotificationRegistration.ForLocation(build, build, false) == build, "same location does not churn revisions");
            Require(NotificationRegistration.ForLocation(build, new Version(9, 5, 8, 0), true) == build, "normal upgrade");
            Require(NotificationRegistration.ForLocation(build, build, true) == new Version(9, 5, 9, 1), "same-version relocation");
            Require(NotificationRegistration.ForLocation(build, new Version(9, 5, 9, 3), true) == new Version(9, 5, 9, 4), "subsequent relocation");
            Reject(() => NotificationRegistration.ForLocation(build, new Version(9, 5, 10, 0), true));
            Reject(() => NotificationRegistration.ForLocation(build, new Version(9, 5, 9, 65535), true));
            Require(NotificationRegistration.SameDirectory(@"C:\Apps\OneTaskBar\", @"c:\apps\OneTaskBar"), "directory case and trailing separator");
            Require(!NotificationRegistration.SameDirectory(@"C:\Apps\OneTaskBar", @"C:\Apps\OneTaskBar\dist"), "build folder is a different identity location");
            Require(!NotificationRegistration.SameDirectory(null, @"C:\Apps\OneTaskBar"), "missing location");
            var xml = new XmlDocument();
            xml.LoadXml(NotificationIdentity.Manifest(new Version(9, 5, 9, 4)));
            var ns = new XmlNamespaceManager(xml.NameTable);
            ns.AddNamespace("p", "http://schemas.microsoft.com/appx/manifest/foundation/windows10");
            var identity = (XmlElement)xml.SelectSingleNode("/p:Package/p:Identity", ns);
            Require(identity.GetAttribute("Version") == "9.5.9.4" && identity.GetAttribute("Name") == NotificationIdentity.Name &&
                identity.GetAttribute("Publisher") == NotificationIdentity.Publisher, "revision changes preserve package identity");
            Require(xml.SelectSingleNode("/p:Package/p:Capabilities/*[@Name='userNotificationListener']", ns) != null,
                "notification capability is retained");
            checks.Add("notification-identity-relocation-preserves-family-and-uses-monotonic-revision");
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Notification registration: " + message);
        }

        static void Reject(Action action)
        {
            bool rejected = false;
            try { action(); } catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "newer releases and exhausted revisions cannot be replaced");
        }
    }
}
