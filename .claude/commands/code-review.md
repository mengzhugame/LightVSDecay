---
description: "执行严格的代码审查 (Code Review)，检查 C# 性能规范与架构耦合"
---

# Code Review 审查标准

请作为一名资深的 Unity 架构师，审查我指定的代码或当前分支未提交的更改。
必须严格对照 `CLAUDE.md` 中的规范进行检查，重点关注以下红线：

1. **性能雷区**：检查 `Update` 或高频循环中是否存在 `GetComponent`、`Find`、`new WaitForSeconds` 或引发装箱/拆箱的 GC Alloc。
2. **时序安全**：所有涉及延迟或计时的逻辑，是否正确区分了 `Time.deltaTime` 和 `Time.unscaledDeltaTime`。
3. **架构解耦**：模块间是否发生了强耦合？是否可以通过 `GameEvents.cs` 的事件总线来替代？
4. **封装性**：变量暴露是否优先使用了 `[SerializeField] private`，有没有滥用 `public`。

审查结束后，请直接列出【致命错误(Blocker) - 必须修复】、【改进建议(Warning)】和【架构优化(Info)】，如果代码存在致命错误，请直接给我提供修复后的代码片段。
