# Architecture

The architectural invariant is a hard boundary between Unreal extraction and Blender processing.

```text
CUE4Parse extractor -> UEFormat/textures/manifest -> Blender add-on
```

This makes failures attributable to one stage and lets each stage be tested independently.

## Core contracts

- `IAesKeyProvider`
- `IMappingsProvider`
- model exporter adapter
- `export-manifest.schema.json`
- material profiles

See `PLAN.md` for milestone gates.
