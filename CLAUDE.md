# LightVSDecay (光与朽) - Claude Code 宪法文档

## 🎯 项目目标
本作是一款结合了“轻度射击（激光塔防）+ Roguelike”元素的休闲解压2D小游戏）。核心体验为无惩罚的宽容机制与极其强烈的割草爽感。

## 🏗️ 目录架构与索引
*   `Assets/Scripts/Core`：游戏核心状态机、事件总线 (`GameEvents.cs`)。
*   `Assets/Scripts/Logic`：技能逻辑、波次管理、战斗核心逻辑。
*   `Assets/Scripts/UI`：游戏内所有UI表现。
*   `Assets/Scripts/Data`：游戏数据配置与存档系统。
*   `Assets/Scripts/VFX`：特效与反馈表现。

**📚 策划案与参考文档指针**（不要在此重复写策划案细节，按需读取）：
*   所有最新的系统策划案（体力系统、技能重构、数值平衡等）均位于外部绝对路径：`/Users/joye.wang/Projects/GamePlanningDoc/20_项目/02_光与朽项目/01_策划文档/`。
*   如需修改具体系统，请先读取对应的策划案文档。

## ⚙️ Unity C# 代码规范 (严格执行)
1. **序列化**：所有需要在 Inspector 面板中调整的变量，必须使用 `[SerializeField] private`，严禁使用 `public` 暴露变量破坏封装。
2. **命名规范**：
   *   类名、方法名：`PascalCase` (如 `GameManager`)
   *   私有变量：`camelCase` (如 `moveSpeed`)
   *   常量、静态只读：`UPPER_SNAKE_CASE`
3. **架构解耦**：禁止跨模块强耦合引用。各模块（UI、Logic、Data）之间通信，**必须优先使用 `Core/GameEvents.cs` 的事件总线机制**。
4. **性能与内存规范**：
   *   **对象池 (Object Pooling)**：游戏中大量生成的实体（如怪物、激光、掉落物、特效）严禁频繁 `Instantiate/Destroy`，必须使用对象池系统。
   *   Update 中严禁使用 `GetComponent`、`Find` 等耗时操作，必须在 `Awake` 或 `Start` 中缓存。
   *   协程中避免每帧 `new WaitForSeconds`，必须提前缓存。
   *   任何计时器、延迟逻辑，**必须考虑 TimeScale 的影响**（如 UI 弹窗时的暂停必须使用 `Time.unscaledDeltaTime`）。
5. **异常防御 (Unity 特色)**：
   *   谨慎使用 `?.` 操作符处理继承自 `UnityEngine.Object` 的对象，因为 Unity 有重载的“伪 Null”，优先使用 `if (obj == null)` 判断。
   *   对所有外部传入的 GameObject 或组件进行判空保护，避免 NullReferenceException。

## 🚀 核心工作流
1. **修改前**：深刻理解需求，如有涉及全局的系统（如技能树），先从外部文档读取其设计规则。
2. **执行中**：单次任务范围保持极简（原子化修改），不要为了修复一个 Bug 去随意重构无关的类。
3. **完成时**：必须撰写有意义的 Git Commit，且每次 Commit 后必须执行 `git push` 以防代码丢失。

## 📝 对话记录规则
每次对话结束时，必须将本次完整对话内容（含用户提问与 Claude 回复）以 Markdown 格式追加保存至：
`D:\Project\GamePlanningDoc\20_项目\02_光与朽项目\04_AI对话记录\Claude对话记录.md`
- 每次对话以 `## [日期] [简短主题]` 作为二级标题分隔，日期格式为 `YYYY-MM-DD`。
- 如目标文件不存在，则创建并写入。
- 不要删除文件中已有的历史记录，只能追加。