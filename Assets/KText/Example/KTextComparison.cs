using UnityEngine;
using UFont = UnityEngine.Font;
using UFontStyle = UnityEngine.FontStyle;

namespace KText.Example
{
    /// <summary>
    /// 文本渲染对比：
    ///   1. KText_Mir3_Version3 — 纯 CPU 软件光栅化（修复版）→ BGRA buffer → DrawTexture
    ///   2. KText_Graphics      — OnGUI 中 DrawMeshNow 直接绘制
    /// </summary>
    public class KTextComparison : MonoBehaviour
    {
        public static string SampleText = "正在加载客户端信息...\n请稍候...";
        public int FontSize = 25;
        public Color TextColor = Color.white;

        public TextAnchor Anchor = TextAnchor.MiddleLeft;
        public HorizontalWrapMode HorizontalWrap = HorizontalWrapMode.Wrap;
        public VerticalWrapMode VerticalWrap = VerticalWrapMode.Overflow;

        public UFont FontAsset;

        private const int PanelW = 420;
        private const int PanelH = 120;
        private const int Spacing = 12;
        private const int StartY = 10;
        private const int TitleH = 40;
        private const int LabelH = 28;

        private Texture2D _whiteTex;

        private byte[] _mir3V3Buffer;
        private Texture2D _mir3V3Texture;

        private void Start()
        {
            var font = FontAsset ?? KTextCommon.LoadDefault();

            _whiteTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _whiteTex.SetPixel(0, 0, Color.white);
            _whiteTex.Apply(false);

            RenderMir3V3(font);
        }

        private void OnDestroy()
        {
            if (_whiteTex != null) Object.DestroyImmediate(_whiteTex);
            if (_mir3V3Texture != null) Object.DestroyImmediate(_mir3V3Texture);
        }

        private void RenderMir3V3(UFont font)
        {
            int w = PanelW, h = PanelH;
            if (_mir3V3Buffer == null || _mir3V3Buffer.Length < w * h * 4)
                _mir3V3Buffer = new byte[w * h * 4];

            for (int i = 0; i < _mir3V3Buffer.Length; i += 4)
            { _mir3V3Buffer[i] = 0; _mir3V3Buffer[i + 1] = 0; _mir3V3Buffer[i + 2] = 0; _mir3V3Buffer[i + 3] = 0; }

            KText_Mir3_Version3.DrawText(_mir3V3Buffer, w, h, w * 4,
                SampleText, font, FontSize, UFontStyle.Normal,
                6, 0, w, h, TextColor,
                Anchor, HorizontalWrap, VerticalWrap);

            if (_mir3V3Texture != null) Object.DestroyImmediate(_mir3V3Texture);
            _mir3V3Texture = KTextCommon.CreateTexture(_mir3V3Buffer, w, h);
        }

        private void OnGUI()
        {
            int y = StartY;
            var font = FontAsset ?? KTextCommon.LoadDefault();

            DrawTitle("KText \u6587\u672c\u6e32\u67d3\u5bf9\u6bd4", ref y);
            y += 6;

            DrawMethodLabel("KText_Mir3_Version3: CPU Software Raster", y);
            y += LabelH;
            DrawSemiTransparentBox(y);
            if (_mir3V3Texture != null)
                Graphics.DrawTexture(new Rect(10, y, PanelW, PanelH), _mir3V3Texture);
            y += PanelH + Spacing;

            DrawMethodLabel("KText_Graphics: DrawMesh Now in OnGUI", y);
            y += LabelH;
            DrawSemiTransparentBox(y);
            KText_Graphics.Draw(SampleText, font, FontSize, UFontStyle.Normal,
                16, y, PanelW, PanelH, TextColor,
                Anchor, HorizontalWrap, VerticalWrap);
        }

        private void DrawSemiTransparentBox(int y)
        {
            var orig = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.7f);
            GUI.DrawTexture(new Rect(10, y, PanelW, PanelH), _whiteTex);
            GUI.color = orig;
        }

        private void DrawTitle(string title, ref int y)
        {
            var orig = GUI.color;
            GUI.color = new Color(0.05f, 0.05f, 0.15f, 0.9f);
            GUI.DrawTexture(new Rect(10, y, PanelW, TitleH), _whiteTex);
            GUI.color = orig;

            GUI.Label(new Rect(10, y, PanelW, TitleH), title,
                MakeLabelStyle(22, FontStyle.Bold, Color.white));
            y += TitleH + Spacing;
        }

        private void DrawMethodLabel(string methodLabel, int y)
        {
            var orig = GUI.color;
            GUI.color = new Color(0.02f, 0.02f, 0.08f, 0.85f);
            GUI.DrawTexture(new Rect(10, y, PanelW, LabelH), _whiteTex);
            GUI.color = new Color(0.3f, 0.7f, 1f, 0.9f);
            GUI.DrawTexture(new Rect(10, y, 4, LabelH), _whiteTex);
            GUI.color = orig;

            GUI.Label(new Rect(20, y + 4, PanelW - 16, LabelH), methodLabel,
                MakeLabelStyle(14, FontStyle.Bold, new Color(0.5f, 0.85f, 1f)));
        }

        private static GUIStyle MakeLabelStyle(int fontSize, FontStyle style, Color color)
        {
            var s = new GUIStyle(GUI.skin.label);
            s.fontSize = fontSize;
            s.fontStyle = style;
            s.normal.textColor = color;
            return s;
        }
    }
}