using UnityEngine;
using LightVsDecay.Core;

namespace LightVsDecay.Weapon
{
    /// <summary>
    /// 光棱塔控制器 - 负责输入处理和旋转控制
    /// 挂载到 LaserPivot 节点上，控制塔身和激光的旋转
    /// </summary>
    public class TurretController : MonoBehaviour
    {
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Inspector 可配置参数
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        [Header("旋转设置")]
        [Tooltip("旋转灵敏度（数值越大，滑动相同距离旋转角度越大）")]
        [SerializeField] private float rotationSensitivity = 180f;
        
        [Tooltip("旋转平滑度（数值越大越跟手，越小越平滑）")]
        [Range(5f, 50f)]
        [SerializeField] private float rotationSmoothness = 15f;
        
        [Tooltip("最小旋转角度（朝右）")]
        [SerializeField] private float minAngle = -90f;
        
        [Tooltip("最大旋转角度（朝左）")]
        [SerializeField] private float maxAngle = 90f;
        
        [Header("大招模式")]
        [Tooltip("大招期间是否禁用手动控制")]
        [SerializeField] private bool disableInputDuringUlt = true;
        
        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 运行时状态
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private float currentAngle = 0f;      // 当前角度（0度 = 朝上）
        private float targetAngle = 0f;       // 目标角度（用于平滑插值）
        private bool isUltActive = false;     // 大招是否激活
        private bool isDragging = false;      // 是否正在拖拽
        
        // 触摸/鼠标输入状态
        private Vector2 lastInputPosition;
        private int activeTouchId = -1;       // 当前激活的触摸ID
        
        // 缓存
        private Transform cachedTransform;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 事件（供其他系统订阅）
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>当旋转角度改变时触发</summary>
        public event System.Action<float> OnAngleChanged;
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // Unity 生命周期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void Awake()
        {
            cachedTransform = transform;
            
            // 读取当前旋转角度作为初始值
            currentAngle = cachedTransform.localEulerAngles.z;
            
            // 将 0-360 转换为 -180~180 范围
            if (currentAngle > 180f)
                currentAngle -= 360f;
            
            targetAngle = currentAngle;
        }
        
        private void Update()
        {
            // 大招期间禁用手动控制
            if (isUltActive && disableInputDuringUlt)
                return;
            
            // 处理输入
            ProcessInput();
            
            // 平滑旋转
            ApplySmoothRotation();
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 输入处理
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        private void ProcessInput()
        {
            // 优先处理触摸输入（移动端）
            if (Input.touchCount > 0)
            {
                ProcessTouchInput();
            }
            // 鼠标输入（编辑器/PC）
            else
            {
                ProcessMouseInput();
            }
        }
        
        /// <summary>
        /// 处理触摸输入
        /// </summary>
        private void ProcessTouchInput()
        {
            // 遍历所有触摸点
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                
                switch (touch.phase)
                {
                    case TouchPhase.Began:
                        // 如果还没有激活的触摸，记录这个触摸
                        if (!isDragging)
                        {
                            isDragging = true;
                            activeTouchId = touch.fingerId;
                            lastInputPosition = touch.position;
                            
                            if (showDebugInfo)
                                Debug.Log($"[TurretController] 触摸开始 ID:{activeTouchId}");
                        }
                        break;
                        
                    case TouchPhase.Moved:
                        // 只响应激活的触摸
                        if (isDragging && touch.fingerId == activeTouchId)
                        {
                            HandleDrag(touch.position);
                        }
                        break;
                        
                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        // 释放对应的触摸
                        if (touch.fingerId == activeTouchId)
                        {
                            isDragging = false;
                            activeTouchId = -1;
                            
                            if (showDebugInfo)
                                Debug.Log("[TurretController] 触摸结束");
                        }
                        break;
                }
            }
        }
        
        /// <summary>
        /// 处理鼠标输入（编辑器测试用）
        /// </summary>
        private void ProcessMouseInput()
        {
            if (Input.GetMouseButtonDown(0))
            {
                isDragging = true;
                lastInputPosition = Input.mousePosition;
                
                if (showDebugInfo)
                    Debug.Log("[TurretController] 鼠标按下");
            }
            else if (Input.GetMouseButton(0) && isDragging)
            {
                HandleDrag(Input.mousePosition);
            }
            else if (Input.GetMouseButtonUp(0))
            {
                isDragging = false;
                
                if (showDebugInfo)
                    Debug.Log("[TurretController] 鼠标释放");
            }
        }
        
