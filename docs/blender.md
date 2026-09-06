# Blender design

Blender 4.5 is the documented baseline. P0/P4 golden path was verified on Blender 5.2.1 with the UEFormat add-on `io_scene_ueformat` 0.10.0.

P4 priorities, in order:

1. Import UEFormat reliably.
2. Preserve armature, weights, material slots and morph targets.
3. Resolve images without broken paths.
4. Build a baseline material graph from explicit profile rules.
5. Validate the result against the export manifest.
6. Only then add advanced WuWa shader parity, Rigify and face controllers.

## Headless entry

```powershell
wuwa2blender blender `
  --manifest work/exports/Jinhsi/manifest.json `
  --save work/blend/Jinhsi.blend

# P5 one-command path (still launches the same headless importer)
wuwa2blender run `
  --asset Client/Content/Aki/Character/Role/FemaleM/Jinxi/R2T1JinxiMd10011/Model/R2T1JinxiMd10011.R2T1JinxiMd10011 `
  --save work/blend/Jinhsi.blend
```

This launches:

```text
blender.exe --background --python blender/scripts/batch_import.py --
  --manifest <manifest.json>
  --save <file.blend>
  --profile config/material-profiles/3x.json
  --report <file.validation.json>
  --pack
```

`--factory-startup` is intentionally not used: the UEFormat add-on is a user extension and must stay enabled.

P7 ships `wuwa_model_tools.zip` for Install from Disk. That zip includes a bundled `profiles/3x.json` so GUI import can find a default profile without the git checkout. Headless `wuwa2blender blender` still passes `--profile` from the CLI payload (`config/material-profiles/3x.json`). The WuWa add-on does not replace UEFormat; both are required.

## Contract

Blender never opens game archives. It only reads:

- `manifest.json` from a P3 export
- files listed there (`.uemodel`, textures, material JSON)
- `config/material-profiles/3x.json` for parameter-name → socket mapping

The WuWa add-on calls the installed UEFormat importer (`UEFormatImport` / `UEModelOptions`). It does not parse `.uemodel` itself.

## P4 material policy

`3x.json` is a PBR-readable baseline, not game-accurate NPR:

- `MainTex` / `PM_Diffuse` → Principled Base Color
- `PM_Normals` / `Normal_Roughness_Metallic` → Normal Map, DirectX Y flip
- `EM` / `HeightLightMap` → Emission
- hair / bangs use hashed alpha from Base Color
- outline / HET / face-shadow slots are skipped on the LOD0 mesh

Packed images are stored in the `.blend` so a reopen does not depend on the staging directory still existing.

## Validation

`*.validation.json` records mesh/armature stats, missing images, and warnings. Non-manifold edges and zero-weight vertices are warnings; the importer does not auto-edit mesh topology.

Jinhsi golden Blender invariants live in `GoldenInvariants` (C#): LOD0 40662/56483/169449, 7 slots, 86 morphs, 204 armature bones. Cooked skeleton length 196 is an export invariant, not the Blender bone count.

P6 Blender smoke uses `tests/fixtures/ueformat-smoke/` (self-authored UEFormat v10 quad + two 1×1 PNGs). It exercises add-on import via `batch_import.py`, manifest parsing, the 3x material resolver, pack/save, and reopen-clean. It does not use game archives. Enable with `WUWA_BLENDER_SMOKE=1`.
