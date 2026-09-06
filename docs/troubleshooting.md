# Troubleshooting taxonomy

Failures should be classified before changing code:

- Archive mount / AES
- Mappings / serialization
- Asset path / dependency resolution
- UEFormat export
- Blender UEFormat import
- Texture path resolution
- WuWa material profile
- Armature / morph validation

Every failure should name the stage and keep the staging directory for inspection.

Test gates:

- Default `dotnet test` must not touch game archives. If a test tries to mount Paks, it is a P6 regression.
- Golden search/export: set `WUWA_INTEGRATION_TESTS=1`. Logs stay under `work/exports/p6-golden/` and `work/logs/`.
- Blender import of game assets is not the smoke path. Use `WUWA_BLENDER_SMOKE=1` against `tests/fixtures/ueformat-smoke/`. If UEFormat add-on is missing, `batch_import.py` exits 2.
- CI (`.github/workflows/ci.yml`) must stay green without a game install and without Blender. If pack fails, check `Detex.dll` landed next to `wuwa2blender.exe` and that zip entries do not include `work/` or `wuwa.local.json`.
- Published CLI: run `wuwa2blender.exe --version`, then `doctor` from the extracted folder after creating `config/wuwa.local.json`. Missing `blender/scripts/batch_import.py` means the zip was stripped or the working tree is not the payload.
