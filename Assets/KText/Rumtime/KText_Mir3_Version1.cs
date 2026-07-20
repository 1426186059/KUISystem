// ============================================================================
// KText_Mir3.cs — 写像素到 BGRA byte[] buffer，兼容 Mir3 传奇框架
// ============================================================================
//
// 【职责】
//   将文本渲染为像素，写入调用方提供的 byte[] buffer（BGRA 格式）。
//   这是最"底层"的版本，适用于需要将文本像素直接嵌入自定义缓冲区的场景。
//
// 【渲染管线】
//   TextGenerator 布局 → Mesh → Camera.Render → RenderTexture → ReadPixels → BGRA buf
//
//   1. KTextCommon.BuildMesh() 生成 Mesh（flipY=false，保持 Y-up）
//   2. 内部 Camera 正交投影覆盖整个 buffer 空间
//   3. Graphics.DrawMesh + Camera.Render 将 Mesh 渲染到 RenderTexture
//   4. ReadPixels 从 RT 读回像素到 Texture2D
//   5. 逐像素转换为 BGRA 格式写入目标 buffer
//
// 【关键约束】
//   - 目标 buffer 格式: BGRA 字节顺序（Mir3 框架要求）
//     buf[i] = B, buf[i+1] = G, buf[i+2] = R, buf[i+3] = A
//   - Camera.enabled = false，防止 Unity 自动渲染
//   - RT 重建时必须先清 _cam.targetTexture = null，否则报 "Releasing render texture
//     that is set as Camera.targetTexture!" 错误
//   - 使用 KText/Font shader（Blend Off，输出直线 alpha）
//   - flipY=false: Camera.Render 到 RT 后 ReadPixels 自动处理 Y 方向
//
// ============================================================================

using UnityEngine;
using UnityEngine.UI;
using UFont = UnityEngine.Font;
using UFontStyle = UnityEngine.FontStyle;

namespace KText
{
    public static class KText_Mir3_Version1
    {
        // ---- 静态资源（懒初始化，全局复用） ----
        private static Material _mat;         // KText/Font shader 材质
        private static Camera _cam;           // 内部渲染用相机（enabled=false）
        private static GameObject _camGO;     // 相机宿主 GameObject
        private static RenderTexture _rt;     // 渲染目标
        private static Texture2D _readTex;    // ReadPixels 回读用纹理
        private static int _rtW, _rtH;        // 当前 RT 尺寸

        /// <summary>
        /// 将文本渲染为像素，写入 BGRA byte[] buffer。
        ///
        /// 参数:
        ///   buf      — 目标缓冲区，BGRA 字节顺序，每像素 4 字节
        ///   bufW/H   — 缓冲区宽高（像素）
        ///   stride   — 缓冲区行字节数（通常 = bufW * 4）
        ///   text     — 要渲染的文本
        ///   font     — Unity 字体对象
        ///   fontSize — 字号（像素）
        ///   style    — 字体样式
        ///   x, y     — 文本左上角在 buffer 中的位置
        ///   clipW/H  — 裁剪区域宽高
        ///   color    — 文本颜色
        ///   alignment— 对齐方式
        /// </summary>
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

            var atlasTex = font.material.mainTexture as Texture2D;
            if (atlasTex == null) return;

            // 1. 构建 Mesh（flipY=false: Camera 渲染不需要翻转）
            var mesh = KTextCommon.BuildMesh(text, font, fontSize, style,
                x, y, clipW, clipH, color, anchor, hWrap, vWrap, false);
            if (mesh == null) return;

            // 2. 确保渲染资源就绪
            EnsureMaterial(atlasTex);
            EnsureCamera();
            EnsureRT(bufW, bufH);

