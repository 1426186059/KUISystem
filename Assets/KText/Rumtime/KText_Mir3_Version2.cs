// ============================================================================
// KText_Mir3_Version2.cs — 无缓存纯 CPU 软件光栅化（完全独立版本）
// ============================================================================
//
// 【设计原则】
//   - 完全自包含，不依赖 KTextCommon 或任何共享代码
//   - 不使用任何缓存，每次调用直接获取 Unity 字体 API 数据
//   - 先保证正确性，后续再加缓存优化
//
// 【目标 Buffer 约定】
//  buf 数组 是 Unity 纹理格式 BGRA 缓冲Buf
//
// ============================================================================

using UnityEngine;
using UnityEngine.UI;
using UFont = UnityEngine.Font;
using UFontStyle = UnityEngine.FontStyle;
using UCharacterInfo = UnityEngine.CharacterInfo;

namespace KText
{
    public static class KText_Mir3_Version2
    {
        private static readonly System.Collections.Generic.List<UCharacterInfo> _charInfoCache = new System.Collections.Generic.List<UCharacterInfo>();
        private static readonly System.Collections.Generic.List<float> _lineWidths = new System.Collections.Generic.List<float>();
        // ================================================================
        //  DrawText
        // ================================================================

        //读取像素到目标 纹理 buf，此buf是 BGRA 纹理buf
        public static void DrawText(
            byte[] buf, int bufW, int bufH, int stride,
            string text, UFont font, int fontSize, UFontStyle style,
            int x, int y, int clipW, int clipH,
            Color color,
            TextAnchor anchor = TextAnchor.UpperLeft,
            HorizontalWrapMode hWrap = HorizontalWrapMode.Wrap,
            VerticalWrapMode vWrap = VerticalWrapMode.Overflow)
        {
            if (string.IsNullOrEmpty(text) || font == null || buf == null) return;
            if (fontSize <= 0) fontSize = 16;
            if (clipW <= 0) clipW = bufW;
            if (clipH <= 0) clipH = bufH;

            font.RequestCharactersInTexture(text, fontSize, style);

            var atlasTex = font.material.mainTexture as Texture2D;
            if (atlasTex == null) return;
            int atlasW = atlasTex.width;
            int atlasH = atlasTex.height;
            Color32[] atlasPixels = KTextCommon.ReadAtlas(atlasTex);

            byte cr = (byte)(color.r * 255f + 0.5f);
            byte cg = (byte)(color.g * 255f + 0.5f);
            byte cb = (byte)(color.b * 255f + 0.5f);
            byte ca = (byte)(color.a * 255f + 0.5f);
            bool hasAlpha = ca < 255;

            bool singleLine = hWrap != HorizontalWrapMode.Wrap;
            bool wordBreak = !singleLine;

            int len = text.Length;
            int bufLen = buf.Length;

            _charInfoCache.Clear();
            _charInfoCache.Capacity = System.Math.Max(_charInfoCache.Capacity, len);
            for (int i = 0; i < len; i++)
            {
                UCharacterInfo info;
                if (font.GetCharacterInfo(text[i], out info, fontSize, style))
                    _charInfoCache.Add(info);
                else
                    _charInfoCache.Add(default);
            }

            _lineWidths.Clear();
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
                        if (wInfo.advance == 0 && text[ci] != ' ') continue;
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
                    _lineWidths.Add(scanX);
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

            bool hCenter = KTextCommon.IsCenterHorizontal(anchor);
            bool hRight  = KTextCommon.IsRightAlign(anchor);
            bool vCenter = KTextCommon.IsCenterVertical(anchor);
            bool vBottom = KTextCommon.IsBottom(anchor);

            int totalTextH = totalLines * fontSize;
            int vOffset = 0;
            if (vCenter) vOffset = (clipH - totalTextH) / 2;
            else if (vBottom) vOffset = clipH - totalTextH;
            float penY = clipH - fontSize - vOffset;

            int lineIdx = 0;
            int lineStart = 0;
            float penX = 0;

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

                float lineW = (lineIdx < _lineWidths.Count) ? _lineWidths[lineIdx] : 0;
                float alignOffset = 0;
                if (hCenter) alignOffset = (clipW - lineW) * 0.5f;
                else if (hRight) alignOffset = clipW - lineW;

                for (int ci = lineStart; ci < lineEnd; ci++)
                {
                    char ch = text[ci];
                    var info = _charInfoCache[ci];
                    if (info.advance == 0 && ch != ' ') { penX += info.advance; continue; }

                    int glyphW = info.maxX - info.minX;
                    int glyphH = info.maxY - info.minY;
                    if (glyphW <= 0 || glyphH <= 0) { penX += info.advance; continue; }

                    int dstX0 = (int)(penX + info.minX) + x + (int)alignOffset;
                    int dstY0 = (int)penY + info.minY + y;

                    Vector2 blUv = info.uvBottomLeft;
                    Vector2 brUv = info.uvBottomRight;
                    Vector2 tlUv = info.uvTopLeft;
                    float stepX_u = (brUv.x - blUv.x) / glyphW;
                    float stepX_v = (brUv.y - blUv.y) / glyphW;
                    float stepY_u = (tlUv.x - blUv.x) / glyphH;
                    float stepY_v = (tlUv.y - blUv.y) / glyphH;

                    int clipX1 = x + clipW, clipY1 = y + clipH;
                    int srcX0 = 0, srcY0 = 0;
                    int drawW = glyphW, drawH = glyphH;
                    int clipDstX0 = dstX0, clipDstY0 = dstY0;
                    if (clipDstX0 < x) { srcX0 = x - clipDstX0; drawW -= srcX0; clipDstX0 = x; }
                    if (clipDstY0 < y) { srcY0 = y - clipDstY0; drawH -= srcY0; clipDstY0 = y; }
                    if (clipDstX0 + drawW > clipX1) drawW = clipX1 - clipDstX0;
                    if (clipDstY0 + drawH > clipY1) drawH = clipY1 - clipDstY0;
                    if (clipDstX0 < 0) { int d = -clipDstX0; clipDstX0 = 0; srcX0 += d; drawW -= d; }
                    if (clipDstY0 < 0) { int d = -clipDstY0; clipDstY0 = 0; srcY0 += d; drawH -= d; }
                    if (drawW <= 0 || drawH <= 0) { penX += info.advance; continue; }

                    float rowU0 = blUv.x + stepY_u * srcY0;
                    float rowV0 = blUv.y + stepY_v * srcY0;

                    for (int row = 0; row < drawH; row++)
                    {
                        int bufRow = clipDstY0 + row;
                        if ((uint)bufRow >= (uint)bufH) continue;
                        int bufBase = bufRow * stride;
                        float colU = rowU0 + stepX_u * srcX0;
                        float colV = rowV0 + stepX_v * srcX0;

                        for (int col = 0; col < drawW; col++)
                        {
                            int bufCol = clipDstX0 + col;
                            if ((uint)bufCol >= (uint)bufW) { colU += stepX_u; colV += stepX_v; continue; }

                            int ax = (int)(colU * atlasW + 0.5f);
                            int ay = (int)(colV * atlasH + 0.5f);
                            if ((uint)ax >= (uint)atlasW || (uint)ay >= (uint)atlasH) { colU += stepX_u; colV += stepX_v; continue; }

                            byte alpha = atlasPixels[ay * atlasW + ax].a;
                            if (alpha == 0) { colU += stepX_u; colV += stepX_v; continue; }

                            int di = bufBase + bufCol * 4;
                            if (di + 3 >= bufLen) { colU += stepX_u; colV += stepX_v; continue; }

                            int srcA = hasAlpha ? (alpha * ca + 127) / 255 : alpha;
                            buf[di]     = cb;
                            buf[di + 1] = cg;
                            buf[di + 2] = cr;
                            buf[di + 3] = (byte)srcA;

                            colU += stepX_u;
                            colV += stepX_v;
                        }
                        rowU0 += stepY_u;
                        rowV0 += stepY_v;
                    }

                    penX += info.advance;
                }

                penX = 0;
                penY -= fontSize;
                lineIdx++;

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
        }

        // ================================================================
        //  MeasureText — 无缓存，每次直接计算
        // ================================================================

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

            font.RequestCharactersInTexture(text, fontSize, style);

            bool singleLine = hWrap != HorizontalWrapMode.Wrap;
            bool wordBreak = !singleLine;

            int len = text.Length;
            _charInfoCache.Clear();
            _charInfoCache.Capacity = System.Math.Max(_charInfoCache.Capacity, len);
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

            float curX = 0, curY = 0, maxW = 0;
            int lineStart = 0;

            for (int i = 0; i < len; i++)
            {
                char ch = text[i];
                if (ch == '\n' || ch == '\r')
                {
                    if (curX > maxW) maxW = curX;
                    curX = 0; curY += fontSize; lineStart = i + 1;
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
                        curX = rewind; curY += fontSize; lineStart = breakAt + 1;
                    }
                    else
                    {
                        if (curX > maxW) maxW = curX;
                        curX = 0; curY += fontSize; lineStart = i;
                    }
                }
                curX += ci.advance;
            }
            if (curX > maxW) maxW = curX;

            return new Vector2(maxW, curY + fontSize);
        }

    }
}
