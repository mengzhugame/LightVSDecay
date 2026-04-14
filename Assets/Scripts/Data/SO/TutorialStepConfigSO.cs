using UnityEngine;

namespace LightVsDecay.Data.SO
{
    /// <summary>挖孔形状</summary>
    public enum HoleShape
    {
        RoundedRect,  // 圆角矩形（默认，cornerRadius 控制圆角大小）
        Rect,         // 直角矩形（cornerRadius = 0）
        Circle,       // 正圆（自动取孔洞短边半径，忽略 cornerRadius）
    }


    /// <summary>
    /// 新手引导步骤配置
    /// 每个引导步骤对应一个 ScriptableObject 资产，配置手指 Prefab 路径、挂载位置和 SDF 挖孔参数。
    /// 创建：右键 → Create → LightVsDecay → Tutorial Step Config
    /// 存放路径：Assets/Resources/Data/Tutorial/
    /// </summary>
    [CreateAssetMenu(fileName = "TutorialStepConfig", menuName = "LightVsDecay/Tutorial Step Config", order = 101)]
    public class TutorialStepConfigSO : ScriptableObject
    {
        [Header("基础信息")]
        [Tooltip("步骤唯一标识（仅用于日志调试，如 tech_select_node）")]
        public string stepID;

        [Header("手指特效 Prefab")]
        [Tooltip("Resources 相对路径，如 Effects/Tutorial/FingerPointerWithRing")]
        public string prefabPath = "Effects/Tutorial/FingerPointerWithRing";

        [Tooltip("Prefab 挂载父节点的场景层级路径（如 Canvas/BattleUI），留空则挂在根 Canvas 下）")]
        public string parentPath = "";

        [Tooltip("Prefab 相对父节点的本地坐标偏移")]
        public Vector3 localPosition = Vector3.zero;

        [Tooltip("Prefab 本地缩放（默认 1,1,1）")]
        public Vector3 localScale = Vector3.one;

        [Header("SDF 挖孔遮罩（仅系统界面引导使用）")]
        [Tooltip("是否启用 TutorialSpotlightOverlay SDF 挖孔遮罩")]
        public bool useSpotlightOverlay = true;

        [Tooltip("挖孔形状：RoundedRect=圆角矩形 / Rect=直角矩形 / Circle=正圆")]
        public HoleShape holeShape = HoleShape.RoundedRect;

        [Tooltip("挖孔区域在目标四周的扩展边距（像素）")]
        public Vector2 holePadding = new Vector2(24f, 24f);

        [Tooltip("圆角半径（像素）。holeShape=RoundedRect 时生效；Circle/Rect 时自动忽略")]
        public float cornerRadius = 20f;

        [Tooltip("遮罩上显示的引导文案（留空则不显示文字）")]
        [TextArea(1, 3)]
        public string overlayMessage;
    }
}
