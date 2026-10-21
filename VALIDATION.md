# Validation — 0.5.5

Windows 11 x64, .NET Framework 4.8, two monitors at 200% and 125% DPI, 143 installed apps and 16 logical processors. Results describe this development PC.

- Build/self-test passed. Background-worker fixture used exactly two threads with peak concurrency two, excluded the caller/UI thread, and both Windows EcoQoS requests succeeded. No CPU affinity or machine power-policy changes were made. Windows chooses the actual core; individual E/LP-E residency was not measured.
- Lazy icon UI fixture initially decoded 30 visible icons; scrolling loaded the last viewport, with 59 cached after the sequence. Stable backdrop, 98 edge-alpha values, 48-DIP fractional scrolling, repeated dismissal and Task Manager menu passed.
- Actual installed app, media active on two monitors with Windows notifications allowed: a 10.11-second post-optimization sample recorded private memory 176.0–176.3 MiB, working set 231.7–232.0 MiB, and main-process CPU 0.241% normalized across 16 logical processors. No forced garbage collection or working-set trim was used. These samples preceded the final helper fix and **excluded the helper**, so they must not be used as total application CPU claims.
- The subsequent whole-product investigation found four old recovery helpers, each consuming approximately one logical core. The message-loop path was removed entirely in favor of an EcoQoS helper that sleeps on the owner handle and checks only the emergency chord every 20 ms. The final isolated recovery regression used 1.30% of one core during a short 1.2-second cold-start sample, rejected a duplicate helper, honored the disabled emergency setting, terminated only its hung fixture when enabled, and exited afterward. Owner-lifetime waits and recovery waits now use native blocking handles.
- Final **whole-product** measurement after six seconds of warm-up, with both processes confirmed alive: eight-second sample, main CPU 0.207%, helper CPU 0.000%, 152.4/14.5 MiB private bytes and 203.4/19.6 MiB working set respectively (16 logical processors). Total working set was 223.0 MiB. Normal installed packaged shutdown left no helpers; relaunch succeeded. These are sampled values, not a fixed resource budget.
- Final Setup extracted exactly 19 allowlisted files with matching SHA-256 values; PowerShell parsing passed.
- Live DWM preview content measured 292 × 171 DIP, with two real fixture thumbnails, narrow-monitor pagination, source activation, no focus stealing and thumbnail disposal all passing.
- Shared media controls dispatched play/next/previous and seek-to-65% without changing the user's actual player; both output buttons share the same callback. Idle media height including margin is 62 DIP. Default Windows output is read from the actual multimedia endpoint, not inferred from the track.
- Now bar: software-layered content and blur remained synchronized; original-slot click collapsed it; marquee, interrupted morph, monotonic collapse, transparent click-through and Alt+Tab exclusion passed. The fixture reached 44/188 DIP heights, had 46 edge-alpha values and a maximum sampled collapse gap of 25.3 ms.
- Popup checks found no orphan visible backdrops; outside clicks closed and released context-menu blur. Low slider values changed both cap axes and zero left no white fill. Notification/quick panels remained mutually exclusive across 110 samples and rapid reversals.
- Normal installed EXE launch automatically activated the registered identity and retained notification access. OneTaskBar's own suppressed-popup test toast was received with its icon and original protocol payload, then removed by its own tag/group. No user's notification was activated or cleared by this check.
- No OS reboot, power-off, sleep or lock was performed. Battery-runtime improvements were not measured over a full discharge cycle. Unsigned local notification registration is optional and is not a production-signed package.

The native Windows Start invocation limitation documented below remains; the custom app drawer is the default. Personal JSON, logs, screenshots, recordings and developer packages are excluded from Git and the installer.

# Validation — 0.5.2

Windows 11 x64, .NET Framework 4.8. Build and self-test passed, including compact launcher bounds, mixed-DPI Now bar placement and artwork contrast fixtures. These results are development-PC checks, not universal frame-rate or driver guarantees.

