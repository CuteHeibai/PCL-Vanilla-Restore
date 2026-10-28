# PCL restoration progress

Current r6 removes the floating button, separate window and custom vanilla installer. Both normal and silent fallback modes use the original PCL download location, BAML/controls and McInstallStart workflow. Direct page restoration remains preferred. Fallback transplants validated native page bodies from the current launcher only when page shape, resources, host and native dependencies match; otherwise retain the working EXE.

- [x] Failing then passing original-location, missing-dependency, changed-resource and generic-member regression tests.
- [x] Cross-version native-fallback remapping without old self AssemblyRefs; output reread and donor-method verification; fingerprints for every transplanted method.
- [x] Full build: 26 binary +17 adaptation +52 updater =95 checks, zero warnings/errors. Obsolete custom-installer tests and probes removed along with that production code.
- [x] Independent review: generic member attributes guard fixed and verified; no remaining reproducible findings.
- [x] Real official package/two-generation probes and privacy scan. Terra verified final normal and synthetic fallback native pages with no extra button/window; fallback installed26.3 in isolated QA and native PCL recognized it. Original folder/version selection restored; no game launch/login.
- [ ] Documentation, r6 single-EXE publication and public-byte/bootstrap verification.

Candidate SHA256:0994f51e5825424c460cb7eaf18874a4b10a345cf5fd3258bf188aef3af96262. Existing r3/r4/r5 users need manual r6 replacement for this changed integration. Fresh Microsoft authorization, forced refresh, power loss and another computer's complete workflow remain untested. Never distribute QA/runtime data or rewrite old public history.
