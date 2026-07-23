using KUISystem;
using UnityEngine;
using UFont = UnityEngine.Font;
using UFontStyle = UnityEngine.FontStyle;

// Texture2D Color32数组索引图（GetPixels32 返回 bottom-up）
//[18, 19, 20, 21, 22, 23]  ← 顶部（Y 最大）
//[12, 13, 14, 15, 16, 17]
//[6, 7, 8, 9, 10, 11]
//[0, 1, 2, 3, 4, 5]        ← 底部（Y=0）
//
// BGRA buffer 同样是 bottom-up，所以 atlas → buffer 不需要翻转 Y

public class KText_Test : MonoBehaviour
{
    public int FontSize = 25;
    public Color TextColor = Color.white;
    public string Text = "Hello";

    private const int TexW = 1024;
    private const int TexH = 100;
    
    private Texture2D _resultTexture;
    private byte[] _bgraBuffer;

    /// <summary>
    /// 判断字形是否在图集中被旋转 90 度存储。
    /// CharacterInfo.flipped 已废弃，改用四个 UV 坐标判断：
    ///   - 正常：uvBottomLeft 与 uvBottomRight 同水平线（Y 相近，X 差大）
    ///   - 旋转：uvBottomLeft 与 uvBottomRight 同垂直线（X 相近，Y 差大）
    /// </summary>
    public static bool IsGlyphFlipped(CharacterInfo info)
    {
        float dx = Mathf.Abs(info.uvBottomLeft.x - info.uvBottomRight.x);
        float dy = Mathf.Abs(info.uvBottomLeft.y - info.uvBottomRight.y);
        return dy > dx;
    }

    /// <summary>
    /// 从图集中抠出单个字形的像素到 2D 数组。
    /// 采用 Unity 官方推荐方法：直接用四个 UV 坐标（uvBottomLeft/uvBottomRight/
    /// uvTopLeft/uvTopRight）建立字形空间→图集空间的映射，自动处理任何旋转/
    /// 翻转方向，无需依赖废弃的 CharacterInfo.flipped。
    ///
    /// 字形空间（返回数组 mQuad[x, y]）：
    ///   x ∈ [0, glyphW), y ∈ [0, glyphH), y=0 = 字形底部
    /// 图集空间：uv 坐标 × 纹理宽高 = 像素索引（GetPixels32 返回 bottom-up，
    ///   UV V=0 = 纹理底部，方向一致，无需 Y 翻转）
    /// </summary>
    public Color32[,] GetQuadTexture(Texture2D atlasTex, Color32[] atlasPixels, CharacterInfo info)
    {
        int glyphW = info.maxX - info.minX;
        int glyphH = info.maxY - info.minY;
        if (glyphW <= 0 || glyphH <= 0)
            return new Color32[0, 0];

        // 四个角 UV：字形空间 → 图集空间
        //   字形 (0,0)        → uvBottomLeft
        //   字形 (glyphW,0)   → uvBottomRight
        //   字形 (0,glyphH)   → uvTopLeft
        //   字形 (glyphW,glyphH) → uvTopRight
        // 无论字形是否被旋转存储，这四个映射关系都不变
        Vector2 bl = info.uvBottomLeft;
        Vector2 br = info.uvBottomRight;
        Vector2 tl = info.uvTopLeft;

        // 双线性步进向量（每字形像素对应图集中多少 UV）
        float stepX_u = (br.x - bl.x) / glyphW;
        float stepX_v = (br.y - bl.y) / glyphW;
        float stepY_u = (tl.x - bl.x) / glyphH;
        float stepY_v = (tl.y - bl.y) / glyphH;

        int aw = atlasTex.width;
        int ah = atlasTex.height;
        Color32[,] mQuad = new Color32[glyphW, glyphH];

        for (int gy = 0; gy < glyphH; gy++)
        {
            for (int gx = 0; gx < glyphW; gx++)
            {
                // 字形象素 (gx, gy) → 图集 UV 坐标
                float u = bl.x + stepX_u * gx + stepY_u * gy;
                float v = bl.y + stepX_v * gx + stepY_v * gy;
                // UV → 图集像素索引（最近邻采样）
                int ax = Mathf.RoundToInt(u * aw);
                int ay = Mathf.RoundToInt(v * ah);
                if (ax >= 0 && ax < aw && ay >= 0 && ay < ah)
                    mQuad[gx, gy] = atlasPixels[ay * aw + ax];
            }
        }
        return mQuad;
    }

    private void Start()
    {
        var font = KTextCommon.LoadDefault();
        RenderTest(font);
    }

