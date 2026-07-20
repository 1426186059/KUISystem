// ============================================================================
// KText_GUI_Text.cs — 专门用来完全代替 KText 的 API
// ============================================================================
//
// 【这个类是什么】
//   作为 Mir3 框架的文本渲染后端，通过 KText_Mir3 桥梁层调用。
//
// 【核心规则 — 必须遵守】
//   1. 本类的所有方法只能使用 Unity GUI 相关 API（GUI.Label / GUIStyle 等）。
//      禁止引入 TextGenerator、Mesh、Shader、ReadPixels 等非 GUI 技术。
//      这是设计意图，不是限制——本类的存在意义就是用纯 GUI API 替代 KText。
//   2. 所有方法必须在 OnGUI 上下文中调用（Unity 要求 GUI API 只能在 OnGUI 中使用）。
//      在 Update / Start / Init 等非 OnGUI 阶段调用会抛出 ArgumentException。
//      调用方有责任确保调用时机正确。
//
// 【在 Mir3 框架中的调用链】
//   CMain.OnGUI(Repaint)
//     → CEnvir.RenderLoop()
//       → RenderGame() → DXControl.Draw()
//         → TextRaster.DrawText() → KText_Mir3.DrawText() → 本类.Draw()
//
//   注意：Mir3 框架中 MeasureText 也可能在非 OnGUI 阶段被调用（如 Init 时
//   DXLabel.GetSize），此时如果 ActiveVersion = GUIText 会报错。
//   解决方案：调用方需确保在 OnGUI 内调用，或在非 OnGUI 阶段切换到其他版本。
//
// 【与其他版本的区别】
//   Version1  — Camera.Render + ReadPixels，写入 BGRA buffer
//   Version2  — 纯 CPU 软件光栅化，写入 BGRA buffer
//   GUIText   — GUI.Label 立即绘制，不写入 buffer（本类）
//
// ============================================================================

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using UFont = UnityEngine.Font;
using UFontStyle = UnityEngine.FontStyle;

namespace KText
{
    public static class KText_GUI
    {
        private static GUIStyle _style; // 复用的 GUIStyle（懒初始化）

        /// <summary>
        /// 使用 GUI.Label 直接渲染文本。
        /// 只能在 OnGUI 上下文中调用。
        /// </summary>
        public static void DrawText(
            string text, UFont font, int fontSize, UFontStyle style,
            int x, int y, int clipW, int clipH,
            Color color,
            TextAnchor anchor = TextAnchor.UpperLeft,
            HorizontalWrapMode hWrap = HorizontalWrapMode.Wrap,
            VerticalWrapMode vWrap = VerticalWrapMode.Overflow)
        {
            if (string.IsNullOrEmpty(text) || font == null) return;
            if (fontSize <= 0) fontSize = 16;

            // 确保 GUIStyle 就绪
            if (_style == null)
                _style = new GUIStyle();

            // 配置 GUIStyle
            Debug.Assert(font != null);
            _style.font = font;
            _style.fontSize = fontSize;
            _style.fontStyle = style;
            _style.normal.textColor = color;
            _style.wordWrap = hWrap == HorizontalWrapMode.Wrap;
            _style.alignment = anchor;
            // 直接调用 GUI.Label 绘制
            GUI.Label(new Rect(x, y, clipW, clipH), text, _style);
        }

        /// <summary>
        /// 测量文本渲染后的尺寸（宽x高）。
        /// 使用 GUIStyle.CalcSize / CalcHeight，结果与 Draw 完全一致。
        /// 只能在 OnGUI 上下文中调用。
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

            if (_style == null)
                _style = new GUIStyle();

            _style.font = font;
            _style.fontSize = fontSize;
            _style.fontStyle = style;
            _style.wordWrap = hWrap == HorizontalWrapMode.Wrap;
            _style.alignment = anchor;

            var content = new GUIContent(text);

            if (_style.wordWrap && maxWidth > 0)
            {
                float h = _style.CalcHeight(content, maxWidth);
                return new Vector2(maxWidth, h);
            }

            return _style.CalcSize(content);
        }
    }
}
