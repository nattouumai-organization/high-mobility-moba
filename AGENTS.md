# Repository Guidelines

## Project Structure & Module Organization

`Game/` is the Unity project root. Gameplay code lives in `Game/Assets/Game/Scripts/`, grouped into `Characters`, `Combat`, `Core`, `Map`, `Movement`, `Minion`, `Structures`, `Points`, `Runes`, and `UI`. Use `Minion/`; the separate `Minions/` directory is currently empty.

Character ScriptableObjects are in `Data/Characters/`, player prefab variants in `Prefabs/Characters/`, and materials in `Art/Materials/`, all under `Game/Assets/Game/`. Active scenes are in `Game/Assets/Game/Scenes/`; avoid confusing them with the older scenes in `Game/Assets/Scenes/`.

Read `GAME_DESIGN.md` for rules, `TECHNICAL_DESIGN.md` for architecture, and `TASKS.md` for progress. Online networking and Steam integration remain planned work.

## Build, Test, and Development Commands

- Open `Game/` through Unity Hub using **Unity 6000.5.4f1**; allow package restoration and compilation to finish.
- Run locally: open `Assets/Game/Scenes/SC_CharacterSelect.unity` and press Play. Continue through rune selection into the prototype.
- Build: use Unity's Build Profiles for Windows. Preserve the registered scene order: `SC_CharacterSelect`, `SC_RuneSelect`, `SC_Prototype`. No custom build CLI is checked in.
- Validation: run `powershell -ExecutionPolicy Bypass -File scripts/Validate-Unity.ps1 -Mode Compile` for batch compilation and `powershell -ExecutionPolicy Bypass -File scripts/Validate-Unity.ps1 -Mode EditMode` for the automated EditMode suite. The script uses Unity 6000.5.4f1, writes logs and test XML to a temporary output directory by default, prevents concurrent validation of the same project, and fails on timeout, compilation/licensing errors, missing XML, or zero test cases.
- Run `git diff --check` from the repository root to detect whitespace errors before committing.

## Coding Style & Naming Conventions

Follow existing C# style: four-space indentation, braces on separate lines, PascalCase types and methods, and `_camelCase` private fields. Match script filenames to their primary classes. Preserve asset prefixes such as `SC_`, `PF_`, and `M_`.

Keep new balance values in ScriptableObjects, following the README policy. Preserve Unity `.meta` files and serialized references when moving assets or renaming fields. No repository-wide formatter or linter configuration is checked in.

## Testing Guidelines

Unity Test Framework 1.7.0 is installed. Initial EditMode tests live under `Game/Assets/Editor/Tests/` so Unity's predefined Editor assembly can access the existing `Assembly-CSharp` without splitting the game assembly. Do not add an asmdef that attempts to reference the predefined `Assembly-CSharp`; Unity does not generate that reference. Run added tests through `scripts/Validate-Unity.ps1`; a valid run must report at least one test case and every case must pass. PlayMode tests require an explicit isolated-scene strategy and are not part of the initial validation foundation. Use descriptive names such as `SecondTowerDestroyed_EndsMatch`.

Check Console errors before and after changes, and playtest before committing. Verify affected skills, death/respawn, and tower victory behavior. Report untested behavior explicitly.

Validation creates a fresh run subdirectory under the temporary directory or the parent supplied with `-OutputDirectory`; previous XML results are never reused. EditMode validation requires a Passed run, Passed suites and cases, consistent nonzero counts, and zero failed, skipped, or inconclusive cases. Test execution omits `-quit` so the test runner can finish and write results.

## Commit & Pull Request Guidelines

Keep changes focused on one feature. History commonly uses `feat:` and `fix(scope):`, for example `fix(oboro): ...`, with Japanese or English descriptions.

PRs should explain the behavior change, reference relevant tasks/issues, list validation performed, and include screenshots for visual changes. Update `CHANGELOG.md` when changing existing specifications and keep `TASKS.md` accurate.
