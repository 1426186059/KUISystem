// ============================================================================
// 禁止 任何 AI模型 修改
// ============================================================================

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UCharacterInfo = UnityEngine.CharacterInfo;
using UFont = UnityEngine.Font;
using UFontStyle = UnityEngine.FontStyle;

namespace KUISystem
{
    public static class KText
    {
        static readonly List<float> mList_LineMaxWidth = new List<float>();
        static readonly List<UCharacterInfo> _charInfoCache = new List<UCharacterInfo>();

        //矩形坐标映射
        private static Vector2Int Quad_TO_UV_XYFunc(UCharacterInfo info, int nAtlasWidth, int nAtlasHeight,
            int nQuadWidth, int nQuadHeight, Vector2Int QuadPos)
        {
            float fXPercent = QuadPos.x / (float)nQuadWidth;
            float fYPercent = QuadPos.y / (float)nQuadHeight;

            // 沿底边插值
            Vector2 uvBottom = Vector2.Lerp(info.uvBottomLeft, info.uvBottomRight, fXPercent);
            // 沿顶边插值
            Vector2 uvTop = Vector2.Lerp(info.uvTopLeft, info.uvTopRight, fXPercent);
            // 沿Y方向插值，得到最终UV
            Vector2 uv = Vector2.Lerp(uvBottom, uvTop, fYPercent);
            // UV 是 [0,1] 归一化坐标，需乘以图集尺寸得到像素索引
            return new Vector2Int(Mathf.RoundToInt(uv.x * nAtlasWidth), Mathf.RoundToInt(uv.y * nAtlasHeight));
        }

        public static void FillQuadTexture(UCharacterInfo info, Color32 color, TextAnchor anchor, int nFontSize,
            int nLineIndex, int nLineCount, float nRowMaxWidth, float nRowNowLength,
            ReadOnlySpan<Color32> atlasPixels, int nAtlasWidth, int nAtlasHeight,
            Span<Color32> targetPixels_BGRA, int targetPixels_Width, int targetPixels_Height,
            int x, int y, int clipW, int clipH,
            int nAscent, int nLineHeight,
            ref float bMinX, ref float bMinY, ref float bMaxX, ref float bMaxY)
        {
            int nQuadWidth = info.maxX - info.minX;
            int nQuadHeight = info.maxY - info.minY;
            if (nQuadWidth <= 0 || nQuadHeight <= 0)
            {
                return;
            }

            // nAscent / nLineHeight 由调用方传入（含 internal leading / 行间距，与 GDI 一致）
            int nContentHeight = (nLineCount + 1) * nLineHeight;
            int nBaseLineOffset = -nLineIndex * nLineHeight;

            int nBeginLinePosX = GetLineBeginPosX(clipW, anchor, nFontSize, Mathf.RoundToInt(nRowMaxWidth));
            int targetBaseLineY = GetContentBeginBaseLineY(clipH, anchor, nAscent, nLineHeight, nContentHeight, nBaseLineOffset);
            int targetBottomLeftX = nBeginLinePosX + Mathf.RoundToInt(nRowNowLength) + info.minX + x;
            int targetBottomLeftY = targetBaseLineY + info.minY + y;

            // 累计文字真实渲染包围盒（buffer 本地坐标，row 0 在底）
            int gRight = targetBottomLeftX + (info.maxX - info.minX);
            int gTop = targetBottomLeftY + (info.maxY - info.minY);
            if (targetBottomLeftX < bMinX) bMinX = targetBottomLeftX;
            if (gRight > bMaxX) bMaxX = gRight;
            if (targetBottomLeftY < bMinY) bMinY = targetBottomLeftY;
            if (gTop > bMaxY) bMaxY = gTop;

            for (int i = 0; i < nQuadHeight; i++)
            {
                int nTargetY = targetBottomLeftY + i;
                if ((uint)nTargetY >= (uint)targetPixels_Height) continue;
                int rowBase = nTargetY * targetPixels_Width;

                for (int j = 0; j < nQuadWidth; j++)
                {
                    int nTaregtX = targetBottomLeftX + j;
                    if ((uint)nTaregtX >= (uint)targetPixels_Width) continue;

                    Vector2Int uv = Quad_TO_UV_XYFunc(info, nAtlasWidth, nAtlasHeight, nQuadWidth, nQuadHeight, new Vector2Int(j, i));
                    if ((uint)uv.x >= (uint)nAtlasWidth || (uint)uv.y >= (uint)nAtlasHeight) continue;

                    // 图集像素索引：row-major，y * width + x
                    Color32 ori = atlasPixels[uv.y * nAtlasWidth + uv.x]; //获取Quad像素
                    if (ori.a == 0) continue; // 跳过完全透明的像素，保留抗锯齿边缘

                    int nTargetIndex = rowBase + nTaregtX;
                    targetPixels_BGRA[nTargetIndex] = ori;
                    targetPixels_BGRA[nTargetIndex].r = color.b;
                    targetPixels_BGRA[nTargetIndex].g = color.g;
                    targetPixels_BGRA[nTargetIndex].b = color.r;
                    targetPixels_BGRA[nTargetIndex].a = (byte)Mathf.RoundToInt(color.a * ori.a / 255.0f);
                }
            }

        }

