# Fixtures / Golden invariants

Do not commit extracted Wuthering Waves assets here.

## P0 golden character — Jinhsi / 今汐 (game 3.6.0)

Recorded 2026-09-06 from a manual FModel 4.4.4 export + UEFormat import into Blender 5.2.1.
No game files, AES keys, mappings dumps, or `.blend`/`.uemodel` are stored in git.

| Field | Value |
|---|---|
| Game version | 3.6.0 |
| Archive layout | `Client/Content/Paks`, 55 `.pak` + 55 `.sig`, no IoStore (`.utoc`/`.ucas`) |
| CUE4Parse profile that mounted | `GAME_WutheringWaves` (UE4.26 family). `GAME_UE4_26` also mounted 55/55 in FModel. |
| FModel mount | 55/55 archives, 2,130,959 files |
| Package | `Client/Content/Aki/Character/Role/FemaleM/Jinxi/R2T1JinxiMd10011/Model/R2T1JinxiMd10011` |
| Object path | `Client/Content/Aki/Character/Role/FemaleM/Jinxi/R2T1JinxiMd10011/Model/R2T1JinxiMd10011.R2T1JinxiMd10011` |
| Unreal `/Game` path | `/Game/Aki/Character/Role/FemaleM/Jinxi/R2T1JinxiMd10011/Model/R2T1JinxiMd10011` |
| Skeleton | `R2T1JinxiMd10011_Skeleton` (same package family) |
| LOD count | 5 |
| LOD0 mesh sections | 7 |
| Skeletal material slots (asset) | 17 (7 body/face/eye/star + outline/HET extras) |
| LOD0 vertices | 40662 |
| LOD0 triangles | 56483 |
| LOD0 indices | 169449 |
| UV channels | 4 |
| Vertex color | yes (`bHasVertexColors`, Blender `COL0`) |
| Morph targets | 86 |
| Cooked skeleton bones (CUE4Parse `FinalRefBoneInfo`) | 196, root `Root` |
| Armature bones (Blender after UEFormat import) | 204, root `Root` |
| Vertex groups (Blender) | 178 |
| Unique texture dependencies | 26 |
| Head bone | `Bip001Head` |
| P2 search queries | `Jinhsi`, `今汐`, `Jinxi`, `R2T1JinxiMd10011` — first two expand via `config/search-aliases.json` |
| P2 expected hit | `Client/Content/Aki/Character/Role/FemaleM/Jinxi/R2T1JinxiMd10011/Model/R2T1JinxiMd10011.R2T1JinxiMd10011` (`SkeletalMesh`) |
| P3 export command | `wuwa2blender export --asset <P2 object path> --out work/exports/Jinhsi` |
| P3 expected staging | `.uemodel` + material JSON + unique textures under preserved UE paths, plus `manifest.json` / `warnings.json` |
| P3 golden check | LOD count 5, LOD0 40662/56483/169449/7, 17 material slots, 86 morphs, 26 unique textures, 196 cooked bones |
| P3 notes | Blender armature bone count 204 is a P4 import invariant, not the cooked `ReferenceSkeleton` length. Cubemaps may land as `.hdr`. |
| P4 import command | `wuwa2blender blender --manifest work/exports/Jinhsi/manifest.json --save work/blend/Jinhsi.blend` |
| P4 expected scene | mesh `R2T1JinxiMd10011_LOD0`, armature `R2T1JinxiMd10011_LOD0_Skeleton`, LOD0 40662/56483/169449, 7 slots, 86 morphs, 204 bones, 4 UV, `COL0`, packed images, reopen with no missing files |
| P4 material profile | `config/material-profiles/3x.json` (PBR baseline; not game NPR) |
| P5 run command | `wuwa2blender run --asset <P2 object path> --save work/blend/Jinhsi.blend` |
| P5 expected job | `work/runs/Jinhsi/job.json` with all stages pass/warn/skipped; same Blender golden as P4 |
| P5 gate | empty `work/` + local config game path + asset path → `work/blend/Jinhsi.blend` |

### Blender golden scene (`手工解包并导入blener.blend`, local only)

UEFormat imported **LOD0 only**:

| Field | Value |
|---|---|
| Mesh object | `R2T1JinxiMd10011_LOD0` |
| Armature | `R2T1JinxiMd10011_LOD0_Skeleton` |
| Vertices / faces / loops | 40662 / 56483 / 169449 (matches FModel LOD0) |
| Material slots on mesh | 7 (Bangs, Hair, Face, Down, Skirt, Eye, Star) |
| Shape keys | 86 morphs + Basis |
| Armature modifier | present |
| Images bound in this file | none (only Blender `Render Result`) |

The missing image bindings are a P0 observation, not a silent success: P4 validation must report unbound textures as errors after import. The automated P4 path packs staging textures into the `.blend` so a reopen has no missing files.

Synthetic/self-authored fixtures may be committed for CI.

## P6 self-authored UEFormat smoke — `tests/fixtures/ueformat-smoke/`

Original 4-vertex quad written by `SyntheticUeModelWriter` (UEFormat v10). No game packages, keys, or mappings.

| Field | Value |
|---|---|
| Object path | `/Game/WuwaSmoke/SmokeCube.SmokeCube` |
| Mesh object after import | `SmokeCube_LOD0` |
| LOD0 vertices / triangles / loops | 4 / 2 / 6 |
| Material slots | 2 (`MI_SmokeHair`, `MI_SmokeBody`) |
| Morph targets | 1 (`Smile`) |
| Bones | 2 (`Root`, `Spine`) |
| UV / vertex color | 1 channel / `COL0` |
| Textures | 2 generated 1×1 PNGs |

`WUWA_INTEGRATION_TESTS=1` runs search + export against the local game install and checks the Jinhsi golden table above. Default `dotnet test` does not mount archives.

`WUWA_BLENDER_SMOKE=1` headless-imports this fixture through `blender/scripts/batch_import.py`. CI must never upload `work/` or game files.

P7 GitHub Actions validates this fixture's `manifest.json` against `schemas/export-manifest.schema.json`, runs default `dotnet test`, and uploads only `dist/wuwa2blender-win-x64.zip` and `dist/wuwa_model_tools.zip`.
