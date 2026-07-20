using UnityEngine;
using UnityEngine.UI;
using UFont = UnityEngine.Font;
using UFontStyle = UnityEngine.FontStyle;

namespace KText.Example
{
    /// <summary>
    /// 6 路文本渲染对比：
    ///   1. KText_Mir3_Version1 — Camera.Render → ReadPixels → BGRA buffer → DrawTexture
    ///   2. KText_Mir3_Version2 — 纯 CPU 软件光栅化 → BGRA buffer → DrawTexture
    ///   3. KText_Mir3_Version3 — 纯 CPU 软件光栅化（修复版）→ BGRA buffer → DrawTexture
    ///   4. KText_Graphics      — OnGUI 中 DrawMeshNow 直接绘制
    ///   5. KText_GUI_Text      — GUI.Label API 直接渲染
    ///   6. KText_UGUI          — UGUI Text 组件 + Camera.Render → BGRA buffer → DrawTexture
    /// </summary>
    public class KTextComparison : MonoBehaviour
    {
        public static string SampleText = "正在加载客户端信息...\n请稍候...";
        //public static string SampleText = "Hello KText! 你好世界 12345 This is a long line to test word wrap auto folding behavior.";
        public int FontSize = 25;
        public Color TextColor = Color.white;

        // UGUI 枚举配置（Inspector 中更直观）
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

        // 1. KText_Mir3_Version1
        private byte[] _mir3V1Buffer;
        private Texture2D _mir3V1Texture;

        // 2. KText_Mir3_Version2
        private byte[] _mir3V2Buffer;
        private Texture2D _mir3V2Texture;

        // 3. KText_Mir3_Version3
        private byte[] _mir3V3Buffer;
        private Texture2D _mir3V3Texture;

        // 6. KText_UGUI
        private byte[] _uguiBuffer;
        private Texture2D _uguiTexture;

        private void Start()
        {
            var font = FontAsset ?? KTextCommon.LoadDefault();

            _whiteTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _whiteTex.SetPixel(0, 0, Color.white);
            _whiteTex.Apply(false);

            RenderMir3V1(font);
            RenderMir3V2(font);
            RenderMir3V3(font);
            RenderUGUI(font);
        }

        private void OnDestroy()
        {
            if (_whiteTex != null) Object.DestroyImmediate(_whiteTex);
            if (_mir3V1Texture != null) Object.DestroyImmediate(_mir3V1Texture);
            if (_mir3V2Texture != null) Object.DestroyImmediate(_mir3V2Texture);
            if (_mir3V3Texture != null) Object.DestroyImmediate(_mir3V3Texture);
            if (_uguiTexture != null) Object.DestroyImmediate(_uguiTexture);
        }

        // ---- 1. KText_Mir3_Version1: Camera.Render → ReadPixels → BGRA buffer ----

        private void RenderMir3V1(UFont font)
        {
            int w = PanelW, h = PanelH;
            if (_mir3V1Buffer == null || _mir3V1Buffer.Length < w * h * 4)
                _mir3V1Buffer = new byte[w * h * 4];

            for (int i = 0; i < _mir3V1Buffer.Length; i += 4)
            { _mir3V1Buffer[i] = 0; _mir3V1Buffer[i+1] = 0; _mir3V1Buffer[i+2] = 0; _mir3V1Buffer[i+3] = 0; }

            // 通过接口调用
            KText_Mir3_Version1.DrawText(_mir3V1Buffer, w, h, w * 4,
                SampleText, font, FontSize, UFontStyle.Normal,
                6, 0, w, h, TextColor,
                Anchor, HorizontalWrap, VerticalWrap);

            if (_mir3V1Texture != null) Object.DestroyImmediate(_mir3V1Texture);
            _mir3V1Texture = KTextCommon.CreateTexture(_mir3V1Buffer, w, h);
        }

        // ---- 2. KText_Mir3_Version2: 纯 CPU 软件光栅化 → BGRA buffer ----

        private void RenderMir3V2(UFont font)
        {
            int w = PanelW, h = PanelH;
            if (_mir3V2Buffer == null || _mir3V2Buffer.Length < w * h * 4)
                _mir3V2Buffer = new byte[w * h * 4];

            for (int i = 0; i < _mir3V2Buffer.Length; i += 4)
            { _mir3V2Buffer[i] = 0; _mir3V2Buffer[i+1] = 0; _mir3V2Buffer[i+2] = 0; _mir3V2Buffer[i+3] = 0; }

            // 通过接口调用
            KText_Mir3_Version2.DrawText(_mir3V2Buffer, w, h, w * 4,
                SampleText, font, FontSize, UFontStyle.Normal,
                6, 0, w, h, TextColor,
                Anchor, HorizontalWrap, VerticalWrap);

            if (_mir3V2Texture != null) Object.DestroyImmediate(_mir3V2Texture);
            _mir3V2Texture = KTextCommon.CreateTexture(_mir3V2Buffer, w, h);
        }

        private void RenderMir3V3(UFont font)
        {
            int w = PanelW, h = PanelH;
            if (_mir3V3Buffer == null || _mir3V3Buffer.Length < w * h * 4)
                _mir3V3Buffer = new byte[w * h * 4];

            for (int i = 0; i < _mir3V3Buffer.Length; i += 4)
            { _mir3V3Buffer[i] = 0; _mir3V3Buffer[i + 1] = 0; _mir3V3Buffer[i + 2] = 0; _mir3V3Buffer[i + 3] = 0; }

            // 通过接口调用
            KText_Mir3_Version3.DrawText(_mir3V3Buffer, w, h, w * 4,
                SampleText, font, FontSize, UFontStyle.Normal,
                6, 0, w, h, TextColor,
                Anchor, HorizontalWrap, VerticalWrap);

            if (_mir3V3Texture != null) Object.DestroyImmediate(_mir3V3Texture);
            _mir3V3Texture = KTextCommon.CreateTexture(_mir3V3Buffer, w, h);
        }

