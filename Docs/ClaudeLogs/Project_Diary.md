# Project Diary — 光与朽

## 2026-04-16：新手引导双BUG修复（TutorialSpotlightOverlay）

### 问题描述
1. **编辑器/真机 BUG**：在 Hierarchy 中将 `TutorialSpotlightOverlay` 节点设为 inactive 后运行游戏，进入科技树界面时遮罩不显示（挖洞效果不出现）。
2. **真机 BUG**：真机上显示纯黑色遮罩（挖洞不渲染），且点击手指特效位置无任何反应（点击无法穿透遮罩）。

### 根因分析

#### Bug 1：`Awake()` 与 `Show()` 的竞态
- 设计意图：`TutorialSpotlightOverlay` 应在 Scene 中以 **inactive** 状态放置（`EnsureInitialized()` 注释有说明）。
- `Awake()` 末尾有 `if (!debugMode) gameObject.SetActive(false)` 逻辑——目的是防止开发者不小心把节点留成 active。
- **问题**：当节点 inactive 起步时，`Awake()` 不会在 Start 时执行；但 `Show()` 调用 `gameObject.SetActive(true)` 时，Unity **同步触发** `Awake()`。此时 `Awake()` 里的 `SetActive(false)` 把节点重新隐藏，随后 `Show()` 继续执行 `SetMessage/RefreshLayout`，但 GameObject 已经是 inactive，遮罩永远不可见。

#### Bug 2：Shader 加载失败导致射线检测全拦截
- 真机上若 `UI/HoleMask` Shader 未加入 **Always Included Shaders**，`Shader.Find()` 返回 null，`_holeMaskMaterial` 为 null。
- `UpdateShaderHole()` 开头判断 `if (_holeMaskMaterial == null || _target == null) return;`——提前返回，导致 `_holeScreenHalfSize` **始终为 Vector2.zero**。
- `IsPointInHole()` 检测到 `_holeScreenHalfSize == Vector2.zero` 后直接返回 `false`（不在洞内），`IsRaycastLocationValid` 返回 `true`（拦截所有点击）。
- 结果：整个遮罩变成纯黑不透明区域，且所有触摸事件全被拦截，按钮完全无法点击。

### 修复方案

**Fix 1**（Bug 1）：在 `Show()` 中调用 `gameObject.SetActive(true)` 之前，先设 `_showRequested = true` 标志，让 `Awake()` 跳过自隐藏逻辑。`SetActive` 同步调用 `Awake` 完成后，立即将标志复位。

**Fix 2**（Bug 2）：将 `UpdateShaderHole()` 中孔洞屏幕坐标的计算（_holeScreenCenter / _holeScreenHalfSize / cornerRadius）与 Shader Material 赋值解耦。只要 `_target != null` 就进行坐标计算（保证 `IsRaycastLocationValid` 正确工作），仅在 `_holeMaskMaterial != null` 时才向 Shader 写属性。

### 文件修改清单
- `Assets/Scripts/UI/Tutorial/TutorialSpotlightOverlay.cs` — 上述两处修复

## 2026-03-31：Ch2 熔浆液三修 + 波次全面重设计

### 本次解决的问题

**问题1：激光射击岩浆液导致抖动/缩小/掉落经验**
- 根因：`EnemyBlob.TakeDamage()` 对 Stationary 敌人没有豁免
- 修复：在 `isDead` 检查后立即加 `if (behaviorType == EnemyBehaviorType.Stationary) return;`
- 效果：一行代码屏蔽了所有副作用（shader wobble、缩放、Die()、XP掉落）

**问题2：普通怪物无法穿过熔浆液（物理阻挡）**
- 根因：熔浆液使用 Kinematic Rigidbody2D，会阻挡 Dynamic 刚体
- 修复：`EnemyBlob.OnSpawn()` 中 `circleCollider.enabled = true` 后，遍历所有活跃 EnemyBlob，对"一方是 Stationary、另一方不是"的碰撞体对调用 `Physics2D.IgnoreCollision()`
- 注意：使用 `other.Data`（公有属性），不能用 `other.data`（私有字段）

**问题3：经验过多，玩家提前到满级**
- 原因：原版经验值导致 W7 前就达到 Level 20（满级）
- 目标：W9 结束时达到 Level 17（共需 7,600 XP）
- 关键：Splitter 死亡会分裂3个 Slime（type 21），每只给 11 XP → Splitter 实际贡献 44 XP
- 调整后各怪物 xpReward：Splitter 20→11，Slime 20→11，Exploder 16→10，Tank 80→45，Gunner 40→25，EliteExploder 200→110，EliteSplitter 400→220
- Lava_Puddle xpReward 20→0，coinReward 5→0（障碍物不应给奖励）

### 波次全面重设计（Chapter02_01.asset）

