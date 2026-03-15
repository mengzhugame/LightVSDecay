// ── MetaballsManager.cs 完整替换 ──
// 文件位置: Assets/Scripts/Logic/Enemy/MetaballsManager.cs

using UnityEngine;
using UnityEngine.UI;

namespace LightVsDecay.Logic.Enemy
{
    /// <summary>
    /// Metaballs 视觉欺诈系统管理器
    /// 负责：RenderTexture创建、专用Camera配置、UI RawImage承载
    /// </summary>
    public class MetaballsManager : MonoBehaviour
    {
        [Header("RT Configuration")]
        [SerializeField] private Vector2Int rtResolutionScale = new Vector2Int(2, 2);
        [SerializeField] private RenderTextureFormat rtFormat = RenderTextureFormat.ARGB32;
        
        [Header("Camera Settings")]
        [SerializeField] private LayerMask enemyBodyLayer;
        [SerializeField] private Color cameraBackground = Color.black; // 必须保持黑色
        [SerializeField] private float cameraSize = 10f;
        
        [Header("UI Display")]
        [SerializeField] private GameObject displayBodyRTObj;
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        
        // 内部引用
        private RenderTexture metaballsRT;
        private Camera metaballsCamera;
        private Material thresholdMaterial; // 【新增】缓存阈值材质引用
        
        // 【新增】Shader属性ID缓存
        private static readonly int BlobColorID = Shader.PropertyToID("_Color");
        
        private void Awake()
        {
            SetupRenderTexture();
            SetupCamera();
            AttachToUI();
        }
        
        private void OnDestroy()
        {
            CleanupRenderTexture();
        }
        
        /// <summary>
        /// 创建RenderTexture（分辨率为屏幕的1/2）
        /// </summary>
        private void SetupRenderTexture()
        {
            int width = 1024 / rtResolutionScale.x;
            int height = 1024 / rtResolutionScale.y;
            
            metaballsRT = new RenderTexture(width, height, 0, rtFormat)
            {
                name = "MetaballsRT",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            
            metaballsRT.Create();
            if (showDebugInfo)
            {
                Debug.Log($"[MetaballsManager] RenderTexture created: {width}x{height}, Format: {rtFormat}");
            }
        }
        
        /// <summary>
        /// 配置专用Camera（只渲染EnemyBody Layer）
        /// </summary>
        private void SetupCamera()
        {
            GameObject cameraObj = new GameObject("MetaballsCamera");
            cameraObj.transform.SetParent(transform);
            cameraObj.transform.position = new Vector3(0, 0, -10);
            
            metaballsCamera = cameraObj.AddComponent<Camera>();
            
            metaballsCamera.orthographic = true;
            metaballsCamera.orthographicSize = cameraSize;
            metaballsCamera.clearFlags = CameraClearFlags.SolidColor;
            metaballsCamera.backgroundColor = cameraBackground;
            metaballsCamera.cullingMask = enemyBodyLayer;
            metaballsCamera.targetTexture = metaballsRT;
            metaballsCamera.depth = -100;
            if (showDebugInfo)
            {
                Debug.Log(
                    $"[MetaballsManager] Camera setup complete. Culling Mask: {LayerMask.LayerToName((int) Mathf.Log(enemyBodyLayer.value, 2))}");
            }
        }
        
        /// <summary>
        /// 将RenderTexture附加到UI RawImage
        /// </summary>
        private void AttachToUI()
        {
            if (displayBodyRTObj == null)
            {
                Debug.LogError("[MetaballsManager] displayBodyRT is not assigned!");
                return;
            }

            // 【修改】缓存材质引用，后续 SetBlobColor 复用
            thresholdMaterial = displayBodyRTObj.GetComponent<MeshRenderer>().material;
            thresholdMaterial.SetTexture("_MainTex", metaballsRT);
        }
        
        /// <summary>
        /// 清理RenderTexture（防止内存泄漏）
        /// </summary>
        private void CleanupRenderTexture()
        {
            if (metaballsRT != null)
            {
                metaballsRT.Release();
                Destroy(metaballsRT);
                if (showDebugInfo)
                {
                    Debug.Log("[MetaballsManager] RenderTexture cleaned up");
                }
            }
        }
        
        /// <summary>
        /// 动态调整Camera视野（如果需要跟随屏幕尺寸变化）
        /// </summary>
        public void UpdateCameraSize(float newSize)
        {
            if (metaballsCamera != null)
            {
                metaballsCamera.orthographicSize = newSize;
            }
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 【新增】章节流体颜色
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 设置流体怪物底色（由 GameManager 在章节初始化时调用）
        /// 修改的是 MetaballsThreshold shader 的 _Color 属性
        /// </summary>
        public void SetBlobColor(Color color)
        {
            if (thresholdMaterial != null)
            {
                thresholdMaterial.SetColor(BlobColorID, color);
                
                if (showDebugInfo)
                {
                    Debug.Log($"[MetaballsManager] Blob颜色已设置: {color}");
                }
            }
            else
            {
                Debug.LogWarning("[MetaballsManager] thresholdMaterial 未初始化，无法设置颜色！");
            }
        }
    }
}