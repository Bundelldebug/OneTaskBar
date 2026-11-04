# One TaskBar

A Windows taskbar replacement with a dock, Start menu, search, quick settings and media controls. [한국어](README-ko.md)

[Stable download](https://github.com/Bundelldebug/OneTaskBar/releases/latest) · [All releases and betas](https://github.com/Bundelldebug/OneTaskBar/releases)

Use `OneTaskBar-Setup.exe` to install for the current user, or extract the entire portable ZIP. Builds are unsigned; Windows SmartScreen or an organization policy may block them.

## Features

- Move the dock to any screen edge, reorder pinned apps, and choose blur, a solid color, an image or no background.
- Search installed apps and local files. Create app folders, or group the app screen by Windows Start menu folders.
- Fit original app artwork into squircles, with a per-app editor and PNG/ICO export. Existing squircle icons are preserved. Artwork that is not a filled shape sits on white, or black if the artwork is white.
- Move and resize quick-settings items. Choose UI 8.5, 9.0 or 9.5 with live glass effects.
- Control playback and output devices from quick settings or the media capsule. The optional brightness and volume labels are off by default.
- Open fullscreen media controls, choose a vertical capsule for side docks, or enable Dynamic Island for media, new notifications and charging, volume and brightness feedback. The dock media capsule, window transitions and Duo status icon each have their own switch.
- Switch windows and desktops with previews. Windows' original Start, search, notifications and Alt+Tab remain selectable in settings.
- Place clock, calendar, battery, player, reminder, weather and other widgets on the desktop in sizes from 2×1 to 4×2. Click or long-press a widget to edit it; dragging snaps to a grid and avoids desktop icons. Opacity and blur are adjustable.
- Collapsing the fullscreen media screen shows widgets below the time and your chosen apps in the lower corners.
- Group, expand and swipe away notifications. Custom notification sounds use a local file you choose.

The dock's Start button opens the app screen or Start menu according to settings. Click it again or click outside to close the menu. App context menus provide pinning, folders and supported desktop-app actions.

The squircle option is off by default under Appearance. The icon editor accepts local images, adjusts artwork size and background color, and can keep an app's original icon. It changes One TaskBar's presentation only. For image conversion, run `OneTaskBar.exe --make-squircle-icon "input.png" "output.ico"`.

## Shortcuts and recovery

| Shortcut | Action |
| --- | --- |
| Ctrl+Alt+H | App settings |
| Ctrl+Alt+Q | Quick settings |
| Ctrl+Shift+Tab+1 | Exit One TaskBar and restore the Windows taskbar |

The separate recovery process restores the Windows taskbar if the main app stops responding. The emergency shortcut can be disabled in settings. It cannot run on the Windows secure desktop.

## Optional integrations

Custom notifications require Windows notification access and optional local identity registration. Setup does not silently register that identity. See [notification setup](docs/NOTIFICATIONS.md).

Samsung features use apps and services already installed on the PC. They are not bundled. Some Samsung Settings versions reject external performance-mode changes; use the provided Samsung Settings link in that case.

High-resolution YouTube covers are off by default. With permission, the app matches the current playing title against local Samsung Browser history and fetches the matching cover from `i.ytimg.com`. It does not send titles, cookies or history rows. If a match is unavailable, it uses the artwork supplied by the media app.

Preferences, pins, searches and logs stay in the local data directory. There is no telemetry service.

## Build

Requires Windows x64, .NET Framework 4.8 and Windows PowerShell. No NuGet restore is needed.

```powershell
./Build.ps1
./tools/BuildInstaller.ps1 -SkipBuild
```

For development, see [debugging](docs/DEBUGGING.md), [isolated UI tests](tools/Test-UI.ps1) and [validation results and limits](VALIDATION.md). CPU samples do not establish battery life; full-discharge runtime and RAM below 150 MiB are not verified targets.

## License

[MIT](LICENSE). See [third-party notices](THIRD_PARTY_NOTICES.txt) and [glass rendering](docs/GLASS_RENDERING.md) for references. Third-party app icons are read from the installed apps. This project is independent of Microsoft, Samsung and Apple.