        /// <summary>
        /// 处理拖拽逻辑 - 核心旋转计算
        /// </summary>
        private void HandleDrag(Vector2 currentPosition)
        {
            // 计算水平滑动距离（归一化到屏幕宽度）
            float deltaX = (currentPosition.x - lastInputPosition.x) / Screen.width;
            
            // 更新上一帧位置
            lastInputPosition = currentPosition;
            
            // 计算角度变化
            // 手指向右滑动（deltaX > 0）→ 塔向右转 → 角度减小
            // 手指向左滑动（deltaX < 0）→ 塔向左转 → 角度增大
            float angleDelta = -deltaX * rotationSensitivity;
            
            // 更新目标角度
            targetAngle += angleDelta;
            
            // 限制角度范围
            targetAngle = Mathf.Clamp(targetAngle, minAngle, maxAngle);
            
            if (showDebugInfo)
                Debug.Log($"[TurretController] DeltaX:{deltaX:F3} AngleDelta:{angleDelta:F2} Target:{targetAngle:F1}");
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 旋转应用
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 平滑应用旋转（避免卡顿）
        /// </summary>
        private void ApplySmoothRotation()
        {
            // 平滑插值到目标角度
            currentAngle = Mathf.Lerp(currentAngle, targetAngle, Time.deltaTime * rotationSmoothness);
            
            // 应用旋转（只修改 Z 轴）
            cachedTransform.localRotation = Quaternion.Euler(0f, 0f, currentAngle);
            
            // 触发事件
            OnAngleChanged?.Invoke(currentAngle);
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 公共接口
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
        /// <summary>
        /// 设置大招状态（由 LaserController 调用）
        /// </summary>
        public void SetUltActive(bool active)
        {
            isUltActive = active;
            
            if (active)
            {
                // 大招开始时停止拖拽
                isDragging = false;
                activeTouchId = -1;
            }
        }
        
        /// <summary>
        /// 强制设置角度（用于大招360度旋转或技能效果）
        /// </summary>
        public void SetAngle(float angle)
        {
            targetAngle = angle;
            currentAngle = angle;
            cachedTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
        
        /// <summary>
        /// 强制设置目标角度（带平滑过渡）
        /// </summary>
        public void SetTargetAngle(float angle)
        {
            targetAngle = Mathf.Clamp(angle, minAngle, maxAngle);
        }
        
        /// <summary>
        /// 获取当前角度
        /// </summary>
        public float GetCurrentAngle() => currentAngle;
        
        /// <summary>
        /// 获取是否正在拖拽
        /// </summary>
        public bool IsDragging() => isDragging;
        
        /// <summary>
        /// 重置到初始位置（朝上）
        /// </summary>
        public void ResetRotation()
        {
            targetAngle = 0f;
            currentAngle = 0f;
            cachedTransform.localRotation = Quaternion.identity;
        }
        
        /// <summary>
        /// 设置旋转灵敏度（用于设置菜单）
        /// </summary>
        public void SetSensitivity(float sensitivity)
        {
            rotationSensitivity = sensitivity;
        }
        
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        // 编辑器调试
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        
#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // 绘制旋转范围
            Vector3 position = transform.position;
            float length = 3f;
            
            // 最小角度（右边界）
            Gizmos.color = Color.red;
            Vector3 minDir = Quaternion.Euler(0, 0, minAngle) * Vector3.up;
            Gizmos.DrawLine(position, position + minDir * length);
            
            // 最大角度（左边界）
            Gizmos.color = Color.blue;
            Vector3 maxDir = Quaternion.Euler(0, 0, maxAngle) * Vector3.up;
            Gizmos.DrawLine(position, position + maxDir * length);
            
            // 当前角度
            Gizmos.color = Color.green;
            Vector3 currentDir = Quaternion.Euler(0, 0, currentAngle) * Vector3.up;
            Gizmos.DrawLine(position, position + currentDir * length);
        }
#endif
    }
}