# GUI Guide

## Distribution

The GUI is portable and has no installer. Copy the complete project folder to another Windows machine and double-click:

```text
Launch-QoderCN-Patcher-GUI.cmd
```

Windows PowerShell 5.1 and .NET WinForms are included with the supported Windows environment. The GUI delegates all patch operations to the same reviewed PowerShell patcher used by the command-line workflow.

## Buttons

- `Inspect`: reads hashes, patch markers, anchors, and installed configuration without changing files.
- `Dry Run`: validates the selected JSON profile and generates a temporary patched Runtime without changing Qoder CN.
- `Install / Upgrade`: asks for Windows administrator approval and applies or upgrades v2.1.
- `Restore latest`: asks for administrator approval and restores the newest verified Runtime backup.
- `Launch Qoder CN`: starts Qoder from the selected installation directory.
- `Refresh profiles`: reloads secret-free JSON files from `configs/`.

## Recommended workflow

1. Keep the default Qoder installation path unless Qoder was installed elsewhere.
2. Select the shared CPA profile or another reviewed JSON configuration.
3. Run `Inspect` and `Dry Run`.
4. Close every Qoder CN window.
5. Select `Install / Upgrade` and approve the UAC prompt.
6. Launch Qoder CN and enter the API Key in Qoder's model settings.

## Security model

- The GUI has no API Key input and does not write credentials.
- JSON profiles containing secret-like properties are excluded from the profile list and rejected by the patcher.
- `Program Files` writes and restores occur only in a separate elevated process after UAC approval.
- The Runtime hash, `app.asar` hash, and minified-code anchors must match the supported build.
- Apply and upgrade operations use verified backups and rollback on patch failures.

## Compatibility

The current release supports only:

- Qoder CN Desktop `0.1.2`
- Qoder CN Runtime / CLI `1.1.31`

If Qoder updates, use `Inspect`. Do not bypass an unsupported hash or anchor error.
