// ============================================================================
// KText_MeshRenderer.cs — 通过 MeshFilter + MeshRenderer 在场景中渲染文本
// ============================================================================
//
// 【职责】
//   MonoBehaviour 组件，将文本作为 Mesh 显示在 3D/2D 场景中。
//   挂在 GameObject 上即可使用，Inspector 中可调整文本、字体、颜色等参数。
//
// 【渲染管线】
//   TextGenerator 布局 → Mesh → MeshFilter → MeshRenderer (场景可见)
//
//   1. KTextCommon.BuildMesh() 生成 Mesh（flipY=false，场景坐标 Y-up）
//   2. Mesh 赋值给 MeshFilter
//   3. MeshRenderer 使用 KText/Scene shader 渲染
//
// 【关键约束】
//   - 需要 MeshFilter + MeshRenderer 组件（[RequireComponent] 自动添加）
//   - 使用 KText/Scene shader（Blend SrcAlpha OneMinusSrcAlpha，透明队列）
//   - flipY=false: 场景坐标系 Y-up，与 TextGenerator 一致
//   - 修改 Inspector 参数会自动触发 OnValidate → RebuildMesh
//   - 也可代码调用 Refresh() 手动重建 Mesh
//
// ============================================================================

using UnityEngine;
using UFont = UnityEngine.Font;
using UFontStyle = UnityEngine.FontStyle;

namespace KText
{
    /// <summary>
    /// 文本 Mesh 渲染组件。
    /// 挂在 GameObject 上，Inspector 中设置参数，文本作为 Mesh 显示在场景中。
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [ExecuteAlways]
    public class KText_MeshRenderer : MonoBehaviour
    {
        [Header("Text Settings")]
        [TextArea] public string Text = "Hello KText";
        public UFont mFont;
        public int FontSize = 16;
        public UFontStyle FontStyle = UFontStyle.Normal;
        public Color TextColor = Color.white;
        public TextAnchor Anchor = TextAnchor.MiddleLeft;
        public HorizontalWrapMode HorizontalWrap = HorizontalWrapMode.Wrap;
        public VerticalWrapMode VerticalWrap = VerticalWrapMode.Overflow;

        [Header("Layout")]
        public int Width = 300;   // 文本布局宽度（像素）
        public int Height = 100;  // 文本布局高度（像素）

        private MeshFilter _mf;
        private Mesh _mesh;
        private bool _initialized;
        private RectTransform mRectTransform;
        private void EnsureInit()
        {
            if (_initialized) return;
            mRectTransform = gameObject.AddMissComponent<RectTransform>();
            mRectTransform.anchorMin = Vector2.one * 0.5f;
            mRectTransform.anchorMax = Vector2.one * 0.5f;
            mRectTransform.pivot = new Vector2(0, 1);

            _mf = gameObject.AddMissComponent<MeshFilter>();
            var mr = gameObject.AddMissComponent<MeshRenderer>();
            if (mr.sharedMaterial == null || mr.sharedMaterial.shader == null
                || mr.sharedMaterial.shader.name != "KText/Scene")
            {
                mr.sharedMaterial = KTextCommon.CreateMaterial();
            }
            mr.sharedMaterial.color = TextColor;

            if(mFont == null)
            {
                mFont = KTextCommon.LoadDefault();
            }

            _initialized = true;
        }

        private void OnEnable()
        {
            EnsureInit();
            RebuildMesh();
        }

        /// <summary>
        /// 手动重建 Mesh。修改参数后调用此方法刷新显示。
        /// </summary>
        public void Refresh()
        {
            RebuildMesh();
        }

        /// <summary>
        /// Inspector 参数变更时自动重建 Mesh（仅在编辑器中）。
        /// </summary>
        private void OnValidate()
        {
            EnsureInit();
            RebuildMesh();
        }

        private void OnDestroy()
        {
            if (_mesh != null) DestroyImmediate(_mesh);
        }

        /// <summary>
        /// 重建文本 Mesh。
        /// 使用 KTextCommon.BuildMesh 生成 Mesh，flipY=false（场景坐标 Y-up）。
        /// </summary>
        public void RebuildMesh()
        {
            var font = mFont ?? KTextCommon.LoadDefault();
            if (font == null) return;

            mRectTransform.sizeDelta = new Vector2(Width, Height);

            // 构建 Mesh（flipY=false: 场景坐标系 Y-up，与 TextGenerator 一致）
            var mesh = KTextCommon.BuildMesh(
                Text, font, FontSize, FontStyle,
                0, 0, Width, Height,
                TextColor, Anchor, HorizontalWrap, VerticalWrap, false);

            if (_mesh != null) DestroyImmediate(_mesh);
            _mesh = mesh;
            _mf.mesh = _mesh;

            // 同步字体图集和颜色到材质
            var mr = GetComponent<MeshRenderer>();
            if (mr != null && mr.sharedMaterial != null)
            {
                var atlasTex = font.material.mainTexture;
                if (atlasTex != null)
                    mr.sharedMaterial.mainTexture = atlasTex;
                mr.sharedMaterial.color = TextColor;
            }
        }

        /// <summary>
        /// 创建 GameObject 并挂载 KText_MeshRenderer，设置参数后返回实例。
        /// 调用方自行管理生命周期。
        /// </summary>
        public void Draw(
            string text, UFont font, int fontSize, UFontStyle style,
            int x, int y, int clipW, int clipH,
            Color color,
            TextAnchor anchor = TextAnchor.UpperLeft,
            HorizontalWrapMode hWrap = HorizontalWrapMode.Wrap,
            VerticalWrapMode vWrap = VerticalWrapMode.Overflow)
        {
            this.Text = text;
            this.mFont = font;
            this.FontSize = fontSize;
            this.FontStyle = style;
            this.TextColor = color;
            this.Anchor = anchor;
            this.HorizontalWrap = hWrap;
            this.VerticalWrap = vWrap;
            this.Width = clipW;
            this.Height = clipH;
            this.transform.position = new Vector3(x, -y, 0);
            this.Refresh();
        }
    }
}
