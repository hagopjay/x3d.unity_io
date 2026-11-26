# CI setup: no Unity install on your machines

Two workflows in `.github/workflows/`:

| Workflow | Needs | What it proves | Status |
|---|---|---|---|
| `core.yml` | nothing | 41 pure-C# tests (.NET 8, C# 9) + the x3d_mcp-gated live server (8 checks) | runs now, free |
| `unity.yml` | a Unity Personal licence in repo secrets | the whole package compiles in a real Unity and 6 EditMode tests pass | **skipped until the secrets exist** |

Public repo: GitHub-hosted standard runners are free for public repositories (Likely: GitHub's own billing doc, not re-read here). The Unity job uses a multi-GB container, so it frees disk first; one run is expected to take on the order of 10-20 minutes the first time (Suspected, not measured) and faster with the Library cache.

## The licence, honestly
GameCI's documented route for a **free Personal licence** still needs ONE local step: Unity **Hub** (not the Editor, no project) on any machine to create a licence file. Unity removed pure-online activation for Personal in 2023, and GameCI's docs (v4) still describe this Hub step (read 26 Nov 2026). A thread on the test-runner repo reports a workaround that "still works" as of 29 Apr 2026, but the workaround itself is not written down there. Unity 6 (6000.x) Personal on GameCI is **not confirmed** by anything read; the workflow defaults to 2022.3.62f1 (a GameCI image tag assumed to exist, Suspected) and takes the version as a manual-run input so a 6000.x can be tried without editing files.

### Paste-ready steps (about 10 minutes, once)
1. On any machine with a browser (your Mac works): install **Unity Hub only** from unity.com/download, sign in with your Unity account.
2. Hub, Preferences, Licenses, **Add**, **Get a free personal license**, finish the dialog. (If no file appears, you did not click Add through to the end.)
3. Copy the contents of the licence file:
   - Windows `C:\ProgramData\Unity\Unity_lic.ulf`
   - macOS `/Library/Application Support/Unity/Unity_lic.ulf`
   - Linux `~/.local/share/unity3d/Unity/Unity_lic.ulf`
4. In https://github.com/hagopjay/x3d.unity_io/settings/secrets/actions add three **repository secrets**:
   - `UNITY_LICENSE` = the whole text of that `.ulf` file
   - `UNITY_EMAIL` = your Unity account email
   - `UNITY_PASSWORD` = your Unity account password
5. In Unity Hub, **sign out** (this returns the seat; Hub 3.7+ has no "Return license" button for Personal).
6. Actions tab, workflow `unity`, **Run workflow**. Read the **Unity EditMode results** check.

If a run says the licence is invalid or a seat is taken, repeat steps 2-5; GameCI has a "returning a license" page for stuck seats.

### Limits and risks
- Unity Personal's terms and a password stored as a secret are your call; a dedicated Unity account for CI is safer than your main one (Suspected best practice, not a Unity requirement).
- GameCI is a third-party project; the action is pinned to `@v4`, not to a commit.
- Fork pull requests do not receive secrets, so the Unity job only runs on pushes and PRs from this repo.
- Android / iOS / Quest **player builds** are not in CI yet: iOS needs macOS runners and signing, Android needs the Android modules image. EditMode compile + tests come first.
