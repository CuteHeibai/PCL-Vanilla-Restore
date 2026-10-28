# Native PCL dialogs and current official update verification

- [x] Inventory every added window/message. Separate installer already removed in r6; remaining system MessageBox calls were Entry notices/confirmations and Hook worker errors.
- [x] Add failing tests for the real Hook native bridge, then route all messages through actual ModMain.MyMsgBox with PCL buttons, warning style, wait/result behavior and UI dispatch. Native API signature guard and binary no-system-dialog checks.
- [x] Full100-check build, independent review, privacy and real official ZIP consecutive-generation restoration.
- [ ] Terra verify native current-version notice/confirmation and real official ZIP download, local adaptation, PowerShell replace/restart, native download and existing game preservation in isolated QA.
- [ ] Document limits (official latest remains2.99.0.1), publish r7 single EXE and verify public bytes/bootstrap.
