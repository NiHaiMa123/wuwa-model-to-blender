# WuWa Model to Blender

面向 **《鸣潮 / Wuthering Waves》本地客户端资源 → Blender** 的可复现资产导出管线。

项目目标不是重新实现一套 Unreal Engine 解析器，而是在成熟开源组件之上建立一条适合自动化、可版本化、可验证的鸣潮专用链路：

```text
Wuthering Waves local game archives
        │
        ▼
CUE4Parse-based extractor
        │
        ├── SkeletalMesh / Skeleton
        ├── MaterialInstance / Texture
        ├── Morph Target
        └── Animation (optional)
        │
        ▼
UEFormat exchange layer
  .uemodel / .ueanim / textures / manifest.json
        │
        ▼
Blender 4.5+
        │
        ├── UEFormat import
        ├── WuWa material reconstruction
        ├── normals / UV / vertex-color checks
        ├── armature / morph / face setup
        └── validation
        │
        ▼
character.blend
```

> 当前仓库是 **v0 设计与工程骨架**。`PLAN.md` 定义了实现顺序与验收标准，尚未宣称已经能够一键解包当前游戏版本。

## 为什么采用这条路线

### 1. CUE4Parse 作为程序化解包核心

FModel 非常适合人工浏览、验证资源路径和手动导出，但它本质上是桌面 GUI。对于“给定角色 → 自动找依赖 → 自动导出 → 自动进 Blender”的目标，直接基于 CUE4Parse 建 CLI 比控制 FModel UI 更稳。

### 2. UEFormat 作为 UE 与 Blender 的边界

UEFormat 已经覆盖 Skeletal Mesh 所需的核心数据，例如：

- LOD / 顶点 / 索引
- Normals / UV / Vertex Color
- Materials
- Skin Weights / Bones / Sockets
- Morph Targets
- `.ueanim` 动画

因此本项目不优先自创 FBX/glTF 转换器，而是把鸣潮专用逻辑放在 UEFormat 前后的两端。

### 3. 鸣潮专用价值放在 Blender 侧

通用 UE 模型“能导入”不等于“能正确渲染”。鸣潮角色还需要处理：

- MaterialInstance 参数与贴图语义恢复
- 角色不同版本/时代的 shader profile
- 法线、切线、UV、顶点色校验
- 面部 Morph / Face Rig
- Armature 与可选 Rigify 处理
- 缺失纹理、透明、头发、眼睛等材质特例

社区项目 `Blender-WuWa-Character-Setup` 已经证明这部分值得做成专门工具，本仓库会优先复用知识与接口，而不是复制代码。

## 目标环境

| 组件 | 初始目标 |
|---|---|
| OS | Windows 11 x64 |
| Game | Wuthering Waves 3.6.x 起作为首个验证基线 |
| Unreal data profile | UE 4.26，允许按游戏版本覆盖 |
| Runtime | .NET 10 |
| Blender | 4.5 LTS 优先 |
| Blender import | UEFormat add-on |

版本号是“首个工程基线”，不是硬编码承诺。任何 AES、Mappings、材质规则都必须允许按游戏版本更新。

## 计划中的 CLI

最终希望形成下面的使用方式：

```powershell
# 环境检查
wuwa2blender doctor

# 在资源索引中搜索
wuwa2blender search "Jinhsi"

# 只导出中间资产
wuwa2blender export `
  --asset "/Game/.../Character/..." `
  --out "work/exports/Jinhsi"

# 把已经导出的 manifest 交给 Blender
wuwa2blender blender `
  --manifest "work/exports/Jinhsi/manifest.json" `
  --save "work/blend/Jinhsi.blend"

# 最终目标：一条命令
wuwa2blender run `
  --asset "/Game/.../Character/..." `
  --save "work/blend/Jinhsi.blend"
```

CLI 目前只是目标接口，详见 `PLAN.md`。

## 项目结构

