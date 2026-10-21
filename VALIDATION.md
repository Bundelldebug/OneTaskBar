# Validation — 0.3.0

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
