# PCL vanilla restoration progress

Current r5 reuses PCL's actual version-list loader, mirror/source helpers and NetFile/FileChecker/LoaderDownload engine. Independent UI and vanilla installation planning remain embedded in the single EXE. Official updates still adapt locally; native-download API or essential-host incompatibility retains the working EXE.

- [x] Native official/BMCLAPI source ordering, version/file settings, concurrency/rate/splitting/retry/cancellation reuse.
- [x] 111 full-build checks: 26 binary, 12 adapter, 73 updater/installer; zero warnings/errors.
- [x] Actual native official-preferred1.8.9 and BMCLAPI-preferred1.5.2 installations. GUI26.3 installation and final-byte26.3 CLI installation. Source settings restored after probes.
- [x] Final-byte GUI/list/status smoke; actual official ZIP adaptation and consecutive generation adaptation.
- [x] Shared completed files survive failed batches; version publication and same-name protection retained.
- [x] Independent review with no remaining P1/P2; final recursive privacy scan clear of known personal/test identifiers and JWT/private-key patterns.
- [x] Original user2.8.2 untouched; production remains one EXE with generated data under ExeDir/PCL and selected game output.
- [ ] Publish r5 assets, verify public download and old-client bootstrap, then advance main/stable.

Final EXE SHA256: 8befb2376c0bf941688819b6fde31afcf760525f115dc4767120977e8acbba04. Existing r3/r4 users need a manual r5 upgrade for this changed engine. Fresh authorization, forced refresh, abrupt power loss and another computer's complete workflow remain untested; r4 game-menu/component evidence is historical. Unknown future host/API structures retain the current usable version. Old public author emails are not rewritten; new commits use noreply. Never distribute QA/runtime data.