    private void RenderTest(UFont font)
    {
        int w = TexW, h = TexH;
        int stride = w * 4;

        if (_bgraBuffer == null || _bgraBuffer.Length < w * h * 4)
            _bgraBuffer = new byte[w * h * 4];

        // 清空 buffer（透明黑）
        for (int i = 0; i < _bgraBuffer.Length; i += 4)
        {
            _bgraBuffer[i]     = 0; // B
            _bgraBuffer[i + 1] = 0; // G
            _bgraBuffer[i + 2] = 0; // R
            _bgraBuffer[i + 3] = 0; // A
        }

        // 确保字形已在图集中
        font.RequestCharactersInTexture(Text, FontSize, UFontStyle.Normal);

        // 获取图集纹理和像素
        var atlasTex = font.material.mainTexture as Texture2D;
        if (atlasTex == null)
        {
            Debug.LogError("无法获取字体图集纹理");
            return;
        }
        var atlasPixels = KTextCommon.ReadAtlas(atlasTex);

        // 调试：检查图集格式
        Debug.Log($"[KText_Test] 图集: {atlasTex.width}x{atlasTex.height}, format={atlasTex.format}, pixels={atlasPixels.Length}");
        if (atlasPixels.Length > 0)
        {
            var sp = atlasPixels[0];
            Debug.Log($"[KText_Test] 图集首像素: r={sp.r} g={sp.g} b={sp.b} a={sp.a}");
        }

        // 预计算颜色 tint（Color32 字节 × 字节 / 255）
        byte tb = (byte)(TextColor.b * 255f);
        byte tg = (byte)(TextColor.g * 255f);
        byte tr = (byte)(TextColor.r * 255f);
        byte ta = (byte)(TextColor.a * 255f);

        // 布局：penX 从左边距开始，基线在垂直中心
        int penX = 10;
        int baselineY = h / 2;
        int pixelWriteCount = 0;

        foreach (char c in Text)
        {
            CharacterInfo info;
            if (!font.GetCharacterInfo(c, out info, FontSize, UFontStyle.Normal))
            {
                Debug.LogWarning($"字符 '{c}' 不在图集中");
                penX += FontSize / 2; // 未知字符留空
                continue;
            }

            // 从图集抠出字形
            var quad = GetQuadTexture(atlasTex, atlasPixels, info);
            int qw = quad.GetLength(0);
            int qh = quad.GetLength(1);
            Debug.Log($"[KText_Test] 字符 '{c}': quad={qw}x{qh}, advance={info.advance}, minY={info.minY}, maxY={info.maxY}, flipped={IsGlyphFlipped(info)}");
            if (qw == 0 || qh == 0)
            {
                penX += info.advance;
                continue;
            }

            // 字形底部在 buffer 中的 Y 坐标
            // info.minY = 字形底部相对基线的偏移（负值 = 基线以下）
            // atlas 和 buffer 都是 bottom-up，所以不翻转
            int glyphBottomY = baselineY + info.minY;

            for (int gy = 0; gy < qh; gy++)
            {
                int bufY = glyphBottomY + gy;
                if (bufY < 0 || bufY >= h) continue;

                for (int gx = 0; gx < qw; gx++)
                {
                    int bufX = penX + gx;
                    if (bufX < 0 || bufX >= w) continue;

                    var p = quad[gx, gy];
                    if (p.a == 0) continue; // 跳过透明像素

                    int di = bufY * stride + bufX * 4;
                    // 图集可能是 Alpha8 格式（RGB=0），所以 RGB 直接用 TextColor，A 用图集 alpha × TextColor alpha
                    _bgraBuffer[di]     = tb;                          // B (TextColor)
                    _bgraBuffer[di + 1] = tg;                          // G (TextColor)
                    _bgraBuffer[di + 2] = tr;                          // R (TextColor)
                    _bgraBuffer[di + 3] = (byte)(p.a * ta / 255);      // A (atlas alpha × TextColor alpha)
                    pixelWriteCount++;
                }
            }

            penX += info.advance;
        }

        Debug.Log($"[KText_Test] 渲染完成，共写入 {pixelWriteCount} 个像素");

        if (_resultTexture != null)
        {
            Object.DestroyImmediate(_resultTexture);
            _resultTexture = null;
        }
        _resultTexture = KTextCommon.CreateTexture(_bgraBuffer, w, h);
    }

    private void OnDestroy()
    {
        if (_resultTexture != null) Object.DestroyImmediate(_resultTexture);
    }

    private void OnGUI()
    {
        if (_resultTexture == null) return;

        // 屏幕居中显示，放大 2 倍
        int dispW = TexW;
        int dispH = TexH;
        int x = (Screen.width - dispW) / 2;
        int y = (Screen.height - dispH) / 2;

        // 半透明黑色背景
        var orig = GUI.color;
        GUI.color = new Color(0, 0, 0, 0.7f);
        GUI.DrawTexture(new Rect(x, y, dispW, dispH), Texture2D.whiteTexture);
        GUI.color = orig;

        // 展示结果纹理（放大 2 倍）
        Graphics.DrawTexture(new Rect(x, y, dispW, dispH), _resultTexture);

        // 标签
        GUI.Label(new Rect(x, y + dispH + 4, dispW, 30),
            "KText_Test: Atlas glyph extraction → BGRA buffer → DrawTexture");
    }
}
