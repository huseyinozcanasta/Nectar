# Nectar — Windows Taskbar Safety Watch (experimental)

An **independent, marker-gated safety process** for Windows 10. It helps restore the native Windows taskbar if Nectar exits unexpectedly while the taskbar is hidden. No administrator permission, network, paid dependency, or external runtime installation is required on a standard Windows 10 machine with .NET Framework 4.x.

**Status:** The safety decision logic has passed 8/8 self-tests. It has also passed idle/coexistence and genuine *graceful* Nectar exit/restart tests on DELIFISEK (Windows 10, 1920×1080, one monitor). **A real forced termination/crash has not been tested**, nor have Windows 11, multiple monitors, Explorer crash, or computer restart.

## Design and safety requirements

- Every 2 seconds, check for a valid `%APPDATA%\com.kua8.nectar\taskbar_hidden.flag` with `abstate=0..3`.
- While any `nectar.exe` process is alive, **never modify Windows taskbar**.
- After 4 consecutive missing-process checks (approximately 8 seconds), if `explorer.exe` and `Shell_TrayWnd` exist, re-check the absence of Nectar immediately before attempting recovery.
- Restore the saved appbar state, undo transparent/layered taskbar styles, show the taskbar and restore its rectangle.
- Use Windows `ABM_GETTASKBARPOS` for positioning. If that fails, use a conservative primary-monitor bottom fallback **only for single-monitor setups**; skip unverified multi-monitor geometry.
- Delete the crash marker only when the taskbar is visibly back on-screen. Keep the marker for later retry if recovery cannot be verified.
- Never terminate Explorer, kill Nectar, modify user settings, access the network, or require elevated permissions.
- Prevent duplicate watchers with a named mutex. A separate `--stop` command signals graceful watchdog shutdown.
- Logs: `%LOCALAPPDATA%\NectarSafetyWatch\watchdog.log`, rotated after 256 KB, with no application or account content.

## Build from source (Windows PowerShell)

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

This invokes the built-in Windows .NET Framework C# compiler (`Framework64\v4.0.30319\csc.exe`) and produces:
- `NectarTaskbarSafetyWatch.Test.exe` — console self-test binary;
- `NectarTaskbarSafetyWatch.exe` — windowless monitor executable.

The build script runs `--self-test` and checks all eight decision cases before producing the windowless build.

## Local deployment

Copy the binary to `%LOCALAPPDATA%\NectarSafetyWatch\NectarTaskbarSafetyWatch.exe`. A per-user `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` entry named `NectarSafetyWatch` launches `"<exe path>" --watch` on logon. No system service is installed. This component can be used independently of the Nectar UI application.

To stop it and remove this autostart entry, use the supplied **`uninstall-safety-watch.ps1`** script. Its optional `-RemoveProgramFile` flag deletes the watchdog executable *after* it has stopped. Logs are intentionally preserved. Nectar itself is not uninstalled or changed.

## Validation evidence on DELIFISEK — 2026-11-02

1. C# build and SHA256: `f3ed9ab791099c228dcc8e2f50c1d039c7d516b9b09dc0bb39cc44a39d201256` for v0.1 windowless binary.
2. Decision self-tests: 8/8 passed. Real marker `abstate=0` found; Nectar was alive, taskbar was hidden.
3. Idle start/stop: watcher started and stopped normally, with zero unintended taskbar changes.
4. Resident monitor: per-user logon Run key installed, only one watchdog process, approximately 17–20 MB working set initially.
5. Safe coexistence: normal Nectar close removed marker, native taskbar reappeared, and after 10 seconds with Nectar absent no recovery action was logged. Nectar restarted; marker reappeared and taskbar was hidden again.
6. Evidence JSON reports in `Documents\Nectar-Update-Staging\test-results\phase1\`: `safety-watch-install.json`, `watchdog-graceful-exit-coexistence.json`.

## Future work / limitations

- A simulated or VM-based crash test should force-kill Nectar, assert the marker persists and the taskbar is restored without relaunch, verify the original appbar state and restore on next logon. **Do not run this destructive test in a user's active Windows session.**
- A hung but still-running `nectar.exe` is intentionally not automatically killed or overridden.
- Multi-monitor behavior requires a robust secondary-taskbar geometry restoration strategy and separate tests.
- This is an experimental safety companion; not a guarantee of recovery from every Windows/Explorer failure or power outage.
