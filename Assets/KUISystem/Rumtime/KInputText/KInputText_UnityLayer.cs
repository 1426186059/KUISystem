using System;
using UnityEngine;

namespace KUISystem
{
    /// <summary>
    /// KInputText 的 Unity 适配层：一个基于 IMGUI/OnGUI 自绘的自包含输入框组件（不依赖 UGUI）。
    ///
    /// 类比关系：
    ///  - 本组件本身        ≈ UGUI 的 InputField；
    ///  - 内部持有的引擎     ≈ InputField 持有的 Text（真正的文本/光标/选区状态）；
    ///  - 屏幕自绘（输出）   ≈ InputField 的视觉呈现；
    ///  - 键鼠/IME（输入）   ≈ InputField 的键盘/事件处理。
    ///
    /// 用法：把本组件挂到任意 GameObject 上，在 Inspector 里配置字段（Text/FontSize/...），
    /// 它会在屏幕上自绘输入框，并通过鼠标点击聚焦、每帧把键盘/IME 字符写入引擎。
    /// 无需手动创建 KInputText，也无需额外管理器。
    ///
    /// 注意：所有“配置项”都是【字段】而非属性——Unity Inspector 只序列化/显示字段。
    /// 它们在 Awake / OnValidate 时同步进底层引擎 _input；运行时的文本/光标变化读自引擎。
    /// </summary>
    public class KInputText_UnityLayer : MonoBehaviour
    {
        #region 引擎（类比 InputField 内部的 Text）
        private readonly KInputText _input = new KInputText();
        /// <summary>底层 KInputText 引擎；需要更底层控制（如编程插入文本）时访问。</summary>
        public KInputText Input { get { return _input; } }
        #endregion

        #region 屏幕自绘参数（类比 RectTransform 的位置/缩放，左上角为原点）
        [Header("屏幕自绘 (Screen)")]
        [Tooltip("屏幕绘制位置（左上为原点），类比 RectTransform 的 anchoredPosition。")]
        public Vector2 ScreenPosition = new Vector2(100, 100);
        [Tooltip("绘制缩放，类比 RectTransform.localScale。")]
        public float ScreenZoom = 1f;
        #endregion

        #region 文本 / 占位符（类比 InputField.text / placeholder）
        [Header("文本 (Text)")]
        [TextArea(1, 10)]
        [Tooltip("输入框内容，类比 InputField.text。")]
        public string Text = string.Empty;

        [Tooltip("占位提示文字（focus 前显示，类比 InputField.placeholder）。")]
        public string Placeholder = string.Empty;

        [Tooltip("占位文字颜色。")]
        public Color PlaceholderColor = new Color(1f, 1f, 1f, 0.5f);

        [Tooltip("密码框（隐藏真实字符，类比 InputField.inputType = Password）。")]
        public bool Password = false;

        [Tooltip("密码替代字符，类比 InputField.asteriskChar。")]
        public char PasswordChar = '*';

        [Tooltip("最大输入长度，类比 InputField.characterLimit（0=不限制）。")]
        public int MaxLength = 32767;

        [Tooltip("只读。")]
        public bool ReadOnly = false;

        [Tooltip("是否启用（禁用时不接受输入、变灰）。")]
        public bool Enabled = true;

        [Tooltip("多行（回车换行）。")]
        public bool Multiline = false;

        [Tooltip("是否可见（不可见则不绘制、不接受输入）。")]
        public bool Visible = true;

        [Tooltip("文本水平对齐，类比 Text.alignment。")]
        public HorizontalAlignment TextAlign = HorizontalAlignment.Left;

        [Tooltip("字符大小写转换（WinForms 兼容）。")]
        public CharacterCasing CharacterCasing = CharacterCasing.Normal;

        [Tooltip("失去焦点时是否隐藏选区高亮（WinForms 默认 true）。")]
        public bool HideSelection = true;
        #endregion

        #region 字体（类比 Text 的 Font / FontSize / FontStyle）
        [Header("字体 (Font)")]
        [Tooltip("字体资源（直接拖入 Font 资产，类比 Text.font；留空则按 FontName 从 Resources/Fonts 查找，再兜底 KText 内置字体）。")]
        public Font m_Font;

        [Tooltip("字号，类比 Text.fontSize。")]
        public int FontSize = 16;

