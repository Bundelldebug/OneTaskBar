# Windows notification connection

The normal EXE installation runs per user without elevation and defaults to the Windows notification center.

## Connect inside the app

1. Open One TaskBar settings → Windows notification connection.
2. Select Connect. One TaskBar creates an identity-only MSIX containing its own manifest and icons, attached to the installed executable directory.
3. If Windows requires administrator approval for this local registration, approve its Windows dialog using the same Windows account. Cancelling leaves native notifications usable.
4. One TaskBar relaunches under its own registered identity. Allow notification access in the Windows consent dialog. The app cannot grant this permission itself.
5. Choose the custom notification center. Existing registration and access are reused on normal EXE/shortcut/login launches.

The development PC was connected successfully, including receipt and removal of One TaskBar's own temporary test toast. Notification content is processed locally. Original protocol/callback payloads are retained when available; other entries open their source app.

## Limits and removal

The optional identity is an **unsigned local-test package**, not a production-signed application package. Windows policies, editions and deployment settings may reject it; this is not a universal notification-installation guarantee. A signed production MSIX and signing certificate are not provided. Setup does not silently register it, add trusted certificates, enable Developer Mode or change machine security policies.

The package identity is exactly `OneTaskBar.Desktop` with the publisher declared in `Package.appxmanifest`; the app checks its publisher and external installation path. It does not use the host application's identity. Registration and notification consent are separate.

Disable integration inside One TaskBar to stop reading. Revoke access in Windows notification permission settings if desired. The ordinary uninstaller preserves user data and the optional local registration; to remove that registration for the current user after closing the app:

```powershell
Get-AppxPackage -Name OneTaskBar.Desktop | Remove-AppxPackage
```

Microsoft documentation: [unsigned packages are for testing](https://learn.microsoft.com/en-us/windows/msix/package/unsigned-package), [desktop application identity](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/grant-identity-to-nonpackaged-apps), [UserNotificationListener](https://learn.microsoft.com/en-us/uwp/api/windows.ui.notifications.management.usernotificationlistener).
