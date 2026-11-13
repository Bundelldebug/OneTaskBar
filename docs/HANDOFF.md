# Shared handoff

Checked: 2026-11-12, Asia/Seoul. Maintained by the active agent.

## Completed request

GitHub #20–#25 fixes, optimization, validation and both authorized releases are complete: **9.5.13 A1Z6 stable** and **10.0.0 A1A7 Beta**. No release work remains pending. Do not install or restart the installed application. The owner clarified that the separate charging-icon window appeared during QA, not a charger connection.

Checkout: `C:/Users/Bundell/Documents/Codex/One TaskBar`, branch `codex/release-9-5-0`, base `3d6eff4`. Main #20–#24 work is committed as `9f0f10623f53be0afab130ad8cb85a7e9dd5b3f4`; #25 and charging guards are committed as `bdb56092d8e046168ca3aad925c1da063c792c2f`. Preexisting `AGENTS.md` and `CLAUDE.md` edits belong to the owner/Claude: leave them unchanged and uncommitted. No other agent is working in this checkout. Historical chat requests are not a queue.

## Current changes

- #20: captionless browser covering its monitor is fullscreen even if Chromium retains WS_MAXIMIZE; normal maximized windows retain the dock.
- #21: Windows Night light shortcut under Display settings.
- #22: charging stays in the existing GoogleBook topbar visual tree. Hidden/concealed hosts cannot open for a preview; QA checks native window membership without painting a separate capsule. The adjacent-window safety band samples a thin strip off the UI thread every two seconds, caches unchanged colors and rejects stale results.
- #23: side-status clock bounds, compact clock/weather widget sizes, regional calendar holidays and weekend colors.
- #24: dock recovery raises only the registered dock/backdrop, never WPF's hidden GW_OWNER.
- #25: Steam client pins resolve steam.exe rather than steamwebhelper or an invalid AppsFolder ID. Pin identity/order and game/profile identities survive serialization and an incomplete Start-app cache. A real Steam installation was not launched; self-test uses inert fixture files.
- Topbar labels/artwork/geometry are reused. Fullscreen enumeration reuses its buffer and avoids repeated process/title reads. Marquee render time accumulates toward 60 Hz instead of skipping alternate frames on 75 Hz.

New files: `TopBarCharge.cs`, `TopBarBand.cs`, `TopBarChecks.cs`, `CalendarHolidays.cs`, `SteamLauncher.cs`. Routing: [DEBUGGING.md](DEBUGGING.md). Detailed evidence and failures: root `VALIDATION.md`.

## Shipping validation

Stable shipping snapshot: `.qa-data/release-A1Z6-final4/OneTaskBar.exe`, SHA256 `B13E42A6D207AAF096624910718F048CADE538DFEB3F3CD075DB607384B3A85D`. All 35 fixtures passed: `.qa-data/release-A1Z6-final4-all/summary.json`. Stable major 9 hides Fluid; the beta-only fixture confirms the gate. Full dist assets are included in this snapshot.

Two final stable idle samples: CPU 0.2102% / 0.0097%, average working set 186.04 / 151.24 MiB and private working set 103.99 / 78.95 MiB. Reports: `.qa-data/release-A1Z6-final-runtime/runtime.json` and `release-A1Z6-final-runtime-repeat/runtime.json`. These are isolated topbar/dock scenes, no active media or recovery helper, not installed whole-product use. No forced GC or working-set trimming. Total-RAM 150 MB and hourly battery-drain goals remain unmet/unmeasured.

Retain failed reports: final2 lacked QA assets; final3 exceeded the existing fullscreen-cover frame-step assertion once. Final4 passed unchanged animation/threshold. Earlier sandbox island failures and actual isolated Edge CDP fullscreen geometry checks are described in VALIDATION.md. No second physical monitor or live YouTube fullscreen-button check was performed.

Stable packaging passed: `.qa-data/packages-A1Z6-final/verification.json` matches all 19 files, Setup exit 0, executable SHA256 as above, no installation. Defender passed all four targets with protection enabled and no exclusion changes: `defender.json`. Assets are frozen in that directory. Setup SHA256 `45BDCF51BC17E0C3082A8D08662793310DA58E4B248FDEE6FBC21107C26FEFB7`; portable SHA256 `E74A32754789C456C948DD0E13B2E62D630662C833CD37125B1B8CAE4DAAFAE2`.

Stable is publicly released as latest: https://github.com/Bundelldebug/OneTaskBar/releases/tag/v9.5.13-A1Z6 . Remote API verified draft=false, prerelease=false, tag bdb5609, and all three asset sizes/SHA256 digests against the frozen package record. Evidence: `.qa-data/packages-A1Z6-final/github-release.json`.

