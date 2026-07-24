// ============================================================================
// KTextLiveInput.cs — 例子3：运行时输入文字，KText_UnityLayer 实时上屏
// ============================================================================
//
// 顶部运行时文本框输入任意中英文，文字变化即赋给 KText_UnityLayer.Text，
// 组件自动重新光栅化并上屏（输入什么，显示什么）。
//
// ============================================================================

using KUISystem;
using UnityEngine;
using UFont = UnityEngine.Font;
using UFontStyle = UnityEngine.FontStyle;

namespace KText.Example
{
    [ExecuteInEditMode]
    public class KTextLiveInput : MonoBehaviour
    {
        [Header("默认文字")]
        [TextArea(2, 5)]
        public string InputText = "在这里输入任意中英文文字，KText 会实时渲染\nKText Mir3 Version3 实时输入演示";

        [Header("字体")]
        public UFont FontAsset;
        public string FontName = "msyh";
        public int FontSize = 32;
        public Color TextColor = Color.white;

        [Header("对齐 / 溢出")]
        public TextAnchor Anchor = TextAnchor.UpperLeft;
        public HorizontalWrapMode HorizontalWrap = HorizontalWrapMode.Wrap;
        public VerticalWrapMode VerticalWrap = VerticalWrapMode.Overflow;

        [Header("上屏组件（留空则自动获取同物体上的 KText_UnityLayer）")]
        public KText_UnityLayer UnityLayer;

        private string _runtimeText;
        private UFont _cachedFont;
        private string _cachedFontKey = "";

        private void OnEnable()
        {
            if (UnityLayer == null)
                UnityLayer = GetComponent<KText_UnityLayer>() ?? gameObject.AddComponent<KText_UnityLayer>();
            if (string.IsNullOrEmpty(_runtimeText))
                _runtimeText = InputText;
        }

        private UFont GetFont()
        {
            string key = (FontAsset != null ? "asset" : "name:" + FontName) + ":" + FontSize;
            if (_cachedFont == null || _cachedFontKey != key)
            {
                _cachedFont = FontAsset != null ? FontAsset : KTextCommon.Load(FontName);
                _cachedFontKey = key;
            }
            return _cachedFont;
        }

        private void OnGUI()
        {
            // 顶部输入框（运行时打字）
            GUILayout.BeginArea(new Rect(0, 0, Screen.width, 104));
            GUILayout.Label("输入文字（支持中英文混排、多行）：", GUILayout.Width(320));
            string newText = GUILayout.TextArea(_runtimeText, GUILayout.Height(50));
            if (newText != _runtimeText)
                _runtimeText = newText;                       // 输入变化 -> 下一帧组件自动重绘
            if (GUILayout.Button("重置为默认", GUILayout.Width(100)))
                _runtimeText = InputText;
            GUILayout.EndArea();

            UFont font = GetFont();
            if (font == null || UnityLayer == null) return;

            // 把输入文字/样式推给 KText_UnityLayer；组件自身 OnGUI 随后渲染
            UnityLayer.Text = _runtimeText;
            UnityLayer.FontAsset = font;
            UnityLayer.FontSize = FontSize;
            UnityLayer.FontStyle = UFontStyle.Normal;
            UnityLayer.TextColor = TextColor;
            UnityLayer.Alignment = Anchor;
            UnityLayer.HorizontalOverflow = HorizontalWrap;
            UnityLayer.VerticalOverflow = VerticalWrap;
            UnityLayer.AutoSize = true;
            UnityLayer.DisplayRect = new Rect(10, 110, Mathf.Min(Screen.width - 20, 760), 1);
        }
    }
}
