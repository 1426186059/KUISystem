using UnityEngine;
using UFont = UnityEngine.Font;
using UFontStyle = UnityEngine.FontStyle;

namespace KText.Example
{
    /// <summary>
    /// 例子 3：运行时输入框。用户在文本框中输入的任意中英文文字，
    /// 通过 KText_UnityLayer（底层 KText 光栅化 + Unity 上屏）实时显示到屏幕。
    /// 字体在 Inspector 中暴露，可随时更换。
    /// </summary>
    [ExecuteInEditMode]
    public class KTextLiveInput : MonoBehaviour
    {
        [Header("字体（Inspector 中可随时更换）")]
        public UFont FontAsset;
        public string FontName = "msyh";

        [Header("渲染设置")]
        public int FontSize = 32;
        public int PanelWidth = 900;
        public int PanelHeight = 220;
        public Color TextColor = Color.white;
        public TextAnchor Anchor = TextAnchor.UpperLeft;
        public HorizontalWrapMode HorizontalWrap = HorizontalWrapMode.Wrap;
        public VerticalWrapMode VerticalWrap = VerticalWrapMode.Overflow;

        [Header("默认文字（也可在 Inspector 中修改；运行时可实时输入）")]
        [TextArea(2, 6)]
        public string InputText = "在这里输入任意中英文文字，KText 会实时渲染\nKText Mir3 Version3 实时输入演示";

        private string _runtimeText;
        private Texture2D _bgTex;
        private GUIStyle _labelStyle;
        private KText_UnityLayer _layer;

        private void OnEnable()
        {
            // 解析同物体上的 KText_UnityLayer 上屏组件（标准组件引用方式）
            _layer = GetComponent<KText_UnityLayer>()
                     ?? gameObject.AddMissComponent<KText_UnityLayer>();

            if (string.IsNullOrEmpty(_runtimeText))
                _runtimeText = InputText;

            if (_bgTex == null)
            {
                _bgTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _bgTex.SetPixel(0, 0, Color.white);
                _bgTex.Apply(false);
            }
        }

        private UFont GetFont()
        {
            if (FontAsset != null) return FontAsset;
            return KTextCommon.Load(FontName, FontSize);
        }

        private void OnGUI()
        {
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                };
                _labelStyle.normal.textColor = new Color(0.6f, 0.85f, 1f);
            }

            var font = GetFont();

            // 准备本帧绘制（重置 KText_UnityLayer 的槽位索引）
            _layer.BeginFrame();

            // 顶部：运行时输入框（打字即实时渲染）
            GUILayout.BeginArea(new Rect(10, 10, PanelWidth, 90));
            GUILayout.Label("输入文字（实时渲染）：", _labelStyle);
            _runtimeText = GUILayout.TextArea(_runtimeText, GUILayout.Height(44));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("重置为默认", GUILayout.Width(100), GUILayout.Height(24)))
                _runtimeText = InputText;
            GUILayout.Label($"   字数: {_runtimeText.Length}", GUILayout.Height(24));
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            // 渲染区背景框
            var orig = GUI.color;
            GUI.color = new Color(0.05f, 0.05f, 0.08f, 0.85f);
            GUI.DrawTexture(new Rect(10, 110, PanelWidth, PanelHeight), _bgTex);
            GUI.color = orig;

            // 通过 KText_UnityLayer 渲染并直接上屏（输入变化会自动重新光栅化）
            _layer.Draw(_runtimeText, font, FontSize, UFontStyle.Normal,
                10, 110, PanelWidth, PanelHeight, TextColor,
                Anchor, HorizontalWrap, VerticalWrap);
        }

        private void OnDisable()
        {
            if (_bgTex != null) Object.DestroyImmediate(_bgTex);
            // KText_UnityLayer 组件自身的 OnDisable 会负责释放其纹理池
        }
    }
}
