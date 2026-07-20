// Texture2D Color32数组索引图
//[18, 19, 20, 21, 22, 23]
//[12, 13, 14, 15, 16, 17]
//[6, 7, 8, 9, 10, 11]
//[0, 1, 2, 3, 4, 5]

using System.Collections.Generic;
using UnityEngine;
using UFont = UnityEngine.Font;
using UFontStyle = UnityEngine.FontStyle;

namespace KText
{
    /// <summary>
    /// 共用工具类，提供字体加载、TextGenerator 布局、Mesh 构建、文本测量。
    /// 所有 4 个渲染版本均依赖此类。
    /// </summary>
    public static class KTextCommon
    {
        // ---- 字体加载 ----

        private static readonly Dictionary<string, UFont> _fontCache = new Dictionary<string, UFont>();
        private static UFont _defaultFont;
        private static readonly System.Text.StringBuilder _sb = new System.Text.StringBuilder(256);

        /// <summary>加载默认内置字体 (LegacyRuntime.ttf)。</summary>
        public static UFont LoadDefault() => Load();

        private static TextGenerator m_TextGenerator;
        public static TextGenerator _TextGenerator
        {
            get
            {
                if (m_TextGenerator == null)
                {
                    m_TextGenerator = new TextGenerator();
                }
                return m_TextGenerator;
            }
        }

        /// <summary>
        /// 按名称加载字体。优先从 Resources/Fonts/ 加载，找不到则返回默认字体。
        /// 结果会缓存到 _fontCache。
        /// </summary>
        public static UFont Load(string name = null)
        {
            UFont f = null;
            if (!string.IsNullOrEmpty(name))
            {
                if (!_fontCache.TryGetValue(name, out f))
                {
                    f = Resources.Load<UFont>("Fonts/" + name);
                    if (f != null) _fontCache[name] = f;
                }
                _fontCache.TryGetValue(name, out f);
            }
            return f ?? DefaultFont;
        }

        private static UFont DefaultFont
        {
            get
            {
                if (_defaultFont == null)
                    _defaultFont = Resources.GetBuiltinResource<UFont>("LegacyRuntime.ttf");
                return _defaultFont;
            }
        }

        // ---- TextGenerator 布局 ----

        /// <summary>
        /// 构建 TextGenerationSettings。
        /// pivot=(0,1) 即左上角，Y-up 坐标系，文字向 Y 负方向延伸。
        /// verticalOverflow=Overflow 允许文字超出裁剪区域。
        /// </summary>
        public static TextGenerationSettings MakeSettings(
            UFont font, int fontSize, UFontStyle style,
            int clipW, int clipH, Color color,
            bool wordBreak, TextAnchor anchor)
        {
            return new TextGenerationSettings
            {
                font = font, color = color, fontSize = fontSize, fontStyle = style,
                verticalOverflow = VerticalWrapMode.Overflow,
                horizontalOverflow = wordBreak ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow,
                generationExtents = new Vector2(clipW, clipH),
                pivot = new Vector2(0, 1),
                alignByGeometry = false, scaleFactor = 1f,
                resizeTextForBestFit = false, richText = false,
                lineSpacing = 1f,
                textAnchor = anchor
            };
        }

        // ---- TextAnchor 对齐辅助 ----

        /// <summary>水平居中对齐。</summary>
        public static bool IsCenterHorizontal(TextAnchor a) =>
            a == TextAnchor.UpperCenter || a == TextAnchor.MiddleCenter || a == TextAnchor.LowerCenter;

        /// <summary>水平右对齐。</summary>
        public static bool IsRightAlign(TextAnchor a) =>
            a == TextAnchor.UpperRight || a == TextAnchor.MiddleRight || a == TextAnchor.LowerRight;

        /// <summary>垂直居中对齐。</summary>
        public static bool IsCenterVertical(TextAnchor a) =>
            a == TextAnchor.MiddleLeft || a == TextAnchor.MiddleCenter || a == TextAnchor.MiddleRight;