Beta selected in AssemblyInfo.cs: 10.0.0 A1A7 Beta. Snapshot `.qa-data/release-A1A7-final/OneTaskBar.exe`, SHA256 `8FB470B65905880779DCCE4AE8E2980DF5CD0CD4CB5C04DA280DB76C732F3002`. All 35 checks passed in `.qa-data/release-A1A7-final-all`. Final idle CPU 0.0097%, average working set 147.43 MiB / private working set 75.13 MiB; `.qa-data/release-A1A7-final-runtime/runtime.json`. No media/helper/hooks, not total-product use. Earlier beta CPU/memory variability is retained in VALIDATION.md. Beta staging matched all 19 files, Setup exit 0, both signatures NotSigned and Defender passed four targets without protection/exclusion changes: `.qa-data/packages-A1A7-final/verification.json` and `defender.json`. Beta source/tag: `7806545cc6df5851a507000387d1b0c6fc581510`, `v10.0.0-A1A7-beta`. Published prerelease: https://github.com/Bundelldebug/OneTaskBar/releases/tag/v10.0.0-A1A7-beta . API verified draft=false/prerelease=true, the tag commit and all three asset sizes/SHA256 digests. Stable remains latest. Evidence: `.qa-data/packages-A1A7-final/github-release.json`. Setup SHA256 `73B6E78594B6E31B445672ACD42896C09C47C96DAE12AC76EADDF9B2987334C1`; portable `376752EE70233F79AF5CF24E6E407480D75D50E7D109DB572F6D8860747B44A8`. No installation. Old packages and beta artifacts are superseded. No issue was commented on or closed.

## Publication constraints

Stable major 9 excludes Fluid UI; beta major 10 exposes it. Preserve settings, pins, widget keys and disabled defaults. Stable is latest, beta prerelease. Both remain unsigned: manual GitHub CLI publication is authorized; do not bypass or weaken Publish-Release.ps1's signing gate. No GitHub issue comments/closures were requested. Do not change startup, notification registration, Windows protection or the installed application. Record published URLs and verified asset hashes after publication.


### Five-item request 2026-11-13 (Claude Code, not published)

- Issue 26 (GoogleBook top bar): battery capsule turns pale with split-tone digits when the bar ink is light (BatteryPill.LightInk); the colour under the bar is read every 60 ms (100 ms on battery) by its own timer instead of every 2 s, and a new colour pours in from the top edge downwards (SafeAreaCurtain.Sample/Pour). The black-window bug was not reproduced; it most likely was the 2 s staleness, so it is unconfirmed.
- Night light moved from Display settings to a quick settings toggle option (
ight-light, opens ms-settings:nightlight; off by default, added through the toggle editor). It is a shortcut, not a real on/off switch.
- Notification arrival dot: dock clock shows the notification count (NotificationBadge, default on, setting under notifications); hidden while the notification center is open.
- 9.5+ look: Glass95 card film and edge got a lit top, shaded lower face and thin dark far edge (film alpha kept <= 38 for the backdrop check). Buttons spring on press in 9.5 (Motion.Press), and panel rows rise and fade in one after another (Motion.Reveal, from Motion.Launcher). This is an approximation of the One UI 9.5 clips; the phone-app video could not be read and no exact copy was made.
- Not checked on a device: the dock dot, the pouring animation, the new arrival motion. Test-UI Settings, Quick, Glass, Notifications, Dock, Launcher, Media, Startup, Resources passed (Media dynamic-island-probe once on the first try, flaky). Installed A1A7 build with these changes; idle CPU 0.106 %, WS 325 MiB (private 186). The 60 ms sampler was not included in that idle measure.

- Follow-up (same day, not published): the owner asked for a stronger 9.5 floating look. Opaque panels and opaque cards were tried and rejected (owner: keep transparency); the glass is unchanged. Instead GlassTileShadow draws a layered soft shadow under every widget tile when Glass95.Raised (9.5, not Fluid), notification cards get a shared DropShadowEffect (Glass95.Lift). Tests passed: Glass, Quick, Notifications, Settings, Dock, Launcher, Media. Installed; idle CPU 0.12 %, WS 406 MiB (private 244). Not checked on a device: notification card shadow cost with many items.

- Tile shadows and the notification card shadow were removed again at the owner's request (they looked wrong on real backdrops). The glass, film and rim are as before the shadow work; the Glass95.Raised/Lift code is gone.
