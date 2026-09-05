# Research notes — 2026-09-05

## Conclusion

Recommended production architecture:

```text
CUE4Parse CLI/core
→ UEFormat + textures + material metadata
→ Blender UEFormat importer
→ WuWa-specific Blender post-processing
```

FModel remains the manual reference/debugging tool. WWMI remains useful for frame-dump/mod workflows, but is not the primary direct archive extraction path for this project.

## Projects reviewed

### CUE4Parse
https://github.com/FabianFG/CUE4Parse

- Programmatic UE4/UE5 archive/package parser.
- Parses the asset families needed for this project, including `USkeletalMesh`, textures and animations.
- Good fit for a headless/CLI pipeline.
- Current upstream build metadata was observed targeting .NET 10 during this research; pin a tested commit/package during P1 instead of relying on `latest` forever.

### FModel
https://github.com/4sval/FModel

- Excellent manual package explorer/exporter built on the CUE4Parse ecosystem.
- Use it to establish the golden path and diagnose package/mapping issues.
- Do not make GUI automation a production dependency.

### UEFormat
https://github.com/h4lfheart/UEFormat

- Purpose-built intermediate format for Unreal extraction workflows.
- Supports mesh LOD data, UVs, vertex colors, materials, weights, skeleton/bones/sockets, morph targets and animation data.
- Provides a Blender add-on, so it is a better boundary than inventing a custom FBX conversion layer.

### WWMI-Tools
https://github.com/SpectrumQT/WWMI-Tools

- Strong Wuthering Waves Blender/mod tooling.
- Primarily associated with frame dumps / model importing / mod creation rather than a clean direct PAK-to-Blender archive pipeline.
- Useful secondary reference and validation source.

### Blender-WuWa-Character-Setup
https://github.com/fnoji/Blender-WuWa-Character-Setup

- Automates WuWa character setup in Blender after `.uemodel` import.
- Encodes valuable WuWa-specific shader, rig and face-control knowledge.
- Some advanced shader workflows depend on Goo Engine; therefore the first milestone here should not make advanced shader parity a prerequisite for correct skeletal import.

### Fmodel-2-Blender-Tools
https://github.com/hysz-01/Fmodel-2-Blender-Tools

- Demonstrates a practical asset pipeline around FModel output.
- Particularly useful design idea: preserve Unreal directory structure and carry model/material JSON + textures together rather than treating `.uemodel` as the only artifact.

### fmodel-mcp
https://github.com/luisep92/fmodel-mcp

- Useful architectural evidence that direct CUE4Parse-backed CLI/export automation is preferable to manipulating the FModel WPF UI.
- Also exposes `.uemodel` / `.ueanim` style export workflows.

### Wuthering Waves AES Archive
https://github.com/Rannytheory/wuwa-aes-archive

- Tracks WuWa AES information across versions/platforms/regions.
- The repository documents UE 4.26 for WuWa and, for newer split/video archives, documents an endpoint/expression pattern that FModel can use to fetch changing keys.
- Design implication: AES must be a replaceable provider, never a hard-coded constant.

## License implications

- CUE4Parse: Apache-2.0 upstream.
- UEFormat: GPL-family upstream.
- Blender-WuWa-Character-Setup: GPLv3 upstream.

Initial architecture should keep external Blender add-ons at a process/add-on boundary. Do not copy GPL implementation code into the C# core without deliberately choosing a compatible project license.
