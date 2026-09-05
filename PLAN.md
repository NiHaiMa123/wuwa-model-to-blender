# Implementation Plan

> 目标：从用户本机《鸣潮》客户端中选择一个角色资源，自动解析依赖、导出 UEFormat + 贴图 + metadata，再由 Blender 4.5 以无人工重命名的方式导入并生成可继续编辑的 `.blend`。

## 0. 总体原则

### 0.1 不重复造轮子

- Unreal archive / package parsing：优先 CUE4Parse。
- 手工排查与资源路径验证：FModel。
- UE → Blender 几何/骨骼中间格式：UEFormat。
- 鸣潮 Blender 材质/角色 setup：借鉴现有 WuWa Character Setup 项目的知识与使用方式。
- WWMI：作为 frame dump / mod / 对照数据来源，不作为本项目主解包核心。

### 0.2 严格分层

```text
[Game Archive]
     │
     ▼
Extractor              C# / CUE4Parse
     │
     ▼
Export Contract        UEFormat + textures + manifest.json
     │
     ▼
Blender Bridge         Python / bpy / UEFormat add-on
     │
     ▼
WuWa Setup             material / rig / morph / validation
     │
     ▼
.blend
```

Extractor 不能直接依赖 Blender；Blender 也不能去猜 archive 内部结构。二者只通过文件和 `manifest.json` 通信。

### 0.3 游戏版本相关逻辑必须可替换

以下内容禁止散落硬编码：

- UE version override
- AES key 来源
- Mappings 来源
- 角色资产路径规则
- 材质参数名 → Blender socket 映射
- 3.x 前后 shader 差异

全部放到 provider / profile / config。

---

# P0 — 先跑通“人工黄金路径”

## 目标

在写自动化前，确认当前版本至少有一个角色可以按现有工具链完整进入 Blender。

## 操作

1. 选择一个当前客户端中的标准可玩角色作为 golden character。
2. 用 FModel 人工加载 PAK/IoStore、AES、Mappings。
3. 记录角色 `USkeletalMesh` 的 Unreal object path。
4. 手工导出：
   - `.uemodel`
   - Skeleton
   - MaterialInstance metadata / JSON（若导出器支持）
   - 全部相关贴图
   - 一个可选 `.ueanim`
5. 用 UEFormat Blender add-on 导入。
6. 用 WuWa Character Setup 或手工方式把材质恢复到“可作为后续自动化基准”的状态。
7. 记录 invariants：
   - LOD 数量
   - mesh section 数量
   - vertex / index 数量
   - bone 数量
   - material slot 数量
   - morph 数量
   - 贴图依赖数量
8. 把记录写入 `tests/fixtures/README.md`，但**不提交任何实际游戏资产**。

## Gate P0

只有当这个人工路径成立，才进入自动化。否则先定位：AES / Mappings / package version / UEFormat / Blender shader 中哪一层不兼容。

---

# P1 — 工程基础与 `doctor`

## C# 项目

- `Wuwa.Core`
  - `AppConfig`
  - `GameProfile`
  - `ExportManifest`
  - provider interfaces
  - path normalization
- `Wuwa.Extractor`
  - CUE4Parse integration
  - archive discovery
  - package index
  - dependency graph
- `Wuwa.Export`
  - UEFormat export adapter
  - texture export
  - metadata serialization
- `Wuwa.Cli`
  - command routing
  - logs
  - orchestration

## 配置

实现 `config/wuwa.local.json`：

- 本地游戏目录
- Paks/IoStore 目录
- platform / region
- AES provider
- mappings provider
- Blender exe
- 输出目录

## `doctor`

第一条真正可用命令：

```text
wuwa2blender doctor
```

检查：

- 游戏目录存在
- 是否发现 `.pak` / `.utoc` / `.ucas`
- AES provider 是否有数据
- Mappings 是否可用
- CUE4Parse 能否初始化 index
- Blender executable 是否存在
- UEFormat Blender add-on 是否安装
- Blender 版本是否满足要求

输出必须给出机器可读 `result.json` 和人类可读日志。

## Gate P1

在用户机器上 `doctor` 全绿；失败项必须能明确定位，不允许只报 `failed to load`。

---

# P2 — CUE4Parse 读取与资源搜索

## 2.1 Provider 初始化

优先目标 profile：

```text
GAME_UE4_26
```