            // 3. 配置正交相机，覆盖整个 buffer 空间
            //    相机位置 (bufW/2, -bufH/2, -10)，看向 (bufW/2, -bufH/2, 0)
            //    orthographicSize = bufH/2 使垂直范围恰好覆盖 [0, -bufH]
            _cam.orthographic = true;
            _cam.orthographicSize = bufH / 2f;
            _cam.aspect = (float)bufW / bufH;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = Color.clear;
            _cam.nearClipPlane = 0.3f;
            _cam.farClipPlane = 1000f;
            _cam.transform.position = new Vector3(bufW / 2f, -bufH / 2f, -10);
            _cam.transform.LookAt(new Vector3(bufW / 2f, -bufH / 2f, 0));

            // 4. 渲染 Mesh 到 RT
            _cam.targetTexture = _rt;
            Graphics.DrawMesh(mesh, Matrix4x4.identity, _mat, 0, _cam);
            _cam.Render();

            // 5. ReadPixels 从 RT 回读到 Texture2D
            var prevRT = RenderTexture.active;
            RenderTexture.active = _rt;
            _readTex.ReadPixels(new Rect(0, 0, bufW, bufH), 0, 0, false);
            _readTex.Apply();
            RenderTexture.active = prevRT;

            // 6. 逐像素转换为 BGRA 写入目标 buffer
            //    跳过 alpha < 1/255 的完全透明像素
            var pixels = _readTex.GetPixels();
            for (int py = 0; py < bufH; py++)
            {
                for (int px = 0; px < bufW; px++)
                {
                    int si = py * bufW + px;
                    int di = py * stride + px * 4;
                    if (di + 3 >= buf.Length) continue;
                    var p = pixels[si];
                    if (p.a < 0.004f) continue;
                    buf[di]     = (byte)(p.b * 255f + 0.5f); // B
                    buf[di + 1] = (byte)(p.g * 255f + 0.5f); // G
                    buf[di + 2] = (byte)(p.r * 255f + 0.5f); // R
                    buf[di + 3] = (byte)(p.a * 255f + 0.5f); // A
                }
            }

            Object.DestroyImmediate(mesh);
        }

        /// <summary>
        /// 测量文本渲染后的尺寸（宽x高）。
        /// </summary>
        public static Vector2 MeasureText(
            string text, UFont font, int fontSize, UFontStyle style,
            int maxWidth,
            TextAnchor anchor = TextAnchor.UpperLeft,
            HorizontalWrapMode hWrap = HorizontalWrapMode.Wrap,
            VerticalWrapMode vWrap = VerticalWrapMode.Overflow)
        {
            return KTextCommon.MeasureText(text, font, fontSize, style,
                maxWidth, anchor, hWrap, vWrap);
        }

        // ---- 内部：资源初始化 ----

        private static void EnsureMaterial(Texture2D atlasTex)
        {
            if (_mat == null)
            {
                _mat = new Material(Shader.Find("KText/Font"));
                _mat.hideFlags = HideFlags.HideAndDontSave;
            }
            _mat.mainTexture = atlasTex;
        }

        private static void EnsureCamera()
        {
            if (_camGO == null)
            {
                _camGO = new GameObject("_KTextMir3Cam");
                _camGO.hideFlags = HideFlags.HideAndDontSave;
                _cam = _camGO.AddComponent<Camera>();
                _cam.enabled = false; // 禁止自动渲染，只在 DrawText 中手动调用 Render
            }
        }

        /// <summary>
        /// 确保 RT 和回读纹理尺寸匹配。
        /// 重建 RT 前必须先清 Camera.targetTexture = null，否则会报错。
        /// </summary>
        private static void EnsureRT(int w, int h)
        {
            if (_rt == null || _rtW != w || _rtH != h)
            {
                if (_rt != null)
                {
                    _cam.targetTexture = null;     // 先解除引用
                    RenderTexture.active = null;
                    _rt.Release();
                    Object.DestroyImmediate(_rt);
                }
                _rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32);
                _rt.Create();
                _rtW = w; _rtH = h;
            }
            if (_readTex == null || _readTex.width != w || _readTex.height != h)
            {
                if (_readTex != null) Object.DestroyImmediate(_readTex);
                _readTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            }
        }
    }
}
