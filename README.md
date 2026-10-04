# PortProxyGUI NG

A lightweight Windows GUI for managing `netsh interface portproxy` rules.

PortProxyGUI NG (Next Generation) is an independent fork of [PortProxyGUI by zmjack](https://github.com/zmjack/PortProxyGUI). The original application and its contributors provide the foundation; [germeshausen](https://github.com/germeshausen) migrated this fork to .NET 10 and added the functional changes described below. The NG suffix distinguishes this fork from the upstream project; it is not an official upstream release.

Fork repository, releases and issue reports: [germeshausen/PortProxyGUI-NG](https://github.com/germeshausen/PortProxyGUI-NG).

This fork focuses on modernization and independent development rather than waiting for upstream changes.

The original [MIT license and copyright notice](LICENSE.md) are retained. The About dialog links to both repositories and identifies the original work and the fork-specific contributions.

> PortProxyGUI NG does not configure Windows Firewall rules.

## Requirements

- Windows x64
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- Administrator privileges for modifying machine-wide portproxy rules

The primary release is distributed as a framework-dependent x64 single executable. A compressed, self-contained x64 single executable is also available for systems without the Desktop Runtime.

For 32-bit Windows systems, a separate `win-x86` build must be produced; the provided x64 executables cannot run on 32-bit Windows. A framework-dependent x86 build requires the matching .NET 10 Desktop Runtime (x86) on a supported Windows version. Alternatively, use a suitable 32-bit-compatible build of the [original PortProxyGUI project](https://github.com/zmjack/PortProxyGUI) with its corresponding runtime requirements; the original version does not include this fork's enhancements.

The WinForms interface follows the current Windows light/dark application theme and refreshes when the Windows preference changes.

## Changes compared with the original version

This fork updates the original PortProxyGUI 1.4.2 codebase by zmjack. Core changes and adjusted functions:

| Area | Change | Benefit |
| --- | --- | --- |
| Runtime | Replaced the .NET Framework 3.5/4.5.1 and .NET 6/8 targets with .NET 10 for Windows. | One current runtime target simplifies building and maintenance. |
| Storage | Replaced SQLite (`config.db`), SQLib and database migrations with JSON using the built-in `System.Text.Json` serializer. | No database engine or third-party runtime packages; configuration is readable and easy to back up. |
| Shared configuration | Moved storage from the user's Documents folder to `%ProgramData%\PortProxyGUI\config.json`. Rules, comments, groups and window settings are shared; no per-user configuration is maintained. | Administrators in different Windows accounts or sessions work with the same saved information. |
| Concurrency | Added a machine-wide exclusive lock-file handle for the application's lifetime. Only one instance can run against the shared configuration. | Prevents competing application instances from overwriting each other's changes; Windows releases the lock after a crash. |
| Safe saving | Writes and flushes a temporary JSON file, then atomically replaces the configuration and retains a `.bak` copy. | Reduces the risk of partially written configuration files and provides a previous-version backup. |
| Rule reconciliation | Compares JSON and Windows rules at startup and on Refresh; missing system rules receive a warning with Delete, Restore and Skip actions. | Makes configuration drift visible without automatically changing Windows rules. |
| Intentional inactivity | Persists `isInactive` when Disable or Skip is used; externally recreated rules are recognized as active again. | Distinguishes deliberately disabled rules from unexpected missing rules, including after restarts. |
| Status display | Replaced the old status images with transparent, DPI-scaled active, inactive and warning symbols, plus explanatory tooltips and a warning count. | Clearer status and better readability in light and dark mode. |
| Import/export | Uses shared JSON configuration files instead of SQLite database files. | Simple, human-readable backups and exchange without a database dependency. |
| Interface | Uses WinForms without Avalonia and follows the Windows light/dark preference. Custom table-header and group-header rendering was required to provide appropriate colors and readable contrast in dark mode. | Fits the Windows appearance without an additional UI framework. The original application also used WinForms. |
| Distribution | Provides x64 single-executable builds: framework-dependent or compressed self-contained. | The small build reuses the installed .NET 10 Desktop Runtime; the larger build includes the runtime for machines without it. |
| Repository | Removed obsolete database migration code and unused assets; generated releases are kept in the ignored `artifacts/` folder. | Less legacy code to maintain and no generated binaries in version control. |

Compatibility note: old SQLite databases are not automatically migrated and cannot be imported as JSON. Active Windows portproxy rules are detected from the system, but comments, groups and disabled rules stored only in an old database need to be transferred separately. Administrator privileges are still required; this remains a Windows-only application.

The executable remains named `PPGUI.exe`, and the configuration directory remains `%ProgramData%\PortProxyGUI` so the display-name change does not move or reset existing data. Portproxy management, comments, grouping and the IP Helper/DNS actions originate from the upstream application; this fork builds on those features.

## Shared configuration

All Windows users share the same JSON configuration:

```text
%ProgramData%\PortProxyGUI\config.json
```

Only one instance can edit the machine-wide configuration at a time. The application keeps an exclusive handle on `portproxy.lock` for its complete lifetime, across Windows users and sessions. A process crash automatically releases that handle. The presence of the file alone does not indicate an active lock; do not delete it to bypass a running instance.

Locking and atomic saving serve different purposes: the lock prevents concurrent application instances, while atomic replacement prevents readers from seeing a partially written configuration. Configuration updates are written to a temporary file, flushed to disk and atomically exchanged. The previous configuration remains available as `config.json.bak`; if the main file contains malformed JSON, the application attempts to load that backup. Manual edits should only be made while the application is closed.

## Rule reconciliation and status

On startup and **Refresh**, the application compares the shared JSON rules with the Windows portproxy configuration. System-only rules are added to JSON; existing system rules update the saved destination while retaining comments and groups. The status column uses transparent, high-contrast symbols in both light and dark mode:

- **Active** (green check): the rule exists in Windows. This indicates configuration presence, not a connectivity or service-health check.
- **Inactive** (gray minus): the rule is absent from Windows and intentionally kept inactive in JSON.
- **Warning** (yellow triangle): the rule exists in JSON but is missing from Windows and has not been acknowledged as inactive. A tooltip and status-bar message explain the warning.

Select warning entries and use the context menu (multiple selection is supported):

- **Delete** removes the missing entry from JSON without changing Windows rules.
- **Restore (from JSON)** recreates the Windows rule using the saved settings.
- **Skip (keep inactive)** keeps the entry in JSON and persists `isInactive: true`, so subsequent refreshes and restarts do not warn again. **Enable** can reactivate it later.

**Disable** also marks a rule as intentionally inactive. If an inactive rule is recreated externally, it becomes active at the next refresh; a later disappearance triggers a new warning. Older JSON files without `isInactive` remain readable; their missing rules initially appear as warnings because previous intentional inactivity cannot be inferred.

The comparison itself does not restore or delete Windows rules. For entries that already exist in Windows, the existing system-to-JSON synchronization remains authoritative for the destination.

## Build

```powershell
dotnet build PortProxyGUI.sln -c Release
dotnet publish PortProxyGUI\PortProxyGUI.csproj -p:PublishProfile=win-x64-singlefile
dotnet publish PortProxyGUI\PortProxyGUI.csproj -p:PublishProfile=win-x64-selfcontained-singlefile
```

The application does not require third-party runtime packages.

Both release variants can be generated with:

```powershell
./PortProxyGUI/publish.ps1 -SelfContained
```

## Development checks

The reconciliation, inactive-state persistence and status icons can be checked without modifying Windows rules or the shared configuration:

```powershell
dotnet run --project tests/RuleReconciliation -c Release -- artifacts/status-icons-preview.png
```

The fork name, About attribution, repository links and light/dark dialog layout can also be verified without touching shared configuration or Windows rules:

```powershell
dotnet run --project tests/AboutRendering -c Release -- artifacts
```