        private static int GetLineBeginPosX(int clipWidth, TextAnchor anchor, int nFontSize, int nLineWidth)
        {
            if (KTextCommon.IsCenterHorizontal(anchor))
            {
                return (clipWidth - nLineWidth) / 2;
            }
            else if (KTextCommon.IsRightAlign(anchor))
            {
                return clipWidth - nLineWidth;
            }
            return 0;
        }

        // 基于字形墨迹复现 WinForm/GDI 的垂直度量（DrawText 与 MeasureTextSize 共用，保证渲染与测量一致）。
        // 严禁依赖 font.ascent/lineHeight/fontSize：KTextCommon.Load 用固定 30pt 创建动态字体，这三个属性基准是 30pt，
        // 与绘制尺寸（如 8pt）混用会让 internalLeading 算成几十像素，字被推到 buffer 外、严重下沉（"太靠下"）。
        // GDI DrawText(DT_TOP)：基线 = bounds.top + tmAscent，tmAscent = tmInternalLeading + 字形墨迹顶，
        // 即字形墨迹顶到行盒顶留 internal leading 空白（Arial 等字体很小，约 10% em）、底部留 descent 空白。
        // 这里全部基于绘制尺寸的墨迹度量：emTop=max(maxY)，emBottom=max(-minY)，emHeight=emTop+emBottom；
        //      internalLeading 取 emHeight 的小比例（上限 1px，贴近 Arial 这类几乎无 internal leading 的字体）；
        //      tmHeight = emHeight + internalLeading；ascent = emTop + internalLeading（≈ tmAscent）。
        private static void ComputeFontMetrics(Font font, int fontSize, int emTop, int emBottom,
            out int outAscent, out int outLineHeight)
        {
            int emHeight = emTop + emBottom;
            int internalLeading = Mathf.Max(1, Mathf.RoundToInt(emHeight * 0.1f)); // 仅绘制尺寸的小留白，避免太靠下
            int tmHeight = emHeight + internalLeading;
            outAscent = emTop + internalLeading;
            outLineHeight = tmHeight;
        }

        //这里得到的是 基线 Y 位置
        // nAscent：含 internal leading 的基线偏移（= font.ascent，详见 WinForm/GDI 的 tmAscent = tmInternalLeading + 字形墨迹高度）。
        //          令 baseline = T0 - nAscent，则 字形墨迹顶留白 = nAscent - maxY = internal leading，
        //          与 WinForm/GDI（DrawText DT_TOP）一致：字整体下沉、顶部留白，而非贴顶。
        // nLineHeight：真实行高（= font.lineHeight，含行间距），多行间距按此累加。
        private static int GetContentBeginBaseLineY(int nDrawZonePixelHeight, TextAnchor anchor,
            int nAscent, int nLineHeight, int nContentHeight, int nBaseLineOffset)
        {
            // T0：第 0 行行盒顶（buffer 坐标，向上）
            int T0;
            if (KTextCommon.IsBottom(anchor))
                T0 = nContentHeight;                                                  // 整体贴底
            else if (KTextCommon.IsCenterVertical(anchor))
                T0 = nDrawZonePixelHeight - (nDrawZonePixelHeight - nContentHeight) / 2; // 整体垂直居中
            else
                T0 = nDrawZonePixelHeight;                                            // 整体贴顶

            // 字形顶部留白 = nAscent - maxY（internal leading），字视觉偏下，与 WinForm/GDI 一致
            return T0 - nAscent + nBaseLineOffset;
        }

        // ================================================================
        //  DrawText
        // ================================================================

