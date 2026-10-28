# Issue work queue

Published bug-fix release: 9.0.7 / 9.5.4-beta.1. The following suggestions are implemented in the next local development build.

## Bugs

- [Issue 5](https://github.com/Bundelldebug/OneTaskBar/issues/5): monochrome recoloring now requires a narrow luminance range as well as low saturation. Two-tone grayscale logos keep their original pixels. Tray ink uses the popup palette rather than the independently configured dock palette. `tray-overflow-probe` checks a black/white logo, a solid monochrome glyph and a color icon.

- Latest screenshots: dock coordinates now stay at the physical monitor edge, independent of another shell work-area reservation. If Explorer moves a surface later, a bounded position check restores the stored layout without interrupting resize/hide animations.
- Latest Task View screenshot: reapply tool-window styles after WPF shows a surface; remove stale Explorer tabs with ITaskbarList and exclude Peek. Capture review expires after two minutes. Normal mode never enables application-window styles.
- [Issue 4](https://github.com/Bundelldebug/OneTaskBar/issues/4): edge-pointer recovery handles browsers that intercept reveal-window mouse messages; overflow apps inherit the dock material/color; tray and native-panel placement use the selected edge; output actions have an inset; solid panels omit glass card/rim decoration; the entire settings header padding supports dragging while controls remain interactive.
- [Issues 1 and 3](https://github.com/Bundelldebug/OneTaskBar/issues/3): existing monitor-scoped fullscreen/passive virtual-surface tests, auto-hide return, small-screen capacity and original icons remain in the release suite. Actual dual-monitor/virtual-driver hardware was not present on this PC.
- Startup cache: retain atomic replacement and retry short file-sharing failures without replacing a valid cache with a partial write.

## Suggestions after publication

Each item was applied to the PC after its focused checks. Existing settings and pins were preserved.

1. `DockSizing.cs`: 60–140%, default 100%. Content zoom is separate from monitor DPI; work-area reservation, media anchors and inward popups follow the chosen size. Commit only after releasing the size control.
2. `PanelTint.cs`: opt-in link to the selected dock color, default off. Frozen tint caches preserve panel ink contrast and the existing transparency policy; disabling restores the original palette.
3. `SettingsPageMotion.cs`: short layered entry/return animation. Cancellation on hidden/unloaded views, no screenshots or persistent bitmap caches.
4. `DockClock.cs`: bold time, smaller date; time-only/date-only choices. Existing runs are reused between ticks; compact modes reduce status space. Full timestamp remains in the tooltip.
5. `UIVersionPreview.cs`, `UIVersionPicker.cs`: code-drawn selected-version illustration and description; scalable dropdown and shared cached control style.
6. `SettingsPages.cs`: search is the only bottom control; exit is a red row at the end of the settings list and is searchable.
7. UI 9.5 optical refinement follows the actual reference implementations. See `GLASS_RENDERING.md` for the GPU contour approximation and its limits. Established filled slider geometry and stable 8.5/9.0 rendering are retained.

Latest color fixes: notification chrome uses the same text palette as Quick Settings; light notification capsule content retains its own contrasting ink. Keyboard HUDs use the same unfilled track brush and underlying panel tint as Quick Settings. Their fade and native keyboard paths remain unchanged.

Earlier [issue 2](https://github.com/Bundelldebug/OneTaskBar/issues/2) suggestions already have implementations for notification-to-Quick navigation, stronger setting selections, dock color/image modes, themed tray overflow, calendar access, scroll fading, matching launcher searches, edge placement and separate theme preferences. Retain their regression coverage.

## Verification boundaries

2026-10-28 animation pass: 27 beta fixtures passed, including swipe dismissal without activation, twelve search recommendations, continuous wave curves and pause settling, overview reuse, hosted child-window Alt+Tab selection, and tray grayscale preservation. A disposable mouse swipe fixture also passed without touching real notifications. These changes are shared with the stable branch. Short CPU samples do not establish battery-life savings; measure the installed build in matching states.

Samsung Notes follow-up: live diagnostics found that its CoreWindow timed out a response check while the outer frame remained responsive. Activation now probes the frame, can target the actual input content, and continues when an optional input-queue link is denied. Normal/maximized/minimized activation and cleanup fixtures passed after the change; the user confirmed both Notes → other app and other app → Notes on the installed build. Failed activation diagnostics contain classes/handles/error codes only, not window titles or document contents.

Use `tools/Test-UI.ps1` and exact-commit CI artifacts. Never test native desktop shortcuts, tray callbacks, or user-window close commands as part of isolated fixtures. Native panel repositioning recognizes only the native shell processes after a user opens their panel; unsupported Windows hosts keep native behavior. Fixture reports and user screenshots remain private under `.qa-data/`.
