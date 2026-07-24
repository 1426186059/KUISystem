// ============================================================================
// KText_UnityLayer.cs — 仿 UGUI Text 的 KText 文字显示组件（标准 MonoBehaviour）
// ============================================================================
//
// 【用法】
//   把本组件拖到场景里任意 GameObject 上，在 Inspector 设置 Text / 字体 / 颜色 /
//   对齐 / 溢出 等属性，运行（或编辑模式，因 [ExecuteInEditMode]）即可直接显示。
//
// 【实现】
//   底层用 KText 核心（CPU 软件光栅化）把文字渲染到 BGRA buffer，再上传为
//   Texture2D，通过 GUI.DrawTexture 直接上屏。每个组件只持有一张贴图，
//   仅在文本/字体/字号/样式/颜色/对齐/尺寸等发生变化时才重新光栅化（无 Slot 池）。
//   与 KText_WinFormLayer 对称：WinForm 版负责 GDI 上屏，本组件负责 Unity 上屏。
//
// ============================================================================

using UnityEngine;
using UFont = UnityEngine.Font;
using UFontStyle = UnityEngine.FontStyle;

namespace KUISystem
{
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    public class KText_UnityLayer : MonoBehaviour
    {
        [Header("文本")]
        [TextArea(3, 10)]
        public string Text = "KText";

        [Header("字体")]
        public UFont FontAsset;
        public int FontSize = 30;
        public UFontStyle FontStyle = UFontStyle.Normal;

        [Header("颜色")]
        public Color TextColor = Color.white;

        [Header("对齐 / 溢出")]
        public TextAnchor Alignment = TextAnchor.UpperLeft;
        public HorizontalWrapMode HorizontalOverflow = HorizontalWrapMode.Wrap;
        public VerticalWrapMode VerticalOverflow = VerticalWrapMode.Overflow;

        [Header("显示区域（屏幕坐标，IMGUI）")]
        public Rect DisplayRect = new Rect(10, 10, 400, 80);
        public bool AutoSize = true; // 为 true 时按内容自适应尺寸（宽度受 DisplayRect 限制）

        private Texture2D _tex;

#if UNITY_EDITOR
        [Header("调试")]
        [Tooltip("实际尺寸（只读：位置+尺寸，来自 MeasureText 测量，与 AutoSize 开关无关）")]
        public Rect ActualSize = new Rect(0, 0, 0, 0);
        [Tooltip("青色框：标出设定的显示区域 DisplayRect")]
        public bool ShowDisplayRect = false;
        [Tooltip("黄色框：实际像素尺寸（扫描像素 buffer，非测量）")]
        public bool ShowTextRect = false;
        [Tooltip("绿色框：实际绘制区域（来自 MeasureText 测量，仅 Editor 下生效）")]
        public bool ShowAutoSizeRect = false;

        private Rect _textBoundsLocal;   // 文字实际像素包围盒（buf 本地坐标，row 0 在底）
        private Texture2D _whiteTex;     // 1x1 白色像素，用于绘制矩形框
#endif

        // 缓存上次渲染参数，用于判断是否需要重绘
        private string _cText;
        private UFont _cFont;
        private int _cFontSize;
        private UFontStyle _cStyle;
        private Color _cColor;
        private TextAnchor _cAnchor;
        private HorizontalWrapMode _cHWrap;
        private VerticalWrapMode _cVWrap;
        private int _cW, _cH;

        private void OnGUI() { Render(); }

        private void Render()
        {
            if (!enabled) return;
            if (string.IsNullOrEmpty(Text)) return;

            // 字体兜底：未拖入 FontAsset 时按 FontName 从 Resources 加载
            if(FontAsset == null)
            {
                FontAsset = KTextCommon.LoadDefault();
            }

            UFont font = FontAsset;
            if (font == null) return;
            int fontSize = FontSize > 0 ? FontSize : 16;

            int clipW, clipH;
            Rect drawRect;

            // 文本实际尺寸（始终测量，与 AutoSize 无关）。Wrap 时按 DisplayRect.width 约束测量，
            // Overflow 时按超长测量，得到真实文字包围盒 (measured.x = 最宽行宽, measured.y = 总行高)。
            int measureMaxW = (HorizontalOverflow == HorizontalWrapMode.Wrap)
                ? Mathf.Max(1, Mathf.RoundToInt(DisplayRect.width))
                : 100000;
            Vector2 measured = KText.MeasureText(Text, font, fontSize, FontStyle,
                measureMaxW, Alignment, HorizontalOverflow, VerticalOverflow);

            if (!AutoSize)
            {
                clipW = Mathf.Max(1, Mathf.RoundToInt(DisplayRect.width));
                clipH = Mathf.Max(1, Mathf.RoundToInt(DisplayRect.height));
                drawRect = DisplayRect;
            }
            else
            {
                // 自动尺寸：宽度受 DisplayRect 限制（Wrap 时），高度按内容增长
                clipW = (HorizontalOverflow == HorizontalWrapMode.Wrap)
                    ? Mathf.Max(1, Mathf.RoundToInt(DisplayRect.width))
                    : Mathf.Max(1, Mathf.CeilToInt(measured.x));
                clipH = Mathf.Max(1, Mathf.CeilToInt(measured.y));
                drawRect = new Rect(DisplayRect.x, DisplayRect.y, clipW, clipH);
            }

#if UNITY_EDITOR
            // 实际尺寸（与 AutoSize 无关）：位置取 DisplayRect 左上角，尺寸取文字真实测量值
            ActualSize = new Rect(DisplayRect.x, DisplayRect.y, measured.x, measured.y);
#endif

            bool dirty = _tex == null
                || _cText != Text
                || !ReferenceEquals(_cFont, font)
                || _cFontSize != fontSize
                || _cStyle != FontStyle
                || _cColor != TextColor
                || _cAnchor != Alignment
                || _cHWrap != HorizontalOverflow
                || _cVWrap != VerticalOverflow
                || _cW != clipW
                || _cH != clipH;

            if (dirty)
            {
                if (_tex == null || _tex.width != clipW || _tex.height != clipH)
                {
                    if (_tex != null) DestroyImmediate(_tex);
                    _tex = new Texture2D(clipW, clipH, TextureFormat.BGRA32, false);
                    _tex.hideFlags = HideFlags.HideAndDontSave;
                    _tex.filterMode = FilterMode.Point;
                }

                var buf = new byte[clipW * clipH * 4];
                for (int i = 0; i < buf.Length; i += 4)
                { buf[i] = 0; buf[i + 1] = 0; buf[i + 2] = 0; buf[i + 3] = 0; }

                // 用 KText 核心（CPU 光栅化）渲染到 BGRA buffer
                KText.DrawText(buf, clipW, clipH, clipW * 4,
                    Text, font, fontSize, FontStyle,
                    0, 0, clipW, clipH, TextColor, Alignment, HorizontalOverflow, VerticalOverflow);

                _tex.LoadRawTextureData(buf);
                _tex.Apply(false);

#if UNITY_EDITOR
                // 扫描渲染结果，得到文字实际像素包围盒（本地坐标，row 0 在底部）—— 仅 Editor 下 ShowTextRect 用
                int minX = clipW, maxX = -1, minY = clipH, maxY = -1;
                for (int row = 0; row < clipH; row++)
                {
                    int rowBase = row * clipW * 4;
                    for (int col = 0; col < clipW; col++)
                    {
                        if (buf[rowBase + col * 4 + 3] > 0)
                        {
                            if (col < minX) minX = col;
                            if (col > maxX) maxX = col;
                            if (row < minY) minY = row;
                            if (row > maxY) maxY = row;
                        }
                    }
                }
                _textBoundsLocal = (maxX >= 0)
                    ? new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1)
                    : new Rect(0, 0, 0, 0);
#endif

                _cText = Text; _cFont = font; _cFontSize = fontSize; _cStyle = FontStyle;
                _cColor = TextColor; _cAnchor = Alignment; _cHWrap = HorizontalOverflow;
                _cVWrap = VerticalOverflow; _cW = clipW; _cH = clipH;
            }

            // 直接上屏（颜色已烘焙进贴图，用白色避免重复着色）
            var prev = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(drawRect, _tex);
            GUI.color = prev;

#if UNITY_EDITOR
            // 调试：用矩形框标出边界（仅 Editor）
            //   实际尺寸框 (ActualSize) —— 绿色：实际绘制区域（位置+尺寸），勾选 ShowAutoSizeRect 时显示
            //   显示区域框 (DisplayRect) —— 青色：你设定的显示区域，勾选 ShowDisplayRect 时显示
            //   实际像素尺寸框 (_textBoundsLocal) —— 黄色：文字真实像素包围盒，勾选 ShowTextRect 时显示
            if (_tex != null && (ShowDisplayRect || ShowTextRect || ShowAutoSizeRect))
            {
                if (_whiteTex == null)
                {
                    _whiteTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    _whiteTex.SetPixel(0, 0, Color.white);
                    _whiteTex.Apply();
                    _whiteTex.hideFlags = HideFlags.HideAndDontSave;
                }
                float t = 2f;
                if (ShowAutoSizeRect)
                {
                    GUI.color = Color.green;
                    DrawRectOutline(ActualSize, t);
                }
                if (ShowDisplayRect)
                {
                    GUI.color = Color.cyan;
                    DrawRectOutline(DisplayRect, t);
                }
                if (ShowTextRect)
                {
                    // 用渲染时扫描得到的真实像素包围盒（与文字实际落位完全一致，适配任意对齐/折行）
                    // 注意：GUI.DrawTexture 按纹理约定，buf row 0 对应矩形底部，故垂直需翻转；水平不变。
                    Rect textRect = new Rect(
                        drawRect.x + _textBoundsLocal.x,
                        drawRect.y + drawRect.height - (_textBoundsLocal.y + _textBoundsLocal.height),
                        _textBoundsLocal.width,
                        _textBoundsLocal.height);
                    GUI.color = Color.yellow;
                    DrawRectOutline(textRect, t);
                }
                GUI.color = Color.white;
            }
#endif
        }

#if UNITY_EDITOR
        /// <summary>用 1x1 白像素贴图绘制矩形边框（4 条边）。</summary>
        private void DrawRectOutline(Rect r, float thickness)
        {
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, thickness), _whiteTex);
            GUI.DrawTexture(new Rect(r.x, r.y + r.height - thickness, r.width, thickness), _whiteTex);
            GUI.DrawTexture(new Rect(r.x, r.y, thickness, r.height), _whiteTex);
            GUI.DrawTexture(new Rect(r.x + r.width - thickness, r.y, thickness, r.height), _whiteTex);
        }
#endif

        /// <summary>释放内部贴图（组件退出时自动调用）。</summary>
        public void Release()
        {
            if (_tex != null) { DestroyImmediate(_tex); _tex = null; }
#if UNITY_EDITOR
            if (_whiteTex != null) { DestroyImmediate(_whiteTex); _whiteTex = null; }
#endif
        }

        private void OnDisable() { Release(); }
        private void OnDestroy() { Release(); }
    }
}
