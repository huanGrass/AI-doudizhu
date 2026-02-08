# 项目 Agent 工作指令（AGENTS.md）
本文档定义本项目当前 AI 协作 / 测试 / 回归 的固定流程。所有规则均为强约束，除非明确说明，否则不得弱化执行。

## 一、基础原则（Ground Rules）
- 所有改动尽量保持功能更完整、可回滚、可验证。
- 凡是需要重复实例化或多处复用的对象（含 UI 与非 UI），必须 prefab 化或以可复用模块形式实现。
- 回复与说明必须使用中文。
- 本地仓库每一次实际改动都必须提交 git，提交信息需清楚描述本次改动内容。
- 若未完成 git commit，不得继续进行下一步修改或分析。
- 每次提交完成后，必须运行 `ci/sync_mirror.ps1`；镜像目录为当前工程目录名 + `_mirror`。
- 若镜像目录未同步，本次提交视为无效。
- 必须安装并启用 Git Hooks：执行 `ci/install_git_hooks.ps1`，确保 `core.hooksPath=.githooks`。
- 当提交包含客户端改动（`Assets/` 或 `ProjectSettings/`）时，`post-commit` 必须自动执行：
  1) `ci/sync_mirror.ps1`
  2) `ci/build_windows_debug.ps1`
- 若客户端自动调试打包失败，本次提交视为未完成，必须修复后重新提交。
