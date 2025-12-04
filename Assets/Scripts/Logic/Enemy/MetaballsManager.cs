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
        [SerializeField] private Vector2Int rtResolutionScale = new Vector2Int(2, 2); // 屏幕分辨率除以此值
        [SerializeField] private RenderTextureFormat rtFormat = RenderTextureFormat.ARGB32;
        
        [Header("Camera Settings")]
        [SerializeField] private LayerMask enemyBodyLayer; // 只渲染 EnemyBody Layer
        [SerializeField] private Color cameraBackground = Color.black; // 纯黑背景
        [SerializeField] private float cameraSize = 10f; // Orthographic Size
        
        [Header("UI Display")]
        [SerializeField] private GameObject displayBodyRTObj; // 承载body的RT的物体
        
        // 内部引用
        private RenderTexture metaballsRT;
        private Camera metaballsCamera;
        
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
                filterMode = FilterMode.Bilinear, // 模糊效果更好
                wrapMode = TextureWrapMode.Clamp
            };
            
            metaballsRT.Create();
            
            Debug.Log($"[MetaballsManager] RenderTexture created: {width}x{height}, Format: {rtFormat}");
        }
        
        /// <summary>
        /// 配置专用Camera（只渲染EnemyBody Layer）
        /// </summary>
        private void SetupCamera()
        {
            // 创建新的GameObject挂载Camera
            GameObject cameraObj = new GameObject("MetaballsCamera");
            cameraObj.transform.SetParent(transform);
            cameraObj.transform.position = new Vector3(0, 0, -10); // Z轴负值
            
            metaballsCamera = cameraObj.AddComponent<Camera>();
            
            // Camera配置
            metaballsCamera.orthographic = true;
            metaballsCamera.orthographicSize = cameraSize;
            metaballsCamera.clearFlags = CameraClearFlags.SolidColor;
            metaballsCamera.backgroundColor = cameraBackground;
            metaballsCamera.cullingMask = enemyBodyLayer; // 只渲染EnemyBody
            metaballsCamera.targetTexture = metaballsRT;
            metaballsCamera.depth = -100; // 渲染优先级最低
            
            Debug.Log($"[MetaballsManager] Camera setup complete. Culling Mask: {LayerMask.LayerToName((int)Mathf.Log(enemyBodyLayer.value, 2))}");
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

            Material mat = displayBodyRTObj.GetComponent<MeshRenderer>().material;
            mat.SetTexture("_MainTex",metaballsRT);
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
                Debug.Log("[MetaballsManager] RenderTexture cleaned up");
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
    }
}