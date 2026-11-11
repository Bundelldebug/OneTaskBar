# Shared handoff

Checked: 2026-11-11, Asia/Seoul. Maintained by the active agent.

## Active request

Fix GitHub #20–#24, optimize, validate and publish the next stable and beta. Base: `3d6eff4` on `codex/release-9-5-0`; planned versions: **9.5.13 A1Z6** and **10.0.0 A1A7 Beta**. Publication is authorized. Do not install or restart the installed application for this task.

Local `AGENTS.md` and `CLAUDE.md` changes predate this task: leave them unmodified and uncommitted. No other agent is working in this checkout. Historical requests in the chat are not a queue.

## Changes ready for final validation

- #20: distinguish a captionless browser covering the entire monitor from a normal maximized browser, including Chromium retaining `WS_MAXIMIZE`. Keep the dock visible for ordinary maximized windows.
- #21: Windows Night light shortcut under Display settings.
- #22: fixed-host charging overlay in GoogleBook top status bar; retained safety band samples colors inside the adjacent maximized window, with worker-thread sampling and stale-result guards. Cache unchanged status labels, artwork and geometry.
- #23: shared side-status row heights prevent clipped clocks; compact clock/weather widget sizes; region-gated Korean calendar holidays and weekend colors.
- #24: `Native.EnsureDockVisible` raises only the known backdrop and dock, never WPF's invisible `GW_OWNER`. Regression covers repeated hide/minimize recovery with blur on, off and unavailable.
- Title marquee uses accumulated render time instead of skipping alternate frames at 75 Hz. A recent-feed fixture compared Windows paths case-sensitively; only its assertion was corrected.

New responsibility-specific files: `TopBarCharge.cs`, `TopBarBand.cs`, `TopBarChecks.cs`, `CalendarHolidays.cs`. Symptom routing is in [DEBUGGING.md](DEBUGGING.md). Release notes are prepared for both versions. No commit or upload yet.

## Evidence and remaining work

Final stable: `.qa-data/release-A1Z6/OneTaskBar.exe`, SHA256 `7CC1E907A42BF730BEEBE824582AEA3687F18C9400B5CFF60D04C2326FD6DF72`, based on `3d6eff4` plus this patch, including the holiday overlap correction. All 35 fixtures passed (`.qa-data/release-A1Z6-all/summary.json`). Isolated idle: CPU 0.0097%, working set 164.36 MiB; `.qa-data/release-A1Z6-runtime/runtime.json`. Setup staging matched all 19 payload files and Defender passed without changing protection/exclusions; `.qa-data/packages-A1Z6/verification.json` and `defender.json`. No installation or upload yet.

Candidate 6: `.qa-data/issues20-24-candidate6/OneTaskBar.exe`, SHA256 `034727B83CE2659763D52B01AF052887EEE7384A8F3A48CF43CBAC1642D70B9B`.

- Read/run summary: `.qa-data/issues20-24-all6/summary.json` passed initial media/quick/self checks, then failed island safety reservation in the default sandbox. The identical binary passed with native window permissions: `.qa-data/issues20-24-island-native6/summary.json`.
- Native changed-area checks passed: `.qa-data/issues20-24-changed-native6/summary.json` (desktop behavior, topbar, fullscreen tiles).
- Real isolated Edge CDP maximize → fullscreen → maximize passed; `.qa-data/browser-fullscreen-20261111/report.json`. Fullscreen retained `WS_MAXIMIZE` and lost its caption. This is not a live YouTube button test or secondary-monitor hardware test.
- Baseline Hidden Window reproduction: `.qa-data/issue24-baseline.json`. Hidden owner changed from invisible to visible after recovery. Fixed regression keeps it hidden.
- Capture microbenchmark: `.qa-data/issues20-24-capture-benchmark.json`; 300 captures/round, three alternating rounds, no forced GC. Candidate wall time 32.73–40.08 ms vs baseline 37.04–42.96 ms. This is not total app or battery performance.
- Earlier marquee rate and recent-feed fixture failures remain in private reports and must be retained in VALIDATION.md.

Next: commit stable, then repeat build, All fixtures, final CPU/memory measurement and packaging for beta with assembly major 10. Publish both verified artifacts. Keep all screenshots/data/reports in ignored `.qa-data/`. Do not run real installation or change notification registration/startup.

## Publication constraints

Stable major 9 hides Fluid UI; beta major 10 exposes it. Preserve saved settings/pins/widget keys and disabled defaults. Publish stable as latest, beta as prerelease. Both builds remain unsigned; do not bypass or weaken the signing gate in `Publish-Release.ps1`. GitHub CLI manual publication was authorized. Do not close/comment on issues without a request.

The 150 MB total-RAM target and hourly battery-drain comparison have not been achieved or measured in this task. Read root VALIDATION.md for release evidence and limitations. Do not repeat stale installed-version or release claims from older chat/handoff snapshots.