        [Tooltip("字体样式（常规/粗体/斜体/粗斜体），类比 FontStyle。")]
        public FontStyle FontStyle = FontStyle.Normal;

        [Tooltip("文字内边距（像素），类比 Text 的 margin。")]
        public int Padding = 3;
        #endregion

        #region 颜色（类比 InputField 的 Text/Selection/Caret 颜色）
        [Header("颜色 (Colors)")]
        [Tooltip("文本颜色，类比 InputField 的 Text 颜色。")]
        public Color TextColor = Color.white;
        [Tooltip("背景颜色（含 alpha），类比 Image.color。")]
        public Color BackgroundColor = new Color(0f, 0f, 0f, 0.85f);
        [Tooltip("光标（插入符）颜色，类比 InputField.caretColor。")]
        public Color CaretColor = Color.white;
        [Tooltip("选区高亮底色，类比 InputField.selectionColor。")]
        public Color SelectionBackColor = new Color(0x2A / 255f, 0x5F / 255f, 0xCF / 255f, 1f);
        #endregion

        #region 边框（类比 Image 的边框 / RectTransform 尺寸）
        [Header("边框 / 尺寸 (Border / Size)")]
        [Tooltip("是否绘制边框（true=FixedSingle，false=None），类比 Image 的有无边框。")]
        public bool Border = true;
        [Tooltip("边框颜色（未聚焦）。")]
        public Color BorderColor = new Color(0x20 / 255f, 0x20 / 255f, 0x20 / 255f, 1f);
        [Tooltip("聚焦时边框颜色，类比 InputField 选中态高亮。")]
        public Color FocusedBorderColor = new Color(0x7A / 255f, 0x9C / 255f, 0xC0 / 255f, 1f);
        [Tooltip("边框宽度（像素）。")]
        public int BorderWidth = 1;
        [Tooltip("控件宽度（像素）。")]
        public int Width = 240;
        [Tooltip("控件高度（像素）。")]
        public int Height = 40;
        #endregion

        #region 光标/选区（运行时只读，类比 InputField.caretPosition / selection）
        [Tooltip("当前光标位置，类比 InputField.caretPosition。")]
        public int CaretPosition { get => _input.CaretPosition; set => _input.CaretPosition = value; }
        [Tooltip("选区起点（=光标位置），类比 InputField.selectionAnchorPosition。")]
        public int SelectionStart { get => _input.SelectionStart; set => _input.SelectionStart = value; }
        [Tooltip("选区长度，类比 InputField.selectionFocusPosition 的差值。")]
        public int SelectionLength { get => _input.SelectionLength; set => _input.SelectionLength = value; }
        [Tooltip("当前选中的文本。")]
        public string SelectedText { get => _input.SelectedText; set => _input.SelectedText = value; }
        [Tooltip("是否有选区。")]
        public bool HasSelection => _input.HasSelection;
        [Tooltip("已输入字符数，类比 InputField.text.Length。")]
        public int CharacterCount => _input.Text.Length;
        [Tooltip("是否聚焦。")]
        public bool IsFocused => _input.Focused;
        [Tooltip("内容建议尺寸（像素），可用来自动撑高，类比 Text.preferredHeight。")]
        public Vector2 PreferredSize => _input.PreferredSize;
        #endregion

        #region 焦点（类比 EventSystem 当前选中的 InputField）
        private static KInputText_UnityLayer _active;
        /// <summary>当前获得焦点的输入框（全局唯一，类比 EventSystem.currentSelectedGameObject）。</summary>
        public static KInputText_UnityLayer Active { get { return _active; } }

        /// <summary>聚焦该输入框（类比 InputField.Select / ActivateInputField）。</summary>
        public void Select() { SetActive(this); }
        /// <summary>取消聚焦（类比 InputField.DeactivateInputField）。</summary>
        public void Deselect() { if (_active == this) SetActive(null); }
        /// <summary>全选文本（类比 InputField.SelectAll）。</summary>
        public void SelectAll() { _input.SelectAll(); }
        /// <summary>清空文本（类比 InputField.text = ""）。</summary>
        public void Clear() { _input.Clear(); }

