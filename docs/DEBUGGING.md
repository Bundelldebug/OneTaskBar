# Focused debugging

Use this routing table before reading source. The probe runner prints one result per fixture, then the location of a compact `summary.json`. Open the failing fixture's report for the assertion and its own PNGs. Reports may include several checks; a successful fixture does not prove behavior on hardware that was absent.

```powershell
./Build.ps1 -SkipTests
./tools/Test-UI.ps1 -List
./tools/Test-UI.ps1 -Area Quick
# One specific isolated fixture:
./tools/Test-UI.ps1 -Probe 'popup-probe'
# Release regression suite, also used by CI:
./tools/Test-UI.ps1 -Area All
```

| Symptom | Read first | Fixture area / useful report |
| --- | --- | --- |
| New widget appears below a gap | `QuickWidgetLayout.PlaceFirst` in `QuickWidgetModel.cs`, `QuickGridSurface.ExtractAction` in `QuickGridEditing.cs` | Quick / `quick9-probe` |
| Toggle group order, extraction, adding or resizing | `QuickGridEditing.cs`, `QuickToggleOrder.cs`, `QuickWidgetBoard.cs` | Quick |
| Wrong connected state or unchanged control flicker | `QuickGridState.cs`, `QuickToggleButton.cs`, `BudsConnection.cs` | Quick; Dock / `desktop-behavior-probe` |
| Wrong icon/name in picker | `QuickWidgetPicker.cs`, `QuickSettingCatalog.cs`, `QuickSettingIcons.cs` | Quick |
| Rounded output corner, selected output, cover tint | `AudioOutputView.cs`, `DetailWindow.cs` sound case, `ArtworkPalette.OutputSurface` | Notifications / `popup-probe` |
| Keyboard volume/brightness bar disappears or mismatches Quick Settings | `HudWindow` in `Windows.cs`, `PillSlider.UnfilledBrush` in `PillSliderGlass.cs`, `GlassWindow` in `Controls.cs`, `NativeOsdGate.cs` | Glass; Settings / `panel-theme-probe` |
| Media wave, title, responsive layout or low-res art | `MediaPlayerModern.cs`, `MediaWave.cs`, `MarqueeTitle.cs`, `YouTubeArtwork.cs` | Media |
| Fullscreen media, artwork size switch or separate PC lock | `FullscreenMedia.cs`, `FullscreenMediaController.cs`; artwork resizing belongs only to this fullscreen surface | `fullscreen-media-probe` also verifies ordinary player artwork opens the app |
| Media minimize/restore overlap, tilt or interrupted handoff | `MediaWindowMotion.cs` owns capture/observation; `MediaWindowPlane.cs` draws one textured plane; `MediaFlightTiming.cs` owns the curve; `MediaWindowLease.cs` journals cloaking for guard recovery | `fullscreen-media-probe`; `media-window-lease.json` has handles/state only, no pixels |
| Beta foreground ink on a bright/dark backdrop | `BackdropInk.cs`: visible floating panels only, one tiny in-memory strip per sample, hysteresis and two reused brushes; no sampling in hidden windows or fixed media/selected colors | `adaptive-ink-probe`; compare baseline if native presentation cadence is slow |
| Install fails when Windows known folders are empty | `Resolve-UserFolder` in `Install.ps1`; Documents/Desktop are optional, LocalAppData has profile/environment fallbacks | `tools/Test-InstallFolders.ps1` |
| Different floating popup and quick-panel tint | `PanelMaterial.PopupSurface`, `WindowPreview.ApplyMaterial`, `WindowSwitcherView.cs` | Dock; Notifications; Settings / `panel-theme-probe` |
| Dock missing on desktop or wrong fullscreen monitor | `DockHost.UpdateVisibility` in `Windows.cs`, `Native.EnsureDockVisible`, `FullscreenVisibility.cs` | Dock |
| Light dock looks solid or background-free controls lack contrast | explicit dock branch in `GlassWindow.ApplyMaterial`, `DockAppearance.Glass` / `Readability`, `Glass95.DockInk` | Dock / `desktop-behavior-probe` includes `DockMaterialChecks` |
| Virtual display leaves the dock hidden after the last app closes | `FullscreenVisibility.PassiveSurface`, `DockHost.UpdateVisibility` desktop-idle recovery | Dock / `desktop-behavior-probe` |
| Beta top capsule, fullscreen/title-bar overlap or status expiry | `DynamicIslandController.cs`, `DynamicIslandState.cs`, `DynamicIsland.cs`, `IslandSafeArea.cs` | Media / `dynamic-island-probe`; `live-state.json` records visibility/motion only |
| Desktop overview stutters or leaves a black preview window | `DesktopOverviewController.cs`, `DesktopOverview.cs`, `SwitchThumbnail` in `WindowSwitcherView.cs` | Dock / `desktop-overview-probe`; `desktop-overview-timing.json` contains counts/timing only |
| Tray icons, native-host fallback or recovery | `TrayOverflowController.cs`, `TrayIcons.cs`, `TrayIconVisual.cs`, `TrayOverflowWindow.cs` | Dock / `tray-overflow-probe`; `tray-proxy-state.json` contains counts only |
| Glass bevel clipping or repeated layout work | `GlassRefraction.cs`, `LiveBackdrop.RefreshRefraction` | Glass / `glass95-probe` |
| Alt+Tab does not raise an existing active app, especially Samsung Notes | `SwitchSession` in `WindowSwitchModel.cs` normalizes foreground children; `WindowActivationDiagnostics.ResponseTarget` probes the host frame, `WindowActivationFocus.cs` targets actual content; a denied optional destination queue must not abort activation | Dock / `window-switcher-probe`, `window-activation-probe`; private `window-activation-last.json` records classes/handles and queue errors, never titles/content |
| Search order, recent chips or keyboard focus | `SearchWindow.cs`, `SearchNavigation.cs`, `LauncherInputChecks.cs` | Launcher |
| Settings blank, search empty strips, version label | `SettingsWindow.cs`, `SettingsPages.cs`, `UIVersionPicker.cs` | Settings |
| Display selection, disconnected monitor or wrong dock count | `DockDisplays.cs`, `ShellController.Relayout` | `self-test` checks saved device selection, primary-following and disconnect fallback; use real secondary hardware to verify its placement |
| Status touch keyboard / Cast button does not open | `DockStatusItems.cs`, `TouchKeyboard.cs`, `SystemPanels.Cast` | `quick9-probe` checks distinct symbols and placement; real system-panel invocation is a separate manual check |
| Dock zoom, compact clock, scaled click/popup placement | `DockSizing.cs`, `DockClock.cs`, `DockGeometry.cs`, `NowBarLayout.cs` | Settings / `beta-feedback-probe`, `DockSizingChecks`; Dock / `tray-overflow-probe` preserves real DPI while scaling anchor gaps |
| Linked panel color or settings transition residue | `PanelTint.cs`, `SettingsPageMotion.cs` | `panel-theme-probe`, `settings-visibility-probe` |
| Notification logo, image, action or stale cache | `NotificationService.cs`, `NotificationIcons.cs`, `NativeToastAssets.cs` | Notifications; `self-test` includes asset/cache checks |
| Notification swipe opens instead of deleting, or fails to settle | `NotificationSwipe.cs`, `NotificationStack.cs` | Notifications / `notification-swipe-probe`, `notification-stack-probe`; add `--inspect --review-ui` for a disposable mouse fixture |
| Custom notification sound or repeated sounds | `NotificationAudio.cs`, `SettingsSound.cs`, `NotificationBanners.Show` | `self-test` checks disabled defaults and independent persistence; live sounds require a fresh eligible banner, not a history refresh |
| Login delay, idle resource growth | `Startup.cs`, `Shell.cs`, `RuntimeCadence.cs`, `MemoryChecks.cs` | Startup; Resources |

