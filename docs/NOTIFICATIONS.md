# Windows notification connection

The normal EXE installation runs per user without elevation and defaults to the Windows notification center.

## Connect inside the app

1. Open One TaskBar settings → Windows notification connection.
2. Select Connect. One TaskBar creates an identity-only MSIX containing its own manifest and icons, attached to the installed executable directory.
3. If Windows requires administrator approval for this local registration, approve its Windows dialog using the same Windows account. Cancelling leaves native notifications usable.
4. One TaskBar relaunches under its own registered identity. Allow notification access in the Windows consent dialog. The app cannot grant this permission itself.
5. Choose the custom notification center. Existing registration and access are reused on normal EXE/shortcut/login launches.

The development PC was connected successfully, including receipt and removal of One TaskBar's own temporary test toast. Notification content is processed locally. Native toast targets, original protocol arguments and registered callbacks are used when available. Other entries are matched to the original native history card; they do not silently fall back to a generic app launch.

## Limits and removal

### Icons and pictures

After listener permission is granted, the app resolves the sender's Windows logo with an installed-app fallback. App-logo overrides and hero/inline images are read from the original matching toast. A bounded, read-only Windows notification-store fallback matches the authorized item's sender, notification ID and text when the public listener projection omits image references. It never writes that store or enumerates unrelated notifications. This optional Windows implementation detail can change; missing payloads or assets leave text-only cards.

Images are decoded once per changed notification from local package/data files or existing Windows URL cache entries, capped in file/decoded size. The app does not fetch remote notification images. Missing pictures and icons omit their space rather than displaying generic placeholder artwork. Original activation matching and notification permission still apply.

### One Taskbar popup banners

Notification-center capsules can be dragged horizontally in either direction to dismiss them with a slide/fade. A short drag returns the capsule to its place; vertical gestures keep scrolling the list. A collapsed stack dismisses its group, while an expanded card dismisses only that item. The capsule X is removed; right-click → Delete and the Delete key remain available. Original notification activation and detail expansion still work on a normal click.

Settings → Panel selection → Custom notification sound accepts a local WAV, MP3, WMA, M4A or AAC file. It is disabled by default and independent of the charging sound. Preview and Clear controls are available. Only a newly accepted popup plays it; initial history, duplicates and burst updates do not replay it. Windows may also play its configured sound; this option does not modify Windows notification policy or sound settings.

After connection, the **One Taskbar notification popups** setting controls banners separately from the native/custom notification-center choice. The desktop dock must be active. Up to three cards appear without taking keyboard focus; hovering pauses expiry, and dismissing a card leaves the original notification in Windows history. Clicking uses the same original-notification activation path as the custom center.

Clicking a live card first invokes its exact native toast view. After that view expires, current protocol/callback data is re-read on demand; adaptive nested toast text is supported. For UWP toast activations or unavailable payloads, One TaskBar asks Windows to invoke the corresponding history card, matching sender group, title and body. Collapsed groups can be expanded and virtualized entries realized. Duplicate matches are not guessed. The native center is temporarily masked during a matched activation and its original attributes are restored, with an owner-checked recovery lease for crashes. If the entry is unavailable or ambiguous, the native center is left visible so the user can choose the original notification. This avoids pretending that a generic app launch executed the notification action.

The listener's first snapshot establishes a baseline. A fresh entry must also match the title and body of an actual Windows toast view before it is shown. This preserves history-only updates and Windows decisions about Do Not Disturb and per-app banners; the history API alone does not reliably preserve `SuppressPopup`. Lock/presentation/full-screen state is also checked. No notification policy or global banner registry setting is changed.

The tested Windows 11 shell exposes the dedicated `ShellExperienceHost` toast window with the Korean “새 알림” title and a `NormalToastView` accessibility view. English “New notification” is also recognized. Only that dedicated host is temporarily made transparent and non-interactive; other shell windows are excluded. The original window attributes are restored when the feature is disabled, notification access fails, desktop mode exits, or the app closes. A persisted owner-checked lease lets the recovery helper restore them after a crash. An unreadable native toast view releases its mask. Other Windows builds, languages or notification implementations can retain their native banners; this does not replace security/lock-screen notices or every application's own custom popup window.

No idle banner polling is added. Native events trigger bounded accessibility reads, notification differences are deduplicated in memory, and the card expiry timer runs only while cards exist. Failed payload lookups are retained between unchanged snapshots and retried when the user clicks; they are not repeated for every history item on every refresh. Recent-file enumeration for the Start menu is unrelated to notification access and runs only when that menu opens, with a five-second cache; only Windows-maintained Recent document records are read. No user-folder or install-date recommendations are synthesized.

The optional identity is an **unsigned local-test package**, not a production-signed application package. Windows policies, editions and deployment settings may reject it; this is not a universal notification-installation guarantee. A signed production MSIX and signing certificate are not provided. Setup does not silently register it, add trusted certificates, enable Developer Mode or change machine security policies.

The package identity is exactly `OneTaskBar.Desktop` with the publisher declared in `Package.appxmanifest`; the app checks its publisher and external installation path. It does not use the host application's identity. Registration and notification consent are separate.

Disable integration inside One TaskBar to stop reading. Revoke access in Windows notification permission settings if desired. The ordinary uninstaller preserves user data and the optional local registration; to remove that registration for the current user after closing the app:

```powershell
Get-AppxPackage -Name OneTaskBar.Desktop | Remove-AppxPackage
```

Microsoft documentation: [unsigned packages are for testing](https://learn.microsoft.com/en-us/windows/msix/package/unsigned-package), [desktop application identity](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/grant-identity-to-nonpackaged-apps), [UserNotificationListener](https://learn.microsoft.com/en-us/uwp/api/windows.ui.notifications.management.usernotificationlistener).