但必须可由 `GameProfile` 覆盖，不能把它写死在底层类中。

## 2.2 AES

抽象：

```text
IAesKeyProvider
├── LocalFileAesKeyProvider
├── RemoteJsonAesKeyProvider
└── ManualAesKeyProvider
```

要求：

- 不在源码内固化 key。
- remote endpoint 可配置。
- 下载后缓存到 `work/cache/`。
- manifest 只记录 provider/source id 与 hash，不回写真实 key。

## 2.3 Mappings

抽象：

```text
IMappingsProvider
├── LocalUsmapProvider
└── RemoteMappingsProvider
```

同样允许缓存与 hash 校验。

## 2.4 Search index

实现：

```text
wuwa2blender search <query>
```

至少输出：

- object path
- export type
- package
- probable character grouping

初期不要试图一次写出“角色名智能识别器”。先保证 object path 搜索稳定。

## Gate P2

对 golden character：CLI 搜索结果可以定位到与 FModel 人工记录相同的 `USkeletalMesh`。

---

# P3 — 依赖解析与 UEFormat staging

## 3.1 Dependency walker

从目标 `USkeletalMesh` 出发，递归收集：

- Skeleton
- Material / MaterialInstance
- Texture2D
- Morph data
- Physics asset（可选）
- Animation（按命令参数选择）

避免“把整个游戏目录解包”这种低效做法。

## 3.2 输出目录

保持原 UE 资源路径，示例：

```text
work/exports/<job-id>/
├── Game/
│   └── ...原始目录层级...
├── manifest.json
├── warnings.json
└── logs/
```

保留原路径有两个目的：

1. MaterialInstance 对纹理的引用更容易解析。
2. 相同纹理依赖可以去重和缓存。

## 3.3 UEFormat adapter

目标产物：

- `.uemodel`
- `.ueanim`（可选）
- texture files
- material metadata JSON

必须验证 UEFormat 是否完整保留：

- skin weights
- bone hierarchy
- material ranges
- vertex color
- multiple UV channels
- morph targets
- sockets（如果角色依赖）

如果 CUE4Parse 当前 exporter API 与 UEFormat spec 有漂移，做一个**薄 adapter**，不要把自定义序列化扩散到业务层。

## 3.4 `manifest.json`

每个 export job 必须记录：

```text
schemaVersion
jobId
gameVersion
ueVersion
sourceObjectPath
toolVersions
mesh
skeleton
materials[]
textures[]
animations[]
materialParameters
sourceHashes
warnings[]
```

## Gate P3

golden character 的自动导出与 P0 人工导出在关键 invariants 上一致。

---

# P4 — Blender Bridge

## 4.1 不重写 UEFormat importer

首版要求用户安装 UEFormat Blender add-on，本项目通过 `bpy.ops` / Python API 调用。

项目自己的 add-on 只负责：

- job/manifest 入口
- 鸣潮材质恢复
- armature/morph 整理
- validation
- batch save

## 4.2 Headless batch

目标：

```powershell
blender.exe --background --python blender/scripts/batch_import.py -- `
  --manifest work/exports/.../manifest.json `
  --save work/blend/Character.blend
```

## 4.3 Material profiles

不要把节点树写成单一巨大 Python 分支。

使用：

```text
config/material-profiles/
├── legacy.json
└── 3x.json
```

profile 定义：

- MaterialInstance parameter aliases
- base color / normal / mask / emission 识别规则
- alpha mode
- eye / hair / skin 特例
- vertex color channel usage
- node-group name/version

首版可以先生成“PBR 可读材质”，再逐步逼近游戏原始 NPR/角色 shader。

## 4.4 Rig / Face

优先两阶段：

**v0.1**
- 保留原 skeleton
- 骨骼层级正确
- 权重正确
- morph 正确

**v0.2**
- 可选 Rigify / 控制器 rig
- face controls
- eye look controls

不要让“自动 Rigify”阻塞最小可用的模型导入。

## 4.5 Validation

导入后自动检查：

- missing textures
- broken image paths
- zero-weight vertices
- missing armature modifier
- bone count mismatch
- material slot mismatch
- morph count mismatch
- non-manifold 只做 warning，不擅自改 mesh
- normal/UV channel 缺失

## Gate P4

一条 headless 命令可以从 P3 manifest 生成 `.blend`，重新打开无 missing file；golden invariants 与 export manifest 对齐。