## Runtime measurements

`tools/Measure-Runtime.ps1` reads only the specified running app, its recovery helper, counters and `live-state.json`. It does not open panels, alter preferences, force GC, trim memory or change power/security policy.

```powershell
./tools/Measure-Runtime.ps1 -Executable 'C:\path\OneTaskBar\App\OneTaskBar.exe' -Seconds 30
```

Compare matching media, monitor and visible-panel states. `totalWorkingSetMiB` includes shared resident pages; `privateWorkingSetMiB` is private resident memory; `privateBytesMiB` is committed private memory, not RAM in use. CPU is normalized across logical processors. Do not claim the 150 MB total-RAM goal from private memory alone.

Wave and marquee motion use compositor frame callbacks only while moving. Marquee endpoint pauses sleep on one dormant timer; paused/hidden waves detach after their amplitude settles. Desktop overview reuses its hidden shell but releases every native thumbnail on hide. Resize work is coalesced; switcher selection alone must not force layout or reposition every preview. The existing motion/lifecycle probes cover these boundaries.

Settings navigation captures only its outgoing viewport, then starts the incoming translation after queued scroll/layout changes. Cancel the old `SmoothScrollViewer` offset clock on content changes; Back restores the home offset without rebuilding its cards. `SettingsMotionChecks` covers transparent gaps between cards, interrupted scrolling, queued cancellation, disabled motion and releasing the temporary frame. Check the viewport transform as well as the window root.

