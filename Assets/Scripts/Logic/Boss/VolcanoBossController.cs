// ============================================================
// VolcanoBossController.cs
// 文件位置: Assets/Scripts/Logic/Boss/VolcanoBossController.cs
// 用途：第二章 Boss ——《熔炉巨兽 (The Magma Furnace)》
//
// 功能总览：
//   ─ 阶段一（HP>70%） ：陨石喷发（被动）+ 汲取融合（召唤6只LavaSlime）+ 火山冲撞
//   ─ 阶段二（30%<HP≤70%）：同上，汲取融合升级为8只
//   ─ 阶段三（HP≤30%） ：绝境碾压×3轮（激光角力），角力期间从裂缝喷射侧向火球
//
//   ─ 阶段切换：Body02/Body03 换图 + 屏幕震动 + VFX/SFX钩子
//   ─ Body03 材质动画：Idle岩浆呼吸脉冲、冲撞前摇变红、Press白炽化
//   ─ 待机动画：整体极小幅度"沉重呼吸"缩放
//   ─ 移动动画：VisualRoot 高频Perlin震动 + 停止瞬间屏幕冲击震
//   ─ 火山口粒子：随阶段提升发射速率
//   ─ VFX/SFX：全部预留 SerializeField 接口，留空时静默
// ============================================================

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using LightVsDecay.Core;
using LightVsDecay.Core.Pool;
using LightVsDecay.Audio;
using LightVsDecay.Data;
using LightVsDecay.Logic.Statistics;
#if DOTWEEN
using DG.Tweening;
#endif

