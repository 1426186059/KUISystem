// ============================================================================
// KText_Mir3.cs — Mir3 框架与 KText 的桥梁
// ============================================================================
//
// 【职责】
//   作为 Mir3 传奇框架与 KText 文本渲染模块之间的唯一桥梁。
//   Mir3 框架只需调用此类，无需关心内部版本实现。
//   通过 Version 枚举切换底层渲染版本。
//
// ============================================================================
using UnityEngine;
using UFont = UnityEngine.Font;
using UFontStyle = UnityEngine.FontStyle;

namespace KUISystem
{
    /// <summary>
    /// Mir3 框架与 KText 的桥梁。
    /// 通过 <see cref="ActiveVersion"/> 切换底层实现，Mir3 框架侧只依赖此类。
    /// </summary>
    public static class KText_WinFormLayer
    {
        /// <summary>
        /// 将文本渲染到 BGRA buffer（GUIText 版本忽略 buf，直接绘制）。
        /// </summary>
        public static void DrawText(
            byte[] buf, int bufW, int bufH, int stride,
            string text, UFont font, int fontSize, UFontStyle style,
            int x, int y, int clipW, int clipH,
            Color32 color,
            TextAnchor anchor = TextAnchor.UpperLeft,
            HorizontalWrapMode hWrap = HorizontalWrapMode.Wrap,
            VerticalWrapMode vWrap = VerticalWrapMode.Overflow,
            float lineHeightCoef = 1.0f,
            float yOffsetCoef = 0.0f)
        {
            KText.DrawText(buf, bufW, bufH, stride,
                text, font, fontSize, style,
                x, y, clipW, clipH, (Color)color, anchor, hWrap, vWrap,
                lineHeightCoef, yOffsetCoef);
        }

        /// <summary>
        /// 在 BGRA buffer 中填充一个实心矩形。
        /// 各版本实现相同，直接调用公共方法。
        /// </summary>
        public static void FillRect(
            byte[] buf, int bufW, int bufH, int stride,
            int x, int y, int w, int h, Color32 color)
        {
            KTextCommon.FillRect(buf, bufW, bufH, stride,
                x, y, w, h, (Color)color);
        }

        /// <summary>
        /// 测量文本渲染后的尺寸（宽x高）。
        /// </summary>
        public static Vector2 MeasureText(
            string text, UFont font, int fontSize, UFontStyle style,
            int maxWidth,
            TextAnchor anchor = TextAnchor.UpperLeft,
            HorizontalWrapMode hWrap = HorizontalWrapMode.Wrap,
            VerticalWrapMode vWrap = VerticalWrapMode.Overflow,
            float lineHeightCoef = 1.0f,
            float yOffsetCoef = 0.0f)
        {
            return KText.MeasureTextSize(text, font, fontSize, style,
                maxWidth, anchor, hWrap, vWrap, lineHeightCoef, yOffsetCoef);
        }
    }
}