```text
wuwa-model-to-blender/
├── README.md
├── PLAN.md
├── AGENTS.md
├── THIRD_PARTY.md
├── .gitignore
├── Directory.Build.props
├── WuwaModelToBlender.slnx
│
├── config/
│   ├── wuwa.example.json
│   └── material-profiles/
│       ├── legacy.example.json
│       └── 3x.example.json
│
├── src/
│   ├── Wuwa.Core/          # 配置、模型、接口、manifest 契约
│   ├── Wuwa.Extractor/     # CUE4Parse、索引、依赖解析、AES/Mappings provider
│   ├── Wuwa.Export/        # UEFormat/纹理/metadata 输出
│   └── Wuwa.Cli/           # doctor/search/export/blender/run
│
├── blender/
│   ├── addon/wuwa_model_tools/
│   │   ├── __init__.py
│   │   ├── importer.py
│   │   ├── materials.py
│   │   ├── rigging.py
│   │   ├── validation.py
│   │   ├── operators.py
│   │   └── ui.py
│   └── scripts/
│       └── batch_import.py
│
├── schemas/
│   ├── export-manifest.schema.json
│   └── material-profile.schema.json
│
├── tests/
│   ├── Wuwa.Extractor.Tests/
│   └── fixtures/
│
├── tools/
│   ├── bootstrap.ps1
│   ├── build.ps1
│   └── smoke-test.ps1
│
├── docs/
│   ├── architecture.md
│   ├── research.md
│   ├── extraction.md
│   ├── blender.md
│   └── troubleshooting.md
│
└── work/                  # 本地解包结果；永不提交
```

## 配置原则

复制：

```powershell
Copy-Item config/wuwa.example.json config/wuwa.local.json
```

本地配置只保存路径与用户选择，不应把游戏资产、AES key、Mappings 或其他不应公开的数据提交到仓库。

关键设计：

- `game.paksDir`：本地客户端归档目录
- `game.ueVersion`：默认 `GAME_UE4_26`，允许版本 profile 覆盖
- `decryption.aes`：文件 / endpoint / 手工输入 provider
- `decryption.mappings`：本地 usmap / endpoint provider
- `blender.executable`：Blender 路径
- `output.root`：本地 staging/output 目录

## 中间产物规范

每次导出必须产生一个 `manifest.json`，至少记录：

- 游戏版本
- 源 Unreal object path
- mesh / skeleton / material / texture / animation 依赖
- 工具版本
- UEFormat 文件路径
- MaterialInstance 参数快照
- Blender 处理 profile
- warnings / validation 结果

这样 Blender 阶段不需要重新猜测 CUE4Parse 阶段发生了什么，也便于升级后做回归对比。

## 关键开源参考

本项目调研基线：

- CUE4Parse — https://github.com/FabianFG/CUE4Parse
- FModel — https://github.com/4sval/FModel
- UEFormat — https://github.com/h4lfheart/UEFormat
- WWMI-Tools — https://github.com/SpectrumQT/WWMI-Tools
- Blender-WuWa-Character-Setup — https://github.com/fnoji/Blender-WuWa-Character-Setup
- Wuthering Waves AES Archive — https://github.com/Rannytheory/wuwa-aes-archive
- Fmodel-2-Blender-Tools — https://github.com/hysz-01/Fmodel-2-Blender-Tools
- fmodel-mcp — https://github.com/luisep92/fmodel-mcp

详细取舍见 `docs/research.md`。

## 非目标

本仓库不计划：

- 分发鸣潮原始模型、贴图、动画或其他受版权保护的游戏资产
- 内置固定 AES key 并把它们提交到 Git
- 绕过反作弊、在线注入或运行时修改游戏
- 以 WWMI frame dump 作为主要“客户端资源解包”方案
- 重写完整 Unreal Engine serializer

## License

仓库骨架暂不选择许可证。原因是后续是否直接复用 GPL 代码会影响整体许可策略。

在确定依赖边界前，优先把 GPL Blender add-on 当作**外部依赖**，而 C# 核心只调用/实现自己的接口。详见 `THIRD_PARTY.md` 和 `PLAN.md`。
