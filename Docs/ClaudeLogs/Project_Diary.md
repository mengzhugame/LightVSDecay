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


## 2026-04-21：战略决策转向 —— 爽感集中迭代 + 自费买量验证

### 背景变化（v1.0→v2.0 战略调整）
v1.0 方案（2026-04-20）基于"等 3 天留存数据"决策。但 2026-04-21 发现：
- 微信小游戏平台**不会自动推流**，167 个注册用户全部来自朋友圈/微信群熟人推广
- 人均停留时长 594 秒 ≈ 9.9 分钟（强正向信号，但样本偏见严重）
- 留存数据即使出来也不具统计意义

同时新增 3 条玩家反馈，全部指向"爽感不足"：
1. 极寒光束：减速效果无视觉感知
2. 广域透镜：变粗但"没用感觉"
3. 金色技能：没有"一下清屏"的质变爽感

### 核心决策
1. **撤销"等数据"策略**，转为"改爽感 + 小额买量"混合路径
2. **不等运营反馈**（等了也要先改爽感，本质是拖延决策）
3. 2-4 周集中迭代：Week 1 爽感 → Week 2 留存基建 → Week 3 审核+500元小额买量 → Week 4 数据定夺
4. **止损线**：投入 6 周或 5000 元后仍无明显改善，接 IAA 广告放置，开始下款立项

### 关键认知与踩坑
1. **自动索敌建议被否决**（v1.0 P1-1）：剥离核心玩法"旋转激光"的手感乐趣。**新设计原则 4**：辅助系统必须辅助而非替代核心玩法。改为"激光磁吸辅助 + 敏感度映射"。
2. **爽感的根因不在技能强度**，而在：
   - **视觉反馈不足**（减速看不出、宽激光没感觉）
   - **稀有度与质变脱节**（金色只是数值更高，不是体验质变）
   - **缺少连杀/Build 成型的正反馈系统**（无 FRENZY 状态、无 COMBO 表现）
3. **不是所有反馈都要改**：Ch1 时长是主观感受（9 波 17 次技能升级已密集），开发者自测节奏合理 → 改为新增 Ch0 新手关卡做 A/B 测试，保留主关卡不动

### 文件修改清单
- `D:/Project/GamePlanningDoc/20_项目/02_光与朽项目/01_策划文档/玩家反馈迭代方案_202604.md` — v1.0→v2.0 全面修订（578 行）
- `Docs/ClaudeLogs/分析-Ch1玩家反馈汇总_202604.md` — 同步副本

### 下一步（Week 1 开始做的事）
1. P0-NEW-1 金色技能质变化重设计（3-5 个真正质变级技能）
2. P0-NEW-2 极寒光束 + 广域透镜视觉反馈增强
3. P0-NEW-3 击杀/连杀/FRENZY 爽感反馈系统
4. P0-5 第一波技能流派引导

### 经验沉淀
- 独立开发者最大的敌人是"完美主义拖延"——2-4 周硬止损线很重要
- 熟人反馈有正向价值（样本少但信号真实），但不能用熟人数据决定留存策略
- **买量素材和游戏内容要并行准备**，避免游戏改完后再做素材造成真空期


### 2026-04-21 补充：变现 MVP 策略（流量主未开通期）

**背景**：当前微信流量主未开通（需 DAU ≥ 1000），广告变现暂不可用。

**核心决策**：三层混合变现架构
1. **广告 SDK 代码保留** — 零切换成本
2. **Fallback 降级机制** — 封装统一 RewardManager，广告不可用时自动切分享
3. **主页独立分享任务** — 与广告解耦的长期拉新系统

**关键认知**：
- 买量的隐藏目标是**冲 DAU 到 1000 开通流量主**，不只是测留存
- 分享疲劳、微信限频、审核摩擦是"全部替换分享"方案的致命伤
- 审核时按"广告为主"提交，运行时降级分享是合规做法

**待新增代码**：
- `Assets/Scripts/Logic/Monetization/RewardManager.cs`（统一变现接口）
- `Assets/Scripts/Logic/Monetization/ShareTaskManager.cs`（分享任务系统）
- 主菜单 UI 增加"邀请好友"入口


### 2026-04-21 再补充：v2.1 方案基于用户第 3 轮反馈的关键修正

**认知纠正（3 条 v2.0 错误）**：
1. **金色技能 ≠ 稀有度颜色**，是 Lv5 满级技能的质变形态。Week 1 优先改造 5 个核心技能（连锁反应/极寒光束/广域透镜/分裂激光/聚能透镜）的 Lv5 质变
2. **视觉反馈技术限制**：流体 RT 方式不支持速度残影和 Shader 覆盖，改走纯粒子路线（冰晶粒子、冷雾、怪物染色+发光粒子补强黑油怪物）
3. **连杀不加伤害**：破坏数值平衡。改为纯视听反馈（音效阶梯、暗角、慢动作、COMEBACK 提示）

**手指疲劳根本解法（v2.1 新）**：
- 根源是"10 分钟单一操作"不是"手指累"
- 方案：关卡 **3 段式重构**（3+3+3 分钟，段间 20-30 秒停顿）
- **段间停顿复用已有的无人机三选一系统**（DroneChoiceTipsPresenter / DroneRewardConfig 等）——不是新增系统，只是节奏重排
- 替代原 v2.0 的 "新增 Ch0 新手关卡" 方案

**大招蓄力改造**：
- 计时 + 击杀双因子填充（时间 50% + 击杀 50%）
- 释放时仅自动锁定一个方向（不是全自动索敌）
- 对敌人分层伤害，保留挑战性

