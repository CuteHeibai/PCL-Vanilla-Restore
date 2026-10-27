# PCL vanilla restoration progress

Current implementation: r3 bootstraps from pinned official 2.99.0.1, then follows official stable packages and restores compatible binaries locally before replacement. Preserve official login/Client ID and installed-version component management; new installations remain vanilla-only. Distribution requires one EXE. Update artifacts remain under ExeDir/PCL/Cache/Update with bin/bak staging and system PowerShell; no temporary PCL updater EXE.

- [x] Shared net8/net48 adapter, method compatibility contract and source/output binding.
- [x] Official commit/ZIP source, strict extraction and version/source comparison.
- [x] Embedded future adapter inheritance; in-memory dependencies without runtime SDK.
- [x] Fail-closed staging and unchanged existing replacement/rollback transaction.
- [x] 21 binary checks, 8 adapter tests and 52 updater regressions; real official download and second-generation restoration.
- [x] Terra GUI smoke and full official-download/local-restoration/replacement/restart test in isolated simulated-old-profile fixture.
- [x] Recursive release-binary privacy scan and r2 bootstrap compatibility validation.
- [x] Usage, update-contract, verification and user-acceptance documentation.

r3 EXE SHA256: fdc4027b085845057fa786f9700d3be4d7c62c5883f87ff1bfbb8b62aa67c51e. Publication order: source branch, release assets, then main/stable bootstrap feed. Future unknown structural changes must be rejected, not installed unmodified.

Prior published r2 SHA256: 0db17d8120beda3175136b34c3a8810d9ae29177516a22776e1505c3a6b387fa. Existing user 2.8.2 EXE SHA256: 82d9044513afe207445df7e6142bb6e017335f600a6879b19ec6016ae440ee12; never overwrite the original launcher or existing game data.

Fresh Microsoft authorization, forced token refresh, power-loss recovery and another computer's full GUI workflow remain untested. New modules no longer carry local PDB paths. Old public commit author emails are not rewritten without the outstanding authorization; future commits use the repository noreply identity. Never distribute runtime account/configuration/log folders with the release EXE.