namespace LightVsDecay.Logic.Boss
{
    /// <summary>
    /// 第二章Boss：熔炉巨兽。
    /// 继承 BaseBossController，在基类状态机基础上实现火山专属视觉与技能。
    /// </summary>
    public class VolcanoBossController : BaseBossController
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置 · 汲取融合
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("汲取融合 · 配置")]
        [Tooltip("阶段一召唤数量（全部为 LavaSlime）")]
        [SerializeField] private int summonCountPhase1 = 6;

        [Tooltip("阶段二召唤数量")]
        [SerializeField] private int summonCountPhase2 = 8;

        [Tooltip("吸收触发点：火山口椭圆 Trigger 节点（Is Trigger = true，无 Rigidbody）；粘液怪以此为移动目标")]
        [SerializeField] private Transform absorptionPoint;

        [Tooltip("吸收半径（小怪到 absorptionPoint 的距离阈值）")]
        [SerializeField] private float absorptionRadius = 1.2f;

        [Tooltip("每次吸收回复的 HP")]
        [SerializeField] private float absorptionHealPerSlime = 2000f;

        [Tooltip("每次吸收增加的攻击力（叠乘，每层 10%）")]
        [SerializeField] private float absorptionATKPerStack = 0.10f;

        [Tooltip("吸收层数上限")]
        [SerializeField] private int absorptionMaxStacks = 6;

        [Tooltip("粘液怪出生点 Y 坐标（光棱塔上方，默认 -8）")]
        [SerializeField] private float slimeSpawnY = -8f;

        [Tooltip("粘液怪 Y 方向随机散布范围（让各怪错落不整齐，默认 1.5）")]
        [SerializeField] private float slimeSpawnYScatter = 1.5f;

        [Tooltip("左侧生成区域 X 最小值（负数，默认 -12）")]
        [SerializeField] private float slimeSpawnLeftXMin = -12f;

        [Tooltip("左侧生成区域 X 最大值（负数，默认 -10）")]
        [SerializeField] private float slimeSpawnLeftXMax = -10f;

        [Tooltip("右侧生成区域 X 最小值（正数，默认 10）")]
        [SerializeField] private float slimeSpawnRightXMin = 10f;

        [Tooltip("右侧生成区域 X 最大值（正数，默认 12）")]
        [SerializeField] private float slimeSpawnRightXMax = 12f;

        [Header("回血上限 · 攻击模式")]
        [Tooltip("Boss 累计回血达到最大HP的此比例后，切换为攻击型召唤（小怪直接攻击玩家）")]
        [Range(0.1f, 1f)]
        [SerializeField] private float healCapRatio = 0.4f;

        [Tooltip("攻击模式：小怪生成点 Y 坐标（屏幕中部，默认 0）")]
        [SerializeField] private float attackSpawnY = 0f;

        [Tooltip("攻击模式：左侧生成 X 坐标（默认 -9）")]
        [SerializeField] private float attackSpawnLeftX = -9f;

        [Tooltip("攻击模式：右侧生成 X 坐标（默认 9）")]
        [SerializeField] private float attackSpawnRightX = 9f;

        [Tooltip("攻击模式：生成点 Y 方向随机散布范围（默认 1.5）")]
        [SerializeField] private float attackSpawnYScatter = 1.5f;

        [Header("冰冻易伤")]
        [Tooltip("Boss 处于完全冻结状态时，受到的额外伤害倍率（默认 1.5 = +50%）")]
        [SerializeField] private float frozenVulnerabilityMultiplier = 1.5f;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置 · 火球喷发
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("火球喷发 · 配置")]
        [Tooltip("火球预制体（挂 LavaProjectile 组件，Layer = BossPollutionBall）")]
        [SerializeField] private GameObject fireballPrefab;

        [Tooltip("火球发射点（火山口 Transform，未设置则使用 Body03 位置）")]
        [SerializeField] private Transform fireballSpawnPoint;

        [Tooltip("每次喷发火球数量")]
        [SerializeField] private int fireballCount = 4;

        [Tooltip("火球落地伤害")]
        [SerializeField] private int fireballDamage = 400;

        [Tooltip("被动喷发间隔（秒，跨 Idle 周期累积计时）")]
        [SerializeField] private float fireballInterval = 10f;

        [Tooltip("落点散布半径（以玩家塔为中心）")]
        [SerializeField] private float fireballSpreadRadius = 2.5f;

        [Tooltip("每颗火球错落发射间隔（秒）")]
        [SerializeField] private float fireballLaunchInterval = 0.35f;

        [Tooltip("贝塞尔弧顶高度（控制点相对起终点中点的额外Y偏移）")]
        [SerializeField] private float fireballArcHeight = 8.5f;

        [Tooltip("火球从喷口到落点的飞行时长（秒）")]
        [SerializeField] private float fireballTravelTime = 1.55f;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置 · 绝境碾压
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("绝境碾压 · 配置")]
        [Tooltip("触发阈值（HP 百分比）")]
        [SerializeField] private float desperatePressThreshold = 0.3f;

        [Tooltip("绝境碾压轮次")]
        [SerializeField] private int desperatePressRounds = 3;


        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置 · 阶段外观（图片换帧）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("阶段外观 · Body02 岩石外壳")]
        [Tooltip("Body02 的 SpriteRenderer（拖入 Body02 子节点）")]
        [SerializeField] private SpriteRenderer body02Renderer;

        [Tooltip("Body02 各阶段图片：[0]=阶段一（完整），[1]=阶段二（中度破裂），[2]=阶段三（重度破裂）")]
        [SerializeField] private Sprite[] body02PhaseSprites = new Sprite[3];

        [Header("阶段外观 · Body03 顶部喷发")]
        [Tooltip("Body03 的 SpriteRenderer（拖入 Body03 子节点）")]
        [SerializeField] private SpriteRenderer body03Renderer;

        [Tooltip("Body03 各阶段图片：[0]=阶段一，[1]=阶段二，[2]=阶段三")]
        [SerializeField] private Sprite[] body03PhaseSprites = new Sprite[3];

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置 · Body03 材质颜色（HDR _Color）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("Body03 材质颜色 (WobblyLiquidSprite Shader → _Color，HDR)")]
        [Tooltip("待机岩浆颜色（橙色，低亮）")]
        [ColorUsage(true, true)]
        [SerializeField] private Color body03IdleColor = new Color(2.0f, 0.72f, 0.08f, 1f);

        [Tooltip("待机呼吸最低亮度（IdleColor 乘以此系数得到最暗值，0~1）")]
        [Range(0.3f, 0.9f)]
        [SerializeField] private float body03IdlePulseMin = 0.55f;

        [Tooltip("Idle 呼吸脉冲周期（秒）")]
        [SerializeField] private float body03IdlePulsePeriod = 2.5f;

        [Tooltip("冲撞前摇：颜色渐变目标（橙红）")]
        [ColorUsage(true, true)]
        [SerializeField] private Color body03ChargeTelegraphColor = new Color(3.5f, 0.25f, 0.04f, 1f);

        [Tooltip("冲撞中（霸体期）颜色（深红）")]
        [ColorUsage(true, true)]
        [SerializeField] private Color body03ChargeActiveColor = new Color(4.5f, 0.08f, 0.04f, 1f);

        [Tooltip("绝境碾压激光角力颜色（白炽）")]
        [ColorUsage(true, true)]
        [SerializeField] private Color body03PressColor = new Color(6f, 4.8f, 3.2f, 1f);

        [Tooltip("汲取融合召唤时颜色（能量外放，变暗）")]
        [ColorUsage(true, true)]
        [SerializeField] private Color body03SummonColor = new Color(0.8f, 0.28f, 0.04f, 1f);

        [Tooltip("Body03 颜色插值速度（值越大，颜色切换越快）")]
        [SerializeField] private float body03LerpSpeed = 2.5f;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置 · 待机沉重呼吸
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("待机动画 · 沉重呼吸缩放")]
        [Tooltip("Scale 幅度（值越小越微妙，推荐 0.01~0.02）")]
        [SerializeField] private float breathingAmplitude = 0.015f;

        [Tooltip("呼吸周期（秒，推荐 3.5~5）")]
        [SerializeField] private float breathingPeriod = 4f;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置 · 移动震动（VisualRoot）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("移动动画 · VisualRoot 身体震动")]
        [Tooltip("预制体中 VisualRoot 子节点（包裹 Body/Body02/Body03/Eyes）")]
        [SerializeField] private Transform visualRoot;

        [Tooltip("震动幅度（世界单位，推荐 0.02~0.04）")]
        [SerializeField] private float moveShakeAmplitude = 0.025f;

        [Tooltip("震动频率（Perlin 采样速度，推荐 15~22）")]
        [SerializeField] private float moveShakeSpeed = 18f;

        [Tooltip("停止时屏幕冲击震强度")]
        [SerializeField] private float stopImpactIntensity = 0.12f;

        [Tooltip("停止时屏幕冲击震持续时长")]
        [SerializeField] private float stopImpactDuration = 0.35f;

        [Tooltip("移动判定阈值（每帧位移低于此值视为停止）")]
        [SerializeField] private float moveThreshold = 0.005f;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置 · 火山口粒子
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("火山口粒子（Body03 顶部子节点）")]
        [Tooltip("火山口粒子系统（CraterParticles 子节点）")]
        [SerializeField] private ParticleSystem craterParticles;

        [Tooltip("阶段一 发射速率（颗/秒）")]
        [SerializeField] private float craterEmissionPhase1 = 10f;

        [Tooltip("阶段二 发射速率")]
        [SerializeField] private float craterEmissionPhase2 = 18f;

        [Tooltip("阶段三 发射速率")]
        [SerializeField] private float craterEmissionPhase3 = 30f;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置 · 阶段切换屏幕震动
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("阶段切换 · 屏幕震动")]
        [Tooltip("阶段一→二震动强度")]
        [SerializeField] private float phase2TransitionShakeIntensity = 0.2f;

        [Tooltip("阶段一→二震动时长")]
        [SerializeField] private float phase2TransitionShakeDuration = 0.5f;

        [Tooltip("阶段二→三震动强度")]
        [SerializeField] private float phase3TransitionShakeIntensity = 0.35f;

        [Tooltip("阶段二→三震动时长")]
        [SerializeField] private float phase3TransitionShakeDuration = 0.8f;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 配置 · 眼睛阶段三颜色
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("眼睛颜色 · 阶段三（红色）")]
        [Tooltip("阶段三眼睛闭眼颜色（暗红）")]
        [SerializeField] private Color eyePhase3ClosedColor = new Color(0.35f, 0.04f, 0.04f, 1f);

        [Tooltip("阶段三眼睛睁眼颜色（亮红）")]
        [SerializeField] private Color eyePhase3OpenColor  = new Color(1.0f, 0.08f, 0.04f, 1f);

        [Tooltip("阶段三眼睛颜色过渡时长（秒）")]
        [SerializeField] private float eyePhase3TintDuration = 0.6f;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // ══ VFX 预留接口 ══════════════════════════════════════
        // 将对应 VFX 预制体拖入此处；留空时自动跳过，不会报错
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("━━ VFX 预留接口 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━")]
        [Tooltip("【阶段一→二】切换特效（留空=暂不播放）")]
        [SerializeField] private GameObject vfxPhase2Transition;

        [Tooltip("【阶段二→三】切换特效（留空=暂不播放）")]
        [SerializeField] private GameObject vfxPhase3Transition;

        [Tooltip("【汲取融合】吸收小怪时特效（在Boss位置播放；留空=暂不播放）")]
        [SerializeField] private GameObject vfxAbsorbSlime;

        [Tooltip("吸收特效持续时长（秒），超时自动销毁，避免特效残留")]
        [SerializeField] private float absorbVFXDuration = 2f;

        [Tooltip("【陨石喷发】顶部发射特效（在Body03位置播放；留空=暂不播放）")]
        [SerializeField] private GameObject vfxMeteorBurst;

        [Tooltip("【绝境碾压】激光角力开始特效（留空=暂不播放）")]
        [SerializeField] private GameObject vfxDesperatePressStart;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // ══ SFX 预留接口 ══════════════════════════════════════
        // 将对应 AudioClip 拖入此处；留空时自动跳过，不会报错
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        [Header("━━ SFX 预留接口 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━")]
        [Tooltip("【陨石发射】每颗陨石飞出时的音效（留空=静默）")]
        [SerializeField] private AudioClip sfxMeteorLaunch;

        [Tooltip("【汲取融合】吸收小怪时的音效（留空=静默）")]
        [SerializeField] private AudioClip sfxAbsorbSlime;

        [Tooltip("【阶段切换】阶段过渡时的爆裂音效（留空=静默）")]
        [SerializeField] private AudioClip sfxPhaseTransition;

        [Tooltip("【绝境碾压】激光角力开始时的音效（留空=静默）")]
        [SerializeField] private AudioClip sfxDesperatePressStart;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        // 阶段
        private int  currentPhase     = 1;
        private bool phase3Triggered  = false;

        // 火球被动计时（跨 Idle 周期累积）
        private float fireballTimer = 0f;

        // 汲取融合
        private List<LightVsDecay.Logic.Enemy.EnemyBlob> summonedSlimes =
            new List<LightVsDecay.Logic.Enemy.EnemyBlob>();
        private int absorptionStacks = 0;
        private float totalHealedHP = 0f;
        private bool healCapReached = false;

        // 绝境碾压
        private int desperatePressRoundsDone = 0;

        // Body03 材质实例与动画
        private Material body03MatInstance;
        private Color    body03CurrentColor;
        private Color    body03TargetColor;
        private bool     isChargeTelegraph; // 当前帧是否处于冲撞前摇

        // 待机呼吸缩放
        private Vector3 breathingBaseScale;

        // 移动检测（用于停止冲击震）
        private Vector3 prevPosition;
        private bool    wasMoving;

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 初始化
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        protected override void OnBossInitialized()
        {
            // —— 重置运行时状态 ——
            currentPhase             = 1;
            phase3Triggered          = false;
            absorptionStacks         = 0;
            totalHealedHP            = 0f;
            healCapReached           = false;
            desperatePressRoundsDone = 0;
            fireballTimer            = 0f;
            summonedSlimes.Clear();

            // —— 缓存呼吸基准缩放 ——
            breathingBaseScale = transform.localScale;

            // —— 缓存移动初始位置 ——
            prevPosition = transform.position;
            wasMoving    = false;

            // —— Body03 材质实例化（避免修改共享材质） ——
            if (body03Renderer != null)
            {
                // 调用 .material 时 Unity 自动创建独立实例
                body03MatInstance  = body03Renderer.material;
                body03CurrentColor = body03IdleColor;
                body03TargetColor  = body03IdleColor;
                body03MatInstance.SetColor("_Color", body03CurrentColor);
            }

            // —— 应用阶段一外观 ——
            ApplyPhaseSprites(1);

            // —— 启动火山口粒子 ——
            SetCraterEmissionRate(craterEmissionPhase1);
            if (craterParticles != null && !craterParticles.isPlaying)
                craterParticles.Play();

            // —— 排除 Body03 不受冰冻蓝色染色影响 ——
            // Body03 使用 WobblyLiquidSprite HDR Shader，蓝色顶点色乘橙色会产生
            // 难看的灰绿色。仅对 Body01/Body02 应用冰冻 tint，保留 Body03 材质原貌。
            if (frostDebuff != null && body03Renderer != null && bodyRenderers != null)
            {
                var filtered = System.Array.FindAll(bodyRenderers, r => r != null && r != body03Renderer);
                frostDebuff.SetTargetRenderers(filtered);
            }

            if (showDebugInfo)
                GameLogger.Log("[VolcanoBoss] 初始化完成");
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 状态机钩子（重写 EnterState / ExitState 拦截状态切换）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        protected override void EnterState(BossState state)
        {
            base.EnterState(state);
            OnVolcanoEnterState(state);
        }

        protected override void ExitState(BossState state)
        {
            OnVolcanoExitState(state);
            base.ExitState(state);
        }

        private void OnVolcanoEnterState(BossState state)
        {
            switch (state)
            {
                case BossState.Idle:
                    body03TargetColor = body03IdleColor; // 回到岩浆橙色
                    break;

                case BossState.Charge:
                    // 前摇期颜色在 UpdateBody03Color 中按时间渐变
                    // 此处设为起始值，防止从上一个状态直接跳变
                    body03TargetColor = body03IdleColor;
                    break;

                case BossState.Summon:
                    body03TargetColor = body03SummonColor; // 能量外放，变暗
                    break;

                case BossState.Press:
                    body03TargetColor = body03PressColor;  // 白炽化
                    // —— VFX 预留 ——
                    PlayVFXAtSelf(vfxDesperatePressStart);
                    // —— SFX 预留 ——
                    PlaySFX(sfxDesperatePressStart);
                    break;

                case BossState.Stun:
                case BossState.Frozen:
                    // 保持当前颜色，不做切换
                    break;
            }
        }

        private void OnVolcanoExitState(BossState state)
        {
            // 离开 Charge 时颜色回归 Idle（下一个 EnterState 会再设置）
            if (state == BossState.Charge)
                body03TargetColor = body03IdleColor;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 每帧更新（OnExtraUpdate 由 BaseBossController.Update 调用）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        protected override void OnExtraUpdate()
        {
            UpdatePhase();
            CheckAbsorption();
            UpdateBody03Color();
            UpdateBreathingScale();
            UpdateMoveShake();
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 阶段管理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void UpdatePhase()
        {
            float hp = HealthPercent;

            // 阶段三（仅触发一次）
            if (!phase3Triggered && hp <= desperatePressThreshold)
            {
                phase3Triggered = true;
                currentPhase    = 3;
                OnEnterPhase3();
                InterruptCharge();
                ForceStun();
                return;
            }

            // 阶段二
            if (currentPhase < 2 && hp <= 0.7f)
            {
                currentPhase = 2;
                OnEnterPhase2();
            }
        }

        private void OnEnterPhase2()
        {
            if (showDebugInfo) GameLogger.Log("[VolcanoBoss] ── 进入阶段二：熔炉爆发 ──");

            // 换图
            ApplyPhaseSprites(2);

            // 粒子加强
            SetCraterEmissionRate(craterEmissionPhase2);

            // 屏幕震动 + 手机震动
            CameraShake.Instance?.Shake(phase2TransitionShakeIntensity, phase2TransitionShakeDuration);
            HapticFeedback.Instance?.Trigger(HapticType.Heavy);

            // —— VFX 预留 ——
            // 接入方式：将阶段切换特效预制体拖入 Inspector → vfxPhase2Transition
            PlayVFXAtSelf(vfxPhase2Transition);

            // —— SFX 预留 ——
            // 接入方式：将阶段切换音效拖入 Inspector → sfxPhaseTransition
            PlaySFX(sfxPhaseTransition);
        }

        private void OnEnterPhase3()
        {
            if (showDebugInfo) GameLogger.Log("[VolcanoBoss] ── 进入阶段三：绝境碾压 ──");

            // 换图
            ApplyPhaseSprites(3);

            // 粒子最强
            SetCraterEmissionRate(craterEmissionPhase3);

            // 屏幕强震 + 手机震动
            CameraShake.Instance?.Shake(phase3TransitionShakeIntensity, phase3TransitionShakeDuration);
            HapticFeedback.Instance?.Trigger(HapticType.Heavy);

            // 眼睛变红（调用 BossEyeController.SetTintColor）
            if (eyeController != null)
                eyeController.SetTintColor(eyePhase3ClosedColor, eyePhase3OpenColor, eyePhase3TintDuration);

            // —— VFX 预留 ——
            // 接入方式：将阶段三切换特效预制体拖入 Inspector → vfxPhase3Transition
            PlayVFXAtSelf(vfxPhase3Transition);

            // —— SFX 预留 ——
            PlaySFX(sfxPhaseTransition);
        }

        /// <summary>将 Body02 / Body03 切换为指定阶段的图片（1/2/3）</summary>
        private void ApplyPhaseSprites(int phase)
        {
            int idx = Mathf.Clamp(phase - 1, 0, 2);

            if (body02Renderer != null && body02PhaseSprites != null
                && idx < body02PhaseSprites.Length && body02PhaseSprites[idx] != null)
                body02Renderer.sprite = body02PhaseSprites[idx];

            if (body03Renderer != null && body03PhaseSprites != null
                && idx < body03PhaseSprites.Length && body03PhaseSprites[idx] != null)
                body03Renderer.sprite = body03PhaseSprites[idx];
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 被动：陨石喷发（Idle 期间持续计时）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        protected override void OnEnterIdle()
        {
            // fireballTimer 不在此重置——跨多个 Idle 周期累积，
            // 达到 fireballInterval 后触发，重置在 OnIdlePassiveUpdate 内完成。
        }

        protected override void OnIdlePassiveUpdate(float deltaTime)
        {
            fireballTimer += deltaTime;
            if (fireballTimer >= fireballInterval)
            {
                fireballTimer = 0f;
                StartCoroutine(FireballBurstRoutine());
            }
        }

        private IEnumerator FireballBurstRoutine()
        {
            if (fireballPrefab == null)
            {
                if (showDebugInfo) GameLogger.LogWarning("[VolcanoBoss] fireballPrefab 未设置！");
                yield break;
            }

            // 记录本次火球喷发触发
            BattleStatistics.Instance?.RecordBossFireballBurst();

            // Body03 白炽化蓄力前摇（1.2s）
            float telegraphTime = 1.2f;
            float elapsed       = 0f;
            Color startColor    = body03CurrentColor;

            while (elapsed < telegraphTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / telegraphTime;
                body03TargetColor = Color.Lerp(startColor, body03PressColor, t);
                yield return null;
            }

            // 查找玩家塔位置
            GameObject towerGO  = GameObject.FindGameObjectWithTag("Tower");
            Vector3    towerPos = towerGO != null ? towerGO.transform.position : Vector3.zero;

            // 火山口发射点：优先用 fireballSpawnPoint，次选 Body03，兜底 Boss 正上方
            Vector3 spawnPos = fireballSpawnPoint != null
                ? fireballSpawnPoint.position
                : (body03Renderer != null
                    ? body03Renderer.transform.position
                    : transform.position + Vector3.up * 2f);

            // 逐颗错落发射
            for (int i = 0; i < fireballCount; i++)
            {
                // 落点：玩家塔位置 + 随机散布偏移
                float   spreadAngle = Random.Range(0f, 360f);
                float   spreadDist  = Random.Range(0.3f, fireballSpreadRadius);
                Vector2 spreadOff   = Quaternion.Euler(0f, 0f, spreadAngle) * Vector2.right * spreadDist;
                Vector3 targetPos   = towerPos + (Vector3)spreadOff;

                // 控制点向目标方向前探，再整体抬高，让轨迹更像锁定目标的导弹俯冲。
                Vector3 midPoint = Vector3.Lerp(spawnPos, targetPos, 0.45f);
                Vector3 controlPoint = midPoint + Vector3.up * fireballArcHeight;

                GameObject go      = Instantiate(fireballPrefab, spawnPos, Quaternion.identity);
                var        lavaPrj = go.GetComponent<LightVsDecay.Logic.Enemy.LavaProjectile>();
                if (lavaPrj != null)
                    lavaPrj.LaunchBezier(spawnPos, controlPoint, targetPos, fireballTravelTime, fireballDamage);

                // Body03 每发时脉冲亮闪
                body03TargetColor = body03PressColor * 1.4f;

                // VFX：喷口特效
                PlayVFXAt(vfxMeteorBurst, spawnPos);

                // SFX：发射音效
                PlaySFX(sfxMeteorLaunch);

                yield return new WaitForSeconds(fireballLaunchInterval);
            }

            // 恢复 Idle 颜色
            body03TargetColor = body03IdleColor;

            if (showDebugInfo)
                GameLogger.Log($"[VolcanoBoss] 火球喷发 {fireballCount} 颗");
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 主动技能选择
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        protected override void ChooseActiveSkill()
        {
            if (phase3Triggered)
            {
                if (desperatePressRoundsDone < desperatePressRounds)
                {
                    ChangeState(BossState.Press);
                    desperatePressRoundsDone++;
                }
                else
                {
                    phase3Triggered = false;
                    ChangeState(BossState.Idle);
                }
                return;
            }

            // 阶段一/二：50% Charge / 50% Summon
            if (Random.value < 0.5f)
                ChangeState(BossState.Charge);
            else
                ChangeState(BossState.Summon);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 汲取融合：召唤
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        protected override IEnumerator ExecuteSummonBehavior()
        {
            summonedSlimes.RemoveAll(e => e == null || e.IsDead);

            int totalCount = (currentPhase >= 2) ? summonCountPhase2 : summonCountPhase1;

            if (EnemyPoolManager.Instance == null)
            {
                if (showDebugInfo) GameLogger.LogWarning("[VolcanoBoss] EnemyPoolManager 不存在！");
                yield break;
            }

            // 缓存 Boss 自身碰撞体，用于忽略与粘液怪的物理碰撞
            Collider2D bossCol = GetComponent<Collider2D>();

            int leftCount = totalCount / 2;

            if (healCapReached)
            {
                // ── 攻击模式：从屏幕中部左右两侧召唤，小怪直接攻向玩家/光棱塔 ──
                for (int i = 0; i < totalCount; i++)
                {
                    float x = (i < leftCount) ? attackSpawnLeftX : attackSpawnRightX;
                    float y = attackSpawnY + Random.Range(-attackSpawnYScatter * 0.5f, attackSpawnYScatter * 0.5f);
                    Vector3 spawnPos = new Vector3(x, y, 0f);

                    var enemy = EnemyPoolManager.Instance.Spawn(EnemyType.LavaSlime, spawnPos);
                    if (enemy != null)
                    {
                        // 不设置 SetOverrideTarget，小怪自动以光棱塔为目标发动攻击
                        Collider2D slimeCol = enemy.GetComponent<Collider2D>();
                        if (bossCol != null && slimeCol != null)
                            Physics2D.IgnoreCollision(bossCol, slimeCol, true);

                        summonedSlimes.Add(enemy);
                    }
                }

                if (showDebugInfo)
                    GameLogger.Log($"[VolcanoBoss] 攻击模式召唤：{totalCount} 只 LavaSlime 从中部两侧出现");
            }
            else
            {
                // ── 回血模式：从屏幕底部左右两侧生成，小怪走向火山口吸收点 ──
                Transform absTarget = (absorptionPoint != null) ? absorptionPoint : this.transform;

                for (int i = 0; i < totalCount; i++)
                {
                    float x = (i < leftCount)
                        ? Random.Range(slimeSpawnLeftXMin,  slimeSpawnLeftXMax)
                        : Random.Range(slimeSpawnRightXMin, slimeSpawnRightXMax);
                    float y = slimeSpawnY - Random.Range(0f, slimeSpawnYScatter);
                    Vector3 spawnPos = new Vector3(x, y, 0f);

                    var enemy = EnemyPoolManager.Instance.Spawn(EnemyType.LavaSlime, spawnPos);
                    if (enemy != null)
                    {
                        enemy.SetOverrideTarget(absTarget);

                        Collider2D slimeCol = enemy.GetComponent<Collider2D>();
                        if (bossCol != null && slimeCol != null)
                            Physics2D.IgnoreCollision(bossCol, slimeCol, true);

                        summonedSlimes.Add(enemy);
                    }
                }

                if (showDebugInfo)
                    GameLogger.Log($"[VolcanoBoss] 汲取融合：召唤 {totalCount} 只 LavaSlime");
            }

            yield break;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 汲取融合：吸收检测（每帧）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void CheckAbsorption()
        {
            // 攻击模式下，小怪不进行吸收，直接攻击玩家
            if (healCapReached) return;
            if (summonedSlimes.Count == 0) return;

            for (int i = summonedSlimes.Count - 1; i >= 0; i--)
            {
                var slime = summonedSlimes[i];
                if (slime == null || slime.IsDead)
                {
                    summonedSlimes.RemoveAt(i);
                    continue;
                }

                // 以 absorptionPoint 为检测中心（未配置时退回 Boss 根节点）
                Vector3 checkCenter = (absorptionPoint != null) ? absorptionPoint.position : transform.position;
                if (Vector2.Distance(checkCenter, slime.transform.position) <= absorptionRadius)
                {
                    AbsorbSlime(slime);
                    summonedSlimes.RemoveAt(i);
                }
            }
        }

        private void AbsorbSlime(LightVsDecay.Logic.Enemy.EnemyBlob slime)
        {
            slime.AbsorbedByBoss();

            // 回血（仅在未到达回血上限时执行）
            if (bossHealth != null && !healCapReached)
            {
                bossHealth.HealHP(absorptionHealPerSlime);
                BattleStatistics.Instance?.RecordBossAbsorption();
                totalHealedHP += absorptionHealPerSlime;

                // 检测回血上限：累计回血 >= Boss最大HP × healCapRatio 时切换为攻击模式
                if (!healCapReached && totalHealedHP >= bossHealth.MaxHealth * healCapRatio)
                {
                    healCapReached = true;
                    if (showDebugInfo)
                        GameLogger.Log($"[VolcanoBoss] 回血上限达到（{totalHealedHP}/{bossHealth.MaxHealth * healCapRatio}），切换为攻击模式召唤");
                }
            }

            // 攻击力叠层（体现为受到伤害减免，见 GetLinkedBuffDamageMultiplier）
            if (absorptionStacks < absorptionMaxStacks)
                absorptionStacks++;

            // Body03 吸收脉冲
            body03TargetColor = body03IdleColor * 1.8f; // 短暂爆亮

            // —— VFX：吸收特效，到时自动销毁 ——
            if (vfxAbsorbSlime != null)
            {
                GameObject vfxGo = Instantiate(vfxAbsorbSlime, transform.position, Quaternion.identity);
                Destroy(vfxGo, absorbVFXDuration);
            }

            // —— SFX 预留 ——
            // 接入方式：将吸收音效拖入 Inspector → sfxAbsorbSlime
            PlaySFX(sfxAbsorbSlime);

            if (showDebugInfo)
                GameLogger.Log($"[VolcanoBoss] 吸收！回血 {absorptionHealPerSlime}，层数: {absorptionStacks}/{absorptionMaxStacks}");
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 汲取融合 Buff：受伤减免倍率（每层 -3%，上限 -18%）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        public override float GetLinkedBuffDamageMultiplier()
        {
            float baseMultiplier = Mathf.Max(0.1f, 1f - absorptionStacks * 0.03f);
            if (IsFrozen) baseMultiplier *= frozenVulnerabilityMultiplier;
            return baseMultiplier;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 火山冲撞：速度倍率
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        protected override float GetChargeSpeedMultiplier()
        {
            return currentPhase >= 2 ? 1.2f : 1f;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 火山冲撞：冲撞完成（路径拖尾预留）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        protected override void OnChargeDashComplete(Vector3 startPos, Vector3 endPos)
        {
            // 冲撞路径熔浆拖尾暂时移除（椭圆水坑形状与冲撞路径视觉不匹配）
            // —— VFX 预留 ——
            // TODO：待熔浆拖尾粒子特效资产完成后，在此处接入。
            // 参考接入方式：在路径上按间隔实例化拖尾特效，而非 LavaPuddle。

            if (showDebugInfo)
                GameLogger.Log("[VolcanoBoss] 火山冲撞完成（拖尾待接入粒子特效）");
        }


        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Body03 材质颜色动画
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void UpdateBody03Color()
        {
            if (body03MatInstance == null) return;

            // 根据状态覆写 TargetColor（部分状态在 OnVolcanoEnterState 中已设置，
            // 此处补充需要每帧动态计算的情况）
            switch (CurrentState)
            {
                case BossState.Idle:
                case BossState.Spawn:
                    // 岩浆呼吸脉冲：在 IdleColor 最低亮度和原始亮度之间 Sin 振荡
                    float pulse = (Mathf.Sin(Time.time * (Mathf.PI * 2f / body03IdlePulsePeriod)) + 1f) * 0.5f;
                    float mult  = Mathf.Lerp(body03IdlePulseMin, 1.0f, pulse);
                    body03TargetColor = body03IdleColor * mult;
                    break;

                case BossState.Charge:
                    // 前摇期：IsInChargeTelegraph = true（基类暴露的属性）
                    if (IsInChargeTelegraph)
                    {
                        // 前摇持续时间从 config 中读取，平滑插值到蓄力红色
                        float telegraphDur = config != null ? config.chargeTelegraphDuration : 1.2f;
                        // 用 stateTimer 近似：观察 body03CurrentColor 与目标的距离
                        // 直接将 target 设为 ChargeTelegraphColor，由 Lerp 速度控制过渡快慢
                        body03TargetColor = body03ChargeTelegraphColor;
                    }
                    else
                    {
                        // 实际冲撞中（霸体期）：深红维持
                        body03TargetColor = body03ChargeActiveColor;
                    }
                    break;

                case BossState.Press:
                    // Press 期间 target 在 PressColor 附近振荡（由 OnPressTick 脉冲覆写，
                    // 脉冲过后此处将 target 拉回 PressColor）
                    body03TargetColor = Color.Lerp(body03TargetColor, body03PressColor,
                                                    Time.deltaTime * body03LerpSpeed * 2f);
                    break;

                case BossState.Summon:
                    body03TargetColor = body03SummonColor;
                    break;
                // Stun / Frozen：保持当前目标颜色，不额外操作
            }

            // 每帧 Lerp：body03CurrentColor → body03TargetColor
            body03CurrentColor = Color.Lerp(body03CurrentColor, body03TargetColor,
                                             Time.deltaTime * body03LerpSpeed);
            body03MatInstance.SetColor("_Color", body03CurrentColor);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 待机沉重呼吸缩放（Scale 极小幅度膨胀）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void UpdateBreathingScale()
        {
            // 只在非冲撞/非Press时呼吸（冲撞期间 Boss 有蓄力缩放，不要叠加）
            if (CurrentState == BossState.Charge || CurrentState == BossState.Press) return;

            float freq   = Mathf.PI * 2f / breathingPeriod;
            float breath = (Mathf.Sin(Time.time * freq) + 1f) * 0.5f; // 0~1

            // X 幅度约为 Y 的一半，营造"气压鼓起"而非均匀膨胀的效果
            float scaleX = breathingBaseScale.x * (1f + breathingAmplitude * 0.6f * breath);
            float scaleY = breathingBaseScale.y * (1f + breathingAmplitude * breath);
            transform.localScale = new Vector3(scaleX, scaleY, breathingBaseScale.z);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 移动震动与停止冲击震
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void UpdateMoveShake()
        {
            if (visualRoot == null) return;

            // 检测本帧是否在移动
            float moved  = Vector3.Distance(transform.position, prevPosition);
            bool isMoving = moved > moveThreshold;

            if (isMoving)
            {
                // PerlinNoise 采样：X / Y 用不同种子偏移避免同向
                float shakeX = (Mathf.PerlinNoise(Time.time * moveShakeSpeed, 0f)        - 0.5f) * 2f * moveShakeAmplitude;
                float shakeY = (Mathf.PerlinNoise(0f,        Time.time * moveShakeSpeed + 3.7f) - 0.5f) * 2f * moveShakeAmplitude * 0.6f;
                visualRoot.localPosition = new Vector3(shakeX, shakeY, 0f);
            }
            else
            {
                // 停止：localPosition 归零
                visualRoot.localPosition = Vector3.MoveTowards(
                    visualRoot.localPosition, Vector3.zero, Time.deltaTime * 0.1f);

                // 刚刚停下来时触发冲击震
                if (wasMoving && !isMoving)
                {
                    CameraShake.Instance?.ImpactShake(Vector2.down, stopImpactIntensity, stopImpactDuration);
                }
            }

            wasMoving    = isMoving;
            prevPosition = transform.position;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 火山口粒子：发射速率设置
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void SetCraterEmissionRate(float rate)
        {
            if (craterParticles == null) return;
            var emission = craterParticles.emission;
            emission.rateOverTime = rate;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // VFX / SFX 辅助方法
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>在 Boss 自身位置播放 VFX（prefab 为 null 时静默）</summary>
        private void PlayVFXAtSelf(GameObject prefab)
        {
            if (prefab == null) return;
            Instantiate(prefab, transform.position, Quaternion.identity);
        }

        /// <summary>在指定位置播放 VFX（prefab 为 null 时静默）</summary>
        private void PlayVFXAt(GameObject prefab, Vector3 position)
        {
            if (prefab == null) return;
            Instantiate(prefab, position, Quaternion.identity);
        }

        /// <summary>播放一次性 SFX（clip 为 null 时静默）</summary>
        private void PlaySFX(AudioClip clip)
        {
            if (clip == null) return;
            AudioManager.Instance?.PlaySFX(clip);
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 调试 GUI
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

#if UNITY_EDITOR
        protected override void OnGUI()
        {
            base.OnGUI();
            if (!showDebugInfo) return;
            GUILayout.BeginArea(new Rect(10, 460, 320, 120));
            GUILayout.Label("=== Ch2 熔炉巨兽 ===");
            GUILayout.Label($"阶段: {currentPhase}  |  汲取层数: {absorptionStacks}/{absorptionMaxStacks}");
            GUILayout.Label($"待吸收小怪: {summonedSlimes.Count}  |  碾压轮次: {desperatePressRoundsDone}/{desperatePressRounds}");
            GUILayout.Label($"火球计时: {fireballTimer:F1}s / {fireballInterval}s");
            GUILayout.Label($"Body03颜色: R={body03CurrentColor.r:F1} G={body03CurrentColor.g:F1} B={body03CurrentColor.b:F1}");
            GUILayout.EndArea();
        }
#endif
    }
}
