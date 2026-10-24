# Code signing policy

Production signing is **not activated**. There is no approved signing account or usable publisher certificate in this workspace. On 2026-10-24 the maintainer reported that the Foundation application was declined. The reason has not been supplied. A local unsigned build passing a virus scan or running once does not establish publisher trust. No signed production release is available until the checks below pass.

The [SignPath Foundation](https://signpath.org/) application is declined. The prepared signing workflow remains inactive; no credentials or certificate have been issued. A future signing provider would need separate enrollment and approval before that workflow can be activated.

## Roles and approval

Project maintainer, reviewer and signing approver: [Bundelldebug](https://github.com/Bundelldebug). External contributions must be reviewed before signing. Repository and signing-service access must use MFA. Each signing request needs maintainer approval under the provider's rules. Keys stay with the signing provider; no private key or certificate trust is installed by this program.

After provider approval, the project attribution will be: “Free code signing provided by SignPath.io, certificate by SignPath Foundation”. This is a planned attribution, not a claim of current sponsorship.

## Release checks

1. Build from reviewed public source on a GitHub-hosted Windows runner. Signing is unavailable to pull requests and unconfigured accounts.
2. Sign the main EXE, recovery EXE and the three shipped PowerShell scripts. Preserve those bytes when packaging.
3. Build Setup containing that signed payload, then sign Setup. A signed outer installer does not replace inner signatures.
4. Pin the approved publisher certificate thumbprint. `tools/Test-ReleaseSignatures.ps1` requires valid embedded Authenticode signatures, timestamps and matching product/version metadata. It does not install roots, change policy or add exclusions.
5. Run the signed main self-test and Setup extraction, compare all 19 allowlisted payload files with dist and the portable ZIP, validate checksums and perform the official Defender scan. Test normal execution on the target PC with Smart App Control enabled before deployment. Repeat signature checks after packaging.
6. Check out the clean source commit recorded in the signed CI candidate's `candidate-origin.json`, and point the version tag at that commit. Use `tools/Publish-Release.ps1` with the matching existing version tag and reviewed release notes. Missing/invalid signatures, runtime failures, changed artifacts or mismatched files stop publication. `-ValidateOnly` performs checks without contacting GitHub or requiring a tag to exist yet. The origin record is bookkeeping; the provider's GitHub origin verification and publisher signatures supply the signing trust.

Development `Build.ps1` and `BuildInstaller.ps1` still produce unsigned test artifacts. They do not constitute a production release. Provider certificate rotation requires updating the pin after reviewing the new certificate; it is not silently accepted.

The 9.0 stable candidate uses numeric file version `9.0.0.0` and product version `9.0.0`. Both are passed separately to the signing configurations and compared across all executables. The signed publication gate requires the exact `v9.0.0` tag. It also supports explicit beta product versions as prereleases without replacing the latest stable release. No unsigned stable release is authorized merely by changing version metadata; that distribution would require a separate maintainer decision with the unsigned status disclosed.

The normal Windows build workflow compiles and runs the self-test without signing secrets. The manual `signed-candidate.yml` workflow runs only on this repository's main branch on a GitHub-hosted runner. It requires the `release-signing` environment, the approved provider configuration and `SIGNPATH_API_TOKEN`. Environment variables are `SIGNPATH_ORGANIZATION_ID`, `SIGNPATH_PROJECT_SLUG`, `SIGNPATH_POLICY_SLUG`, `SIGNPATH_PAYLOAD_CONFIG`, `SIGNPATH_SETUP_CONFIG` and the reviewed `PUBLISHER_THUMBPRINT`. Configure maintainer approval in GitHub and SignPath before enabling requests. Referenced actions are pinned to reviewed commit IDs. No signing request is sent when configuration is missing, and the workflow has no release-publication permission. The provider-specific signing path cannot be exercised until enrollment is approved.

Windows policies, certificate revocation, antivirus definitions and SmartScreen reputation can change. These checks reduce known risks and prevent accidentally releasing unsigned production files; they do not promise zero vulnerabilities or unconditional execution on every PC. [Microsoft signing guidance](https://learn.microsoft.com/en-us/windows/apps/develop/smart-app-control/code-signing-for-smart-app-control).

## Privacy

The signing service receives public CI source/build information and this project's artifacts, not user settings, logs, notifications, recordings or installed OEM images. The payload has an explicit allowlist.

At runtime, app layouts, recent app IDs/timestamps, notification data and optional Phone Link metadata are processed locally. The optional YouTube artwork feature is off by default and requires consent; it looks up the exact playing title locally and requests a public image by video ID from `i.ytimg.com`. It does not send browser titles, cookies or history rows. Original notification/app/Windows actions may open their own network services when the user selects them. Product icons are read from installed applications and are not redistributed.

The optional notification identity is currently a documented unsigned local-test package. Production identity/package signing and publisher details must be reviewed with the selected provider; an EXE signature alone does not turn that development package into a production-signed MSIX. Windows consent and registration checks remain in force.
