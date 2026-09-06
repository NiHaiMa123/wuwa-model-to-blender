# Extraction design

Planned extraction flow:

1. Resolve local config and game profile.
2. Discover PAK/IoStore files.
3. Resolve AES and mappings through providers.
4. Initialize CUE4Parse.
5. Search/resolve an explicit `USkeletalMesh` object path.
6. Walk only the required dependency graph.
7. Export UEFormat, textures and material metadata while preserving UE paths.
8. Emit a versioned manifest and warnings.

P3 `export` walks only the target `USkeletalMesh` plus Skeleton / MaterialInstance / Texture2D / Morph references, writes UEFormat staging under `work/exports/<job-id>/`, and emits `manifest.json`. Animation is opt-in and not part of the P3 default.

P4 `blender` consumes that `manifest.json` only. It does not remount archives or walk dependencies again.

P5 `run` is the orchestrator: it calls doctor, resolves the asset (object path or search alias), reuses the P3 export pipeline and P4 Blender launch, and records every stage in `work/runs/<job-id>/job.json`. Export staging is reused when the source fingerprint (canonical object path, AES hash, mappings hash, LOD/anim flags, tool version) and on-disk files still match. Failures leave `work/exports/` and `work/blend/` in place.

P6 local integration (`WUWA_INTEGRATION_TESTS=1`) remounts the user's install, searches Jinhsi, exports to `work/exports/p6-golden/`, and asserts `GoldenInvariants`. Default unit tests never call CUE4Parse against archives.

P7 does not change extraction. The published CLI zip still talks to Blender only through `manifest.json` plus listed files. Release artifacts must not contain `work/`, AES material, mappings dumps, or game packages.

Do not extract the entire game as a prerequisite for one character.
