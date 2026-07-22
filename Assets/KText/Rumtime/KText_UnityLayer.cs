// ============================================================================
// KText_UnityLayer.cs — Unity 显示适配层（标准 MonoBehaviour 组件形式）
// ============================================================================
//
// 【职责】
//   作为 Unity 与 KText 核心渲染之间的桥梁组件。
//   底层使用 KText（纯 CPU 软件光栅化）把文字渲染到 BGRA buffer，
//   再上传为 Texture2D，并通过 IMGUI(GUI.DrawTexture) 直接绘制到屏幕。
//
// 【与 KText_WinFormLayer 的关系】
//   KText_WinFormLayer 是 static，负责把 buffer 画到 WinForm(GDI)；
//   KText_UnityLayer   是 MonoBehaviour，负责把 buffer 画到 Unity 屏幕。
//   两者底层都依赖 KText 核心，无需关心光栅化细节。
//
// 【标准用法（组件形式）】
//   1) 把本组件挂到场景中的任意 GameObject 上（与业务脚本同一物体即可）。
//   2) 在业务脚本里拿到本组件引用（GetComponent / 序列化字段）：
//        private KText_UnityLayer _layer;
//        _layer = GetComponent<KText_UnityLayer>();
//   3) 在 OnGUI 开头调用 _layer.BeginFrame();
//      之后多次调用 _layer.Draw(...) 即可（每行/每段一次）。
//
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using UFont = UnityEngine.Font;
using UFontStyle = UnityEngine.FontStyle;

namespace KText
{
    /// <summary>
    /// Unity 显示适配层：用 KText 核心渲染并直接上屏（标准 MonoBehaviour 组件）。
    /// </summary>
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    public class KText_UnityLayer : MonoBehaviour
    {
        [Header("默认字体/样式（调用 Draw 时未显式指定则使用这些兜底值）")]
        public UFont FontAsset;
        public string FontName = "msyh";
        public int FontSize = 26;
        public Color TextColor = Color.white;

        private class Slot
        {
            public Texture2D tex;
            public string lastText;
            public UFont lastFont;
            public int lastFontSize;
            public int lastW, lastH;
            public Color lastColor;
            public UFontStyle lastStyle;
            public TextAnchor lastAnchor;
            public HorizontalWrapMode lastHWrap;
            public VerticalWrapMode lastVWrap;
        }

        private readonly List<Slot> _pool = new List<Slot>();
        private int _callIndex;

        /// <summary>每帧开始绘制前调用，重置绘制槽位索引。</summary>
        public void BeginFrame()
        {
            _callIndex = 0;
        }

        /// <summary>
        /// 用 KText 渲染文本，并直接绘制到屏幕（IMGUI 坐标，左上角为原点，Y 向下）。
        /// 同一帧内第 N 次调用会复用第 N 个纹理槽位，内容不变时不会重复光栅化。
        /// </summary>
        public void Draw(
            string text, UFont font, int fontSize, UFontStyle style,
            int x, int y, int clipW, int clipH,
            Color color,
            TextAnchor anchor = TextAnchor.UpperLeft,
            HorizontalWrapMode hWrap = HorizontalWrapMode.Wrap,
            VerticalWrapMode vWrap = VerticalWrapMode.Overflow)
        {
            if (string.IsNullOrEmpty(text)) return;

            // 字体兜底：调用方未传则使用组件上配置的默认字体
            if (font == null)
                font = FontAsset != null ? FontAsset : KTextCommon.Load(FontName, fontSize);
            if (font == null) return;

            if (fontSize <= 0) fontSize = FontSize > 0 ? FontSize : 16;
            if (clipW <= 0) clipW = 1;
            if (clipH <= 0) clipH = 1;

            int slotIndex = _callIndex++;
            Slot slot = (slotIndex < _pool.Count) ? _pool[slotIndex] : null;
            if (slot == null)
            {
                slot = new Slot();
                _pool.Add(slot);
            }

            bool needRender = slot.tex == null
                || slot.lastText != text
                || !ReferenceEquals(slot.lastFont, font)
                || slot.lastFontSize != fontSize
                || slot.lastW != clipW
                || slot.lastH != clipH
                || slot.lastColor != color
                || slot.lastStyle != style
                || slot.lastAnchor != anchor
                || slot.lastHWrap != hWrap
                || slot.lastVWrap != vWrap;

            if (needRender)
            {
                if (slot.tex == null || slot.tex.width != clipW || slot.tex.height != clipH)
                {
                    if (slot.tex != null) DestroyImmediate(slot.tex);
                    slot.tex = new Texture2D(clipW, clipH, TextureFormat.BGRA32, false);
                    slot.tex.hideFlags = HideFlags.HideAndDontSave;
                    slot.tex.filterMode = FilterMode.Point;
                }

                var buf = new byte[clipW * clipH * 4];
                for (int i = 0; i < buf.Length; i += 4)
                { buf[i] = 0; buf[i + 1] = 0; buf[i + 2] = 0; buf[i + 3] = 0; }

                // 用 KText 核心（CPU 光栅化）渲染到 BGRA buffer
                KText.DrawText(buf, clipW, clipH, clipW * 4,
                    text, font, fontSize, style,
                    0, 0, clipW, clipH, color, anchor, hWrap, vWrap);

                slot.tex.LoadRawTextureData(buf);
                slot.tex.Apply(false);

                slot.lastText = text;
                slot.lastFont = font;
                slot.lastFontSize = fontSize;
                slot.lastW = clipW;
                slot.lastH = clipH;
                slot.lastColor = color;
                slot.lastStyle = style;
                slot.lastAnchor = anchor;
                slot.lastHWrap = hWrap;
                slot.lastVWrap = vWrap;
            }

            // 直接上屏（颜色已烘焙进贴图，使用白色避免重复着色）
            var prevColor = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(x, y, clipW, clipH), slot.tex);
            GUI.color = prevColor;
        }

        /// <summary>释放内部纹理池（场景/组件退出时自动调用）。</summary>
        public void Release()
        {
            foreach (var s in _pool)
                if (s.tex != null) DestroyImmediate(s.tex);
            _pool.Clear();
            _callIndex = 0;
        }

        private void OnDisable() { Release(); }
        private void OnDestroy() { Release(); }
    }
}
