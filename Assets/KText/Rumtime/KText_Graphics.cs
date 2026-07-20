// ============================================================================
// KText_Graphics.cs — 在 OnGUI 中通过 Graphics.DrawMeshNow 直接绘制文本
// ============================================================================
//
// 【职责】
//   在 OnGUI 回调中，将文本 Mesh 直接绘制到屏幕。
//   类似 GUI.Label 的效果，但使用自定义 Mesh + Shader 渲染。
//
// 【渲染管线】
//   TextGenerator 布局 → Mesh → Material.SetPass → Graphics.DrawMeshNow
//
//   1. KTextCommon.BuildMesh() 生成 Mesh（flipY=true，Y-up → Y-down）
//   2. 设置材质的 mainTexture 为字体图集
//   3. Material.SetPass(0) 激活 shader pass
//   4. Graphics.DrawMeshNow 在当前 IMGUI 上下文中绘制 Mesh
//
// 【关键约束】
//   - 必须在 OnGUI 回调中调用（依赖 IMGUI 的渲染上下文）
//   - flipY=true: IMGUI 坐标系 Y-down，TextGenerator 坐标系 Y-up，需要翻转
//   - 使用 KText/Font shader（Blend Off，输出直线 alpha）
//   - 无需 buffer / RT / Camera，最轻量的 GPU 渲染版本
//   - 每次调用后 Mesh 会被立即销毁
//
// ============================================================================

using UnityEngine;
using UFont = UnityEngine.Font;
using UFontStyle = UnityEngine.FontStyle;

namespace KText
{
    public static class KText_Graphics
    {
        private static Material _mat; // KText/Font shader 材质（懒初始化）

        /// <summary>
        /// 在 OnGUI 中直接绘制文本到屏幕。
        ///
        /// 参数:
        ///   text     — 要渲染的文本
        ///   font     — Unity 字体对象
        ///   fontSize — 字号（像素）
        ///   style    — 字体样式
        ///   x, y     — 文本左上角在屏幕中的位置（IMGUI 坐标，Y-down）
        ///   clipW/H  — 裁剪区域宽高
        ///   color    — 文本颜色
        ///   alignment— 对齐方式
        /// </summary>
        public static void Draw(
            string text, UFont font, int fontSize, UFontStyle style,
            int x, int y, int clipW, int clipH,
            Color color,
            TextAnchor anchor = TextAnchor.UpperLeft,
            HorizontalWrapMode hWrap = HorizontalWrapMode.Wrap,
            VerticalWrapMode vWrap = VerticalWrapMode.Overflow)
        {
            if (string.IsNullOrEmpty(text) || font == null) return;
            if (fontSize <= 0) fontSize = 16;

            var atlasTex = font.material.mainTexture as Texture2D;
            if (atlasTex == null) return;

            // 构建 Mesh（flipY=true: TextGenerator Y-up → IMGUI Y-down）
            var mesh = KTextCommon.BuildMesh(text, font, fontSize, style,
                x, y, clipW, clipH, color, anchor, hWrap, vWrap, true);
            if (mesh == null) return;

            // 确保材质就绪
            if (_mat == null)
            {
                _mat = new Material(Shader.Find("KText/Font"));
                _mat.hideFlags = HideFlags.HideAndDontSave;
            }
            _mat.mainTexture = atlasTex;
            _mat.SetPass(0); // 激活 shader pass

            // 在当前 IMGUI 上下文中绘制 Mesh
            Graphics.DrawMeshNow(mesh, Matrix4x4.identity);
            Object.DestroyImmediate(mesh);
        }
    }
}
