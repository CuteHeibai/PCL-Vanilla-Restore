# PCL native download implementation integration

User correction (2026-10-28): both download mechanics and mirror selection must use PCL's implementation. Reuse actual native APIs instead of maintaining a parallel downloader or URL replacement policy.

- [x] Confirm pinned official EXE with REA and decompile ModDownload/ModNet/ModLoader. Cross-check upstream main93ede5d9e638c99fcec853a9ba1eecd91ceee7f5 source through GitHub API.
- [x] Add bridge to native shared version-list loader, source helpers, NetFile/FileChecker and LoaderDownload. Keep version-list and file-source settings separate. Preserve cancellation and batch progress.
- [x] Replace custom HTTP/parallel transfer in production. Native shared files use final library/assets paths so completed files survive batch errors and PCL's normal cross-game reuse can operate. Installer metadata/client/finalization remain isolated.
- [x] Support native v1 catalog: package SHA1 when available, JSON validation otherwise; binary/artifact checksums remain mandatory. Allow existing BMCLAPI origin.
- [x] Guard native API signatures and embedded FileChecker before accepting automatic updates. Add failing then passing contract and failed-batch regression tests.
- [x] Actual native-engine CLI installs: official-preferred fresh1.8.9 and BMCLAPI-preferred fresh1.5.2. Restore original source preferences. All probe changes are on QA copies, never production entry point.
- [x] Final source review, actual GUI install, 111 checks, final-byte official/native probes and privacy scan; documentation updated.
- [x] Published r5 single EXE; verified both uploaded asset digests, public EXE bytes and old r2 production bootstrap download/verification twice. Source/main/stable advanced without rewriting history.
