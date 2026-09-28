# Agent Guide - EasyToolkit

This repository is the single source of the EasyToolkit modules and the only
place where they are developed and tested. Read [README.md](README.md) first;
its "Developing and testing" and "Compatibility rules" sections are binding.

- One UPM package per module in `Packages/com.easytoolkit.<module>`. Keep
  modules generic: no game code, no project assumptions, no cycles.
- Do not raise minimum `unity` or dependency versions. Guard newer-only APIs
  with `UNITY_*_OR_NEWER` and keep a fallback.
- Keep `.meta` GUIDs stable. Give every new file and folder a `.meta` (let
  Unity create it by opening the project).
- Every behavior change needs tests in the owning package (`Tests/`), or in
  `Assets/Tests` for cross-module integration. Run EditMode and PlayMode tests
  before handing off, and report the exact counts.
- If `Temp/UnityLockfile` exists, an Editor owns the project. Never start a
  second one; use its Test Runner or ask for it to be closed.
- Record user-visible changes in [CHANGELOG.md](CHANGELOG.md) under the
  unreleased version, with migration notes for breaking changes.
- Commit only when asked. Release = version bump in all packages + changelog +
  tag (see README "Releasing").
