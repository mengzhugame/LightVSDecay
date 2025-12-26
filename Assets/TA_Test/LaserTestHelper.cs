using UnityEngine;

namespace NeonGamblingTower.Laser
{
    /// <summary>
    /// 激光测试辅助工具 - Step 2
    /// 提供运行时按钮和滑块快速测试激光参数和反射功能
    /// </summary>
    public class LaserTestHelper : MonoBehaviour
    {
        [Header("=== 控制器引用 ===")]
        [SerializeField] private LaserLineRendererController laserController;
        
        [Header("=== 基础测试参数 ===")]
        [Range(5f, 30f)]
        [SerializeField] private float testLength = 19f;
        
        [Range(0.1f, 3f)]
        [SerializeField] private float testWidth = 0.5f;
        
        [Range(-90f, 90f)]
        [SerializeField] private float testAngle = 0f;
        
        [Header("=== 反射测试 ===")]
        [Range(0, 3)]
        [SerializeField] private int testMaxReflections = 0;
        
        [Header("=== 自动测试 ===")]
        [SerializeField] private bool autoRotate = false;
        [SerializeField] private float autoRotateSpeed = 30f;
        
        [SerializeField] private bool pulseWidth = false;
        [SerializeField] private float pulseSpeed = 2f;
        [SerializeField] private float pulseMinWidth = 0.2f;
        [SerializeField] private float pulseMaxWidth = 1.5f;
        
        [Header("=== 运行时信息（只读）===")]
        [SerializeField] private int currentReflections = 0;
        [SerializeField] private bool hasHitEnemy = false;
        [SerializeField] private Vector3 laserEndPoint;
        
        private float previousLength;
        private float previousWidth;
        private float previousAngle;
        private int previousReflections;
        
        private void Start()
        {
            if (laserController == null)
            {
                laserController = GetComponent<LaserLineRendererController>();
            }
            
            previousLength = testLength;
            previousWidth = testWidth;
            previousAngle = testAngle;
            previousReflections = testMaxReflections;
        }
        
        private void Update()
        {
            // 检测 Inspector 中参数变化
            CheckParameterChanges();
            
            // 自动旋转测试
            if (autoRotate)
            {
                AutoRotateTest();
            }
            
            // 宽度脉冲测试
            if (pulseWidth)
            {
                PulseWidthTest();
            }
            
            // 更新运行时信息
            UpdateRuntimeInfo();
        }
        
        /// <summary>
        /// 检测参数变化并应用
        /// </summary>
        private void CheckParameterChanges()
        {
            if (laserController == null) return;
            
            // 长度变化
            if (!Mathf.Approximately(testLength, previousLength))
            {
                laserController.SetLength(testLength);
                previousLength = testLength;
                Debug.Log($"[LaserTest] 长度设置为: {testLength}");
            }
            
            // 宽度变化
            if (!Mathf.Approximately(testWidth, previousWidth))
            {
                laserController.SetWidth(testWidth);
                previousWidth = testWidth;
                Debug.Log($"[LaserTest] 宽度设置为: {testWidth}");
            }
            
            // 角度变化（仅当不自动旋转时）
            if (!autoRotate && !Mathf.Approximately(testAngle, previousAngle))
            {
                laserController.SetRotation(testAngle);
                previousAngle = testAngle;
                Debug.Log($"[LaserTest] 角度设置为: {testAngle}");
            }
            
            // 反射次数变化
            if (testMaxReflections != previousReflections)
            {
                laserController.SetMaxReflections(testMaxReflections);
                previousReflections = testMaxReflections;
                Debug.Log($"[LaserTest] 最大反射次数设置为: {testMaxReflections}");
            }
        }
        
        /// <summary>
        /// 自动旋转测试
        /// </summary>
        private void AutoRotateTest()
        {
            if (laserController == null) return;
            
            testAngle = Mathf.PingPong(Time.time * autoRotateSpeed, 180f) - 90f;
            laserController.SetRotation(testAngle);
        }
        
        /// <summary>
        /// 宽度脉冲测试
        /// </summary>
        private void PulseWidthTest()
        {
            if (laserController == null) return;
            
            float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) / 2f;
            testWidth = Mathf.Lerp(pulseMinWidth, pulseMaxWidth, t);
            laserController.SetWidth(testWidth);
        }
        
        /// <summary>
        /// 更新运行时信息
        /// </summary>
        private void UpdateRuntimeInfo()
        {
            if (laserController == null) return;
            
            currentReflections = laserController.GetCurrentReflectionCount();
            hasHitEnemy = laserController.HasHitEnemy();
            laserEndPoint = laserController.GetEndPoint();
        }
        
        /// <summary>
        /// 重置为默认值
        /// </summary>
        [ContextMenu("重置参数")]
        public void ResetParameters()
        {
            testLength = 19f;
            testWidth = 0.5f;
            testAngle = 0f;
            testMaxReflections = 0;
            autoRotate = false;
            pulseWidth = false;
            
            if (laserController != null)
            {
                laserController.SetLength(testLength);
                laserController.SetWidth(testWidth);
                laserController.SetRotation(testAngle);
                laserController.SetMaxReflections(testMaxReflections);
            }
            
            Debug.Log("[LaserTest] 参数已重置");
        }
        
        /// <summary>
        /// 测试最大长度
        /// </summary>
        [ContextMenu("测试 - 最大长度")]
        public void TestMaxLength()
        {
            testLength = 30f;
            if (laserController != null)
            {
                laserController.SetLength(testLength);
            }
        }
        
        /// <summary>
        /// 测试最大宽度
        /// </summary>
        [ContextMenu("测试 - 最大宽度")]
        public void TestMaxWidth()
        {
            testWidth = 3f;
            if (laserController != null)
            {
                laserController.SetWidth(testWidth);
            }
        }
        
        /// <summary>
        /// 测试1次反射
        /// </summary>
        [ContextMenu("测试 - 1次反射")]
        public void TestReflection1()
        {
            testMaxReflections = 1;
            if (laserController != null)
            {
                laserController.SetMaxReflections(testMaxReflections);
            }
            Debug.Log("[LaserTest] 设置1次反射");
        }
        
        /// <summary>
        /// 测试2次反射
        /// </summary>
        [ContextMenu("测试 - 2次反射")]
        public void TestReflection2()
        {
            testMaxReflections = 2;
            if (laserController != null)
            {
                laserController.SetMaxReflections(testMaxReflections);
            }
            Debug.Log("[LaserTest] 设置2次反射");
        }
        
        /// <summary>
        /// 测试3次反射
        /// </summary>
        [ContextMenu("测试 - 3次反射")]
        public void TestReflection3()
        {
            testMaxReflections = 3;
            if (laserController != null)
            {
                laserController.SetMaxReflections(testMaxReflections);
            }
            Debug.Log("[LaserTest] 设置3次反射");
        }
        
        /// <summary>
        /// 关闭反射
        /// </summary>
        [ContextMenu("测试 - 关闭反射")]
        public void TestReflectionOff()
        {
            testMaxReflections = 0;
            if (laserController != null)
            {
                laserController.SetMaxReflections(testMaxReflections);
            }
            Debug.Log("[LaserTest] 关闭反射");
        }
    }
}