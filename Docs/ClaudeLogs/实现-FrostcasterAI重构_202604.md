# FrostcasterAI 重构记录

日期：2026-04-02

---

## 换位方式决策

采用平滑移动（与 LavaGunnerAI 一致）。理由：castInterval=2s 极短，瞬移会导致画面混乱，
平滑移动填补两次施法间隙，玩家可预判位置，产生策略感。

---

## 状态机

Entering → Charging(发光蓄力 2s) → Casting(压缩→召唤冰墙→弹起→复原→发光淡出) → Repositioning → Charging…

---

## EnemyData 变更

- Header "远程炮手设置（RangedGunner 专用）" → "远程怪通用设置（RangedGunner / FrostCaster 共用）"
- 删除 frostcasterStopYPercent、frostcasterCastInterval（由 gunnerStopYPercent / gunnerShootInterval 替代）
- 保留 frostcasterIceWallCount / frostcasterRandomWallCount / frostcasterIceWallType（施法者专属）

---

## 数值更新

| Asset | 字段 | 新值 |
|---|---|---|
| Frost_Caster | gunnerStopYPercent | 0.7 |
| Frost_Caster | gunnerShootInterval | 2 |
| Frost_Caster | gunnerMaxDistFromTower | 6.5 |
| Frost_Caster | gunnerRepositionRange | 3 |
| Frost_EliteCaster | gunnerStopYPercent | 0.65 |
| Frost_EliteCaster | gunnerShootInterval | 8 |
| Frost_EliteCaster | gunnerMaxDistFromTower | 6.5 |
| Frost_EliteCaster | gunnerRepositionRange | 2.5 |