The activation fixture must put its own source window in the foreground first; it normalizes a hosted foreground to the outer frame for setup. Samsung Notes' `Windows.UI.Core.CoreWindow` can time out WM_NULL while its `ApplicationFrameWindow` is responsive. Probe that frame and activate the actual CoreWindow when needed. Source/target queue links can be denied independently; keep successful links, attempt normal activation, and always detach. No simulated Alt key, foreground-timeout registry changes, elevation or general-use SwitchToThisWindow fallback is used.

## Boundaries and compatibility

- Isolated fixture data must not use the installed application's data directory. `Test-UI.ps1` creates a fresh directory for each run and restores `ONE_TASKBAR_DATA` afterward.
- Live diagnostic signals in `Program.cs` are different from isolated fixtures. The shared runner intentionally excludes signals and hardware-changing probes.
- Floating notifications, Alt+Tab and window previews share the Quick panel material. The output selector has its own dark gray palette. Keyboard HUDs share Quick Settings' unfilled slider brush and underlying panel tint. Notification chrome uses the panel text palette; capsule content retains its local contrast palette.
- UI 9.5 changes glass material, not the established filled volume/brightness slider shape. Preserve 1×1 circles, pill buttons and the 8.5/9.0 switch.
- Notification images remain native resolution up to 256 px; evict inactive app caches. Current-video artwork consent must remain scoped to the authorized track.
- A normal restart/install and published asset verification are separate from fixture success. Preserve settings and use exact tested CI bytes for releases; do not overwrite existing release tags or assets.

UI review: launch the installed app with --desktop --review-ui, then use --open-panel notifications|quick|start|search. Review mode exposes owned surfaces to the capture tool; restart without --review-ui afterward. Review expires after two minutes and restores shell exclusion even if a review is interrupted. Normal sessions and fixtures retain hidden tool windows. DockShellChecks covers task-list style recovery, externally moved docks, browser edge interception and four-edge tray placement.

Desktop overview and tray fixtures create their own source windows. They never send desktop-switch shortcuts, close user windows or invoke the real tray. Test the real desktop shortcut and tray callbacks separately. Tray review mode writes provider labels/IDs to a local `tray-provider-review.json`; keep it private. Normal mode records counts only. No process injection or shell patcher is used. Explorer's original window styles/opacity are restored when the popup closes and by the recovery helper after a crash.

The beta Dynamic Island is opt-in. It uses existing shell media, audio and power updates; its only timer is a one-shot feedback expiry. A 32-DIP native top appbar keeps maximized title bars below the compact capsule. Collapsed media shows artwork and activity bars, with its title in expanded controls only. The island remains visible outside fullscreen; fullscreen and shutdown remove its reservation. A process-bound lease recovers the reservation after a crash. Keyboard feedback falls back to the normal HUD on another display or during fullscreen. Hidden media releases artwork and animation subscriptions; disabled mode creates no island window. Trackpad remapping is absent; desktop overview selection applies to the dock button and physical Win+Tab only. The native work-area fixture records whether Explorer is available; headless CI still runs pure geometry and lifecycle checks.
