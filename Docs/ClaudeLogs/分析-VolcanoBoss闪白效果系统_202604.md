# 火山Boss闪白效果系统分析

**日期**：2026-04-01
**文件**：`BossHealth.cs` / `LaserController.cs` / `BaseBossController.cs`

---

## 用户需求

1. 为什么Boss会闪白，与激光受击白色冲突？
2. 为什么body02（岩石护甲）也会闪白？
3. 所有白闪效果只要 body03（熔浆液），body02不要白闪
4. 只有打到核心（眼睛）时，body02和眼睛才闪白
5. 为什么有时攻击Boss没有伤害飘字？护甲被打也要有灰色飘字
6. `redBodyEffect` 是否需要？

---

## 一、现有系统结构（代码事实）

### 1.1 白闪触发链

```
LaserController.ProcessBossBodyHit()
  ├─ isEyeOpen == true  → bossHealth.TakeCoreDamage()  → TriggerHitEffect() ← 触发全身白闪
  └─ isEyeOpen == false → bossHealth.TakeBodyDamage()  → 无 TriggerHitEffect（无白闪）

LaserController.ProcessBossEyeHit()  → bossHealth.TakeCoreDamage() → TriggerHitEffect() ← 触发全身白闪
```

**结论**：白闪只在打到核心时触发（`TakeCoreDamage`）。`TakeBodyDamage`（护甲）不触发白闪。这与用户期望相符，**但问题在于哪些材质被白闪波及**。

### 1.2 白闪波及所有材质（根因）

`BossHealth.CacheBodyMaterials()` 从 `bossController.BodyRenderers` 取所有 `SpriteRenderer[]`：
- 如果 Inspector 里未手动填 `bodyRenderers`，则自动执行 `GetComponentsInChildren<SpriteRenderer>()`
- 结果：**body01（核心）+ body02（岩石护甲）+ body03（熔浆液）全部被缓存进 `bodyMaterials[]`**
- `TriggerHitEffect()` 遍历所有 `bodyMaterials` 设 `LiquidHitIntensity = 1.0` → **全身闪白**

### 1.3 白闪亮度过高

`HitFlashCoroutine` 第一帧直接从 `intensity = 1.0` 开始（最亮），这对有 `LiquidHitIntensity` Shader 的熔浆材质非常刺眼。

### 1.4 白闪与激光命中叠加的视觉冲突

- 激光每帧命中Boss时，每次都会调用 `TriggerHitEffect()`（重置协程）
- 协程从 intensity=1.0 重新开始，持续 0.15s
- 激光持续射击 = 协程持续被重置 = **Boss处于持续最亮白色状态**，看起来一直发白光

---

## 二、伤害飘字问题（Q5）

### 查看代码

- `TakeBodyDamage()` → `ShowBodyDamagePopup()` → `ShowBossShieldDamage()` ✅ 有灰色飘字
- `TakeCoreDamage()` → `ShowCoreDamagePopup()` → `ShowBossCoreDamage()` ✅ 有红色飘字

**理论上两种命中路径都会出现飘字，但实测"有时候没有"的原因：**

**根因：熔岩液怪（LavaPuddle）遮挡了激光**

LavaPuddle 是 `IsStationary == true` 的敌人，在 `DetectPenetrationDamage` 中触发 `break`，中断激光穿透。当熔岩液怪出现在Boss前方时，激光被吸收，未到达Boss → Boss没有伤害 → 没有飘字。

这是正确的机制（LavaPuddle的护盾作用），不是Bug。

**但是** 如果玩家射击角度让激光同时扫到Boss和熔岩液，时序上可能存在单帧漏检，引发偶发的"无飘字"。

> ❓ 还有一个可能：Boss处于特定状态（如Frozen）时，`BossController`的某些状态转换是否会临时禁用碰撞器？如确认有这个情况，需进一步检查。

---

## 三、方案设计（待用户确认）

### 方案A：分层材质（推荐）

在 `BossHealth` 中增加两套材质数组：

| 变量名 | 内容 | 用途 |
|---|---|---|
| `lavaOnlyMaterials[]` | 仅 body03 | 普通白闪（护甲被打时的轻微反馈）→ 目前 TakeBodyDamage 不触发白闪，如需加入则用此组 |
| `allFlashMaterials[]` | body02 + body03 | 核心/眼睛被打时的强烈白闪 |

**效果**：
- 激光打护甲（眼睛关闭）→ 灰色飘字 + **无白闪**（现状保持）
- 激光打护甲（眼睛开启）→ 红色飘字 + **全层白闪**（body02+body03）
- 激光直接打眼睛 → 红色飘字 + **全层白闪**

### 方案B：降低白闪亮度峰值

将 `HitFlashCoroutine` 的起始强度从 `1.0` 改为 `0.6`，减少刺眼感：

```csharp
float intensity = Mathf.Lerp(0.6f, 0f, t);  // 原为 Lerp(1f, 0f, t)
```

### 方案C：避免每帧重置（解决持续白光问题）

在 `TriggerHitEffect()` 加一个最小间隔：

```csharp
private const float MIN_FLASH_INTERVAL = 0.12f;
if (Time.time - lastHitTime < MIN_FLASH_INTERVAL && hitFlashCoroutine != null) return;
```

---

## 四、redBodyEffect 方案（Q6）

**结论：需要且代码已支持。**

`BaseBossController` 已有：
```csharp
[SerializeField] protected GameObject redBodyEffect;
```

在 Charge、Press、Enrage 三个状态下自动 `SetActive(true)`，其余时候 `SetActive(false)`。

**用户方案**：复制 body03（熔浆液层），修改 Shader 参数让它发红光，挂到 Boss 预制体上，在 Inspector 将该 GameObject 拖入 `redBodyEffect` 字段 → **不需要任何代码修改**，直接可用。

---

## 五、代码变更清单（等待确认后执行）

| 文件 | 变更内容 | 优先级 |
|---|---|---|
| `BossHealth.cs` | `CacheBodyMaterials()` 拆分为两组：`lavaOnlyMaterials`（body03）和 `allFlashMaterials`（body02+body03）；`TriggerHitEffect()` 接受参数决定用哪组 | 高 |
| `BossHealth.cs` | 峰值强度从 1.0 降至 0.6（可调参数化到 Inspector）| 中 |
| `BossHealth.cs` | 加最小闪烁间隔防止持续白光（≈0.1s cooldown）| 高 |
| `BossHealth.cs` | `TakeBodyDamage()` 可选：加 body03 轻微白闪（强度 0.3）给玩家命中反馈 | 低/可选 |
| `BaseBossController.cs` | 无需修改（`redBodyEffect` 已实现）| — |

---

*分析完成，等待用户确认方案后开始修改。*