- Now bar probe used an actual WPF/compositor window with fixture media and injected transport callbacks. The capsule grew from 44 to 188 DIP high and collapsed monotonically through intermediate sizes; a rapid reversal continued from its current rectangle. The measured collapse had 12 update frames and a maximum interval of 34.7 ms. The native host stays fixed while the surface and compositor clip change; cached content avoids reflow during the morph.
- Short title width was 142.7 DIP; a long title stopped at the available 316-DIP cap and scrolled. Previous/play/next and seek-to-65-percent callbacks arrived correctly. Paused state, session-end hiding and full-width reappearance passed. The fixture did not change the user's real media player. The installed desktop app separately reported a live Windows media title and a visible Now bar.
- Clicks outside the visible capsule reached the underlying test window, and both native hosts were excluded from Alt+Tab. Corner capture had 46 distinct alpha values. Dominant-color fixtures cover white, black, yellow, red, blue and transparent artwork; the red capture's computed white-text contrast was 7.33:1 with the tint and artwork glow included against a white backdrop.
- Quick-feature probe verified all eight UI Automation toggle states, actual mute and Windows performance-mode changes, keep-awake and animation changes. It restored original audio, power mode and preferences. It did not change Wi-Fi, Bluetooth or startup registration and did not execute shutdown, restart, sleep or lock. The power-menu probe inspected all four command bindings without running them.
- Compact app/search UI regression passed: constant backdrop over nine opening samples, 98 distinct edge alpha values, fractional scrolling totaling 48 DIP, Task Manager context menu and repeated reopen/dismiss. Direct typing, deferred Korean composition and committed search text passed. One preceding typing attempt stopped before sending keys because Windows denied foreground focus; the independent rerun passed.
- Current UI stress sample: private bytes 304,873,472; working set 349,192,192; managed bytes 13,166,840, with 143 apps. This is not an idle-memory measurement or a same-workload comparison including Now bar.

Known limitation retained: the native Windows Start invocation probe on this PC opens SearchHost although physical Ctrl+Esc opens Start. The custom app drawer remains the default. Custom Windows-notification reading still requires separately registered identity and Windows consent; Setup does not register the unsigned development identity.

# Validation — 0.4.3

Build/self-test passed. Dock ordering fixtures cover inactive pins being launched, pinned apps closing, already-running pins at startup, manual moves and new unpinned entries appending. Existing slots retain their relative order. The actual-window dock probe again passed intermediate growth/shrink frames, entry/exit fades, retained button objects and interrupted resizing. Quick settings was rendered after the material change; active button backgrounds and slider fills now use the same shared white color constant. Shutdown, sleep and lock were not executed during validation.

The preceding native-OSD recovery probe also passed: an isolated child exited without disposing its opacity lease, then the recovery path restored the native host opacity. Reference captures and personal probe output remain excluded from the installer.

# Validation — 0.4.2

Actual dock window widths grew from 308 to 400 physical pixels through intermediate frames, then shrank back to 308. Entry/exit fades, retained button references, stable launch order and interrupted growth/removal all passed. Native OSD opacity was already zero before the native SHOW event; normal-exit restoration passed. Power menu wiring was inspected without executing shutdown, sleep or lock.

# Validation — 0.4.1

Interior cards and controls now use translucent gray tint over the existing live backdrop. The top power menu contains shutdown, sleep and lock, with animated dismissal. Menu wiring is inspected without executing power actions.

# Validation — 0.4.0

Windows 11 x64, .NET Framework 4.8; these results do not establish support for every driver or shell customization.

