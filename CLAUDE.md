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

# 《光与朽》项目开发全局指令 (Claude Code 核心行为规范)

## 0. 核心原则：文件驱动 (File-Driven)
**绝对不要依赖终端的聊天记录！** 你的终端输出随时会被清空或遗忘。
所有的需求分析、架构设计、逻辑推导和代码修改，**必须**落地为本地文件。终端只用于简短确认和状态汇报。

## 1. 策划与分析阶段 (Planning & Analysis)
当你接收到新的开发需求（如新怪物、新 Boss、新系统）或读取策划文档后：
* **禁止**在终端输出长篇大论的分析结果。
* **必须**在项目的 `Docs/ClaudeLogs/` 目录下（如无该目录请自动创建），生成或更新一个专门的 Markdown 文件。
* 命名规范示例：`Docs/ClaudeLogs/分析-怪物系统重构_202603.md`。
* 只有当文件生成完毕后，在终端提示我：“已将分析结果保存至 [文件路径]，请确认是否按此计划执行修改。”
* 文件内需要有我提到的需求问题描述，还有你回复或分析的过程。

## 2. 编码与执行阶段 (Coding & Execution)
* **禁止刷屏：** 不要把完整的 C# 脚本代码打印在终端里让我复制。
* **直接操作：** 明确告诉我你即将修改哪些文件，得到我的允许后，直接利用工具链读写并修改本地的 `.cs` 文件或配置。
* **防御性修改：** 修改复杂核心逻辑前，先在本地生成一个 `[原文件名]_备份.cs` 或将修改方案写入日志文件。

## 3. 会话记忆与存档 (Memory & Logging)
在每一项重大任务完成，或者我要求你进行总结时：
* 请自动将本次任务的核心决策、踩过的坑、或是对现有代码的重大改动，追加记录到 `Docs/ClaudeLogs/Project_Diary.md` 文件中。
* 以后每次重启会话遇到上下文断层时，你需要优先去查阅这个 Diary 文件以恢复记忆。

## 4. Unity 专属规范
* 严格遵循项目中现有的命名空间（如 `LightVsDecay.Core` 等）。
* 尽量保持日志输出受开关控制（如使用封装好的 `GameLogger` 而不是原生的 `Debug.Log`）。
* 除非我明确要求，否则不要触碰 `Library/`、`Logs/` 以及美术源文件目录下的内容。