**CPS 合作方案**：
- 不立即开启（版本差会被算法永久降权）
- Week 1-2 改爽感 → Week 3 小额买量验证 → 次留 > 25% 才开 CPS
- CPS 和自费买量是互补而非二选一

**分享变现的清醒认知**：
- 分享不是救命稻草，游戏本身爽才是根本
- 不做"全部替换为分享"（UI改动大、审核摩擦、限频）
- 不做"只加分享按钮"（按钮点击率极低）
- 走三层混合：广告保留 + Fallback 降级 + 主页独立任务

**文档状态**：v2.1 已落地至《玩家反馈迭代方案_202604.md》

---

## 2026-04-21 深夜：v2.2 竞品《我的防线》情报接入 + 战略方案 C 敲定

### 触发事件
- 第 4 位运营反馈到位，核心三点：
  1. "玩法类似《我的防线》但完善度不如"
  2. 美术：怪是黑的一坨，背景深色，只能看见两个眼睛
  3. 操控：左右滑动不舒服，操作靠上方位置灵敏度很高
  4. 宝箱 + 三选一有冗余
- 开发者亲自玩了《我的防线》微信小游戏，确认玩法相似
- 情绪低谷出现："一个人独立做的游戏没法和大公司比"

### 关键情报接入

**《我的防线》真实体量**（点点出海分析文章数据）：
- 厂商：杭州烈酷科技，**网易持股 40%**，创始人是黄峥大学同学
- 海外版《Galaxy Defense》累计流水 **2740 万美元**（1.9 亿 RMB）
- 月流水稳定 286 万美元（持续 10 个月超 200 万）
- 微信小游戏畅销榜 TOP25-28，上线半个月
- 苹果+安卓广告创意合计 **3400+** 条（几百万人民币级买量）
- 关键事实：**烈酷做了 3 款塔防才跑出爆款**（Space Tower → Geometry Tower → Galaxy Defense）

**对手的玩法特征**：
- 靶心 Like：玩家在屏幕下方，敌人从上方下落
- **控制炮台左右平移**射击（多炮台齐射，最多 5 种）
- 4 层养成（装备/芯片/守护者/涂装）
- 单局 5 分钟
- **玩家负评**：通关路径单一，只能刷芯片，后期数值膨胀

### 战略认知校准（v2.2 核心）

**差异化护城河的真实性确认**：
- 《我的防线》=左右平移 + 多炮台齐射 + 只有上方来敌
- 《光与朽》=旋转激光 + 单束集火 + **上方+左右两侧来敌**
- 这是两种根本不同的操作范式，**差异化是真的存在**
- 但差异化**没有被玩家感知**——运营 5 分钟下来觉得"像《我的防线》"
- 侧翼来敌是旋转激光的"战术必然性"——《我的防线》的左右平移炮台应对不了侧翼

**放弃对标爆款体量**：
- 买量规模（3400+ 创意）、美术完善度、4 层养成——独立开发者追不上
- 新目标：**成为"激光旋转射击"细分品类的长尾代表作**
- 月流水几万到几十万人民币即为成功

**方案 C 战略路径（用户 2026-04-21 敲定）**：
```
Week 1-2 改一版（重点：操控+美术+差异化放大+Lv5质变）
  ↓
Week 3 花 500-1000 元自费买量验证
  ↓
Week 3 末 Data Gate：
  ├─ 数据不好 → CPS 合作 + IAA 躺平 + 下款立项（明确止损）
  └─ 数据可以 → 持续完善（商业化 / 海外 / 5 月广州展找投资发行）
```

### Week 1 排序调整（v2.2 重排）

**新前置的 3 条任务**（运营反馈驱动）：
1. **P0-NEW-5 操控灵敏度修复**（极坐标映射 + 敏感度滑块）——立项差异化的命门
2. **P0-NEW-6 侧翼来敌差异化放大**（前 3 波 40% 比例 + 预警 UI）——让差异化在前 2 分钟被感知
3. **P0-NEW-4 美术黑油辨识度补强**（发光粒子 + 眼睛状态机）——第一印象必救

**保留的 v2.1 Week 1 任务**：
- P0-NEW-1 Lv5 满级 5 技能质变（对方短板"路径单一"的反向进攻）
- P0-NEW-2 粒子反馈
- P0-NEW-3 连杀视听
- P0-5 流派引导
- 大招蓄力改造

### 新增的第 5 条设计原则

**独立开发者不硬拼体量，只拼差异化锐度**：
- 选 1-2 个差异化维度做到 S 级（旋转激光手感、侧翼来敌机制、肉鸽构筑深度）
- 其他维度"不拖后腿即可"
- 不要在所有维度都追求 A 级——这是独立开发者的死亡陷阱

### 情绪层面的认知

- **烈酷科技做了 3 款塔防才爆款**——对方也不是一拍即中
- **594 秒人均停留是真数据**——核心玩法能抓住人 10 分钟
- **撤退不等于失败，是止损纪律**——独立游戏 7/10 是这个结局
- **方案 C 既有止损线又有向上空间**——任何结果都有明确下一步

### 文档状态
- v2.2 已落地：《玩家反馈迭代方案_202604.md》（主文档，1231 行）
- 同步至：`Docs/ClaudeLogs/分析-Ch1玩家反馈汇总_202604.md`
- 新增原始情报：`Docs/竞品游戏《我的防线》.txt`（用户提供）
