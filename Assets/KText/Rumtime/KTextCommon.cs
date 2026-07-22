// Texture2D Color32数组索引图
//[18, 19, 20, 21, 22, 23]
//[12, 13, 14, 15, 16, 17]
//[6, 7, 8, 9, 10, 11]
//[0, 1, 2, 3, 4, 5]

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;
using UFont = UnityEngine.Font;
using UFontStyle = UnityEngine.FontStyle;

namespace KText
{
    public static class KTextCommon
    {
        // ---- 字体加载 ----
        private static readonly Dictionary<string, UFont> _fontCache = new Dictionary<string, UFont>();
        private static UFont _defaultFont;

        /// <summary>加载默认内置字体 (LegacyRuntime.ttf)。</summary>
        public static UFont LoadDefault() => Load();
        /// <summary>
        /// 按名称加载字体。优先从 Resources/Fonts/ 加载，找不到则返回默认字体。
        /// 结果会缓存到 _fontCache。
        /// </summary>
        public static UFont Load(string name = null, int FontSize = 30)
        {
            UFont f = null;
            if (!string.IsNullOrEmpty(name))
            {
                if (!_fontCache.TryGetValue(name, out f))
                {
                    f = InnerLoadFont(name, FontSize);
                    if (f != null)
                    {
                        Debug.Log("Current Load Font: " + f.name);
                        _fontCache[name] = f;
                    }
                }
                _fontCache.TryGetValue(name, out f);
            }
            return f ?? DefaultFont;
        }

        private static UFont InnerLoadFont(string name = null, int FontSize = 0)
        {
            UFont f = null;
            string[] fontNames = Font.GetOSInstalledFontNames();
            if (fontNames != null && fontNames.Contains(name))
            {
                f = Font.CreateDynamicFontFromOSFont(name, FontSize);
            }

            if (f == null)
            {
                f = Resources.Load<UFont>("Fonts/" + name);
            }
            return f;
        }

        private static UFont DefaultFont
        {
            get
            {
                if (_defaultFont == null)
                {
                    _defaultFont = Resources.GetBuiltinResource<UFont>("LegacyRuntime.ttf");
                    Debug.Log("Current Load Font: " + _defaultFont.name);
                }
                return _defaultFont;
            }
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

        /*private static readonly Dictionary<int, Color32[]> _atlasCache = new Dictionary<int, Color32[]>();
            int key = atlasTex.GetInstanceID();
            Color32[] cached;
                if (_atlasCache.TryGetValue(key, out cached))
                    return cached;
        */
        //每个Font 都对应了一个 Texture2D 字体图集：使用 _atlasCache 会造成 显示不同的字的时候，字显示不全的问题。
        public static Color32[] ReadAtlas(Texture2D atlasTex)
        {
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

            return result;
        }

        public static Material CreateMaterial()
        {
            return new Material(Shader.Find("GUI/Text Shader"));
        }

    }
}
