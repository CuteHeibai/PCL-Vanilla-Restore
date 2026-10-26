# PCL 2.99.0.1 binary adaptation

Authorized plan: preserve official binary login and component management; restore vanilla installation only; deliver one EXE. All launcher-controlled temporary files belong to ExeDir/PCL. Updates use pre-adapted complete images, bin/bak staging and built-in PowerShell, never a temporary PCL EXE. Publish source/adaptation tooling to CuteHeibai/PCL-Vanilla-Restore after validation.

- [x] Preserve pinned official input and verify unmodified rewrite starts.
- [x] Restore vanilla navigation/list/install without enabling modpack installation.
- [x] Embed portable updater and redirect launcher temporary paths.
- [x] Static regression tests and real isolated application/download checks.
- [x] Review, package, source publication and release with accurate verification record.

Published 2.99.0.1-r2. Final EXE SHA256: 0db17d8120beda3175136b34c3a8810d9ae29177516a22776e1505c3a6b387fa. Source and stable feed on main; public production download/metadata verification passed twice. See verification.md for completed runtime checks and the fresh-authentication/forced-refresh boundary.

Existing E:/Pcl2/Plain Craft Launcher 2.exe is 2.8.2, SHA256 82d9044513afe207445df7e6142bb6e017335f600a6879b19ec6016ae440ee12; do not overwrite it or existing .minecraft.