        /// <summary>垂直底部对齐。</summary>
        public static bool IsBottom(TextAnchor a) =>
            a == TextAnchor.LowerLeft || a == TextAnchor.LowerCenter || a == TextAnchor.LowerRight;

        // ---- Mesh 构建 ----

        /// <summary>
        /// 核心方法：用 TextGenerator 布局文本，生成 Mesh（顶点 + UV + 三角索引）。
        ///
        /// 参数:
        ///   text     — 要渲染的文本
        ///   font     — Unity 字体对象
        ///   fontSize — 字号（像素）
        ///   style    — 字体样式（Normal/Bold/Italic）
        ///   x, y     — 文本左上角在目标空间中的位置
        ///   clipW/H  — 裁剪区域宽高
        ///   color    — 文本颜色
        ///   anchor   — 对齐方式（UGUI TextAnchor）
        ///   hWrap    — 水平折叠模式（Wrap=自动换行, Overflow=不换行）
        ///   vWrap    — 垂直折叠模式（保留语义）
        ///   flipY    — true: 翻转 Y 轴（用于 IMGUI 屏幕坐标，Y-down）
        ///              false: 不翻转（用于 Camera.Render 到 RT，或 MeshRenderer 场景坐标）
        ///
        /// 返回: 生成的 Mesh 对象，调用方负责销毁。
        /// </summary>
        public static Mesh BuildMesh(
            string text, UFont font, int fontSize, UFontStyle style,
            int x, int y, int clipW, int clipH,
            Color color, TextAnchor anchor,
            HorizontalWrapMode hWrap = HorizontalWrapMode.Wrap,
            VerticalWrapMode vWrap = VerticalWrapMode.Overflow,
            bool flipY = false)
        {
            // 1. 请求字体将所需字符烘焙到图集
            font.RequestCharactersInTexture(text, fontSize, style);

            // 2. 预处理换行符：\r\n → \n, \r → \n（\n 是硬换行，singleLine 也换行）
            bool singleLine = hWrap != HorizontalWrapMode.Wrap;
            bool wordBreak = !singleLine;
            _sb.Length = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\r')
                {
                    _sb.Append('\n');
                    if (i + 1 < text.Length && text[i + 1] == '\n') i++;
                }
                else _sb.Append(c);
            }
            string processText = _sb.ToString();

            // 3. TextGenerator 布局
            var settings = MakeSettings(font, fontSize, style, clipW, clipH, color,
                wordBreak, anchor);
            _TextGenerator.Populate(processText, settings);
            int vertCount = _TextGenerator.vertexCount;
            if (vertCount == 0) return null;

            // 4. 提取顶点数据，每个字符 = 4 个顶点 = 1 个 quad
            var verts = _TextGenerator.verts;
            int quadCount = vertCount / 4;
            var positions = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];
            var colors = new Color[vertCount];

            for (int i = 0; i < vertCount; i++)
            {
                var v = verts[i];
                float px = x + v.position.x;
                // flipY: TextGenerator Y-up (v.position.y 向下为负) → 目标 Y-down
                // 不翻转: 直接用 Y-up 坐标
                float py = flipY ? (y - v.position.y) : (y + v.position.y);
                positions[i] = new Vector3(px, py, 0);
                uvs[i] = v.uv0;
                colors[i] = color;
            }

            // 5. 构建三角索引 (每个 quad = 2 个三角形 = 6 个索引)
            var tris = new int[quadCount * 6];
            for (int i = 0; i < quadCount; i++)
            {
                int b = i * 4, t = i * 6;
                tris[t] = b; tris[t+1] = b+1; tris[t+2] = b+2;
                tris[t+3] = b; tris[t+4] = b+2; tris[t+5] = b+3;
            }

