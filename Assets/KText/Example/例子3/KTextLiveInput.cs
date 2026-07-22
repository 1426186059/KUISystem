using UnityEngine;
using UFont = UnityEngine.Font;
using UFontStyle = UnityEngine.FontStyle;

namespace KText.Example
{
    /// <summary>
    /// 例子 3：运行时输入框。用户在文本框中输入的任意中英文文字，
    /// 会实时通过 KText_Mir3_Version3 重新渲染并显示。
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
        private Texture2D _tex;
        private Texture2D _bgTex;
        private GUIStyle _labelStyle;
        private UFont _lastFont;
        private int _lastFontSize;
        private string _lastText;

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(_runtimeText))
                _runtimeText = InputText;

            if (_bgTex == null)
            {
                _bgTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _bgTex.SetPixel(0, 0, Color.white);
                _bgTex.Apply(false);
            }
            RebuildIfNeeded(true);
        }

        private UFont GetFont()
        {
            if (FontAsset != null) return FontAsset;
            return KTextCommon.Load(FontName, FontSize);
        }

        private void RebuildIfNeeded(bool force = false)
        {
            var font = GetFont();
            if (!force && font == _lastFont && FontSize == _lastFontSize &&
                _runtimeText == _lastText && _tex != null)
                return;

            _lastFont = font;
            _lastFontSize = FontSize;
            _lastText = _runtimeText;
            Render(font);
        }

        private byte[] pixelBuf = new byte[0];

        private void Render(UFont font)
        {
            if (font == null) return;

            int w = PanelWidth, h = PanelHeight;
            if(pixelBuf.Length != w * h * 4)
            {
                pixelBuf = new byte[w * h * 4];
            }

            var buf = pixelBuf;
            //for (int i = 0; i < buf.Length; i += 4)
            //{ buf[i] = 0; buf[i + 1] = 0; buf[i + 2] = 0; buf[i + 3] = 0; }

            try
            {
                KText_Mir3_Version3.DrawText(buf, w, h, w * 4,
                    _runtimeText, font, FontSize, UFontStyle.Normal,
                    8, 8, w, h, TextColor,
                    Anchor, HorizontalWrap, VerticalWrap);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[KTextLiveInput] 渲染失败: {e.Message}");
                return;
            }

            if (_tex != null) Object.DestroyImmediate(_tex);
            _tex = KTextCommon.CreateTexture(buf, w, h);
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

            RebuildIfNeeded();

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

            if (_tex != null)
                Graphics.DrawTexture(new Rect(10, 110, PanelWidth, PanelHeight), _tex);
        }

        private void OnDisable()
        {
            if (_tex != null) Object.DestroyImmediate(_tex);
            if (_bgTex != null) Object.DestroyImmediate(_bgTex);
        }
    }
}
