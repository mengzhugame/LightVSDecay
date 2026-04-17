// ============================================================
// AndroidPostBuildProcessor.cs
// 文件位置: Assets/Editor/AndroidPostBuildProcessor.cs
// 用途：Android 打包后处理器
//   - 在 Unity 生成 Gradle 工程之后，以代码方式向
//     已生成的 AndroidManifest.xml 注入所需权限。
//   - 不在 Assets/Plugins/Android/ 放任何 AndroidManifest.xml，
//     避免干扰 Unity 的图标资源生成流程。
// ============================================================

#if UNITY_EDITOR && UNITY_ANDROID
using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

namespace LightVsDecay.Editor
{
    public class AndroidPostBuildProcessor : IPostGenerateGradleAndroidProject
    {
        // 回调执行顺序（数值越小越先执行）
        public int callbackOrder => 0;

        // 需要注入的权限列表
        private static readonly string[] RequiredPermissions = new[]
        {
            "android.permission.VIBRATE",
        };

        public void OnPostGenerateGradleAndroidProject(string gradleProjectPath)
        {
            // gradleProjectPath 指向 tuanjieLibrary（或 unityLibrary）目录
            string manifestPath = Path.Combine(gradleProjectPath, "src", "main", "AndroidManifest.xml");

            if (!File.Exists(manifestPath))
            {
                Debug.LogWarning($"[AndroidPostBuildProcessor] 未找到 AndroidManifest.xml：{manifestPath}");
                return;
            }

            var doc = new XmlDocument();
            doc.Load(manifestPath);

            XmlNamespaceManager nsManager = new XmlNamespaceManager(doc.NameTable);
            nsManager.AddNamespace("android", "http://schemas.android.com/apk/res/android");

            XmlElement manifest = doc.DocumentElement;
            if (manifest == null) return;

            bool modified = false;
            foreach (string permission in RequiredPermissions)
            {
                // 检查权限是否已存在，避免重复添加
                string xpath = $"uses-permission[@android:name='{permission}']";
                XmlNode existing = manifest.SelectSingleNode(xpath, nsManager);
                if (existing != null) continue;

                XmlElement permNode = doc.CreateElement("uses-permission");
                permNode.SetAttribute("name", "http://schemas.android.com/apk/res/android", permission);
                manifest.AppendChild(permNode);
                modified = true;
                Debug.Log($"[AndroidPostBuildProcessor] 已注入权限：{permission}");
            }

            if (modified)
                doc.Save(manifestPath);
        }
    }
}
#endif