        //读取像素到目标 纹理 buf，此buf是 BGRA 纹理buf
        // 兼容旧调用：不输出包围盒
        public static void DrawText(
            byte[] buf, int bufW, int bufH, int stride,
            string text, UFont font, int fontSize, UFontStyle style,
            int x, int y, int clipW, int clipH,
            Color color,
            TextAnchor anchor = TextAnchor.UpperLeft,
            HorizontalWrapMode hWrap = HorizontalWrapMode.Wrap,
            VerticalWrapMode vWrap = VerticalWrapMode.Overflow)
        {
            Rect ignore;
            DrawText(buf, bufW, bufH, stride, text, font, fontSize, style,
                x, y, clipW, clipH, color, anchor, hWrap, vWrap, out ignore);
        }

        // 实现版：额外输出文字真实渲染包围盒（buffer 本地坐标，row 0 在底）
        public static void DrawText(
            byte[] buf, int bufW, int bufH, int stride,
            string text, UFont font, int fontSize, UFontStyle style,
            int x, int y, int clipW, int clipH,
            Color color,
            TextAnchor anchor,
            HorizontalWrapMode hWrap,
            VerticalWrapMode vWrap,
            out Rect bounds)
        {
            bounds = Rect.zero;
            float bMinX = float.MaxValue, bMinY = float.MaxValue, bMaxX = float.MinValue, bMaxY = float.MinValue;
            if(font == null)
            {
                UnityEngine.Debug.LogError("font == null");
            }

            if (string.IsNullOrWhiteSpace(text) || font == null || buf == null) return;

            Span<Color32> targetBuf = MemoryMarshal.Cast<byte, Color32>(buf);

            if (fontSize <= 0) fontSize = 16;
            if (clipW <= 0) clipW = bufW;
            if (clipH <= 0) clipH = bufH;

            font.RequestCharactersInTexture(text, fontSize, style);
            var atlasTex = font.material.mainTexture as Texture2D;
            if (atlasTex == null)
            {
                UnityEngine.Debug.LogError("font.material.mainTexture == null");
                return;
            }

            int atlasW = atlasTex.width;
            int atlasH = atlasTex.height;
            Color32[] atlasPixels = KTextCommon.ReadAtlas(atlasTex);

            Color32 c32 = color;

            bool singleLine = hWrap != HorizontalWrapMode.Wrap;
            bool wordBreak = !singleLine;

            int len = text.Length;
            mList_LineMaxWidth.Clear();

            _charInfoCache.Clear();
            _charInfoCache.Capacity = Mathf.Max(_charInfoCache.Capacity, len);
            for (int i = 0; i < len; i++)
            {
                char ch = text[i];
                if (ch == '\n' || ch == '\r')
                {
                    _charInfoCache.Add(default);
                    continue;
                }
                UCharacterInfo info;
                if (font.GetCharacterInfo(ch, out info, fontSize, style))
                    _charInfoCache.Add(info);
                else
                    _charInfoCache.Add(default);
            }

            // 基于字形墨迹复现 WinForm/GDI 的垂直定位（不依赖 font.ascent/lineHeight/fontSize，
            // 这三个属性对动态字体在运行时不可靠，会返回 0 或基准尺寸错误，导致回退回 fontSize）。
            // GDI DrawText(DT_TOP)：基线 = bounds.top + tmAscent，tmAscent = tmInternalLeading + 字形墨迹顶。
            // 即字形墨迹顶到行盒顶留 internal leading 空白、底部留 descent 空白 —— 字整体偏下（偏低）。
            int emTop = 0, emBottom = 0;
            for (int ci = 0; ci < _charInfoCache.Count; ci++)
            {
                var cc = _charInfoCache[ci];
                if (cc.maxY > emTop) emTop = cc.maxY;
                if (-cc.minY > emBottom) emBottom = -cc.minY;
            }
            int realAscent, realLineHeight;
            ComputeFontMetrics(font, fontSize, emTop, emBottom, out realAscent, out realLineHeight);

            int totalLines = 0;
            {
                int scanPos = 0;
                while (scanPos < len)
                {
                    float scanX = 0;
                    int scanEnd = len;
                    for (int ci = scanPos; ci < len; ci++)
                    {
                        char ch = text[ci];
                        if (ch == '\n' || ch == '\r')
                        {
                            scanEnd = ci;
                            if (ch == '\r' && ci + 1 < len && text[ci + 1] == '\n') scanEnd = ci + 1;
                            break;
                        }
                        var wInfo = _charInfoCache[ci];
                        if (wInfo.advance == 0 && ch != ' ') continue;
                        if (wordBreak && scanX + wInfo.advance > clipW && ci > scanPos)
                        {
                            int breakAt = -1;
                            for (int j = ci - 1; j >= scanPos; j--)
                                if (text[j] == ' ') { breakAt = j; break; }
                            scanEnd = (breakAt >= scanPos) ? breakAt : ci;
                            break;
                        }
                        scanX += wInfo.advance;
                    }
                    mList_LineMaxWidth.Add(scanX);
                    totalLines++;
                    if (scanEnd < len && (text[scanEnd] == '\n' || text[scanEnd] == '\r'))
                    {
                        scanPos = scanEnd + 1;
                        if (text[scanEnd] == '\r' && scanPos < len && text[scanPos] == '\n') scanPos++;
                    }
                    else
                    {
                        if (scanEnd < len && text[scanEnd] == ' ')
                            scanPos = scanEnd + 1;
                        else
                            scanPos = scanEnd;
                    }
                }
            }

            int nLineCount = totalLines - 1;
            float nRowNowLength = 0;
            int nLineIndex = 0;
            int lineStart = 0;

            while (lineStart < len)
            {
                float scanX = 0;
                int lineEnd = len;

                for (int ci = lineStart; ci < len; ci++)
                {
                    char ch = text[ci];
                    if (ch == '\n' || ch == '\r')
                    {
                        lineEnd = ci;
                        if (ch == '\r' && ci + 1 < len && text[ci + 1] == '\n') lineEnd = ci + 1;
                        break;
                    }

                    var info = _charInfoCache[ci];
                    if (info.advance == 0 && ch != ' ') continue;

                    if (wordBreak && scanX + info.advance > clipW && ci > lineStart)
                    {
                        int breakAt = -1;
                        for (int j = ci - 1; j >= lineStart; j--)
                            if (text[j] == ' ') { breakAt = j; break; }
                        lineEnd = (breakAt >= lineStart) ? breakAt : ci;
                        break;
                    }

                    scanX += info.advance;
                }

                for (int ci = lineStart; ci < lineEnd; ci++)
                {
                    char ch = text[ci];
                    var info = _charInfoCache[ci];
                    if (info.advance == 0 && ch != ' ') continue;

                    FillQuadTexture(info, c32, anchor, fontSize,
                        nLineIndex, nLineCount, mList_LineMaxWidth[nLineIndex], nRowNowLength,
                        atlasPixels, atlasW, atlasH,
                        targetBuf, clipW, clipH, x, y, clipW, clipH,
                        realAscent, realLineHeight,
                        ref bMinX, ref bMinY, ref bMaxX, ref bMaxY);

                    nRowNowLength += info.advance;
                }

                nLineIndex++;
                nRowNowLength = 0;

                if (lineEnd < len && (text[lineEnd] == '\n' || text[lineEnd] == '\r'))
                {
                    lineStart = lineEnd + 1;
                    if (text[lineEnd] == '\r' && lineStart < len && text[lineStart] == '\n')
                        lineStart++;
                }
                else
                {
                    if (lineEnd < len && text[lineEnd] == ' ')
                        lineStart = lineEnd + 1;
                    else
                        lineStart = lineEnd;
                }
            }

            if (bMaxX >= bMinX && bMaxY >= bMinY)
                bounds = new Rect(bMinX, bMinY, bMaxX - bMinX, bMaxY - bMinY);

            // 把渲染包围盒写入缓存（与 MeasureTextSize 共用 _mtCacheRect），
            // 这样相同输入下后续 MeasureTextSize / DrawText 可直接复用，避免重复计算。
            _mtCacheRect = bounds;
            _mtCacheText = text;
            _mtCacheFont = font;
            _mtCacheFontSize = fontSize;
            _mtCacheStyle = style;
            _mtCacheMaxWidth = clipW;
            _mtCacheAnchor = anchor;
            _mtCacheHWrap = hWrap;
            _mtCacheVWrap = vWrap;
            _mtCacheValid = true;
        }

