# AGENTS.md

本仓库可能由多个 coding agent 协作。所有 agent 默认遵守以下约束。

## Project objective

建立 Wuthering Waves 本地资源 → UEFormat staging → Blender 的可复现自动化链路。

## Rules

1. 先读 `README.md` 和 `PLAN.md`，按当前 milestone 工作，不跨阶段重构。
2. 不提交任何鸣潮原始游戏资产、AES key、Mappings dump 或用户本机路径。
3. 游戏版本相关差异只能进入 provider/profile/config，禁止散落 magic values。
4. C# Extractor 与 Blender Python 之间只能通过稳定文件契约（主要是 `manifest.json`）通信。
5. 不控制 FModel GUI 做生产自动化；FModel 只用于人工验证/排错。
6. 不自创新的模型格式，除非 UEFormat 确认无法承载所需数据并有最小复现证据。
7. 每次改 exporter/importer 都要更新 manifest/tool version，并保留可比较日志。
8. 首要保证 skeleton / weights / material slots / morph / texture references 正确，再做美化 shader 和 Rigify。
9. 任何自动“修 mesh”的操作默认禁止；validator 可以报警，修改必须是显式 operator。
10. 新依赖先检查许可证；GPL 源码不要直接复制进非 GPL core。

## Completion discipline

一个 task 完成前至少回答：

- 输入是什么？
- 输出是什么？
- 有什么可机器验证的 invariant？
- 失败是否会留下足够日志？
- 是否新增了游戏版本耦合？如果有，是否已放入 profile/provider？