            return new Mesh
            {
                vertices = positions,
                uv = uvs,
                colors = colors,
                triangles = tris
            };
        }

        // ---- 文本测量 ----

        /// <summary>
        /// 测量文本渲染后的宽高。
        /// 返回 Vector2(width, height)，单位像素。
        /// </summary>
        public static Vector2 MeasureText(
            string text, UFont font, int fontSize, UFontStyle style,
            int maxWidth,
            TextAnchor anchor = TextAnchor.UpperLeft,
            HorizontalWrapMode hWrap = HorizontalWrapMode.Wrap,
            VerticalWrapMode vWrap = VerticalWrapMode.Overflow)
        {
            if (string.IsNullOrEmpty(text) || font == null) return Vector2.zero;
            if (fontSize <= 0) fontSize = 16;
            if (maxWidth <= 0) maxWidth = 10000;

            bool singleLine = hWrap != HorizontalWrapMode.Wrap;
            // 预处理换行符：\r\n → \n, \r → \n（\n 是硬换行，singleLine 也换行）
            _sb.Length = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\r')
                {
                    _sb.Append('\n');
                    if (i + 1 < text.Length && text[i + 1] == '\n') i++;
                }
                else _sb.Append(c);
            }
            string processText = _sb.ToString();

            font.RequestCharactersInTexture(processText, fontSize, style);
            var settings = MakeSettings(font, fontSize, style, maxWidth, 10000,
                Color.white, !singleLine && hWrap == HorizontalWrapMode.Wrap,
                anchor);
            _TextGenerator.Populate(processText, settings);
            return new Vector2(_TextGenerator.rectExtents.width, _TextGenerator.rectExtents.height);
        }

        // ---- BGRA Buffer 填充 ----

        /// <summary>
        /// 在 BGRA byte[] buffer 中填充一个实心矩形。
        /// 各渲染版本共用，无需各自实现。
        /// </summary>
        public static void FillRect(
            byte[] buf, int bufW, int bufH, int stride,
            int x, int y, int w, int h, Color color)
        {
            if (buf == null || w <= 0 || h <= 0) return;
            Color32 c = color;
            int x1 = x + w, y1 = y + h;
            for (int py = y; py < y1; py++)
            {
                if ((uint)py >= (uint)bufH) continue;
                for (int px = x; px < x1; px++)
                {
                    if ((uint)px >= (uint)bufW) continue;
                    int idx = py * stride + px * 4;
                    if (idx + 3 < buf.Length)
                    { buf[idx] = c.b; buf[idx+1] = c.g; buf[idx+2] = c.r; buf[idx+3] = c.a; }
                }
            }
        }

        /// <summary>
        /// 将 BGRA buffer 数据上传为 Texture2D（BGRA32 格式）。
        /// 各版本共用，无需各自实现。
        /// </summary>
        public static Texture2D CreateTexture(byte[] buf, int w, int h)
        {
            if (buf == null || w <= 0 || h <= 0) return null;
            var tex = new Texture2D(w, h, TextureFormat.BGRA32, false);
            tex.LoadRawTextureData(buf);
            tex.Apply(false);
            return tex;
        }

        private static readonly Dictionary<int, Color32[]> _atlasCache = new Dictionary<int, Color32[]>();

        public static Color32[] ReadAtlas(Texture2D atlasTex)
        {
            int key = atlasTex.GetInstanceID();
            Color32[] cached;
            if (_atlasCache.TryGetValue(key, out cached))
                return cached;

            Color32[] result;
            if (atlasTex.isReadable)
            {
                result = atlasTex.GetPixels32();
            }
            else
            {
                int w = atlasTex.width;
                int h = atlasTex.height;
                var readTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(atlasTex, rt);
                var prevRT = RenderTexture.active;
                RenderTexture.active = rt;
                readTex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                readTex.Apply();
                RenderTexture.active = prevRT;
                RenderTexture.ReleaseTemporary(rt);

                result = readTex.GetPixels32();
                UnityEngine.Object.DestroyImmediate(readTex);
            }

            _atlasCache[key] = result;
            return result;
        }

    }
}
