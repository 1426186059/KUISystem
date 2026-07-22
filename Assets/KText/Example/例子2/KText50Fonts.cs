using System.Collections.Generic;
using UnityEngine;
using UFont = UnityEngine.Font;
using UFontStyle = UnityEngine.FontStyle;

namespace KText.Example
{
    /// <summary>
    /// 例子 2：使用 KText_UnityLayer（底层 KText CPU 光栅化 + Unity 上屏）以【同一个字体】
    /// 渲染 50 行文字，每行显示不同的中英文混合内容。字体在 Inspector 中暴露，可随时更换。
    /// </summary>
    [ExecuteInEditMode]
    public class KText50Fonts : MonoBehaviour
    {
        [Header("字体（Inspector 中可随时更换，所有行共用同一字体）")]
        public UFont FontAsset;
        public string FontName = "msyh";

        [Header("渲染设置")]
        public int FontSize = 26;
        public int LineCount = 50;
        public int CellHeight = 44;
        public int PanelWidth = 760;
        public Color TextColor = Color.white;
        public TextAnchor Anchor = TextAnchor.MiddleLeft;

        [Header("自定义文字（每条一行；留空则该行自动生成混排文字）")]
        [TextArea(1, 3)]
        public string[] Texts;

        // 中英文词库，用于自动生成互不相同的混排文字
        private static readonly string[] Cn =
        {
            "传奇", "风云", "龙吟", "剑气", "明月", "江湖", "天涯", "英雄", "青山", "流水",
            "星河", "战旗", "铁血", "逍遥", "苍穹", "幻境", "王者", "荣耀", "清风", "落霞",
        };
        private static readonly string[] En =
        {
            "Mir3", "KText", "Dragon", "Hero", "Sword", "Moon", "Sky", "Wind", "Fire", "Star",
            "Legend", "Quest", "Realm", "Storm", "Shadow", "Light", "Brave", "Echo", "Nova", "Zen",
        };

        private readonly List<string> _lines = new List<string>();
        private Texture2D _bgTex;
        private GUIStyle _labelStyle;
        private Vector2 _scroll;
        private System.Random _rnd = new System.Random();
        private UFont _lastFont;
        private int _lastFontSize;
        private int _lastLineCount;
        private KText_UnityLayer _layer;

        private void OnEnable()
        {
            // 解析同物体上的 KText_UnityLayer 上屏组件（标准组件引用方式）
            _layer = GetComponent<KText_UnityLayer>()
                     ?? gameObject.AddMissComponent<KText_UnityLayer>();
            Init();
        }

        private void Init()
        {
            if (_bgTex == null)
            {
                _bgTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _bgTex.SetPixel(0, 0, Color.white);
                _bgTex.Apply(false);
            }
            EnsureLines();
        }

        private UFont GetFont()
        {
            if (FontAsset != null) return FontAsset;
            return KTextCommon.Load(FontName, FontSize);
        }

        private string GenerateText(int i)
        {
            int a = i % Cn.Length;
            int b = (i * 7 + 3) % Cn.Length;
            int c = (i * 3 + 1) % En.Length;
            int d = (i * 5 + 2) % En.Length;
            return $"{i + 1,2}. {Cn[a]}{Cn[b]} {En[c]} · {En[d]} 中文English混排 {i + 1}";
        }

        private void EnsureLines()
        {
            int n = Mathf.Clamp(LineCount, 1, 500);
            if (_lines.Count == n) return;
            _lines.Clear();
            for (int i = 0; i < n; i++)
                _lines.Add((Texts != null && i < Texts.Length && !string.IsNullOrEmpty(Texts[i]))
                    ? Texts[i]
                    : GenerateText(i));
        }

        private void OnGUI()
        {
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                };
                _labelStyle.normal.textColor = new Color(0.6f, 0.85f, 1f);
            }

            var font = GetFont();
            if (!ReferenceEquals(font, _lastFont) || FontSize != _lastFontSize || LineCount != _lastLineCount)
            {
                _lastFont = font;
                _lastFontSize = FontSize;
                _lastLineCount = LineCount;
            }

            EnsureLines();

            // 准备本帧绘制（重置 KText_UnityLayer 的槽位索引）
            _layer.BeginFrame();

            // 顶部工具栏
            GUILayout.BeginArea(new Rect(0, 0, Screen.width, 44));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("随机文字", GUILayout.Width(100), GUILayout.Height(32)))
                RandomizeTexts();
            if (GUILayout.Button("应用自定义文字", GUILayout.Width(140), GUILayout.Height(32)))
                EnsureLinesFromTexts();
            GUILayout.Label($"行数: {_lines.Count}    字号: {FontSize}    字体: {(font != null ? font.name : "null")}", GUILayout.Height(32));
            FontSize = Mathf.RoundToInt(GUILayout.HorizontalSlider(FontSize, 12, 48, GUILayout.Width(160), GUILayout.Height(32)));
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            float top = 48f;
            float rowH = CellHeight + 8f;
            float totalH = _lines.Count * rowH;
            float contentW = PanelWidth + 20f;

            _scroll = GUI.BeginScrollView(new Rect(0, top, Screen.width, Screen.height - top), _scroll,
                new Rect(0, 0, Mathf.Max(contentW, Screen.width), totalH + 20));

            for (int i = 0; i < _lines.Count; i++)
            {
                float y = 10 + i * rowH;

                var orig = GUI.color;
                GUI.color = (i % 2 == 0)
                    ? new Color(0.10f, 0.10f, 0.15f, 0.85f)
                    : new Color(0.05f, 0.05f, 0.08f, 0.85f);
                GUI.DrawTexture(new Rect(10, y, contentW - 20, CellHeight), _bgTex);
                GUI.color = orig;

                // 通过 KText_UnityLayer 渲染并直接上屏
                _layer.Draw(_lines[i], font, FontSize, UFontStyle.Normal,
                    10, (int)y, PanelWidth, CellHeight, TextColor,
                    Anchor, HorizontalWrapMode.Overflow, VerticalWrapMode.Overflow);
            }

            GUI.EndScrollView();
        }

        private void EnsureLinesFromTexts()
        {
            int n = Mathf.Clamp(LineCount, 1, 500);
            _lines.Clear();
            for (int i = 0; i < n; i++)
                _lines.Add((Texts != null && i < Texts.Length && !string.IsNullOrEmpty(Texts[i]))
                    ? Texts[i]
                    : GenerateText(i));
        }

        private void RandomizeTexts()
        {
            for (int i = 0; i < _lines.Count; i++)
            {
                if (Texts != null && i < Texts.Length && !string.IsNullOrEmpty(Texts[i]))
                    continue; // 保留用户在 Inspector 中自定义的该行文字
                _lines[i] = GenerateText(_rnd.Next(100000));
            }
        }

        private void OnDisable()
        {
            if (_bgTex != null) Object.DestroyImmediate(_bgTex);
            // KText_UnityLayer 组件自身的 OnDisable 会负责释放其纹理池
        }
    }
}
