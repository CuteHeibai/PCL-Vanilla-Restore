# PCL vanilla restoration progress

Current r4 implementation includes independent vanilla download in the single EXE. Official updates prefer native restoration; changed native download methods use independent mode only when host startup, portability and updater contracts remain compatible. Host-incompatible updates retain the current usable EXE and its independent engine.

- [x] Embedded official catalog, verified client/library/native/assets/logging downloads, legacy asset maps and standard vanilla layout.
- [x] WPF search/category/directory/name/progress/cancel UI with always-available main-window entry.
- [x] Shared native/host compatibility guards; fallback and next-generation inheritance; native mode can resume.
- [x] 99 full-build checks: 24 binary, 10 adapter, 65 updater/installer.
- [x] Official26.3,26.4-snapshot-5,1.5.2 installations; GUI1.8.9 and synthetic fallback1.8.9; PCL recognition and26.3 menu verified.
- [x] Same-volume complete publication, cross-volume exclusive ownership, no existing-version overwrite; staging remains under ExeDir/PCL.
- [x] Independent review, privacy scan and legacy bootstrap candidate compatibility.
- [x] Usage, engine, verification and acceptance docs updated.

r4 EXE SHA256: 6bf2ac269ca700070b4057e2a5d2d049200c43cd39474548a07efd5e39b4423d. Release assets must be published before advancing main/stable. Existing r3 users need a manual r4 upgrade to obtain this newly embedded engine; r2's old feed can bootstrap r4. Unknown future host structures and future Mojang schema changes are not promised compatible.

Original user2.8.2 EXE SHA25682d9044513afe207445df7e6142bb6e017335f600a6879b19ec6016ae440ee12 remains untouched. Never distribute QA/runtime data. Fresh authorization, forced refresh, abrupt power loss and another computer's complete workflow remain untested. Old public author emails are not rewritten; new commits use noreply.
