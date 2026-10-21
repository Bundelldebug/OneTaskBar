# Optional developer notification identity

The normal installer does not request administrator rights. It uses the Windows notification center by default. Choose the custom center for its calendar, but an unpackaged EXE cannot read Windows toast notifications through UserNotificationListener.

A production notification listener needs an appropriately signed Windows package identity and user consent. v0.3.0 does not include such a signed package. Do not redistribute the developer unsigned MSIX as a normal end-user installer.

For local development only:

1. Build and install the application with `Build.ps1` and `Install.ps1`.
2. Run `python tools-package.py` with Python 3 to create an identity-only test MSIX. It has no third-party dependencies.
3. From the source directory run `Enable-Notifications.ps1 -DeveloperRegistration`. If Windows rejects executable registration with an access-denied/admin error, explicitly rerun Windows PowerShell as administrator and repeat that command. The script does not automatically elevate or change certificate trust, developer mode, or security policies.
4. Launch the registered app from the shortcut printed by that script, select the custom notification center, and press its Windows notification access button. Windows alone controls that permission.

The manifest declares userNotificationListener, runFullTrust, bluetooth, and radios for the existing custom-center/device integration. Consent can be revoked in Windows Settings. To remove the local identity, close One TaskBar and run `Get-AppxPackage OneTaskBar.Desktop | Remove-AppxPackage` for that user. The EXE installation and data remain separate.

Microsoft documentation: [unsigned packages are for testing](https://learn.microsoft.com/en-us/windows/msix/package/unsigned-package), [grant identity to a desktop application](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/grant-identity-to-nonpackaged-apps).
