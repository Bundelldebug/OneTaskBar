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

- Animations from the FamilyTaes clips (read frame by frame in the browser: seek each video, diff frames, contact sheets): a Reminder bottom bar rising into a sheet, a SmartThings card collapsing into a pill with the cards below following, a Bixby pill, a search pill growing into the bar with the keyboard. Measured: 0.3-0.45 s, biggest step first, no overshoot. Added SwiftEase (1-exp(-7t), normalised) and Motion.Morph; used for the 9.5 panel open (Motion.Spring), the row reveal and the notification stack height morph. Not copied: the Bixby glow rim and the pill-to-bar morph. The tarunvats33 phone-app clip was only partly read. Tests passed (dynamic-island and desktop-widgets probes failed once and passed on rerun, known flaky). Not checked by eye on a device.

### Release 9.5.13 A1Z7 (stable) and 10.0.0 A1A8 Beta (Claude Code, 2026-11-13)

- Published at the owner's request, both unsigned, stable latest, beta prerelease: https://github.com/Bundelldebug/OneTaskBar/releases/tag/v9.5.13-A1Z7 and v10.0.0-A1A8-beta. Contents: issue 26 top bar changes, night light as a quick toggle, dock notification count, 9.5 card light, spring press and row reveal, SwiftEase motion (see the entries above).
- Test-UI Quick/Settings/Glass/Notifications/Dock/Launcher/Media/Startup/Resources passed on both builds. dynamic-island-probe failed 3 times in a row on the beta build, then passed on the new build and on the A1A7 exe in an A/B (2/2 each): timing flake, not a regression. Test-ReleasePayload passed for the stable package; its version check cannot accept a name ending in Beta, so the beta zip was checked by hand (exe version 10.0.0 A1A8 Beta, same hash as dist, 19 files). Signature check skipped: no certificate, builds are unsigned.
- Live measure of the installed build before packaging: CPU 0.09-0.80 %, WS 545-637 MiB (private 374-520). It measures the owner's running session and grew with use, so it is not the clean idle number from earlier (0.12 %, 406 MiB). Not measured: clean idle of the final packages. Not checked by eye: dock dot, pouring animation, new motion.


### Next-version request 2026-11-15 (Claude Code, not published)

- Issue 28: top-bar match tolerance doubled and a maximized-strip fallback (TopBarBand.MaximizedStrip) for Explorer/Settings-style frames. Root cause not reproduced on a device.
- Dock: search and multi-desktop buttons can be hidden (Personalization settings; DockSearchButton/DockDesktopsButton, default on). KakaoTalk tray click falls back to relaunching the executable if no window opens (TrayWake.cs); unverified, tray icon not found by UIA in test.
- Calendar widget: 4x4 size with month arrows (title returns to this month). Now Bar frame cap raised from 120 to 240 Hz (FrameRate.cs).
- Notification banners: moved from top-right to bottom-right beside the status area (stack upward, above the dock when it overlaps), and the surface/text are now a fixed dark film with light text for legibility. Windows notification center conflict not investigated separately.
- Not done: "shrink the taskbar itself" (the slider and bar reservation already follow dock size; owner to clarify). Test-UI Notifications/Dock/Settings/Quick/Media/Launcher/Glass passed; installed via Apply-ToPC. Not checked by eye on a device.

### Release 9.5.13 A1Z8 (stable) and 10.0.0 A1A9 Beta (Claude Code, 2026-11-15)

