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
| Keyboard volume/brightness bar disappears or lacks contrast | `HudWindow` in `SystemPanels.cs`, `PillSliderGlass.cs`, `GlassWindow` in `Controls.cs`, `NativeOsdGate.cs` | Glass; Settings / `beta-feedback-probe` |
| Media wave, title, responsive layout or low-res art | `MediaPlayerModern.cs`, `MediaWave.cs`, `MarqueeTitle.cs`, `YouTubeArtwork.cs` | Media |
| Different floating popup and quick-panel tint | `PanelMaterial.PopupSurface`, `WindowPreview.ApplyMaterial`, `WindowSwitcherView.cs` | Dock; Notifications; Settings / `panel-theme-probe` |
| Dock missing on desktop or wrong fullscreen monitor | `DockHost.UpdateVisibility` in `Windows.cs`, `Native.EnsureDockVisible`, `FullscreenVisibility.cs` | Dock |
| Virtual display leaves the dock hidden after the last app closes | `FullscreenVisibility.PassiveSurface`, `DockHost.UpdateVisibility` desktop-idle recovery | Dock / `desktop-behavior-probe` |
| Desktop overview stutters or leaves a black preview window | `DesktopOverviewController.cs`, `DesktopOverview.cs`, `SwitchThumbnail` in `WindowSwitcherView.cs` | Dock / `desktop-overview-probe`; `desktop-overview-timing.json` contains counts/timing only |
| Tray icons, native-host fallback or recovery | `TrayOverflowController.cs`, `TrayIcons.cs`, `TrayIconVisual.cs`, `TrayOverflowWindow.cs` | Dock / `tray-overflow-probe`; `tray-proxy-state.json` contains counts only |
| Glass bevel clipping or repeated layout work | `GlassRefraction.cs`, `LiveBackdrop.RefreshRefraction` | Glass / `glass95-probe` |
| Alt+Tab does not raise an existing active app | `WindowSwitcherManager.cs`, `WindowActivation.cs` | Dock / `window-activation-probe` |
| Search order, recent chips or keyboard focus | `SearchWindow.cs`, `SearchNavigation.cs`, `LauncherInputChecks.cs` | Launcher |
| Settings blank, search empty strips, version label | `SettingsWindow.cs`, `SettingsPages.cs`, `UIVersionPicker.cs` | Settings |
| Notification logo, image, action or stale cache | `NotificationService.cs`, `NotificationIcons.cs`, `NativeToastAssets.cs` | Notifications; `self-test` includes asset/cache checks |
| Login delay, idle resource growth | `Startup.cs`, `Shell.cs`, `RuntimeCadence.cs`, `MemoryChecks.cs` | Startup; Resources |

## Runtime measurements

`tools/Measure-Runtime.ps1` reads only the specified running app, its recovery helper, counters and `live-state.json`. It does not open panels, alter preferences, force GC, trim memory or change power/security policy.

```powershell
./tools/Measure-Runtime.ps1 -Executable 'C:\path\OneTaskBar\App\OneTaskBar.exe' -Seconds 30
```

Compare matching media, monitor and visible-panel states. `totalWorkingSetMiB` includes shared resident pages; `privateWorkingSetMiB` is private resident memory; `privateBytesMiB` is committed private memory, not RAM in use. CPU is normalized across logical processors. Do not claim the 150 MB total-RAM goal from private memory alone.

## Boundaries and compatibility

- Isolated fixture data must not use the installed application's data directory. `Test-UI.ps1` creates a fresh directory for each run and restores `ONE_TASKBAR_DATA` afterward.
- Live diagnostic signals in `Program.cs` are different from isolated fixtures. The shared runner intentionally excludes signals and hardware-changing probes.
- Floating notifications, Alt+Tab and window previews share the Quick panel material. The output selector has its own dark gray palette. Keyboard HUD contrast is separate. Avoid changing one global tint to repair a single surface.
- UI 9.5 changes glass material, not the established filled volume/brightness slider shape. Preserve 1×1 circles, pill buttons and the 8.5/9.0 switch.
- Notification images remain native resolution up to 256 px; evict inactive app caches. Current-video artwork consent must remain scoped to the authorized track.
- A normal restart/install and published asset verification are separate from fixture success. Preserve settings and use exact tested CI bytes for releases; do not overwrite existing release tags or assets.

UI review: launch the installed app with --desktop --review-ui, then use --open-panel notifications|quick|start|search. Review mode exposes owned surfaces to the capture tool; restart without --review-ui afterward. Normal sessions and fixtures retain hidden tool windows.

Desktop overview and tray fixtures create their own source windows. They never send desktop-switch shortcuts, close user windows or invoke the real tray. Test the real desktop shortcut and tray callbacks separately. Tray review mode writes provider labels/IDs to a local `tray-provider-review.json`; keep it private. Normal mode records counts only. No process injection or shell patcher is used. Explorer's original window styles/opacity are restored when the popup closes and by the recovery helper after a crash.
