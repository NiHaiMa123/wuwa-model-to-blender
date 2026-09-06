# UEFormat smoke fixture

Self-authored geometry for P6. **Not** extracted from Wuthering Waves.

| File | Role |
|---|---|
| `SmokeCube.uemodel` | UEFormat v10 quad: 4 verts, 2 tris, 2 materials, 2 bones, 1 morph, 1 UV, vertex colors |
| `T_SmokeDiffuse.png` | 1×1 generated PNG (red) |
| `T_SmokeNormal.png` | 1×1 generated PNG (flat-ish blue) |
| `manifest.json` | Same contract as a P3 export |

Regenerate (deterministic):

```powershell
$env:WUWA_WRITE_SMOKE_FIXTURE = "1"
dotnet test tests/Wuwa.Extractor.Tests --filter CommittedFixture_ExistsAndMatchesWriter
```

Headless Blender smoke:

```powershell
$env:WUWA_BLENDER_SMOKE = "1"
dotnet test tests/Wuwa.Extractor.Tests --filter HeadlessImport_SelfAuthoredUeFormatFixture
```
