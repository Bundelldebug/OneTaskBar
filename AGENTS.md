# One TaskBar development map

Windows x64, C# 5, .NET Framework 4.8, WPF plus Win32/WinRT. `Build.ps1` compiles every `src/*.cs`; there is no project file or package restore.

Start with the relevant row in `docs/DEBUGGING.md`. Search exact symbols with `rg -n`; read individual methods instead of dumping compressed legacy files or all probe reports.

| Area | Entry points |
| --- | --- |
| Quick settings | `QuickWindow.cs`; `QuickGridSurface.cs` constructs, `QuickGridEditing.cs` edits, `QuickGridState.cs` refreshes, `QuickWidgetTile.cs` renders |
| Widget layout | `QuickWidgetModel.cs` stores/places; `QuickWidgetBoard.cs` handles gestures and animations |
| Toggle definitions | `QuickActionCatalog.cs`, `QuickSettingCatalog.cs`, `QuickSettingIcons.cs`, `QuickToggleOrder.cs` |
| Colors and blur | `PanelMaterial.cs`, `Glass95.cs`, `AppTheme.cs`, `LiveBackdrop.cs` |
| Dock / fullscreen | `Windows.cs`, `DockGeometry.cs`, `FullscreenVisibility.cs`, `DesktopIntegration.cs` |
| Output / HUD / media | `AudioOutputView.cs`, `DetailWindow.cs`, `MediaPlayerView.cs`, `MediaPlayerModern.cs`, `NowBar.cs` |
| Launcher | `StartWindow.cs`, `SearchWindow.cs`, `SearchNavigation.cs`, `LauncherLayout.cs` |
| Notifications | `NotificationService.cs`, `NotificationStack.cs`, `NotificationSwipe.cs`, `NotificationBanners.cs`, `NotificationAudio.cs`, `BudsConnection.cs` |
| Preferences / settings | `Preferences.cs`, `SettingsWindow.cs`, `SettingsPages.cs`, `SettingsPersonalization.cs` |
| Runtime | `Shell.cs`, `RuntimeCadence.cs`, `Startup.cs`, `MemoryChecks.cs` |

Build: `./Build.ps1 -SkipTests`. Run relevant isolated fixtures: `./tools/Test-UI.ps1 -Area Quick` (or `-List`). CI uses the same runner with `-Area All`. Read its `summary.json` first and only the failed probe's JSON. Report directories and fixture settings live under ignored `.qa-data/`.

Stable builds expose UI 8.5/9.0; beta also exposes 9.5 through `ReleaseFeatures.GlassUI`. Shared fixes belong in both release branches. Preserve persisted widget/toggle keys, pins, settings and the disabled-by-default options during changes.

Keep normal control flow on separate lines in touched/new code. Split by responsibility rather than adding another large mixed file. Reuse frozen brushes/artwork; unchanged refreshes should not allocate or restart animations. Release timers, backdrop hosts and image caches when their owners retire. Do not force GC or trim the working set to manufacture measurements.

Public GitHub writing: describe the user-visible change and necessary usage details in plain language. Release notes should be short; keep implementation logs, request history and repeated validation boilerplate in development documents. Retain material limitations, licensing and unsigned-build notices.
