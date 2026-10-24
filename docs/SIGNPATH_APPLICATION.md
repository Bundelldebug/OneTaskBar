# SignPath Foundation application record

**Declined, as reported by the maintainer on 2026-10-24.** The reason has not been supplied. The form was submitted earlier that day with approved terms/privacy consent, but receipt did not establish provider approval, account activation or certificate issuance. Contact information and the confirmation screenshot are not published. No new application or appeal has been submitted.

- Project: One TaskBar
- Repository: https://github.com/Bundelldebug/OneTaskBar
- Existing releases: https://github.com/Bundelldebug/OneTaskBar/releases
- License: MIT, including original project artwork. Third-party notices are in THIRD_PARTY_NOTICES.txt.
- Maintainer / reviewer / proposed signing approver: Bundelldebug
- Contact email: **provided privately in the application form; not published here**
- Signing policy: https://github.com/Bundelldebug/OneTaskBar/blob/main/docs/CODE_SIGNING.md
- Requested service: free open-source code signing for Windows GitHub releases.

## Project description

One TaskBar is a per-user Windows desktop dock, launcher and control center, implemented in C#/.NET Framework WPF with Win32 and WinRT integration. It offers independent app layouts, search, media controls, quick settings and an optional notification center. A separate recovery helper restores the original taskbar when the app exits or crashes. It is independent of Microsoft and Samsung.

The normal installer does not request administrator rights. Startup, layout and notification choices are controlled by the user. Optional notification integration requires Windows registration/consent and currently uses a clearly documented local-test identity; production identity signing needs review with the selected provider. The app does not disable antivirus, Smart App Control, UAC, security policies or certificate validation, and does not install trust roots or exclusions.

Own Windows EXEs and installer scripts are the requested signing targets. Framework/OS libraries are system dependencies, not included as project-owned signing targets. Samsung product images are read from installed applications at runtime and are not included in the source or release payload. Public CI builds must be tied to the reviewed source commit, and requests must require maintainer approval.

## Outstanding enrollment steps

Confirm maintainer contact information, repository/signing MFA, reviewer/approver configuration and provider eligibility. After acceptance, configure the approved project, certificate, artifact configurations and CI secret; pin its verified publisher thumbprint. Review the optional production notification-package identity separately. Then create a signed candidate, perform local functional/security checks with protections enabled and publish only through the gated release command.
