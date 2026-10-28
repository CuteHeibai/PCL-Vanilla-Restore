# Independent Vanilla Fallback Implementation Plan

**Goal:** Keep vanilla installation available through an embedded independent engine when native restoration fails.
**Architecture:** Runtime-contained catalog/planner/verified-transfer/installer and WPF window. AdaptationCore distinguishes full native compatibility from stricter host-only integration compatibility; both inherit runtime and updater.
**Tech stack:** C#, net48, WPF, dnlib, Mojang HTTPS JSON, existing console tests.
**Spec:** docs/superpowers/specs/2026-10-28-independent-vanilla.md

## Global constraints
- One distributed EXE, no SDK/sidecar requirements; all staging under ExeDir/PCL.
- Only vanilla installation, preserve authentication/component management.
- No overwrite of existing versions or user saves; no raw unintegrated EXE update.
- Browser/UI via gpt-6.1-terra only.

## Tasks
- [x] Add failing engine tests: official-shaped manifests/plans, OS rules/native classifiers, asset layout, hash failure, cancellation, safe paths and existing-version protection. Implement VanillaModel/VanillaInstaller and injected transport for deterministic tests.
- [x] Add independent WPF window and stable main-window button, async install/progress/cancellation. Keep public Entry bridge simple and no new user dependencies.
- [x] Add adaptation tests for native-method changes→independent mode, host changes→reject, inherited runtime, and mode metadata. Implement fallback without relaxing host contract.
- [x] Build r4, run existing/new suites, inspect binary/privacy, run actual official independent install. Terra tests independent UI and PCL recognition/game launch where available.
- [ ] Review code, document exact evidence/boundaries, publish source/r4 EXE and bootstrap stable feed after artifact publication; verify public bytes.
