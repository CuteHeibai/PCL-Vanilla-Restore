# Embedded portable updater

`src/RestoreRuntime/RestoreRuntime.csproj` builds `PCLRestore.Runtime.dll` for .NET Framework 4.8. The launcher embeds the DLL and loads its bytes. The runtime also embeds the pinned dnlib 4.5.0 metadata reader and its MIT license. Both libraries load in memory; no DLL is extracted and no downloaded candidate code is loaded or executed.

The public synchronous bridge API is `PCLRestore.Runtime.Entry.Initialize(object[])`, `CheckUpdates(object[])`, `CheckSilently(object[])`, and `MarkReady(object[])`. Initialization receives the executable path, four-part upstream version string, and integer patch revision. Other methods receive an empty object array. Return values are boxed booleans. Run check methods on a worker thread: dialogs use the WPF dispatcher, and a successful helper handshake requests application shutdown on that dispatcher. Call `MarkReady` after the actual main window has finished loading.

## Update contract

The default feed is `https://raw.githubusercontent.com/CuteHeibai/PCL-Vanilla-Restore/main/update/stable.json`. A feed must contain `schemaVersion: 1`, a four-part `upstreamVersion`, a nonnegative integer `patchRevision`, an HTTPS `artifactUrl`, and 64 hexadecimal characters in `artifactSha256`. Optional source metadata is tolerated. Versions compare upstream version first, patch revision second; downgrades and equal releases are not installed.

Downloads go to `ExeDir\PCL\Cache\Update\candidate.bin`. Before installation the runtime checks SHA-256, MZ/PE executable flags, managed assembly identity, assembly version, and the embedded `PCLRestore.Profile.json` resource. The resource must match feed version/revision and contain `vanillaDownload: true`, `portableTemp: true`, and `updaterProtocol: 1`. dnlib reads candidate metadata and resources as data without loading the candidate into the CLR. Unknown, malformed, unavailable, or incompatible feeds never fall back to raw upstream packages.

Every updater-owned artifact stays under `ExeDir\PCL\Cache\Update`: settings, lock, candidate, staged backup, `previous.bak`, failed images, PowerShell script, transaction JSON, readiness JSON, and log. There is no system-temporary-directory fallback.

Optional `settings.json` in this directory:

```json
{
  "checkAutomatically": true,
  "promptBeforeInstall": true
}
```

Missing or malformed settings retain these defaults. The official launcher scheduling/configuration remains the caller's responsibility; `CheckSilently` adds this local automatic-check opt-out. Manual checks always ask before downloading/installing. Background checks ask by default; explicitly setting `promptBeforeInstall` to false permits unattended installation of a fully validated adapted package.

## Replacement transaction

The runtime and helper share an exclusive file lock. The runtime also refuses a second transaction while a recorded helper process is live. Built-in Windows PowerShell is started hidden, with no execution-policy override. A session/PID handshake must arrive before the launcher shuts down, so blocked script execution leaves the current application running.

The script waits for the requesting PID to exit (up to 120 seconds), obtains the lock, checks the pending session and both file hashes again, stages the current executable inside the updater directory, promotes it to `previous.bak`, and moves `candidate.bin` to the exact original executable path. It starts that same path with its containing directory as the working directory. No temporary PCL executable or helper executable is created.

The main-window-ready call writes a GUID/session plus expected-image hash. The script requires both values to match and rechecks the installed image hash. A failure while replacing, starting, or waiting for readiness restores the original bytes at the original path and restarts them. The prior backup survives an initial locked-image failure. Successful updates retain `previous.bak`. A rollback may stop only the exact process that the script started for the failed candidate; it never searches for or kills another PCL process. The normal readiness timeout is 90 seconds.

## Verification (2026-10-26)

Commands run from `E:\Pcl2\PCL-Vanilla-Restore`:

```powershell
dotnet build tests/UpdaterTests/UpdaterTests.csproj -c Release --nologo -v:q
& .\tests\UpdaterTests\bin\Release\net48\UpdaterTests.exe
```

Release build: **0 warnings, 0 errors**. Console tests: **32 passed, 0 failed**.

- Schema/hash/version/URL rejection, revision ordering and downgrade rejection, unknown feed, and unavailable fetch.
- Generated PE fixtures: valid adapted image, wrong assembly name/version, missing profile, wrong revision, absent vanilla adaptation, unknown updater protocol, and a DLL disguised with an `.exe` extension. The candidate fixture includes a file-writing `Main`; no such marker is created during validation.
- Real built-in Windows PowerShell executes the generated transaction script against disposable fixture bytes: successful replacement, start failure, readiness timeout, wrong session, wrong expected hash, replacement failure after moving the original, tampered candidate, locked original with an existing backup, and exclusive-lock contention. Paths include spaces, Chinese characters, and apostrophes. Fixture launch/readiness code is substituted only in the test-generated script; production has no testing bypass.
- Settings defaults/disable flag and session/hash-bound readiness acceptance/rejection.
- Empty assembly identity and repeated candidate validation. The original CLR reflection-only approach rejected a fixture with an empty assembly name; the public release probe also exposed an AppDomain loading restriction. Candidate validation now uses dnlib and can retry without caching candidate assemblies in the CLR.

The first test run failed all 16 original schema/transaction assertions against explicit unimplemented stubs. Four settings/readiness assertions then failed before their implementation. A further regression reproduced loss of an existing backup when the current executable was locked; it now passes after staged-backup handling. The PowerShell integration run also exposed unavailable `Get-FileHash` in the inherited module environment; production hashing now uses the framework SHA-256 API directly.

The tests write only beneath `tests/UpdaterTests` and never launch user PCL or touch `E:\Pcl2\.minecraft`. They use framework compiler-generated executable fixtures strictly for metadata inspection. No real update download/install, real WPF shutdown/restart/loaded handshake, GitHub publication, machine power-loss recovery, or forced external tampering was performed by this runtime subtask. Machine policy may legitimately prevent PowerShell execution; the updater reports failure without bypassing it. Unexpected filesystem/permission failures can still prevent rollback; the log records the retained backup location.

Final build output: `src/RestoreRuntime/bin/Release/net48/PCLRestore.Runtime.dll`. Generated `bin`, `obj`, and fixture directories are ignored by local `.gitignore` files. Subsequent real-launcher and public-channel checks are recorded in verification.md.
