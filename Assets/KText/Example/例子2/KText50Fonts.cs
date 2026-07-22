using System.Collections.Generic;
using UnityEngine;
using UFont = UnityEngine.Font;
using UFontStyle = UnityEngine.FontStyle;

namespace KText.Example
{
    /// <summary>
    /// 例子 2：利用 KText_Mir3_Version3（纯 CPU 软件光栅化）批量渲染 50 种字体。
    /// 每个字体显示不同的中英文混合文字，并可通过滚动视图浏览 / 随机切换文字。
    /// </summary>
    public class KText50Fonts : MonoBehaviour
    {
        private const int MaxFonts = 50;

        public int FontSize = 26;
        public int CellHeight = 46;
        public int PanelWidth = 760;
        public float LeftColumn = 220f;
        public float RowSpacing = 8f;
        public Color TextColor = Color.white;
        public TextAnchor Anchor = TextAnchor.MiddleLeft;

        // 中英文混合文字池，用于"任意显示不同的文字"
        private static readonly string[] SampleTexts =
        {
            "中文 English 测试 KText 渲染",
            "Hello World 你好世界 123",
            "传奇 Mir3 字体展示 Font",
            "Unity 字体渲染 中英文混排",
            "The quick brown fox 快速的狐狸",
            "KText 性能测试 Performance 50",
            "风云变幻 龙争虎斗 Dragon",
            "日落西山 英雄迟暮 Sunset",
            "剑气纵横 三万里 Sword",
            "明月几时有 把酒问青天 Moon",
            "ABCDEFG 一二三四五 67890",
            "代码 Code 与诗 Poetry 之间",
        };

        private class Item
        {
            public string FontName;
            public UFont Font;
            public Texture2D Texture;
            public string Text;
        }

        private readonly List<Item> _items = new List<Item>();
        private Texture2D _bgTex;
        private GUIStyle _labelStyle;
        private Vector2 _scroll;
        private System.Random _rnd = new System.Random();
        private int _lastFontSize;

        private void Start()
        {
            _bgTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _bgTex.SetPixel(0, 0, Color.white);
            _bgTex.Apply(false);

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
            };
            _labelStyle.normal.textColor = new Color(0.6f, 0.85f, 1f);

            _lastFontSize = FontSize;
            BuildFonts();
        }

        private void OnDestroy()
        {
            if (_bgTex != null) Object.DestroyImmediate(_bgTex);
            foreach (var it in _items)
                if (it.Texture != null) Object.DestroyImmediate(it.Texture);
        }

        private void BuildFonts()
        {
            foreach (var it in _items)
                if (it.Texture != null) Object.DestroyImmediate(it.Texture);
            _items.Clear();

            var fonts = Resources.LoadAll<UFont>("Fonts");
            var list = new List<UFont>(fonts);
            list.Sort((a, b) => a.name.CompareTo(b.name));

            int count = Mathf.Min(MaxFonts, list.Count);
            for (int i = 0; i < count; i++)
            {
                var font = list[i];
                if (font == null) continue;

                var item = new Item
                {
                    FontName = font.name,
                    Font = font,
                    Text = SampleTexts[i % SampleTexts.Length],
                };
                RenderItem(item);
                if (item.Texture != null)
                    _items.Add(item);
            }
        }

        private void RenderItem(Item item)
        {
            int w = PanelWidth;
            int h = CellHeight;

            var buf = new byte[w * h * 4];
            for (int i = 0; i < buf.Length; i += 4)
            { buf[i] = 0; buf[i + 1] = 0; buf[i + 2] = 0; buf[i + 3] = 0; }

            try
            {
                KText_Mir3_Version3.DrawText(buf, w, h, w * 4,
                    item.Text, item.Font, FontSize, UFontStyle.Normal,
                    6, 0, w, h, TextColor,
                    Anchor, HorizontalWrapMode.Overflow, VerticalWrapMode.Overflow);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[KText50Fonts] 字体 '{item.FontName}' 渲染失败: {e.Message}");
                return;
            }

            if (item.Texture != null) Object.DestroyImmediate(item.Texture);
            item.Texture = KTextCommon.CreateTexture(buf, w, h);
        }

        private void OnGUI()
        {
            // 顶部工具栏
            GUILayout.BeginArea(new Rect(0, 0, Screen.width, 44));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("随机文字", GUILayout.Width(100), GUILayout.Height(32)))
                RandomizeTexts();
            if (GUILayout.Button("重新加载字体", GUILayout.Width(120), GUILayout.Height(32)))
                BuildFonts();
            GUILayout.Label($"字体数量: {_items.Count}    字号: {FontSize}", GUILayout.Height(32));
            FontSize = Mathf.RoundToInt(GUILayout.HorizontalSlider(FontSize, 12, 48, GUILayout.Width(160), GUILayout.Height(32)));
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            if (FontSize != _lastFontSize)
            {
                _lastFontSize = FontSize;
                foreach (var it in _items)
                    RenderItem(it);
            }

            float top = 48f;
            float rowH = CellHeight + RowSpacing;
            float totalH = _items.Count * rowH;
            float contentW = LeftColumn + PanelWidth + 20f;

            _scroll = GUI.BeginScrollView(new Rect(0, top, Screen.width, Screen.height - top), _scroll,
                new Rect(0, 0, Mathf.Max(contentW, Screen.width), totalH + 20));

            for (int i = 0; i < _items.Count; i++)
            {
                var it = _items[i];
                float y = 10 + i * rowH;

                var orig = GUI.color;
                GUI.color = (i % 2 == 0)
                    ? new Color(0.10f, 0.10f, 0.15f, 0.85f)
                    : new Color(0.05f, 0.05f, 0.08f, 0.85f);
                GUI.DrawTexture(new Rect(10, y, contentW - 20, CellHeight), _bgTex);
                GUI.color = orig;

                GUI.Label(new Rect(18, y + (CellHeight - 22) / 2f, LeftColumn - 24, 22), it.FontName, _labelStyle);

                if (it.Texture != null)
                    Graphics.DrawTexture(new Rect(LeftColumn, y, PanelWidth, CellHeight), it.Texture);
            }

            GUI.EndScrollView();
        }

        private void RandomizeTexts()
        {
            foreach (var it in _items)
            {
                it.Text = SampleTexts[_rnd.Next(SampleTexts.Length)];
                RenderItem(it);
            }
        }
    }
}
