using UnityEngine;

namespace LightVsDecay.Debugging
{
    /// <summary>
    /// Minimal FPS display for device testing. Auto-created at startup and only draws text.
    /// </summary>
    public sealed class FpsDisplay : MonoBehaviour
    {
        private const float RefreshInterval = 0.25f;

        private static FpsDisplay instance;

        private float elapsed;
        private int frames;
        private float fps;
        private GUIStyle labelStyle;
        private GUIStyle backgroundStyle;

        // [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null)
            {
                return;
            }

            GameObject go = new GameObject(nameof(FpsDisplay));
            DontDestroyOnLoad(go);
            instance = go.AddComponent<FpsDisplay>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            frames++;

            if (elapsed >= RefreshInterval)
            {
                fps = frames / elapsed;
                frames = 0;
                elapsed = 0f;
            }
        }

        private void OnGUI()
        {
            EnsureStyles();

            float scale = Mathf.Clamp(Screen.dpi / 220f, 1f, 1.8f);
            float width = 150f * scale;
            float height = 48f * scale;
            Rect rect = new Rect(Screen.width - width - 12f * scale, 12f * scale, width, height);

            GUI.Box(rect, GUIContent.none, backgroundStyle);

            labelStyle.normal.textColor = fps >= 58f
                ? new Color(0.45f, 1f, 0.45f)
                : fps >= 45f
                    ? new Color(1f, 0.85f, 0.25f)
                    : new Color(1f, 0.35f, 0.28f);

            GUI.Label(rect, $"FPS {fps:F1}", labelStyle);
        }

        private void EnsureStyles()
        {
            if (labelStyle != null)
            {
                return;
            }

            Texture2D background = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            background.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.65f));
            background.Apply();

            int fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height / 34f, 20f, 36f));
            backgroundStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = background }
            };

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = fontSize,
                fontStyle = FontStyle.Bold
            };
        }
    }
}
