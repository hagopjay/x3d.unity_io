# x3d.unity_io: the Gobbler's Unity bridge

Import, play, live-link and export X3D in Unity, with a receipt that names every node not drawn. Moved here from `x3d.lab/unity` (x3d.lab #37).

| Path | What |
|---|---|
| `io.x3d.unity/` | the UPM package: `Runtime/Core` (pure C#), `Runtime/Unity`, `Runtime/XR` (AR Foundation), `Editor` |
| `tests/X3D.Core.Tests` | 41 off-device tests (.NET 8, C# 9) |
| `tests/UnityProject` | tiny project with 6 EditMode tests, run headless by GameCI |
| `tools/` | `gate.py` (x3d_mcp XSD + semantic gate), `live_server.mjs`, `test_live.sh` |
| `.github/workflows/` | `core.yml` (free, no secrets), `unity.yml` (needs a Unity Personal licence, see `docs/CI-SETUP.md`) |
| `docs/UNITY-BRIDGE.md` | where / how / why / when, XR matrix, Unity vs Unreal, Keijiro credit |

No Unity install is needed on any of your machines: `docs/CI-SETUP.md` has the one-time licence steps.
No third-party code is vendored (Keijiro Takahashi's projects are credited as inspiration only; licences were not verified).