---

# P5 — 一键 `run`

实现：

```text
wuwa2blender run --asset <object-path> --save <file.blend>
```

内部状态机：

```text
ResolveConfig
→ Doctor
→ Index
→ ResolveDependencies
→ Export
→ ValidateExport
→ LaunchBlender
→ ValidateBlend
→ SaveResult
```

要求：

- 每个 stage 都有明确 status。
- 可以断点重跑。
- 输入没有变化时可复用缓存。
- 日志带 job-id。
- 失败时保留 staging，不自动删现场。

## Gate P5

新建空 `work/` 后，只给 game path + asset path，可以完整生成 `.blend`。

---

# P6 — 测试体系

## 单元测试

只使用人工构造的小 fixture：

- config parsing
- path normalization
- dependency graph
- manifest serialization
- material profile matching

## 本地集成测试

需要真实游戏安装，因此默认不在 CI 跑。

环境变量/配置显式启用：

```text
WUWA_INTEGRATION_TESTS=1
```

检查 golden character invariants。

## Blender smoke test

headless 导入一个**自建/可合法提交的 UEFormat fixture**，测试：

- add-on 注册
- manifest 解析
- material resolver
- save `.blend`

CI 中绝不上传游戏资源。

---

# P7 — 打包与发布

## Windows CLI

目标：

```powershell
dotnet publish src/Wuwa.Cli -c Release -r win-x64 --self-contained true
```

发布包：

```text
wuwa2blender-win-x64.zip
```

## Blender add-on

单独打包：

```text
wuwa_model_tools.zip
```

## CI

GitHub Actions 只执行：

- dotnet restore/build/test
- Python syntax/lint
- JSON schema validation
- Blender-independent unit tests

若后续能合法获得/自建测试模型，再增加 Blender headless CI。

---

# 风险与对策

| 风险 | 影响 | 设计对策 |
|---|---|---|
| 游戏更新导致 AES 变化 | 无法 mount archive | `IAesKeyProvider` + endpoint/cache，不固化 key |
| Mappings 变化 | package property 解析失败 | `IMappingsProvider`，与 game profile 绑定 |
| UE serialization customizations | CUE4Parse 读不全 | 把 override 限定在 `Wuwa.Extractor` |
| UEFormat exporter/API 变化 | Blender 中间层断裂 | `IModelExporter` 薄 adapter + schema/version 记录 |
| 3.x 材质规则变化 | 模型能导入但渲染错误 | JSON material profile + regression character |
| Character Setup 依赖 Goo Engine | 标准 Blender 无法完全复刻 | v0.1 先 PBR/基础恢复；高级 shader 作为 profile/optional integration |
| GPL 代码复用 | 影响整个仓库许可 | 首版外部依赖边界；复制源码前先确定 license strategy |
| 游戏资源版权 | 仓库不可公开分发资产 | `work/` 全忽略，仅本地提取；CI 使用自建 fixture |
| “找到角色”规则不稳定 | 自动化选错资产 | 第一阶段要求显式 object path，搜索仅辅助 |

---

# v0.1 Definition of Done

v0.1 只有满足以下全部条件才算完成：

1. 用户提供本地游戏归档目录。
2. AES/Mappings 可以由 provider 获取或由用户本地提供。
3. `doctor` 能明确验证环境。
4. `search` 能找到 golden character 的 `USkeletalMesh`。
5. `export` 只解析目标及依赖，不需要整包全部解压。
6. 输出 `.uemodel` + textures + `manifest.json`。
7. Blender 4.5 可自动导入并保存 `.blend`。
8. 骨骼层级、权重、material slot、Morph 等关键 invariants 与人工基线一致。
9. 缺失贴图/材质/骨骼问题必须出现在 validation report，而不是静默成功。
10. 仓库中不包含真实游戏资产、AES key、Mappings dump。

---

# 实施顺序建议

真正开始编码时按这个顺序，不要并行铺太多功能：

```text
P0 人工 golden path
→ P1 doctor
→ P2 search / mount
→ P3 单角色 export + manifest
→ P4 Blender 基础导入
→ P5 一键 run
→ 材质精修
→ animation
→ Rigify / face controls
→ 批量角色
```

**最重要的第一个里程碑不是“做一个漂亮 UI”，而是：同一个角色从 object path 到 `.blend` 能稳定重复跑两次，结果一致。**
