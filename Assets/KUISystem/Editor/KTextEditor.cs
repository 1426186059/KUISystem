using KUISystem;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace KText.Editor
{
    public class KTextEditor : EditorWindow
    {
        private Font _font;
        private int _fontSize = 32;
        private string _bakeText = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()_+-=[]{};:,.<>?/ ";
        private Vector2 _previewScroll;
        private Texture2D _atlasPreview;
        private string _statusMsg = "";

        [MenuItem("KUISystem/KText/字体图集导出窗口")]
        public static void Open()
        {
            var window = GetWindow<KTextEditor>("KText 字体图集");
            window.minSize = new Vector2(400, 500);
            // 打开时自动加载默认字体
            if (window._font == null)
            {
                window._font = KTextCommon.LoadDefault();
                window.RefreshAtlasPreview();
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("字体图集导出工具", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // 字体选择
            var newFont = (Font)EditorGUILayout.ObjectField("Font", _font, typeof(Font), false);
            if (newFont != _font)
            {
                _font = newFont;
                RefreshAtlasPreview();
            }

            // 字号
            var newSize = EditorGUILayout.IntField("FontSize", _fontSize);
            if (newSize != _fontSize)
            {
                _fontSize = newSize;
                RefreshAtlasPreview();
            }

            // 烘焙文本（决定图集中包含哪些字符）
            EditorGUILayout.LabelField("烘焙文本（图集会包含这些字符）:");
            var newBake = EditorGUILayout.TextArea(_bakeText, GUILayout.Height(40));
            if (newBake != _bakeText)
            {
                _bakeText = newBake;
                RefreshAtlasPreview();
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(
                "1. 窗口打开时自动加载默认字体。\n" +
                "2. 可拖入任意 Font 资源替换。\n" +
                "3. 点「保存图集到 KText/Temp」会将图集导出为 PNG（Alpha→白色可见）。",
                MessageType.Info);

            EditorGUILayout.Space(10);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("刷新图集预览", GUILayout.Height(28)))
                    RefreshAtlasPreview();

                using (new EditorGUI.DisabledScope(_font == null))
                {
                    if (GUILayout.Button("保存图集到 KUISystem/Temp", GUILayout.Height(28)))
                        SaveFontAtlas(_font);
                }
            }

            // 状态信息
            if (!string.IsNullOrEmpty(_statusMsg))
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox(_statusMsg, MessageType.Info);
            }

            // 预览
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("图集预览:", EditorStyles.boldLabel);

            if (_atlasPreview != null)
            {
                _previewScroll = EditorGUILayout.BeginScrollView(_previewScroll);
                var rect = EditorGUILayout.GetControlRect(
                    GUILayout.MinWidth(_atlasPreview.width),
                    GUILayout.MinHeight(_atlasPreview.height));
                GUI.DrawTexture(rect, _atlasPreview, ScaleMode.ScaleToFit);
                EditorGUILayout.EndScrollView();
            }
            else
            {
                EditorGUILayout.HelpBox("未选择字体，或字体尚未生成图集纹理。", MessageType.Warning);
            }
        }

        /// <summary>
        /// 刷新图集预览（从 font.material.mainTexture 提取出 RGBA32 可读副本，
        /// Alpha 通道复制到 RGB 让字形以白色可见）。
        /// </summary>
        private void RefreshAtlasPreview()
        {
            if (_atlasPreview != null)
            {
                DestroyImmediate(_atlasPreview);
                _atlasPreview = null;
            }

            if (_font == null)
            {
                _statusMsg = "未选择字体。";
                return;
            }

            // 关键：动态字体图集初始为空，必须先 RequestCharactersInTexture 烘焙字符
            if (!string.IsNullOrEmpty(_bakeText))
            {
                _font.RequestCharactersInTexture(_bakeText, _fontSize);
            }

            _atlasPreview = ExtractVisibleAtlas(_font);

            // 统计非零 alpha 像素数，判断图集是否真的有内容
            int nonZero = 0;
            if (_atlasPreview != null)
            {
                var px = _atlasPreview.GetPixels32();
                for (int i = 0; i < px.Length; i++)
                    if (px[i].a > 0) nonZero++;
            }

            _statusMsg = _atlasPreview != null
                ? $"图集: {_atlasPreview.width}x{_atlasPreview.height}, format={_atlasPreview.format}\n非零像素: {nonZero}/{_atlasPreview.width * _atlasPreview.height}"
                : $"字体 {_font.name} 无法获取图集纹理。";
            Repaint();
        }

        /// <summary>
        /// 保存字体图集 PNG 到 Assets/KText/Temp。
        /// </summary>
        private void SaveFontAtlas(Font font)
        {
            // 先烘焙字符到图集
            if (!string.IsNullOrEmpty(_bakeText))
                font.RequestCharactersInTexture(_bakeText, _fontSize);

            var src = ExtractVisibleAtlas(font);
            if (src == null)
            {
                _statusMsg = $"字体 {font.name} 无法获取图集纹理。";
                return;
            }

            // 保存到 Assets/KText/Temp
            string outputDir = Path.Combine(Application.dataPath, "KUISystem", "Temp");
            Directory.CreateDirectory(outputDir);

            string outputPath = Path.Combine(
                outputDir,
                $"{font.name}_atlas_{src.width}x{src.height}.png");

            File.WriteAllBytes(outputPath, src.EncodeToPNG());

            // 临时副本销毁（ExtractVisibleAtlas 总是返回新创建的纹理）
            DestroyImmediate(src);

            _statusMsg = $"已保存: {outputPath}\n输出目录: {outputDir}";
            Debug.Log($"[KTextEditor] 字体图集已保存: {outputPath}");
            Debug.Log($"[KTextEditor] 输出目录: {outputDir}");

            EditorUtility.RevealInFinder(outputPath);
        }

        /// <summary>
        /// 从字体图集提取出 RGBA32 可读副本，并把 Alpha 复制到 RGB 让字形以白色可见。
        /// 
        /// Alpha8 图集的 RGB 全为 0，字形信息只存在 Alpha 通道。
        /// Blit 到 RGBA32 RenderTexture 时 Unity 默认 shader 可能输出 (0,0,0,0)，
        /// 连 Alpha 都丢。所以优先直接 GetPixels32 拿原始 Alpha 数据，
        /// 不可读时才走 Blit 回退（并尝试多种 RenderTextureFormat）。
        /// </summary>
        private static Texture2D ExtractVisibleAtlas(Font font)
        {
            if (font == null || font.material == null)
                return null;

            var atlasTex = font.material.mainTexture as Texture2D;
            if (atlasTex == null)
                return null;

            int w = atlasTex.width;
            int h = atlasTex.height;

            // 创建 RGBA32 可读副本
            var result = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color32[] pixels;

            if (atlasTex.isReadable)
            {
                // Alpha8 也可读，GetPixels32 返回 Color32，a 通道有字形数据
                pixels = atlasTex.GetPixels32();
            }
            else
            {
                // 不可读，Blit 到 RenderTexture 中转
                // 尝试多种格式，确保 Alpha 通道被保留
                var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(atlasTex, rt);
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                try
                {
                    result.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                    result.Apply();
                    pixels = result.GetPixels32();
                }
                finally
                {
                    RenderTexture.active = prev;
                    RenderTexture.ReleaseTemporary(rt);
                }
            }

            // 把 Alpha 复制到 RGB，字形以白色显示
            // 同时确保 Alpha 也保留，这样 PNG 在透明背景上显示白色字形
            for (int i = 0; i < pixels.Length; i++)
            {
                byte a = pixels[i].a;
                pixels[i].r = a;
                pixels[i].g = a;
                pixels[i].b = a;
            }

            result.SetPixels32(pixels);
            result.Apply();
            return result;
        }
    }
}