- Build and self-test passed, including PHY-to-Wi-Fi-generation fixtures, settings migration and OSD host filtering.
- Actual connection reports 802.11be / Wi-Fi 7. Disconnected/disabled glyphs omit the generation number.
- UI probe passed with 143 installed apps: stable backdrop across nine opening frames, 89 edge alpha levels, fractional scrolling totals 48 DIP, Task Manager context menu and repeated reopen/dismiss.
- Direct typing and Korean IME composition probe passed; rounded search preserved.
- Native Explorer volume command: custom HUD appeared in 119 ms and one native OSD was suppressed. Actual brightness event reached the HUD in 46 ms. Original audio and brightness were restored. These are single-run measurements, not hardware-wide latency guarantees.
- App restore/group minimize and icon bounce containment passed in the foreground-fixture run; later real-mouse repetitions were sensitive to foreground changes during interactive testing.
- The independent dock button preference and Windows glyph render correctly. Native Start invocation has NOT passed the automated check on this PC (SearchHost appears instead); physical Ctrl+Esc was confirmed by the user to open Start. This route remains under investigation and must not be described as verified.
- Reference video was sampled locally to tune the feature area's expand/collapse easing; reference media is excluded from distribution.

The measurements below describe the previous 0.3.0 release, not a new same-workload memory comparison.

# Historical validation — 0.3.0

Tests run on Windows 11 x64, .NET Framework 4.8, an unlocked desktop, and a catalog of 142 installed applications. They do not establish compatibility with every GPU, Windows build, device, or third-party program. Personal test JSON and screenshots are excluded from Git and release payloads.

- Self-test: exact 0/100 slider fill; AC/charge protection state; one animation per connection; mixed-DPI/negative monitor placement; desktop watt input; independent native/custom preferences; Korean initial search, cancellation and history; icon/layout geometry.
- UI probe: background opacity/transform/tint remain constant through 9 opening frames; drawer has 35 distinct alpha levels at its curved edge; 12 fractional wheel deltas move exactly the expected 48 DIP; Task Manager menu exists; repeated reopen/dismiss works.
- Typing probe: actual synthetic keyboard input into the app drawer becomes `chat` in launcher search; WPF Korean composition start/update defers transition and final `챗` commits without losing the query; search uses rounded soft corners.
- Start-key probe: one solo Win tap invokes the custom drawer once, focuses it, and does not open Windows Start simultaneously.
- Windows-search probe: opening the native search sends the seed query and reads back the same value from its actual search edit control.
- Recovery probe: separate watchdog ignores Ctrl+Shift+Tab+1 when disabled, then terminates an isolated unresponsive OneTaskBar fixture when enabled; helper exits. Measured termination was 62 ms on this run. This is not a secure-desktop test.

## Memory comparison

Same UI workload, catalog, opening frames, corner render, scroll, and repeated reopen sequence; no forced GC or working-set purge:

| Measurement | Before | After | Change |
| --- | ---: | ---: | ---: |
| Private bytes | 365,780,992 | 314,920,960 | −13.9% |
| Working set | 410,427,392 | 364,003,328 | −11.3% |
| Managed bytes sampled | 14,396,184 | 12,694,968 | −11.8% |

These are a **UI stress workload**, including test render targets, not idle-memory claims. Changes include lazy panel construction, cached drawer layout and button templates, bounded logo decode, detached thumbnail crops, weak image metadata, released search cards, and released compositor resources. Readings vary with desktop content and caching.

## Installer and desktop recovery

The embedded Setup payload extracted successfully with an executable SHA-256 match, and all PowerShell sources parsed without errors. Setup then installed on the test PC from a **non-elevated** process (exit 0) with startup registered in HKCU and an entry in Windows Apps.

Updating while the installed desktop app was running, uninstalling, and reinstalling all completed successfully. Uninstall removed the application and preserved the settings file byte-for-byte; reinstall preserved that same file. The final installed app matched the built executable's SHA-256.

The desktop recovery probe launched the real desktop mode, confirmed that the Windows taskbar was hidden, sent the emergency shortcut, and verified process termination plus an inactive lease. The original primary-monitor work area was restored exactly (2880×1656 before, 2880×1680 during, 2880×1656 after). The ordinary desktop probe also passed: battery morph reached 1 and returned to 0, its right edge moved 0 pixels, no extra animation window appeared, and normal exit restored the work area.
