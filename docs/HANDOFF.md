# Shared handoff

Checked: 2026-11-09, Asia/Seoul. Maintained by whichever agent is working in this checkout.
This is a snapshot, not an automatic task queue. Check the current branch, files and latest user instruction before acting.

## Current task

Updated 2026-11-09 19:10 KST by Claude Code.

- Owner's active request (to Claude): "use the Liquid Glass skill and continue the regular-app Fluid animation". This continues the earlier request to make window motion into and out of the Now Bar and Dynamic Island natural and visible, add Fluid motion for apps opened or minimized from the dock, and check every minimize and activate path on the real device.
- The Liquid Glass skill exists only in Codex's plugin cache, not as a Claude skill. Its principles were applied by hand: clear body, sheen, glow pooled at the bottom, lit rim.
- Commit `6348abc` (on top of `593ed9a`, both unpushed):
  - Regular-app drop is now visible Liquid Glass, with a stronger fill, a lit rim and a thin dark edge.
  - Restore uses a 0.32 s drop that fades before the window settles; minimize keeps the 0.44 s drop.
  - Cloak wait is 1 frame.
  - Live state reports `appMotionHeard`, `appMotionPlayed` and `appGhostFrames`.
  - Media restore holds the plane for 160 ms.
- Verified on the real device by recording with ffmpeg ddagrab and reading frames (`.qa-data/video/app4`–`app9`):
  - Claude-window minimize through its dock icon: the glass sheet shrinks into the icon.
  - Restore through the dock icon: the glass rises with the window and is gone after about 0.17 s.
  - The installed build matches `dist` (SHA256 `A9472C4D…AA2FB`), applied via `.build/Apply-ToPC.ps1`.
- Tests on that binary:
  - `Test-UI -Area Media` passed.
  - `-Area Dock` failed once in `desktop-widgets-probe` ("adding widgets does not steal foreground activation"); it passed on two isolated reruns.
- Not yet verified on the real device:
  - Media restore white frame with the 160 ms hold.
  - Launch drop from a pinned app.
  - Win+Down and title-bar minimize on the media app.
  - Dock click while the Now Bar is expanded.
  - Dynamic Island flights.
- Follow-up, owner said the now bar flight was too slow. Commit `6a2e016` (installed, SHA256 `CCDAB6C8…6BA`):
  - Fluid minimize/restore now take 420/440 ms, down from 620/640.
  - After a restore, the plane stays until `MediaRestorePaint` sees the real window drawn (PrintWindow white-share against the pre-minimize picture), at most 0.5 s.
  - Real recording `media10`: the flight ends in about 0.4 s.
  - The paint wait itself is not verified on a clean run: during `media11`, the owner's Samsung Internet had a "새 분할 화면 탭" (split-screen tab) open (origin unclear), and browser real-tests were stopped.
  - `Test-UI -Area Media` passed.
- Next step: real-test those paths with `scratchpad mediatest2.ps1`-style recordings (Claude session-local; re-create under `.qa-data` if needed). Do not publish without the owner's approval.

### Update 2026-11-09 evening (Claude Code)

- `3dd8c6c`: Now Bar flight 360/380 ms; launch ghost from the dock follows the real window (WinEvents show/uncloak/foreground, snapshot of pre-existing windows, 2.6 s hold limit). Verified by 120 Hz recordings and AppLog timestamps.
- Fullscreen usability audit: expanded/collapsed screens render correctly; Esc closes (first Esc leaves tile editing); tile edit mode works. Fixes: focus fallback via `Native.Activate` in `FullscreenMedia.Present`, Space = play/pause and Left/Right = previous/next (off while editing tiles).
- `Test-UI -Area Media` passed; real device: opening by `--open-panel media-screen` now takes focus and Esc closes it.
- Not tested: corner-app click, media-pill click, X button, fullscreen interplay with browser F11, caption marquee edge fade (hard cut, left as is). Nothing published.

### Eight-item request 2026-11-09 night (Claude Code)

- Issue 18 (dock hover preview closed on the arrow when a later page had fewer tiles): popup keeps the full-page width when there are several pages (`WindowPreview.Rebuild`).
- Buds connection popup now appears bottom-centre above a bottom dock (`BudsConnectionPopup.Anchor`, `BudsConnections.DockTopFor`); island buds notice shows a wide product picture at its own aspect (`IslandNotificationView.Present`).
- Storage-share and camera-share notices from Samsung packages are routed to the Galaxy Connect bottom popup by title/body text (`NotificationBanners.IsGalaxyConnect`). The real AppIds were not observed (the notification database is locked), so this matches text, not ids.
- Fluid "배경에 맞춤": `FluidAutoGlow` now reads the wallpaper through `DesktopWallpaper.CurrentFile` (IDesktopWallpaper: slideshow, Spotlight, per-monitor) plus Wallpaper Engine and a plain-colour desktop; the old code used only the classic setting. Not reproduced on another PC.
- AI Select (`AISelect.exe`) and `smartselect` added to the capture tools that must not count as fullscreen (`FullscreenVisibility.captureTools`). Not triggered on the device.
- Dock app minimize ghost: `CloseTime` .44 → .2 s. 120 Hz recording `min11`/`min12`: the real window shrinks in about 0.1 s and the ghost used to outlast it by 0.5 s; it still starts about 0.08 s after the window does (event latency).
- Liquid Glass: shared card film and edge rim reworked (clear body, top specular sheen, glow lens at the bottom, soft opposite-corner catch light) in `Glass95.cs`, following the plugin `liquid-glass` skill's principles (it is SwiftUI-specific, so only principles apply). Dock, quick panel and other Fluid brushes (`Fluid.Tint`, `Fluid.Rim`) were left alone.
- `Test-UI` Glass, Notifications, Media, Dock, Quick: no failures. Installed to this PC. Nothing published.

