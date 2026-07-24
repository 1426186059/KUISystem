// ============================================================================
// KTextComparison.cs — 例子1：KText 两种 Unity 上屏方式对比
// ============================================================================
//
// 【对比内容】（底层渲染都是 KText 核心 CPU 光栅化，仅上屏集成方式不同）
//   左列：KText 核心手动上屏
//         KText.DrawText -> KTextCommon.CreateTexture -> Graphics.DrawTexture
//   右列：KText_UnityLayer 组件上屏（标准 MonoBehaviour 形式）
//         _layer.Draw(...)
//
// 两个类并列挂在同一 GameObject 上，可直观对比同一段文字两种集成方式的画面。
// ============================================================================

using KUISystem;
using UnityEngine;
using UFont = UnityEngine.Font;
using UFontStyle = UnityEngine.FontStyle;

namespace KText.Example
{
    [ExecuteInEditMode]
    public class KTextComparison : MonoBehaviour
    {
        public UFont FontAsset;
        public string FontName = "msyh";
        public int FontSize = 30;
        public Color TextColor = Color.white;
        public int CellHeight = 50;
        public int ColumnWidth = 360;
        public int LeftX = 30;
        public int RightX = 420;

        private readonly string[] _lines =
        {
            "KText 两种上屏方式对比",
            "Hello, KText! 软件光栅化文字",
            "中文 English 混排 1234567890",
            "左侧：KText 核心手动上屏",
            "右侧：KText_UnityLayer 组件上屏",
            "底层都是 KText 核心渲染",
        };

        private KText_UnityLayer _layer;
        private Texture2D[] _manualTex;
        private string[] _manualRendered;
        private UFont _manualFont;
        private int _manualFontSize;

        private void OnEnable()
        {
            // 解析同物体上的 KText_UnityLayer 上屏组件（标准组件引用方式）
            _layer = GetComponent<KText_UnityLayer>()
                     ?? gameObject.AddComponent<KText_UnityLayer>();
        }

        private void OnGUI()
        {
            UFont font = FontAsset != null ? FontAsset : KTextCommon.Load(FontName);
            if (font == null) return;

            GUI.Label(new Rect(30, 10, 820, 30), "KText 两种 Unity 上屏方式对比（底层都是 KText 核心渲染）");
            GUI.Label(new Rect(LeftX, 40, ColumnWidth, 20), "← 手动上屏 (KText.DrawText + Graphics.DrawTexture)");
            GUI.Label(new Rect(RightX, 40, ColumnWidth, 20), "← 组件上屏 (KText_UnityLayer 仿 UGUI Text)");

            // 左列：KText 核心手动上屏
            EnsureManualCache(font);
            int y = 70;
            for (int i = 0; i < _lines.Length; i++)
            {
                if (_manualTex != null && _manualTex[i] != null)
                    Graphics.DrawTexture(new Rect(LeftX, y, ColumnWidth, CellHeight), _manualTex[i]);
                y += CellHeight;
            }

            // 右列：把整段文字交给 KText_UnityLayer 组件，由其自身 OnGUI 上屏
            if (_layer != null)
            {
                _layer.Text = string.Join("\n", _lines);
                _layer.FontAsset = font;
                _layer.FontSize = FontSize;
                _layer.FontStyle = UFontStyle.Normal;
                _layer.TextColor = TextColor;
                _layer.Alignment = TextAnchor.UpperLeft;
                _layer.HorizontalOverflow = HorizontalWrapMode.Overflow;
                _layer.VerticalOverflow = VerticalWrapMode.Overflow;
                _layer.AutoSize = true;
                _layer.DisplayRect = new Rect(RightX, 70, ColumnWidth, 1);
            }
        }

        // 仅在文字/字体/字号变化时才重新光栅化，避免每帧重复创建贴图
        private void EnsureManualCache(UFont font)
        {
            if (_manualTex == null || _manualTex.Length != _lines.Length)
            {
                _manualTex = new Texture2D[_lines.Length];
                _manualRendered = new string[_lines.Length];
            }

            bool rebuild = !ReferenceEquals(_manualFont, font) || _manualFontSize != FontSize;
            if (!rebuild)
                for (int i = 0; i < _lines.Length; i++)
                    if (_manualRendered[i] != _lines[i]) { rebuild = true; break; }

            if (rebuild)
            {
                for (int i = 0; i < _lines.Length; i++)
                {
                    if (_manualTex[i] != null) DestroyImmediate(_manualTex[i]);
                    _manualTex[i] = RenderManual(_lines[i], font);
                    _manualRendered[i] = _lines[i];
                }
                _manualFont = font;
                _manualFontSize = FontSize;
            }
        }

        private Texture2D RenderManual(string text, UFont font)
        {
            int w = ColumnWidth, h = CellHeight;
            var buf = new byte[w * h * 4];
            for (int i = 0; i < buf.Length; i += 4) { buf[i] = 0; buf[i + 1] = 0; buf[i + 2] = 0; buf[i + 3] = 0; }

            // 用 KText 核心（CPU 光栅化）渲染到 BGRA buffer
            KUISystem.KText.DrawText(buf, w, h, w * 4,
                text, font, FontSize, UFontStyle.Normal,
                0, 0, w, h, TextColor, TextAnchor.UpperLeft,
                HorizontalWrapMode.Overflow, VerticalWrapMode.Overflow);

            var tex = KTextCommon.CreateTexture(buf, w, h);
            tex.Apply(false);
            return tex;
        }

        private void OnDisable()
        {
            if (_manualTex != null)
                foreach (var t in _manualTex)
                    if (t != null) DestroyImmediate(t);
            _manualTex = null;
            _manualRendered = null;
        }
    }
}
