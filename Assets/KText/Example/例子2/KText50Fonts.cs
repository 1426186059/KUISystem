using System.Collections.Generic;
using UnityEngine;
using UFont = UnityEngine.Font;
using UFontStyle = UnityEngine.FontStyle;

namespace KText.Example
{
    /// <summary>
    /// 例子 2：使用 KText_Mir3_Version3（纯 CPU 光栅化）以【同一个字体】渲染 50 行文字，
    /// 每行显示不同的中英文混合内容。字体在 Inspector 中暴露，可随时更换（含编辑模式预览）。
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

        private class Line
        {
            public string Text;
            public Texture2D Texture;
        }

        private readonly List<Line> _lines = new List<Line>();
        private Texture2D _bgTex;
        private GUIStyle _labelStyle;
        private Vector2 _scroll;
        private System.Random _rnd = new System.Random();
        private UFont _lastFont;
        private int _lastFontSize;
        private int _lastLineCount;

        private void OnEnable() { Init(); }

        private void Init()
        {
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

        private string GenerateText(int i)
        {
            int a = i % Cn.Length;
            int b = (i * 7 + 3) % Cn.Length;
            int c = (i * 3 + 1) % En.Length;
            int d = (i * 5 + 2) % En.Length;
            return $"{i + 1,2}. {Cn[a]}{Cn[b]} {En[c]} · {En[d]} 中文English混排 {i + 1}";
        }

        private void RebuildIfNeeded(bool force = false)
        {
            var font = GetFont();
            if (!force && font == _lastFont && FontSize == _lastFontSize &&
                LineCount == _lastLineCount && _lines.Count > 0)
                return;

            _lastFont = font;
            _lastFontSize = FontSize;
            _lastLineCount = LineCount;
            BuildLines(font);
        }

        private void BuildLines(UFont font)
        {
            foreach (var l in _lines)
                if (l.Texture != null) Object.DestroyImmediate(l.Texture);
            _lines.Clear();

            int n = Mathf.Clamp(LineCount, 1, 500);
            for (int i = 0; i < n; i++)
            {
                string text = (Texts != null && i < Texts.Length && !string.IsNullOrEmpty(Texts[i]))
                    ? Texts[i]
                    : GenerateText(i);
                var line = new Line { Text = text };
                RenderLine(line, font);
                _lines.Add(line);
            }
        }

        private void RenderLine(Line line, UFont font)
        {
            if (font == null) return;

            int w = PanelWidth, h = CellHeight;
            var buf = new byte[w * h * 4];
            for (int i = 0; i < buf.Length; i += 4)
            { buf[i] = 0; buf[i + 1] = 0; buf[i + 2] = 0; buf[i + 3] = 0; }

            try
            {
                KText_Mir3_Version3.DrawText(buf, w, h, w * 4,
                    line.Text, font, FontSize, UFontStyle.Normal,
                    6, 0, w, h, TextColor,
                    Anchor, HorizontalWrapMode.Overflow, VerticalWrapMode.Overflow);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[KText50Fonts] 渲染失败: {e.Message}");
                return;
            }

            if (line.Texture != null) Object.DestroyImmediate(line.Texture);
            line.Texture = KTextCommon.CreateTexture(buf, w, h);
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

            RebuildIfNeeded();

            // 顶部工具栏
            GUILayout.BeginArea(new Rect(0, 0, Screen.width, 44));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("随机文字", GUILayout.Width(100), GUILayout.Height(32)))
                RandomizeTexts();
            if (GUILayout.Button("重新渲染", GUILayout.Width(100), GUILayout.Height(32)))
                RebuildIfNeeded(true);
            GUILayout.Label($"行数: {_lines.Count}    字号: {FontSize}", GUILayout.Height(32));
            FontSize = Mathf.RoundToInt(GUILayout.HorizontalSlider(FontSize, 12, 48, GUILayout.Width(160), GUILayout.Height(32)));
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            float top = 48f;
            float rowH = CellHeight + 8f;
            float totalH = _lines.Count * rowH;
            float leftW = 60f;
            float contentW = leftW + PanelWidth + 20f;

            _scroll = GUI.BeginScrollView(new Rect(0, top, Screen.width, Screen.height - top), _scroll,
                new Rect(0, 0, Mathf.Max(contentW, Screen.width), totalH + 20));

            for (int i = 0; i < _lines.Count; i++)
            {
                var ln = _lines[i];
                float y = 10 + i * rowH;

                var orig = GUI.color;
                GUI.color = (i % 2 == 0)
                    ? new Color(0.10f, 0.10f, 0.15f, 0.85f)
                    : new Color(0.05f, 0.05f, 0.08f, 0.85f);
                GUI.DrawTexture(new Rect(10, y, contentW - 20, CellHeight), _bgTex);
                GUI.color = orig;

                if (ln.Texture != null)
                    Graphics.DrawTexture(new Rect(leftW, y, PanelWidth, CellHeight), ln.Texture);
            }

            GUI.EndScrollView();
        }

        private void RandomizeTexts()
        {
            var font = GetFont();
            for (int i = 0; i < _lines.Count; i++)
            {
                if (Texts != null && i < Texts.Length && !string.IsNullOrEmpty(Texts[i]))
                    continue; // 保留用户在 Inspector 中自定义的该行文字
                _lines[i].Text = GenerateText(_rnd.Next(100000));
                RenderLine(_lines[i], font);
            }
        }

        private void OnDisable()
        {
            foreach (var l in _lines)
                if (l.Texture != null) Object.DestroyImmediate(l.Texture);
            _lines.Clear();
        }

        private void OnDestroy()
        {
            if (_bgTex != null) Object.DestroyImmediate(_bgTex);
        }
    }
}
