# 极寒之核 Boss 技能系统 · Unity 接入文档

**版本**：v1.0（对应策划文档 v1.1）
**日期**：2026-04-02

---

## 一、新建 Prefab：冰刺投射体

### 1. 创建 GameObject

在 Project 窗口右键 → Create Empty，命名 `IceSpikeProjectile`。

### 2. 添加组件

| 组件 | 配置 |
|---|---|
| `SpriteRenderer` | 拖入冰刺贴图 |
| `Rigidbody2D` | Body Type = Dynamic；Gravity Scale = 0；Collision Detection = Continuous |
| `CircleCollider2D` | Is Trigger = **✅ True** |
| `IceSpikeProjectile`（脚本） | Move Speed = 16，Max Lifetime = 10 |

### 3. 设置 Layer

**必须设为 `BossPollutionBall` 层**（与火球/陨石同层，使激光可以检测到）。

### 4. 保存 Prefab

将 GameObject 拖入 `Assets/Resources/Prefab/` 或美术资源目录，保存为 Prefab。

---

## 二、GlacialBossController Inspector 配置

选中 `Frost_Boss` 预制体，找到 `GlacialBossController` 组件，逐项配置：

### 技能1：冰墙构建 · 视觉

| 字段 | 拖入目标 |
|---|---|
| `Red Body Effect` | `Frost_Boss/RedBodyEffect`（SpriteRenderer） |
| `Ice Wall Glow Fade In Duration` | 1.5（默认） |
| `Ice Wall Glow Fade Out Duration` | 1.0（默认） |

### 技能2：冰封射线 · 视觉

| 字段 | 拖入目标 |
|---|---|
| `Laser Line Renderer` | `Frost_Boss/Laser`（LineRenderer 组件） |
| `Laser End VFX` | `Frost_Boss/Laser/EndVFX`（Transform） |
| `Body03 Crystal` | `Frost_Boss/Body03`（SpriteRenderer） |
| `Freeze Ray Charge Up Duration` | 1.0（默认） |
| `Freeze Ray Shield Damage Per Second` | 500（默认） |

### 技能4：绝对零度 · 冰刺

| 字段 | 拖入目标 |
|---|---|
| `Bing Ci Transforms[0]` | `Frost_Boss/BingCi01` |
| `Bing Ci Transforms[1]` | `Frost_Boss/BingCi02` |
| `Bing Ci Transforms[2]` | `Frost_Boss/BingCi03` |
| `Bing Ci Transforms[3]` | `Frost_Boss/BingCi04` |
| `Bing Ci Renderers[0]` | `Frost_Boss/BingCi01`（SpriteRenderer） |
| `Bing Ci Renderers[1]` | `Frost_Boss/BingCi02`（SpriteRenderer） |
| `Bing Ci Renderers[2]` | `Frost_Boss/BingCi03`（SpriteRenderer） |
| `Bing Ci Renderers[3]` | `Frost_Boss/BingCi04`（SpriteRenderer） |
| `Ice Spike Projectile Prefab` | 上一步创建的 `IceSpikeProjectile` Prefab |

---

## 三、LineRenderer 配置（技能2激光视觉）

选中 `Frost_Boss/Laser`，配置 LineRenderer：

| 属性 | 推荐值 |
|---|---|
| Positions Count | 2 |
| Width Start / End | 0.1 / 0.1（或根据美术调整） |
| Color | 蓝白色渐变 |
| Material | 激光专用 Unlit 材质 |
| Use World Space | **✅ True**（关键：坐标以世界坐标设置） |

> `GlacialBossController` 脚本会在每帧通过 `SetPosition(0, bossPos)` 和 `SetPosition(1, hitPoint)` 控制端点，无需手动设置初始位置。

---

## 四、TurretController Inspector 配置

选中光棱塔（含 `TurretController` 的 GameObject），配置：

| 字段 | 操作 |
|---|---|
| `Frozen Overlay` | 拖入冰封覆盖图的 SpriteRenderer（需预先制作冰冻覆盖图节点，初始 `SetActive(false)`） |
| `Freeze Click Reduction Per Tap` | 0.1（默认，每次点击缩短 0.1 秒） |

### 制作冰封覆盖图节点

1. 在光棱塔下新建子节点，命名 `FrozenOverlay`
2. 添加 `SpriteRenderer`，拖入半透明蓝白冰块贴图
3. 初始在 Inspector 中取消 GameObject 的 Active（`SetActive(false)`）
4. 将此 SpriteRenderer 拖入 `Frozen Overlay` 字段

---

## 五、修复冰墙移动 Bug（重要）

打开 `Assets/Resources/Data/EnemyDatabase.asset`，在 Inspector 中点击数组末尾 **+**，将 `Frost_IceWall.asset` 添加进去。

**原因**：`Frost_IceWall` 的 `EnemyType = 20`，但 EnemyDatabase 只有 0~19 共 20 条，缺少第 20 号条目，导致 `GetData()` 返回 null，行为类型默认为 Chase（追击），冰墙因此会移动。

---

## 六、IceSpikeProjectile Prefab Tags

冰刺投射体检测光棱塔命中依赖 Tag：
- 光棱塔本体 GameObject Tag = `"Tower"`
- 护盾 GameObject Tag = `"Shield"`

请确认场景中两者 Tag 已正确设置。

---

## 七、测试清单

- [ ] **技能1**：Boss 进入 Summon 状态后，RedBodyEffect 从暗到亮（约1.5s），生成2~3堵冰墙，再淡出
- [ ] **技能2**：Boss 静止，Body03 闪烁1s，然后蓝色激光从Boss中心朝塔射出3s
  - [ ] 激光命中护盾时，护盾扣血，光棱塔不冻结
  - [ ] 激光命中塔本体时，光棱塔显示冰封覆盖图并冻结1.5s
  - [ ] 玩家点击屏幕可加速解冻
  - [ ] 激光结束后 LineRenderer 和 EndVFX 正确隐藏
- [ ] **技能4**：全屏警告，3s 蓄力期间造成 ≥5000 伤害可打断（Boss 眩晕）
  - [ ] 未打断：4 枚冰刺拔出 → 旋转对准塔 → 依次射出
  - [ ] 激光可以打爆冰刺（血量 5000）
  - [ ] 所有冰刺被打爆 → 无惩罚
  - [ ] 有冰刺命中塔 → 触发真实伤害 + 冻结3s + FrostSlime 涌入
  - [ ] 2s 后 BingCi 在原位透明度淡入重新出现
- [ ] **冻结 QTE**：冻结期间点击屏幕每次缩短 0.1s

---

*接入完成后请在 Unity Editor 中运行场景，逐项验证测试清单。*