- Published at the owner's request, both unsigned, stable latest, beta prerelease: https://github.com/Bundelldebug/OneTaskBar/releases/tag/v9.5.13-A1Z8 (target fc23656) and https://github.com/Bundelldebug/OneTaskBar/releases/tag/v10.0.0-A1A9-beta (target e8e27f0). Contents: the 2026-11-15 next-version items above. Remote digests verified against `.qa-data/packages-A1Z8-final` and `packages-A1A9-final`. Setup SHA256 stable BBF5B1CB..33C8, portable 0A32138C..ECE9; beta setup 38223BC8..CD2A, portable F16CA8AA..EFE.
- Test-UI -Area All: all 35 probes passed on both snapshots (`.qa-data/release-A1Z8-final`, `release-A1A9-final`). Test-ReleasePayload passed for stable (19 entries, NotSigned). Beta zip checked by hand (19 entries, version 10.0.0 A1A9 Beta). No signature: no certificate.
- Idle runtime, isolated topbar/dock fixture, no media/helper: stable CPU 0.0096 % / 0.0193 %, WS 147.7 / 147.5 MiB, private 75.2 / 75.1 MiB; beta CPU 0.0032 %, WS 148.2 MiB, private 75.1 MiB. Installed-app measure (older A1A8-based local build, owner's live session): CPU 0.19 %, WS 558 MiB (private 391). Total-product idle of the packages was not measured.
- Note: BuildInstaller recompiles, so the shipped exe bytes differ from the tested snapshot copy (same source, new build timestamp/MVID); the package, not the snapshot, was what Test-ReleasePayload checked.
- Still unverified on a device: issue 28 fix, KakaoTalk relaunch, dock button hiding, 4x4 calendar look, bottom-right banners, 143 Hz frame rate. The "shrink the taskbar itself" request was dropped by the owner.

### Liquid glass redo 2026-11-15 (Claude Code, not published)

- Owner asked to rebuild the liquid glass option's UI and animation with the liquid-glass-web skill. The app is WPF with no WebView2 (no managed DLL, no restore), so the skill's SVG refraction cannot be used; ported its design instead: bezel rings in GlassOptics.LiquidEdge follow the skill's falloff (1 - sqrt(u(2-u))) with a cool outer and warm second ring, and GlassLens.Pulse plays a 720 ms swell plus a highlight sweep once around the rim when a liquid window opens (fast-rise ease, no overshoot, off with animations/Light mode). No real refraction of the backdrop. Menu-from-pill morph and lens drag were not done. Test-UI Glass/Quick/Settings/Notifications/Dock passed; installed; not checked by eye.

### Fluid notification animation, refraction option (Claude Code, 2026-11-17, not published)

- Request: make the Fluid notification animation like the GalaxyOS Beta 3 short (https://www.youtube.com/shorts/W2KLpjoHcuc); the card comes out of the bottom bar itself with a bounce and a glow, and other Fluid elements get the same feel; add the liquid glass refraction as an option, off by default, with the newest glass skill (Prism).
- Video reading (360x480 frames, 0.05-0.2 s steps in the browser): the notification arrives at about 27.6-28.4 s (an earlier pass wrongly used the 22 s home-screen greeting card). A frosted white card swells from a small pill with a bright glow, the icon spins in, then the words fade in.
- Built: NotificationBanner Fluid arrival (NotificationBanners.cs): the bar piece nearest below the card (dock Apps/Status/Start) glows (GlowCue.cs, a click-through window, 520 ms), then after 70 ms the card starts at that piece's size and place and springs up (damping .62, window moved each frame, surface scaled from its bottom centre, inner bloom, icon pop, text fade-in, 560 ms). Closing runs back into the bar. Other Fluid panels flare through GlassLens.Flare in GlassWindow.Pop (glow only, their own FluidBud morph is unchanged).
- Option: PreferenceData.LiquidRefraction (default false), settings row under Liquid glass (UI 9.5+). RefractionGlass.cs draws notification banners from a one-time screen read with the Prism menu preset (bevel + lip bend along the edge normal, vibrance, tint, dark line, light-facing rim; blur 2 px, tint .5). It is a still, not a live refraction; only banners use it.
- Checks: new banner-refraction-probe (Test-UI Notifications) checks the default, bevel/SDF math, banner capture, refracting state, stage frames, the bar glow (a pill window stands in for the dock), the panel flare; screenshots in .qa-data. All 36 probes passed individually; flakes: fluid-island once, settings-visibility 3 times then passed, desktop-overview/desktop-widgets/quick9/window-activation fail in Test-UI -Area All but pass alone, the A1Z8 build fails them the same way (A/B), so it is the busy desktop, not a regression.
- Not verified on a device: the real dock glow and launch from the real dock, the real notification path, 143 Hz smoothness. The dock pieces are not visible in the fixture shell, so the dock glow was only seen with a stand-in pill.

## Prism liquid glass overhaul (2026-11-17)
- Liquid material now follows the Prism numbers: vibrance 1.35, flat tint with a top sheen (`LiquidGlass.cs`), blur 8/6; Fluid keeps its own tint at level 0.
- Tried live GPU edge refraction (D2D DisplacementMap + a map drawn into a CompositionDrawingSurface). Windows.UI.Composition rejects the DisplacementMap effect at `CreateEffectFactory` (E_INVALIDARG, even with no properties; blur/border graphs are accepted). Removed; do not retry without a different host (own D3D swap chain or DirectComposition).
- The refraction option still only affects notification banners (CPU path, `RefractionGlass.cs`). Not verified: real dock look, GPU cost of the new tint on a real device.
- Checks run: Glass, Quick, Settings, Notifications, Dock areas passed. Not published.

### Refraction on every liquid panel (2026-11-17)
- The compositor cannot displace, so panels (non-dock `GlassWindow`s) get a still rim: `GlassWindow.PrepareRim` reads the edge band from the screen just before `Show()` and `RefractionGlass.Render(...,rim:true)` draws it, fading inward over the live blur; `WithRim` lays it over the tint in `ApplyMaterial`.
- Excluded: dock pieces, Now Bar, HUD, island, notification center, windows with `BackdropClip`/`RegionClip`, and panels over 1.5M px. Banners keep their full still.
- Known limits: the rim is a still, so it goes stale if the desktop behind moves while the panel stays open; a window shown before it is placed gets the rim for its old spot. Show cost ~130 ms for a 380x240 panel.
- Probe: `panel-refraction-probe` (Notifications area). Areas Glass, Quick, Settings, Notifications, Dock, Launcher, Media passed. Real dock/panels on the owner's screen not looked at.
