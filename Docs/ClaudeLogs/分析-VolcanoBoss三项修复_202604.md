# 熔岩火山 Boss 三项修复分析

> 日期：2026-04-01

---

## 问题 1 — 汲取融合技能全链路修复

### 现状 & 根因

| 现象 | 根因 |
|---|---|
| 粘液怪贴着 Boss 全身堆积 | `SetOverrideTarget(this.transform)` 指向 Boss 根节点 Transform，粘液怪与 Boss Rigidbody 发生物理碰撞 |
| Boss 被越推越远 | 粘液怪 Rigidbody2D 与 Boss Rigidbody2D 在同一物理层发生碰撞，持续施加推力 |
| 无缩小死亡动画 | `AbsorbedByBoss()` 直接调用 `ReturnToPool()`，没有动画过渡 |
| 无蒸汽特效/音效 | `AbsorbedByBoss()` 内部没有 VFX/SFX 调用 |
| Boss 血条无缓冲回血效果 | 现有血条 buffer 动画仅针对掉血（solid 先降，buffer 慢追），没有专门的回血动画分支 |

### 修复方案

**步骤 1：用户操作**
- 在 Boss Prefab 的 `VisualRoot` 下新建一个椭圆 GameObject，命名 `AbsorptionPoint`
- 挂 `Collider2D`（椭圆形），**勾选 Is Trigger**，不添加 Rigidbody
- 此节点位置放在 Boss 顶部火山口处

**步骤 2：VolcanoBossController.cs**
- 新增 `[SerializeField] private Transform absorptionPoint`，Inspector 拖入椭圆节点
- `ExecuteSummonBehavior()` 改为 `enemy.SetOverrideTarget(absorptionPoint ?? transform)`
- 召唤后对每只粘液怪调用 `Physics2D.IgnoreCollision(bossCollider, slimeCollider)` 消除物理推力
- 距离检测改为检查到 `absorptionPoint.position` 的距离（而非 `this.transform.position`）

**步骤 3：EnemyBlob.cs — AbsorbedByBoss()**
- 将 `AbsorbedByBoss()` 改为启动协程：
  1. 0.4s 内 Scale 线性缩到 0（视觉缩小消失）
  2. 在最小 Scale 时播放蒸汽 VFX（`VFXPoolManager.Instance.PlayEnemySteam()`）和死亡音效（`AudioManager.Instance.PlayEnemyDie()`）
  3. 协程结束后调用 `ReturnToPool()`
  4. 期间禁用碰撞体防止重复触发

**步骤 4：HUDPanel.cs — Boss 血条回血动画**
- 在 `OnBossHealthChanged` 里检测 HP 是否上涨（`normalizedHP > bossCurrentHP`）
- 回血时：`bossBloodBuffer.fillAmount` **立即跳到新值**（红色先亮），而 `bossBloodFill` 用协程慢追上去，形成"先亮红底再填实"效果
- 掉血时保持原有逻辑（solid 先降，buffer 慢追）

---

## 问题 2 — Boss 眼睛是否需要旋转/拆分

### 结论：**不需要旋转，不需要拆分左右眼**

理由：
- 当前 `BossEyeController` 做的是眨眼动画（squint/open），通过 Sprite 替换 + Scale 变化实现
- 双眼在同一张图上对眨眼逻辑没有任何影响，两眼同步眨眼反而体现"庞然巨物"的对称感
- 旋转眼球在游戏语境里没有意义（火山不是有机生命体，眼睛不用追踪方向）
- 若未来需要左右眼独立行为（如被激光打歪一只眼时单独受损），届时再拆分，现在保持一张图最省事

**本次无需代码修改。**

---

## 问题 3 — 冰冻蓝色染色污染 Body03

### 现状 & 根因

- `BaseBossController.Awake()` 创建 `FrostDebuff` 组件并调用 `frostDebuff.SetTargetRenderers(bodyRenderers)`
- `bodyRenderers` 若未手动在 Inspector 配置，会自动 `GetComponentsInChildren<SpriteRenderer>()` 抓取所有子节点，**包括 Body03 的 SpriteRenderer**
- `FrostDebuff.UpdateColorTint()` 每帧对所有 `targetRenderers` 写入蓝色 tint（通过 `SpriteRenderer.color`）
- Body03 使用 `WobblyLiquidSprite` Shader，HDR 颜色通过 `material.SetColor("_Color", ...)` 控制。SpriteRenderer.color 作为顶点色乘数叠加其上，蓝色乘橙色 HDR = 难看的灰绿色

### 修复方案

在 `VolcanoBossController.OnBossInitialized()` 末尾，重新设置 FrostDebuff 的目标渲染器，**排除 body03Renderer**：

```
// 伪代码
var filtered = bodyRenderers.Where(r => r != body03Renderer).ToArray();
frostDebuff.SetTargetRenderers(filtered);
```

这样冰冻减速的蓝色只影响 Body01（内部流体层）和 Body02（外壳层），不碰 Body03 的 HDR 材质。

---

## 修改文件清单

| 文件 | 修改内容 |
|---|---|
| `VolcanoBossController.cs` | 吸收点 Transform、忽略碰撞、排除 Body03 frost tint |
| `EnemyBlob.cs` | `AbsorbedByBoss()` 改为动画协程 |
| `HUDPanel.cs` | 血条回血分支动画 |
| 策划文档 `熔岩火山Boss动作与技能总览.md` | 同步以上三项修正 |

> 无需修改：`FrostDebuff.cs`、`BossHealth.cs`、`BaseBossController.cs`（最小化改动原则）