        // ---- 6. KText_UGUI: UGUI Text + Camera.Render → BGRA buffer ----

        private void RenderUGUI(UFont font)
        {
            int w = PanelW, h = PanelH;
            if (_uguiBuffer == null || _uguiBuffer.Length < w * h * 4)
                _uguiBuffer = new byte[w * h * 4];

            for (int i = 0; i < _uguiBuffer.Length; i += 4)
            { _uguiBuffer[i] = 0; _uguiBuffer[i+1] = 0; _uguiBuffer[i+2] = 0; _uguiBuffer[i+3] = 0; }

            // 通过接口调用
            KText_UGUI.Instance.DrawText(_uguiBuffer, w, h, w * 4,
                SampleText, font, FontSize, UFontStyle.Normal,
                6, 0, w, h, TextColor,
                Anchor, HorizontalWrap, VerticalWrap);

            if (_uguiTexture != null) Object.DestroyImmediate(_uguiTexture);
            _uguiTexture = KTextCommon.CreateTexture(_uguiBuffer, w, h);
        }

        // ---- OnGUI: draw all 6 sections ----

        private void OnGUI()
        {
            int y = StartY;
            var font = FontAsset ?? KTextCommon.LoadDefault();

            // 标题
            DrawTitle("KText \u6587\u672c\u6e32\u67d3\u5bf9\u6bd4", ref y);
            y += 6;

            // 1. KText_Mir3_Version1
            DrawMethodLabel("KText_Mir3_Version1: Camera.Render \u2192 ReadPixels", y);
            y += LabelH;
            DrawSemiTransparentBox(y);
            if (_mir3V1Texture != null)
                Graphics.DrawTexture(new Rect(10, y, PanelW, PanelH), _mir3V1Texture);
            y += PanelH + Spacing;

            // 2. KText_Mir3_Version2
            DrawMethodLabel("KText_Mir3_Version2: CPU Software Raster", y);
            y += LabelH;
            DrawSemiTransparentBox(y);
            if (_mir3V2Texture != null)
                Graphics.DrawTexture(new Rect(10, y, PanelW, PanelH), _mir3V2Texture);
            y += PanelH + Spacing;

            // 3. KText_Mir3_Version3
            DrawMethodLabel("KText_Mir3_Version3: CPU Software Raster", y);
            y += LabelH;
            DrawSemiTransparentBox(y);
            if (_mir3V3Texture != null)
                Graphics.DrawTexture(new Rect(10, y, PanelW, PanelH), _mir3V3Texture);
            y += PanelH + Spacing;

            // 4. KText_Graphics
            DrawMethodLabel("KText_Graphics: DrawMesh Now in OnGUI", y);
            y += LabelH;
            DrawSemiTransparentBox(y);
            KText_Graphics.Draw(SampleText, font, FontSize, UFontStyle.Normal,
                16, y, PanelW, PanelH, TextColor,
                Anchor, HorizontalWrap, VerticalWrap);
            y += PanelH + Spacing;

            // 5. KText_GUI_Text
            DrawMethodLabel("KText_GUI_Text: GUI.Label API", y);
            y += LabelH;
            DrawSemiTransparentBox(y);
            KText_GUI.DrawText(SampleText, font, FontSize, UFontStyle.Normal,
                16, y, PanelW - 6, PanelH, TextColor,
                Anchor, HorizontalWrap, VerticalWrap);
            y += PanelH + Spacing;

            // 6. KText_UGUI
            DrawMethodLabel("KText_UGUI: UGUI Text + Camera.Render", y);
            y += LabelH;
            DrawSemiTransparentBox(y);
            if (_uguiTexture != null)
                Graphics.DrawTexture(new Rect(10, y, PanelW, PanelH), _uguiTexture);
        }

        // ---- helpers ----

        /// <summary>
        /// 绘制半透明黑色面板背景
        /// </summary>
        private void DrawSemiTransparentBox(int y)
        {
            var orig = GUI.color;
            GUI.color = new Color(0, 0, 0, 0.7f);
            GUI.DrawTexture(new Rect(10, y, PanelW, PanelH), _whiteTex);
            GUI.color = orig;
        }

        /// <summary>
        /// 绘制页面大标题
        /// </summary>
        private void DrawTitle(string title, ref int y)
        {
            // 标题背景
            var orig = GUI.color;
            GUI.color = new Color(0.05f, 0.05f, 0.15f, 0.9f);
            GUI.DrawTexture(new Rect(10, y, PanelW, TitleH), _whiteTex);
            GUI.color = orig;

            GUI.Label(new Rect(10, y, PanelW, TitleH), title,
                MakeLabelStyle(22, FontStyle.Bold, Color.white));
            y += TitleH + Spacing;
        }

        /// <summary>
        /// 在面板顶部叠加渲染方式标签条（最后绘制，覆盖在内容上方）
        /// </summary>
        private void DrawMethodLabel(string methodLabel, int y)
        {
            var orig = GUI.color;
            // 标签背景：深色半透明 + 左侧彩色竖条
            GUI.color = new Color(0.02f, 0.02f, 0.08f, 0.85f);
            GUI.DrawTexture(new Rect(10, y, PanelW, LabelH), _whiteTex);
            // 左侧彩色竖条
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
