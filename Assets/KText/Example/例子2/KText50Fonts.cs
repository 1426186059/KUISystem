// ============================================================================
// KText50Fonts.cs — 例子2：用 KText_UnityLayer（仿 UGUI Text）渲染 50 行不同中英文
// ============================================================================
//
// 同一个字体渲染 50 行（每行不同中英文），字体在 Inspector 暴露、可随时更换。
// 50 行拼成一段多行文字，直接赋给 KText_UnityLayer.Text，由组件上屏。
//
// ============================================================================

using KText;
using UnityEngine;
using UFont = UnityEngine.Font;
using UFontStyle = UnityEngine.FontStyle;

namespace KText.Example
{
    [ExecuteInEditMode]
    public class KText50Fonts : MonoBehaviour
    {
        [Header("字体（Inspector 拖入或填名称，可随时更换）")]
        public UFont FontAsset;
        public string FontName = "msyh";
        public int FontSize = 26;
        public Color TextColor = Color.white;

        [Header("内容")]
        public int LineCount = 50;

        [Header("上屏组件（留空则自动获取同物体上的 KText_UnityLayer）")]
        public KText_UnityLayer UnityLayer;

        private readonly string[] Cn =
        {
            "乾坤", "江湖", "风云", "侠客", "剑气", "明月", "清风", "落英",
            "山河", "星辰", "烟雨", "长歌", "碧落", "红尘", "霜雪", "流年",
            "苍穹", "幽兰", "青衫", "醉梦", "天涯", "听雨", "归鸿", "寒江",
        };
        private readonly string[] En =
        {
            "KText", "Mir3", "Unity", "Font", "Render", "Graphics", "Engine",
            "Dragon", "Phoenix", "Sword", "Magic", "Shadow", "Light", "Storm",
            "Legend", "Legacy", "Crystal", "Cosmos", "Spirit", "Galaxy",
        };

        private System.Random _rnd = new System.Random();
        private string _content = "";
        private UFont _cachedFont;
        private string _cachedFontKey = "";

        private void OnEnable()
        {
            if (UnityLayer == null)
                UnityLayer = GetComponent<KText_UnityLayer>() ?? gameObject.AddComponent<KText_UnityLayer>();
            BuildContent();
        }

        private UFont GetFont()
        {
            string key = (FontAsset != null ? "asset" : "name:" + FontName) + ":" + FontSize;
            if (_cachedFont == null || _cachedFontKey != key)
            {
                _cachedFont = FontAsset != null ? FontAsset : KTextCommon.Load(FontName, FontSize);
                _cachedFontKey = key;
            }
            return _cachedFont;
        }

        private void BuildContent()
        {
            var sb = new System.Text.StringBuilder();
            int n = Mathf.Clamp(LineCount, 1, 500);
            for (int i = 0; i < n; i++)
            {
                string cn = Cn[_rnd.Next(Cn.Length)];
                string en = En[_rnd.Next(En.Length)];
                int num = _rnd.Next(1000, 9999);
                sb.Append(cn).Append(' ').Append(en).Append(' ').Append(num);
                if (i < n - 1) sb.Append('\n');
            }
            _content = sb.ToString();
        }

        private void OnGUI()
        {
            // 顶部工具栏
            GUILayout.BeginArea(new Rect(0, 0, Screen.width, 46));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("随机文字", GUILayout.Width(80)))
                BuildContent();
            GUILayout.Space(10);
            GUILayout.Label("字号", GUILayout.Width(36));
            FontSize = (int)GUILayout.HorizontalSlider(FontSize, 12, 48, GUILayout.Width(120));
            GUILayout.Space(10);
            GUILayout.Label("行数: " + LineCount, GUILayout.Width(70));
            if (GUILayout.Button("+", GUILayout.Width(28))) { LineCount = Mathf.Min(500, LineCount + 1); BuildContent(); }
            if (GUILayout.Button("-", GUILayout.Width(28))) { LineCount = Mathf.Max(1, LineCount - 1); BuildContent(); }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            UFont font = GetFont();
            if (font == null || UnityLayer == null) return;

            // 把内容/样式推给 KText_UnityLayer；组件自身 OnGUI 随后渲染
            UnityLayer.Text = _content;
            UnityLayer.FontAsset = font;
            UnityLayer.FontSize = FontSize;
            UnityLayer.FontStyle = UFontStyle.Normal;
            UnityLayer.TextColor = TextColor;
            UnityLayer.Alignment = TextAnchor.UpperLeft;
            UnityLayer.HorizontalOverflow = HorizontalWrapMode.Wrap;
            UnityLayer.VerticalOverflow = VerticalWrapMode.Overflow;
            UnityLayer.AutoSize = true;                                  // 自适应高度，完整显示 50 行
            UnityLayer.DisplayRect = new Rect(10, 52, Mathf.Min(Screen.width - 20, 760), 1);
        }
    }
}
