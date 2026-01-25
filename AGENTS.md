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

## 二、测试 + UI 评审流程（Test + UI Review Loop）
### 适用范围
以下改动视为 UI 相关改动：
- UI Prefab
- Layout / Anchor
- 颜色 / 字体 / 样式
- UI 相关脚本中影响显示的逻辑

### 固定流程
1) 运行 Unity 批处理截图命令：
   - `Unity.exe -batchmode -quit -projectPath "<project>" -executeMethod BatchTools.UIBatchRunner.RunAll -logFile "<project>\\Logs\\ui_batch.log"`
2) 查看生成的截图文件：
   - `Screenshots/ui_shot_1.png`
3) 若 UI 显示不正确：
   - 仅允许进行小范围调整（布局 / 颜色 / Anchor）。
   - 不得同时引入无关逻辑改动。
   - 调整后必须重新运行截图命令并对比结果。


## 三、规则 / 逻辑测试（Rule / Logic Tests）
- 规则与核心逻辑测试使用 Unity Test Framework（EditMode / PlayMode）。
- 以下改动视为核心逻辑改动：
  - 游戏规则
  - 状态机
  - 回合流程
  - 判定逻辑

### 强制规则
- 每一次核心逻辑改动，必须新增或更新至少 1 个测试。
- 若测试失败：
  - 不得继续修改其他模块。
  - 必须优先修复测试，直至通过。

## 四、一键检查流程（One Click Flow）
本项目所有改动的最终验收，统一通过 `ci/one_click_check.bat` 完成。

### 强制要求
- 产物必须包含：
  - `Logs/playmode-results.xml`
  - `Screenshots/ui_shot_1.png`
  - `report.md`（禁止手动修改）
- `one_click_check` 任一阶段失败，本轮工作视为未完成。
- 未通过一键检查，不得进入下一轮修改。

## 五、总约束声明（重要）
- 本文档中的“必须 / 不得 / 否则视为失败”均为硬约束。
- 所有流程设计的目的：
  - 改动可追溯
  - 行为可验证
  - 回归可自动执行
- 不得以“为了加快进度”为理由跳过任何流程步骤。