        private static void SetActive(KInputText_UnityLayer layer)
        {
            if (_active == layer) return;
            if (_active != null) _active._input.KillFocus();
            _active = layer;
            if (_active != null) _active._input.SetFocus();
            // IME 组合模式：本输入框聚焦时开启，让中文/日文等候选词能进入游戏；
            // 失焦时恢复默认。注意：仅在 Play 模式有效——编辑器下 Input 不活跃，IME 需 Play 后测试。
            if (Application.isPlaying)
            {
                UnityEngine.Input.imeCompositionMode = _active != null ? IMECompositionMode.On : IMECompositionMode.Auto;
            }
        }
        #endregion

        #region 配置同步（Inspector 字段 -> 引擎）
        private void ApplyToEngine()
        {
            // 兜底：若 Inspector 没拖字体，用系统自带的 Arial 创建一个 UnityEngine.Font
            // （KTextCommon.Load(家族名) 内部走 Font.CreateDynamicFontFromOSFont，只认
            // 操作系统已安装的字体，如 Arial、Microsoft YaHei 等）。
            if (m_Font == null)
            {
                m_Font = Resources.GetBuiltinResource<UnityEngine.Font>("LegacyRuntime.ttf");
            }
            

            // KText 的文字光栅化由原生 DLL 完成，它只能按“字体家族名”在【操作系统已安装】的
            // 字体里查（CreateDynamicFontFromOSFont）。所以统一用家族名解析一次，与全游戏
            // TextRaster.cs 的 KTextCommon.Load(font.Name) 走同一条路。
            // 注意：拖进来的 TTF 资产只是工程里的文件，系统字体库里没有它，解析会失败并回退到
            // 默认字体（Arial 类）——这就是“只有 Arial 正常、其它都不正常”的根因。要让自定义字体
            // 生效，必须先把 TTF 安装到系统（双击→安装），再用其家族名解析。
            //string famName = (m_Font.fontNames != null && m_Font.fontNames.Length > 0)
            //    ? m_Font.fontNames[0] : m_Font.name;
            //Font resolved = KTextCommon.Load(famName);
            _input.Font = m_Font;

            // 引擎文本仅在非运行期从 Inspector 同步；运行期以用户实时输入为准，
            // 否则 OnValidate / 字段回写会把已输入的文字清空（只剩背景）。
            _input.Text = Text;
            _input.Placeholder = Placeholder;
            _input.PlaceholderColor = PlaceholderColor;
            _input.Password = Password;
            _input.PasswordChar = PasswordChar;
            _input.MaxLength = MaxLength;
            _input.ReadOnly = ReadOnly;
            _input.Enabled = Enabled;
            _input.Multiline = Multiline;
            _input.Visible = Visible;
            _input.TextAlign = TextAlign;
            _input.CharacterCasing = CharacterCasing;
            _input.HideSelection = HideSelection;
            _input.FontSize = FontSize;
            _input.FontStyle = FontStyle;
            _input.Padding = Padding;

            _input.TextColor = TextColor;
            _input.BackgroundColor = BackgroundColor;
            _input.CaretColor = CaretColor;
            _input.SelectionBackColor = SelectionBackColor;

            _input.BorderStyle = Border ? 1 : 0;
            _input.BorderColor = BorderColor;
            _input.FocusedBorderColor = FocusedBorderColor;
            _input.BorderWidth = BorderWidth;
            _input.Width = Width;
            _input.Height = Height;
        }

        private void Awake()
        {
            _input.Text = Text; // 运行期也以 Inspector 的 Text 作为初始内容（仅 Awake 这一次）
            ApplyToEngine();
        }
        //private void OnValidate() { ApplyToEngine(); }
        #endregion

        #region 事件透传（类比 InputField 的 onValueChanged / onEndEdit 等）
        public event EventHandler TextChanged { add => _input.TextChanged += value; remove => _input.TextChanged -= value; }
        public event Action ValueChanged { add => _input.ValueChanged += value; remove => _input.ValueChanged -= value; }
        public event Action<string> OnValueChanged { add => _input.OnValueChanged += value; remove => _input.OnValueChanged -= value; }
        public event Action GotFocusEvent { add => _input.GotFocus += value; remove => _input.GotFocus -= value; }
        public event Action LostFocusEvent { add => _input.LostFocus += value; remove => _input.LostFocus -= value; }
        public event Action<bool> TabPressed { add => _input.TabPressed += value; remove => _input.TabPressed -= value; }
        public event Action SubmitPressed { add => _input.SubmitPressed += value; remove => _input.SubmitPressed -= value; }
        public event Action CancelPressed { add => _input.CancelPressed += value; remove => _input.CancelPressed -= value; }
        public event Action<int> FunctionKeyPressed { add => _input.FunctionKeyPressed += value; remove => _input.FunctionKeyPressed -= value; }
        public event Action AltEnterPressed { add => _input.AltEnterPressed += value; remove => _input.AltEnterPressed -= value; }
        #endregion