按用户要求重新设计出怪顺序：

| 波次 | 新设计 | 关键逻辑 |
|------|--------|---------|
| W1 | 11 Splitters(4+4+3) | 纯教学波 |
| W2 | 4E先出 + 12S延迟 | 自爆铺路，分裂者跟进 |
| W3 | 6E先 + 2T + 10S | 引入坦克 |
| W4 | 6E先 + 2T + 2G + 10S | 引入炮手，利用熔浆掩护 |
| W5 | 8E先 + 2T + 2G + 12S | 规模提升 |
| W6 | **1EE**先 + 7E + 2T + 2G + 10S | 精英自爆怪（type 12）制造大熔浆 |
| W7 | **2EE**先 + 9E + 2T + 2G + 10S | 双精英，熔浆之海 |
| W8 | 8E先 + 2T + 2G + **2ES** + 12S | 精英分裂怪（type 11）登场 |
| W9 | 1EE+9E先 + 1ES + 3T + 3G + 24S | 全怪种，最大强度 |
| W10 | Boss | 无变化 |

预估总经验：~7,785 XP（接近 7,600 目标）

### 踩坑记录
- `behaviorType` 字段在 EnemyBlob 内部直接可用，但 `data` 是私有字段，跨实例只能用 `Data`（public property）
- Lava_Slime 是 type 21（不是直接出波次的怪，由 Splitter 分裂产生），必须把它的 xpReward 也一起降低才能控制经验总量

### 文件修改清单
- `Assets/Scripts/Logic/Enemy/EnemyBlob.cs` — TakeDamage Stationary 豁免 + OnSpawn 碰撞忽略
- `Assets/Resources/Data/MonsterData/Lava_Puddle.asset` — xpReward/coinReward → 0
- `Assets/Resources/Data/MonsterData/Lava_Splitter.asset` — xpReward 20→11
- `Assets/Resources/Data/MonsterData/Lava_Slime.asset` — xpReward 20→11
- `Assets/Resources/Data/MonsterData/Lava_Exploder.asset` — xpReward 16→10
- `Assets/Resources/Data/MonsterData/Lava_Tank.asset` — xpReward 80→45
- `Assets/Resources/Data/MonsterData/Lava_Gunner.asset` — xpReward 40→25
- `Assets/Resources/Data/MonsterData/Lava_EliteExploder.asset` — xpReward 200→110
- `Assets/Resources/Data/MonsterData/Lava_EliteSplitter.asset` — xpReward 400→220
- `Assets/Resources/Data/LevelWaveData/Chapter02_01.asset` — 波次全面重写

## 2026-04-07：Ch3 数据采集系统（V6.0）

### 本次任务
为第三章（极寒虚空）的全部新机制接入 BattleStatistics 数据采集，扩展 CSV 从 97 列至 124 列。

### 修改文件
- **BattleStatTypes.cs**：KillCounter/SpawnCounter 新增 Ch3 六种怪物+冰墙，新增 `Ch3Stats`、`GlacialBossStats` 两个统计器，WaveStatData 新增 27 个字段
- **BattleStatistics.cs**：新增 `_waveCh3`、`_glacialBossStats`、`_waveIceWallPeakCount`、`_turretCurrentlyFrozen` 运行时字段；Update 追踪冰墙峰值和塔冻时长；新增 12 个 RecordXxx 方法；CSV 扩展至 124 列
- **IceShieldController.cs**：TakeDamage → `RecordIceShieldDamageAbsorbed`；OnShieldBroken → `RecordIceShieldBroken`
- **FrostcasterAI.cs**：SpawnIceWalls 末尾 → `RecordFrostcasterCast`
- **EnemyBlob.cs**：TriggerCatalystBurst 起始 → `RecordCatalystBurst`
- **GlacialBossController.cs**：IceWallBuild/FreezeRay/Charge/AbsZero/AbsZeroInterrupted 各插入统计调用
- **TurretController.cs**：FreezeCoroutine 起始/结束 → `RecordTowerFreezeStart` / `RecordTowerFreezeEnd`

### 关键设计决策
- 塔冻时长用 `_turretCurrentlyFrozen` 标志 + `Update` 逐帧累计，而非在 RecordTowerFreezeStart 直接加 duration——避免 QTE 缩短冻结时长导致数据虚高
- 冰刺统计（RecordIceSpikeIntercepted/Hit）已接入接口，调用点通过 IceSpikeProjectile.OnDestroyedByLaser / OnReachedTower 委托触发，当前 GlacialBossController 已用 spike.OnReachedTower += 和 spike.OnDestroyedByLaser += 绑定，需在 IceSpikeProjectile 内部回调时补调 RecordXxx（待 IceSpikeProjectile 实现后由该文件负责调用）
