# Architecture

The architectural invariant is a hard boundary between Unreal extraction and Blender processing.

```text
CUE4Parse extractor -> UEFormat/textures/manifest -> Blender add-on
```

This makes failures attributable to one stage and lets each stage be tested independently.

P4 keeps the same boundary: `wuwa2blender blender` only launches Blender against a finished staging directory. The WuWa add-on calls the installed UEFormat importer and writes `*.validation.json` next to the `.blend`.

P5 `run` does not punch a new hole in that boundary. It is a state machine over the existing commands:

```text
ResolveConfig → Doctor → Index → ResolveDependencies
→ Export → ValidateExport → LaunchBlender → ValidateBlend → SaveResult
```

C# still talks to Blender only through `manifest.json` plus the files it lists. Resume state lives in `work/runs/<job-id>/job.json`.

## Core contracts

- `IAesKeyProvider`
- `IMappingsProvider`
- model exporter adapter
- `export-manifest.schema.json`
- `run-job.schema.json`
- material profiles

P6 splits tests so CI never needs game files:

- Unit tests: config, paths, manifest, material profile, run job, self-authored UEFormat header.
- `WUWA_INTEGRATION_TESTS=1`: mount the local install, search/export Jinhsi, check `GoldenInvariants`.
- `WUWA_BLENDER_SMOKE=1`: headless import of `tests/fixtures/ueformat-smoke/` (original geometry).

P7 packaging keeps the same boundary. `tools/pack.ps1` publishes a win-x64 self-contained CLI zip and a Blender add-on zip. GitHub Actions runs restore/build/test, Python syntax/lint, JSON schema validation, then pack; it never sets the integration/smoke env vars and never uploads `work/` or game archives.

See `PLAN.md` for milestone gates.
