# One TaskBar

<img src="assets/OneTaskBar.png" width="96" alt="One TaskBar logo">

A per-user Windows dock, app launcher, and control center with live backdrop blur. Written in C# and WPF, with an independent recovery watchdog. [한국어](README-ko.md)

## Install

Download **OneTaskBar-Setup.exe** from [Releases](https://github.com/Bundelldebug/OneTaskBar/releases/latest), open it, and select Install. It installs for the current user, offers startup and desktop-shortcut choices, and launches the dock. Normal installation and use do **not** require administrator rights. No certificates, machine policies, or system executables are modified.

- Windows 11 x64 is the tested platform. Windows 10 2004+ x64 with .NET Framework 4.8 is a compatibility target; some shell integration depends on the Windows build. ARM64/native x86 builds are not provided.
- The initial release is not code-signed; Windows may show an unknown-publisher/SmartScreen prompt. Verify the release checksum. The installer does not disable Windows protection.
- App: `%LOCALAPPDATA%\OneTaskBar\App`; settings: `%LOCALAPPDATA%\OneTaskBar\Data`.
- Uninstall through Windows Settings → Apps → One TaskBar. Settings are retained. The Windows taskbar is restored when the app exits.
- For a portable run, extract the entire ZIP and open `OneTaskBar.exe --desktop`. To install that ZIP, run `Install.ps1` with Windows PowerShell. The app still stores preferences in LocalAppData.

## Features

- Separate app and status capsules; original Windows/app icons, Windows system font, seconds on the clock, running indicators, and drag-to-reorder pins.
- Rounded, antialiased surfaces using layered WPF alpha and a matching DirectComposition clip. Blur stays steady while app/search contents animate.
- Compact app drawer and launcher search, capped at 720 × 680 DIP: apps, Windows settings, local files/images, Korean initial consonants, and removable recent searches. Type directly in the app drawer; active IME composition finishes before the transition.
- Independent choices for Windows/custom quick settings, notifications, keyboard Start, and search. Windows shortcuts continue to work with the solo Start-key option.
- Touch panning and fractional touchpad scrolling; discrete wheel scrolling uses a short transition.
- System volume, brightness where supported, radio/device controls, media transport and centered artwork, battery/power mode, and theme toggle.
- Ten quick toggles read back actual state: mute, dark mode, Do Not Disturb, power efficiency, keep screen awake, Wi-Fi, Bluetooth, dock animations, startup and slider scrolling. Unsupported hardware controls are disabled or hidden. Power efficiency selects the Windows performance overlay; it is not a separate Energy Saver implementation. Keep-awake lasts only while the app is running and still allows manual sleep.
- A bottom-left Now bar follows active Windows media sessions. Its 44-DIP capsule fits the title up to the right status capsule's width (282 DIP maximum), keeps artwork and text left aligned, scrolls longer titles, and expands into playback/seek controls when the media app supports them. Artwork supplies the background color, adjusted for readable white text. Crowded displays place it above the dock without moving existing app slots.
- Missing radios, Samsung applications, unsupported brightness devices, and absent batteries are handled without placeholder applications. Desktop users can hide the battery slot or manually show 1–9999 W; this value is **not a measurement**.
- Charging status updates immediately on AC connection; the battery capsule expands once per cable connection. Startup does not simulate a plug event.
- App context menus include Task Manager. Toggle animations, blur, hover, startup, indicators, and recovery in settings.

## Recovery and shortcuts

| Shortcut | Action |
| --- | --- |
| Ctrl+Alt+H | One TaskBar settings |
| Ctrl+Alt+Q | Quick settings |
| Ctrl+Shift+Tab+1 | Force-exit **One TaskBar itself** and restore the Windows taskbar |

The emergency shortcut is enabled by default and can be disabled in settings. It is monitored by `OneTaskBar.Guard.exe`, independent of the WPF UI thread, so it also works if the UI stops responding. It validates the process path, PID, and start time; it does not terminate the foreground application. The same watchdog attempts restoration after a crash or external termination. It cannot intercept keys on the Windows secure desktop or after the watchdog itself is terminated.

## Notifications and optional integrations

The standard EXE installer defaults to the **Windows notification center**, which works without package registration or notification-reading permission. The custom center contains notifications without a calendar.

Open One TaskBar settings → Windows notification connection to register its optional local identity and request access inside the app. Windows may require a one-time administrator approval for registration and always controls notification consent. Existing registration is reused on normal launch and login. The identity is an **unsigned local-test package**, not a production-signed MSIX; it is optional and is not silently registered by Setup. See [notification setup and limits](docs/NOTIFICATIONS.md). Native Windows notifications remain available.

Samsung Settings / device-connection buttons use applications and logos already installed on the PC; those applications are not bundled. An optional, pre-existing Windhawk charging companion is paused while the dock owns the desktop and resumed on exit. Windhawk is not required for the dock or installed by Setup.

## Build and verify

No NuGet restore or SDK download is needed on a Windows desktop with .NET Framework 4.8, Windows PowerShell 5.1, and the OS WinMetadata files.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Build.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\BuildInstaller.ps1 -SkipBuild
```

Output: `dist/OneTaskBar.exe`, `release/OneTaskBar-Setup.exe`, portable ZIP, and SHA256SUMS. The installer embeds an explicit payload allowlist, excluding personal state, developer MSIX packages, logs, and reference media. The original vector icon can be regenerated with `tools/MakeAssets.ps1`.

```powershell
$env:ONE_TASKBAR_DATA = Join-Path $PWD '.qa-data'
Start-Process .\dist\OneTaskBar.exe '--self-test' -Wait
Start-Process .\dist\OneTaskBar.exe '--ui-probe --optimized' -Wait
Start-Process .\dist\OneTaskBar.exe '--typing-probe' -Wait
Start-Process .\dist\OneTaskBar.exe '--nowbar-probe' -Wait
Start-Process .\dist\OneTaskBar.exe '--quick-feature-probe' -Wait
Start-Process .\dist\OneTaskBar.exe '--recovery-probe' -Wait
```

Interactive probes require an unlocked desktop and focus their own test windows. Test results stay in the data directory; they are not uploaded. See [VALIDATION.md](VALIDATION.md) for measured results and limits.

## Privacy and licensing

No telemetry, account, or background web service is required. Search runs locally. Preferences, recent queries, shell pin caches, and diagnostic logs stay in the data directory. The custom notification center accesses notification data only after Windows permission. An app or document you choose to open may use its own network services.

Code and the original One TaskBar icon are under the [MIT license](LICENSE). [Third-party notices](THIRD_PARTY_NOTICES.txt) include the Liquid Dock reference. This repository contains no Samsung APK, extracted launcher artwork, wallpapers, fonts, or user screenshots/recordings. Installed third-party logos are read at runtime and remain their owners' property. This is an independent project and is not affiliated with Microsoft or Samsung. Open-source licensing does not guarantee that no third party will ever make a copyright or trademark complaint.

## 8.5.1

- Caps the collapsed Now bar at the right status capsule's width and keeps the thumbnail and title left aligned, including short titles.
- Removes the playback-state icon from the collapsed capsule, leaving artwork and title.
- Reduces the active quick-settings player from 188 to 140 DIP. Quick settings and expanded Now bar show artwork behind a dark translucent overlay for readable text.
- Artwork surfaces are frozen, shared and bounded to 640 pixels, with weak caching so past covers can be collected. No live artwork blur or per-refresh decoding is added.
- Same-title artwork events replace stale thumbnails. Two bounded retries after a track change handle late covers; identical image bytes reuse their decoded bitmap, and transient failures retain only the current track's cover.
- Wheel/trackpad slider adjustments in quick settings default off. Enable them with the slider-scrolling quick toggle or app setting; otherwise scrolling continues through the panel.
- A thin black dock divider separates pinned apps on the left from running-only apps on the right. Dragging across it saves pin/unpin changes. The dragged icon follows the pointer while neighboring icons slide into place; cancellation restores the layout.
- Routine updates increment the patch number only; major and minor versions change at the owner's request.

## 8.5.0

- Fixes the delayed one-core CPU saturation in the packaged recovery helper. A hash-verified copy runs outside the notification package, and startup checks its readiness and unpackaged identity. Keyboard and owner-exit events replace the 20-ms polling loop.
- Idle dock updates drop to 1 Hz on battery (0.5 Hz in fullscreen); display-off work sleeps for up to 30 seconds. Direct volume, brightness and foreground events remain responsive, and opening panels retains a faster cadence.
- Volume, media and notification changes use callbacks; device, brightness, app and pinned-item fallback scans run less often. Battery-powered title overflow scrolls once, then rests until hovered. Stationary title pauses no longer redraw continuously.
- The previous 0.5.5 short CPU samples did not detect the delayed helper regression. Resource measurements and test limits are recorded in [VALIDATION.md](VALIDATION.md); a full battery discharge comparison has not been performed.

## 0.5.5

- Live DWM window previews with enlarged image content, monitor-aware pagination and released thumbnail handles.
- One shared media player in quick settings and Now bar, including default output-device name and an icon button for switching outputs. Idle media becomes a slim capsule.
- In-app Windows notification connection, Do Not Disturb toggle, calendar-free notification panel and mutually exclusive quick/notification panels.
- Softer low-volume/brightness shapes, matched blur during transitions, signal-aware Wi-Fi arcs, click-away context menus and Now bar collapse, and white battery numbers.
- Background work uses two reusable EcoQoS workers; Windows selects efficient cores on supported hybrid CPUs. No hard CPU affinity is imposed. Icons decode on entering the viewport, media metadata follows change events, and fallback polling is reduced. UI/animation work and urgent keyboard feedback retain their normal execution paths.
- Battery operation lowers the general UI pulse from 10 to 5 Hz, independently of animation frames and input callbacks. This reduces periodic work; battery-runtime gains have not been measured.
- Fixes the recovery helper's undispatched STA messages that could saturate a core and leave helpers behind after restart. The helper now uses a sleeping native owner wait with 20-ms emergency-key checks, avoids window-message and file-watcher traffic, and rejects duplicates.
- Setup resolves physical executable paths for virtualized hosts and skips identical files during updates.

## 0.5.2

Adds the artwork-colored Now bar with real media-session controls, adaptive title width and marquee overflow. Expansion and collapse animate the capsule inside a fixed native host, keeping text layout cached and avoiding repeated native window resizing and backdrop placement. Transparent space outside the capsule passes clicks through; the bar stays out of Alt+Tab.

Quick settings, connection details, notifications, app drawer and search share the gray live-glass palette. App/search panels use compact bounds and continuous content transitions. Quick settings contains actual stateful toggles, a plain settings glyph and a power menu with restart, shutdown, sleep and lock.

## Earlier changes

### 0.4.0

The dock button can independently open the app drawer or Windows Start. Connected PHY data drives Wi-Fi generation badges (4/5/6/7); unknown/offline connections have no generation badge. Clicking the foreground application minimizes it, with a visible icon bounce. Panels now use a light frosted material and original monochrome settings symbols. Brightness uses Windows power notifications, while the dedicated Explorer OSD is replaced only when the custom HUD is available.

Known issue: native Start button invocation is not yet verified on the development PC; automated invocation opens SearchHost although physical Ctrl+Esc opens Start. The app drawer remains the default.

0.4.2 adds retained dock cells with animated capsule growth/shrink, icon entry/exit and stable launch ordering (newest on the right), preserving manual moves. The gray control center has a more transparent neutral tint and a shutdown/sleep/lock menu. Native OSD opacity is prepared before presentation and restored on exit or watchdog recovery.

0.4.3 keeps every existing dock slot fixed when its app starts or stops; only new entries append on the right. Quick settings uses a stronger translucent black tint over the live backdrop, with one shared white for enabled radio buttons, theme/audio buttons and slider fills.
