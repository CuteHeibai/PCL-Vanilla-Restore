# Inline native fallback implementation plan

Goal: remove the separate download window/button and silently recover the original PCL download location, UI and installer when direct page patching is incompatible.

Architecture: native-compatible input still uses RestoreVanilla. Otherwise require host/download APIs plus identical page type contracts and original WPF/core resource bytes; transplant the previously validated native page bodies from the current launcher into matching candidate definitions. Remap every local reference to the candidate; missing dependencies reject the update. Both paths must have native list, navigation and vanilla-only hooks, and call the original McInstallStart workflow. Keep portable paths/update transaction/protected login and component methods.

- [x] Add failing regression for restored list/navigation/install hooks after page-body mismatch, missing page dependency rejection, resource mismatch rejection and second-generation inheritance.
- [x] Implement guarded native page transplantation and remove all separate-window hooks/messages. Remove the former custom installer, obsolete tests/probes and update binary checks.
- [x] Full95-check build, real official two-generation adaptation and privacy scan; independent review found generic member attributes gap, fixed with failing/passing regression and reviewer confirmation.
- [x] Terra checks normal and synthetic fallback in the original download location, no extra button/window, real26.3 vanilla installation and PCL recognition. Original folder/version selection restored, no game launch.
- [x] Privacy scan, docs and r6 publication; server asset hashes/public EXE match final, old r2 production module downloaded and verified public r6 twice. Main/stable advanced after assets, history/tags retained.
