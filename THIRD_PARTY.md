# Third-party strategy

本文件只记录工程边界，不替代各上游项目的正式许可证文本。

| Project | Intended role | Initial integration strategy |
|---|---|---|
| CUE4Parse | UE archive/package parsing | NuGet / external dependency in C# extractor |
| CUE4Parse-Conversion | UEFormat / texture / material export adapter | NuGet `1.2.2.202609`, same Apache-2.0 CUE4Parse repo; thin wrapper in `Wuwa.Export` |
| FModel | Manual inspection and golden-path debugging | External GUI tool; no UI automation in production |
| UEFormat | Model/animation exchange + Blender importer | External exporter/importer dependency; record version in manifest |
| WWMI-Tools | Frame-dump/mod reference | Reference/secondary diagnostic path only |
| Blender-WuWa-Character-Setup | WuWa shader/rig knowledge | External optional add-on / reference first |
| Fmodel-2-Blender-Tools | Asset-pipeline architecture reference | Reference only unless compatible code reuse is deliberately licensed |

## Licensing rule

CUE4Parse currently uses Apache-2.0, while UEFormat and Blender-WuWa-Character-Setup use GPL-family licensing. Before copying source code from any GPL project into this repository, explicitly decide whether this repository (or the affected distributable) will adopt a compatible GPL license.

Until then, prefer process/add-on boundaries and documented APIs over copying implementation code.