## Checkout and publication

Main checkout: `One TaskBar/` under the shared `Documents/Codex` workspace.

| Item | Verified state |
| --- | --- |
| Branch | `codex/release-9-5-0` |
| Source HEAD | `593ed9a5070e62655fa916417cca860370dddf26` |
| Tracking branch and live GitHub branch | `658406c6ee3cbe1bf3b80c9a301630b44d7941d8` |
| Local difference | One unpushed source commit; working tree was clean before this documentation sync |
| Latest stable | [9.5.13](https://github.com/Bundelldebug/OneTaskBar/releases/tag/v9.5.13), published 2026-11-09 14:43 KST; commit `173dcf41f2cab70ca7bbfeab7956f875149091b3` |
| Latest beta | [10.0.0 A1A2 Beta](https://github.com/Bundelldebug/OneTaskBar/releases/tag/v10.0.0-A1A2-beta), published 2026-11-09 16:29 KST; commit `658406c6ee3cbe1bf3b80c9a301630b44d7941d8` |

Both releases were public, not drafts, when checked through the GitHub API. The local HEAD is newer than the beta release.
Do not repeat older 9.5.2-era version or completion claims from Codex chat history.

The sibling `OneTaskBar-release-9.0.2/` worktree is on `codex/release-9-0-10`, HEAD `7852723`, with many modified and untracked files. Leave it untouched unless explicitly assigned work there.

## Unfinished source work

HEAD is now `6348abc` (see Current task). The earlier `593ed9a` is explicitly marked **in progress**. It adds normal-app minimize/restore/launch motion and adjusts media flight, drawing readiness and the Now Bar cover action.

Changed source files in that commit:

- `src/AppWindowMotion.cs` (new)
- `src/MediaFlightTiming.cs`, `src/MediaWindowMotion.cs`, `src/MediaWindowPlane.cs`
- `src/NowBar.cs`, `src/Shell.cs`, `src/Windows.cs`

Do not publish it or describe its animation behavior as verified from the commit message alone. No cause analysis or further implementation was done during this sync.

## Current implementation map

All filenames below are under `src/`; use [DEBUGGING.md](DEBUGGING.md) for symptom-specific entry points.

| Area | Current entry points and constraints |
| --- | --- |
| Release gates | `AssemblyInfo.cs`: `10.0.0 A1A2`. `Fluid.cs`: UI version `100`, available only on assembly major 10 or later. Stable 9.5.x retains 8.5/9.0/9.5 UI but hides Fluid. `ReleaseFeatures` is in `AppTheme.cs`. |
| Fluid panels | `FluidBud.cs`, `FluidLight.cs`, `FluidFlare.cs`, `FluidAutoGlow.cs`, `DynamicIslandFluid.cs`. Interrupted panel motion can reverse from its current position. |
| Shared motion | `SpringMotion.cs` owns spring state and handoff; `SpringEase.cs` owns timed easing. Media flight uses `MediaFlightTiming.cs`, `MediaFlightPose.cs`, `MediaWindowPlane.cs` and `NowBarFlight.cs`. |
| Desktop widgets | `DesktopWidgets.cs`, `DesktopWidgetModel.cs`, `DesktopWidgetLayouts.cs`, `DesktopWidgetNative.cs`, `DesktopWidgetView.cs`; gallery and grid-sized tiles already exist. Do not restart the earlier widget request from scratch. |
| Shared information tiles | `InfoTiles.cs`, `InfoTileClocks.cs`, `InfoTileActions.cs`, `InfoTileDevices.cs`, `TileScheme.cs`; reused by desktop and collapsed fullscreen media. |
| Fullscreen media | `FullscreenWidgets.cs`, `FullscreenTileEditing.cs`, `SettingsFullscreenTiles.cs`, `MediaPlayerFullscreen.cs`; shared player, compact pill and clock sizing are already implemented. |
| Widget material/input | `WidgetMaterial.cs` blurs wallpaper for desktop child windows; `DesktopIcons.cs` reads icon bounds through UI Automation. `--desktop-widgets-live` touches the real desktop and is not a read-only fixture. |
| Cadence decisions | Dock resize/reveal was returned to its own 16 ms timer after frame synchronization broke length animation. Media wave runs at 60 Hz on battery. Do not undo these changes as a speculative optimization. |

In-app update checks follow stable releases; beta downloads are manual. Build labels and feature gates must be checked separately.

## Evidence available locally

Existing `dist/OneTaskBar.exe` metadata, read during this sync:

- Product version: `10.0.0 A1A2`; modified 2026-11-09 16:59:48 KST.
- SHA256: `B6E8EABEEF5B6E3D92F6139A23B7377D7F5B04429C64B3F7D805760ACFDC6EE9`.
- The binary's source commit was not independently established. Its version is not proof that it matches HEAD, an installed copy or a release asset.

Existing reports for that SHA256 were **read, not rerun**:

| Local report | Result |
| --- | --- |
| `.qa-data/ui-0d4dd00b76a24be28b8d0f70c0668c4d/summary.json` | Passed: self-test and dock, widgets, switching, activation, preview, overview and tray fixtures |
| `.qa-data/ui-c8d759654f0a4d7f9994b7139b8b24eb/summary.json` | Passed: self-test and media, Now Bar, island, Fluid island, fullscreen media and fullscreen tile fixtures |
| `.qa-data/ui-8a8e4c6e6cf543ecbefc93d32ac2a4c4/summary.json` | Earlier failure on the same binary: fullscreen-tiles-probe, "expanding hides tiles and corner apps again". A later run passed; the cause was not investigated in this sync. |

These ignored reports are local evidence and may not exist in another clone. Do not commit screenshots, fixture data or user content to make them portable.
The branch-filtered GitHub CI list returned an older successful run for `09c5dc0` (2026-11-02), not proof of this HEAD.
Installed application version, live behavior, complete regression coverage and current power/memory performance were not verified in this sync.

## Documentation changes from this takeover

- `AGENTS.md`: shared takeover/checkpoint protocol, current source map and corrected Fluid release gate.
- `CLAUDE.md`: imports both `AGENTS.md` and this file.
- `docs/HANDOFF.md`: this shared state record.
- The conversation workspace `2026-10-18/wi/AGENTS.md`, outside this Git checkout, routes Codex to these shared files.

These documentation edits are local and uncommitted. No source files or app settings were changed. No commit or push was made.

## Next handoff

Replace this snapshot when work changes; do not append an ever-growing chat transcript. Retain unresolved failures and still-relevant constraints.
Record the owner's active request (or stopped state), agent/branch/base commit, changed files, completed and unfinished work, and the next concrete step.
For validation, identify commands actually run, result/report paths, source commit and binary hash when known. Record installation and publication separately, with their own evidence.
Keep durable rules in [AGENTS.md](../AGENTS.md) and navigation in [DEBUGGING.md](DEBUGGING.md). Both agents should update the same record.

### Eleven-item request 2026-11-09 night, second batch (Claude Code)

- Buds connect: the volume bar is suppressed for 2.5 s after the default output changes (`Shell.cs`); the Samsung battery/case notice replaces the plain connection notice and wins within 30 s (`BudsConnection.cs`). Not tested with real buds.
- UI version moved to the top of settings. Fresh installs start on Fluid (or 95/90 where unavailable); skipped when `ONE_TASKBAR_DATA` is set so fixtures keep the old default.
- AI Select / capture overlays: dock windows are sunk just below a covering capture-tool window and raised again afterwards (`DockHost.SinkBelow/RiseAgain`, `FullscreenVisibility.CaptureLayer`). Not triggered on the device; island/top bar are not sunk.
- Island search also lists up to two files (debounced); arrows/Enter work; window takes focus (`IslandSearchView.cs`, `DynamicIsland.ToggleSearch`).
- Desktop styles are presets: custom desktop first, Windows, Mac, GoogleBook (`SettingsDesktopStyle.cs`).
- Quick settings sound page has a button to a separate per-app volume page (icon = mute, slider = level), `AppVolumes.cs`; verified on the device.
- Desktop switcher animation: spring placement and longer chrome travel in Fluid (`DesktopOverview.cs`); not watched on the device.
- Automatic app folders (off by default, setting "앱 자동 폴더 정리"): `StartAutoFolders.cs`, 3x3 folder icon in `StartWindow.LargeFolderIcon`; kinds with fewer than 3 apps stay loose; keyword matching by name only. Unit-checked, not seen on the device.
- Item 4 (Fluid polish): only the desktop switcher and earlier Glass95 work; no broad restyle was done.
- `Test-UI` Quick, Settings, Launcher, Notifications, Media, Dock, Glass, Startup, Resources passed. Installed with `Apply-ToPC.ps1`. Idle measure of the installed build: CPU 0.045 %, working set 378 MiB (private 234 MiB), report in `.qa-data/runtime-7d2b66c5…json`. Nothing published.
- One stray click earlier opened a release-notes file pane in the Claude app (harmless).
