using System;
using System.IO;
using System.Runtime.InteropServices;
using Windows.ApplicationModel;
using Windows.Management.Deployment;

namespace OneTaskBar
{
    internal static class NotificationRegistration
    {
        internal static bool SameDirectory(string first, string second)
        {
            if (String.IsNullOrWhiteSpace(first) || String.IsNullOrWhiteSpace(second)) return false;
            return String.Equals(Path.GetFullPath(first).TrimEnd('\\', '/'),
                Path.GetFullPath(second).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
        }

        internal static Version VersionOf(Package package)
        {
            var value = package.Id.Version;
            return new Version(value.Major, value.Minor, value.Build, value.Revision);
        }

        // Windows cannot relocate an existing package full name. A newer identity
        // revision changes its external path without removing its data or consent.
        internal static Version ForLocation(Version build, Version registered, bool changedLocation)
        {
            if (!changedLocation || registered == null || registered < build) return build;
            if (registered.Major != build.Major || registered.Minor != build.Minor || registered.Build != build.Build)
                throw new InvalidOperationException("다른 경로에 더 최신 버전이 등록되어 있습니다. 최신 One TaskBar에서 알림을 연결해 주세요.");
            if (registered.Revision >= UInt16.MaxValue)
                throw new InvalidOperationException("알림 등록 버전을 갱신할 수 없습니다. One TaskBar를 업데이트해 주세요.");
            return new Version(build.Major, build.Minor, build.Build, registered.Revision + 1);
        }

        internal static void CheckResult(DeploymentResult result)
        {
            if (result.ExtendedErrorCode != null)
                throw new COMException(result.ErrorText, result.ExtendedErrorCode.HResult);
        }
    }
}
