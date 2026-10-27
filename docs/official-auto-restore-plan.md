# Official update with local restoration

User correction: official stable PCL updates must be downloaded and patched locally by the launcher. A prebuilt modified release is no longer the primary update source. Existing single-EXE distribution, portable cache, vanilla-only creation, official login, component modification, bin/bak replacement and rollback constraints remain.

## Design

The official source is Meloong-Git/PCL's latest commit affecting 最新正式版.zip. Fetch the ZIP by immutable commit URL, extract only Plain Craft Launcher 2.exe into the launcher's update directory, and inspect it without executing it. The runtime carries a .NET Framework-compatible dnlib adapter and the current injected hooks/runtime bytes. New candidates receive the same download restoration, portability and official-auto-update implementation before replacement.

Automatic compatibility is structural, not a version/hash whitelist: a normalized method-body contract for the critical patch sites must match the known official implementation, allowing the assembly/display version to advance. Every anchor must remain unique. Preserve raw input and its SHA256; reject incompatible structure without producing or installing an unmodified launcher. Post-validate the rewritten image, original login/component/modpack bodies, input identity/version, vanilla navigation/loader and embedded future auto-updater. Record source commit/input hash/modified hash and method fingerprints. Unknown future structural changes are not promised compatible.

Reuse the existing replacement transaction. All original ZIP, raw image, candidate image, diagnostics and backup files remain beneath ExeDir/PCL/Cache/Update. No runtime SDK, compiler, extra DLL, or temporary updater EXE. Embed fixed dependencies and load in memory. New builds remove local debug paths; new commits use the repository's noreply identity. Previously published Git history is not rewritten without the outstanding authorization.

## Tasks

1. Share the adapter between CLI and runtime via a multi-target net8/net48 AdaptationCore library. Add a compatibility contract, dynamic version/source metadata, and postconditions. Keep CLI's pinned-input mode for reproducible baseline builds. Test official baseline, synthetic newer compatible version, bad/ambiguous anchor, modified protected methods and repeated adaptation rejection.
2. Add official commit/ZIP parsing, exact-member extraction, size/path guards and semantic version/source comparison. Test official metadata, malformed metadata, missing/duplicate/unsafe archive entries, oversize limits and unchanged current version.
3. Embed the adapter in the runtime, stage official images, adapt locally and bind the transaction to locally produced SHA256. Retain user confirmation and scheduling settings. Reject every failure before moving the running EXE; preserve the transaction rollback and cache rules.
4. Build revision 3, run existing metadata/transaction regressions and patcher checks plus auto-adaptation tests. Download the real official ZIP, patch and validate it headlessly. Verify the next output carries its own adapter/runtime so a second generation can patch again. Under the user's later rule, delegate GUI checks to gpt-6.1-terra. Use an isolated simulated-old-profile fixture to trigger the real current official download, local patch, production replacement and WPF restart; label this scope accurately.
5. Update build/usage/change/verification docs. Publish the source and a bootstrap r3 release after checks; the existing r2 adapted-feed updater can install this bootstrap once. Thereafter compatible official versions are patched locally; incompatible structure retains the working version and reports the need for a newer adapter.
