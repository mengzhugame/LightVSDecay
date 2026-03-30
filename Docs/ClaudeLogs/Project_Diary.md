# Project Diary — 光与朽

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