        #region 生命周期
        private void OnDestroy()
        {
            if (_active == this) SetActive(null);
        }
        #endregion

        #region 输入（文字）— 类比 InputField 的键盘处理，由本组件每帧驱动
        private void Update()
        {
            // Inspector 字段已在 Awake 同步进引擎；这里仅驱动键盘输入与光标闪烁。
            // 运行时若用代码改了字段，可手动再调用 ApplyToEngine() 同步。
            // 注意：不要强制 imeCompositionMode = On。一旦强制开启，系统默认的中文（拼音）IME
            // 会把每个英文字母都吞进拼音组合（e.character 变为 '\0'），导致英文输入整块不显示；
            // 而空格是拼音的“确认”键，不受影响——表现就是“空格正常、字母消失”。
            // 保持默认 Auto 即可：英文直接透传到 e.character，中文仍由用户当前 IME 正常组合。
            if (_input.Focused)
            {
                _input.HandleKeyboardInput(); // 同步 IME 组合串预览 + 光标闪烁
            }

            // 关键修复：把纹理渲染（含 KText.dll 的 DrawText / 原生字体请求）放到 Update，
            // 而不是 OnGUI 的 Repaint 阶段。在 IMGUI 绘制上下文里调用原生字体 API 会重入/打断
            // GUI 绘制，导致同帧的 GUI.DrawTexture 整帧不执行（表现：有文字时整块不显示，
            // 删文字后 DrawText 被跳过、GUI 绘制恢复、背景显示）。移到 Update 后，OnGUI 只负责
            // 把已渲染好的纹理贴到屏幕上，不再触碰原生字体调用。
            _input.GetTexture(_input.Width, _input.Height);
        }

        private void OnGUI()
        {
            Event e = Event.current;
            if (e == null) return;

            if (e.type == EventType.MouseDown)
            {
                Rect r = GetScreenRect();
                if (r.Contains(e.mousePosition))
                {
                    SetActive(this);
                    // localX：相对输入框左边缘（引擎内部会再扣除 Padding 对齐文字内容）
                    _input.OnMouseDown(e.mousePosition.x - r.x, e.mousePosition.y - r.y, e.clickCount > 1);
                    e.Use();
                }
                else if (_active == this)
                {
                    SetActive(null); // 点击空白处失焦
                }
            }
            else if (e.type == EventType.KeyDown)
            {
                // 键盘/IME 输入：e.character 同时包含大写字母与 IME 已提交字符
                if (_active == this) _input.HandleKeyEvent(e);
            }
            else if (e.type == EventType.Repaint)
            {
                Render(); // 仅在 Repaint 阶段自绘（左上原点，IMGUI 坐标系），避免每事件重建纹理
            }
        }
        #endregion

        #region 输出（渲染）— OnGUI 自绘（项目不依赖 UGUI，显示走 IMGUI）
        /// <summary>屏幕命中矩形（左上原点，IMGUI 坐标系），用于鼠标点击聚焦判定与自绘定位。</summary>
        private Rect GetScreenRect()
        {
            return new Rect(ScreenPosition.x, ScreenPosition.y, _input.Width * ScreenZoom, _input.Height * ScreenZoom);
        }

        private void Render()
        {
            if (!_input.Visible) return;
            // 纹理已在 Update 里（GetTexture）渲染好；这里只负责把纹理贴到屏幕，
            // 绝不在 GUI 绘制上下文里调用 DrawText，避免原生字体调用重入打断 IMGUI 绘制。
            Texture2D tex = _input.Canvas;
            if (tex == null) return;
            // 走 IMGUI 批处理（而非裸 Graphics.DrawTexture），与 OnGUI 的 Repaint 同通道；
            // 半透明背景的混合由 KInputText.GetTexture 设置的 alphaIsTransparency 保证。
            GUI.DrawTexture(GetScreenRect(), tex);
        }
        #endregion
    }
}
