# Issue work queue

Bug-fix release: 9.0.7 / 9.5.4-beta.1. Suggestions are intentionally a later local-development phase.

## Bugs

- Latest screenshots: dock coordinates now stay at the physical monitor edge, independent of another shell work-area reservation. If Explorer moves a surface later, a bounded position check restores the stored layout without interrupting resize/hide animations.
- Latest Task View screenshot: reapply tool-window styles after WPF shows a surface; remove stale Explorer tabs with ITaskbarList and exclude Peek. Capture review expires after two minutes. Normal mode never enables application-window styles.
- [Issue 4](https://github.com/Bundelldebug/OneTaskBar/issues/4): edge-pointer recovery handles browsers that intercept reveal-window mouse messages; overflow apps inherit the dock material/color; tray and native-panel placement use the selected edge; output actions have an inset; solid panels omit glass card/rim decoration; the entire settings header padding supports dragging while controls remain interactive.
- [Issues 1 and 3](https://github.com/Bundelldebug/OneTaskBar/issues/3): existing monitor-scoped fullscreen/passive virtual-surface tests, auto-hide return, small-screen capacity and original icons remain in the release suite. Actual dual-monitor/virtual-driver hardware was not present on this PC.
- Startup cache: retain atomic replacement and retry short file-sharing failures without replacing a valid cache with a partial write.

## Suggestions after publication

Apply and check each independently, preserving existing settings and default behavior.

1. Dock scale as a percentage.
2. Optional panel background color shared with the dock.
3. Settings category navigation motion.
4. Clock typography and compact display choices.
5. UI-version illustrations and descriptions.
6. Settings footer cleanup; place exit in the settings list.
7. UI 9.5 optical refinement using the user-selected reference implementations. Preserve the established filled slider shape and the stable 8.5/9.0 appearance.

Earlier [issue 2](https://github.com/Bundelldebug/OneTaskBar/issues/2) suggestions already have implementations for notification-to-Quick navigation, stronger setting selections, dock color/image modes, themed tray overflow, calendar access, scroll fading, matching launcher searches, edge placement and separate theme preferences. Retain their regression coverage.

## Verification boundaries

Use `tools/Test-UI.ps1` and exact-commit CI artifacts. Never test native desktop shortcuts, tray callbacks, or user-window close commands as part of isolated fixtures. Native panel repositioning recognizes only the native shell processes after a user opens their panel; unsupported Windows hosts keep native behavior. Fixture reports and user screenshots remain private under `.qa-data/`.