        // ================================================================
        //  MeasureTextSize — 文本尺寸测量，带单条缓存（_mtCacheRect）。
        //  输入未变时直接复用缓存，不再重新计算；DrawText 也会把渲染包围盒写入同一缓存。
        // ================================================================
        private static string            _mtCacheText;
        private static UFont             _mtCacheFont;
        private static int               _mtCacheFontSize;
        private static UFontStyle        _mtCacheStyle;
        private static int               _mtCacheMaxWidth;
        private static TextAnchor        _mtCacheAnchor;
        private static HorizontalWrapMode _mtCacheHWrap;
        private static VerticalWrapMode  _mtCacheVWrap;
        private static bool              _mtCacheValid;
        private static Rect              _mtCacheRect;

        public static Vector2 MeasureTextSize(
            string text, UFont font, int fontSize, UFontStyle style,
            int maxWidth,
            TextAnchor anchor = TextAnchor.UpperLeft,
            HorizontalWrapMode hWrap = HorizontalWrapMode.Wrap,
            VerticalWrapMode vWrap = VerticalWrapMode.Overflow)
        {
            if (_mtCacheValid
                && _mtCacheText == text
                && ReferenceEquals(_mtCacheFont, font)
                && _mtCacheFontSize == fontSize
                && _mtCacheStyle == style
                && _mtCacheMaxWidth == maxWidth
                && _mtCacheAnchor == anchor
                && _mtCacheHWrap == hWrap
                && _mtCacheVWrap == vWrap)
            {
                return new Vector2(_mtCacheRect.width, _mtCacheRect.height);
            }

            if (string.IsNullOrEmpty(text) || font == null) return Vector2.zero;
            if (fontSize <= 0) fontSize = 16;
            if (maxWidth <= 0) maxWidth = 10000;

            font.RequestCharactersInTexture(text, fontSize, style);

            bool singleLine = hWrap != HorizontalWrapMode.Wrap;
            bool wordBreak = !singleLine;

            int len = text.Length;
            _charInfoCache.Clear();
            _charInfoCache.Capacity = Mathf.Max(_charInfoCache.Capacity, len);
            for (int i = 0; i < len; i++)
            {
                char ch = text[i];
                if (ch == '\n' || ch == '\r')
                {
                    _charInfoCache.Add(default);
                    continue;
                }
                UCharacterInfo info;
                if (font.GetCharacterInfo(ch, out info, fontSize, style))
                    _charInfoCache.Add(info);
                else
                    _charInfoCache.Add(default);
            }

            // 与 DrawText 共用同一套垂直度量（ComputeFontMetrics），确保测量尺寸与渲染位置一致
            int emTop = 0, emBottom = 0;
            for (int ci = 0; ci < _charInfoCache.Count; ci++)
            {
                var cc = _charInfoCache[ci];
                if (cc.maxY > emTop) emTop = cc.maxY;
                if (-cc.minY > emBottom) emBottom = -cc.minY;
            }
            int _ascentUnused, realLineHeight;
            ComputeFontMetrics(font, fontSize, emTop, emBottom, out _ascentUnused, out realLineHeight);

            float curX = 0, curY = 0, maxW = 0;
            int lineStart = 0;

            for (int i = 0; i < len; i++)
            {
                char ch = text[i];
                if (ch == '\n' || ch == '\r')
                {
                    if (curX > maxW) maxW = curX;
                    curX = 0; curY += realLineHeight; lineStart = i + 1;
                    if (ch == '\r' && i + 1 < len && text[i + 1] == '\n') i++;
                    continue;
                }

                var ci = _charInfoCache[i];
                if (ci.advance == 0 && ch != ' ') continue;

                if (wordBreak && curX + ci.advance > maxWidth && i > lineStart)
                {
                    int breakAt = -1;
                    for (int j = i - 1; j >= lineStart; j--)
                        if (text[j] == ' ') { breakAt = j; break; }
                    if (breakAt >= lineStart)
                    {
                        float rewind = 0;
                        for (int j = breakAt + 1; j < i; j++)
                            rewind += _charInfoCache[j].advance;
                        if (curX > maxW) maxW = curX;
                        curX = rewind; curY += realLineHeight; lineStart = breakAt + 1;
                    }
                    else
                    {
                        if (curX > maxW) maxW = curX;
                        curX = 0; curY += realLineHeight; lineStart = i;
                    }
                }
                curX += ci.advance;
            }
            if (curX > maxW) maxW = curX;

            Vector2 size = new Vector2(maxW, curY + realLineHeight);

            _mtCacheRect = new Rect(0, 0, size.x, size.y);
            _mtCacheText = text;
            _mtCacheFont = font;
            _mtCacheFontSize = fontSize;
            _mtCacheStyle = style;
            _mtCacheMaxWidth = maxWidth;
            _mtCacheAnchor = anchor;
            _mtCacheHWrap = hWrap;
            _mtCacheVWrap = vWrap;
            _mtCacheValid = true;

            return size;
        }

    }
}
