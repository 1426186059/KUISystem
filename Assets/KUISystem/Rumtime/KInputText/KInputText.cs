using System;
using UnityEngine;

namespace KUISystem
{
    /// <summary>
    /// 一个自包含的、不依赖 Mir3 游戏框架（不继承 DXControl、不引用 RenderingPipelineManager、
    /// 也不再引用 Mir3UnityAdapter.System_Drawing）的输入框引擎。
    /// 实现 Winform TextBox 的核心功能：文本、光标、选区、剪贴板、密码掩码、最大长度、
    /// 只读、键盘(含 IME 输入)与鼠标定位。
    ///
    /// 渲染只依赖 <see cref="KText"/>（KText_WinFormLayer / KTextCommon）。所有绘制都写入
    /// 一块"自下而上"的 BGRA32 字节缓冲（row 0 = 底部），与 KText 的原生缓冲约定一致，
    /// 因此无需再做行翻转即可直接 LoadRawTextureData 到 Texture2D（参见 KText 自带的例子）。
    ///
    /// 上层（TextBox_Interface / DXTextBox）用它提供的能力来实现 TextBox。
    /// </summary>
    #region 枚举（WinForms 兼容，命名空间级，便于上层直接引用）
    public enum HorizontalAlignment { Left = 0, Right = 1, Center = 2 }
    public enum CharacterCasing { Normal = 0, Upper = 1, Lower = 2 }
    #endregion

    public class KInputText
    {
        #region 静态焦点模型
        private static KInputText _active;
        public static KInputText Active
        {
            get { return _active; }
            set
            {
                if (_active == value) return;
                if (_active != null) _active.KillFocus();
                _active = value;
                if (_active != null) _active.SetFocus();
            }
        }
        #endregion

        #region 样式 / 状态（公开属性，供 DXTextBox / 例子使用）
        public bool Password { get; set; } = false;
        public char PasswordChar { get; set; } = '*';
        public bool UseSystemPasswordChar { get { return Password; } set { Password = value; } }

        public int MaxLength { get; set; } = 32767;
        public bool ReadOnly { get; set; } = false;
        public bool Multiline { get; set; } = false;

        public bool Visible { get; set; } = true;
        public bool Enabled { get; set; } = true;

        public Color BackgroundColor { get; set; } = new Color(0, 0, 0, 0.85f);
        public Color TextColor { get; set; } = Color.white;
        public Color CaretColor { get; set; } = Color.white;
        public Color BorderColor { get; set; } = new Color(0x20 / 255f, 0x20 / 255f, 0x20 / 255f, 1f);
        public Color FocusedBorderColor { get; set; } = new Color(0x7A / 255f, 0x9C / 255f, 0xC0 / 255f, 1f);
        public Color SelectionBackColor { get; set; } = new Color(0x2A / 255f, 0x5F / 255f, 0xCF / 255f, 1f);
        /// <summary>直接赋值的字体资源（Unity 适配层用；拖入 Font 资产后优先于 FontName 使用）。</summary>
        public Font Font { get; set; } = null;
        /// <summary>占位提示文字（空且未聚焦时显示，类比 UGUI InputField.placeholder）。</summary>
        public string Placeholder { get; set; } = string.Empty;
        /// <summary>占位文字颜色。</summary>
        public Color PlaceholderColor { get; set; } = new Color(1f, 1f, 1f, 0.5f);
        public float FontSize { get; set; } = 12f;
        public FontStyle FontStyle { get; set; } = FontStyle.Normal;

        public int Padding { get; set; } = 3;
        public int BorderWidth { get; set; } = 1;
        public int BorderStyle { get; set; } = 1; // 0=None 1=FixedSingle

        public bool Focused { get; private set; } = false;

        // WinForms 兼容样式
        public HorizontalAlignment TextAlign { get; set; } = HorizontalAlignment.Left;
        public CharacterCasing CharacterCasing { get; set; } = CharacterCasing.Normal;
        /// <summary>失去焦点时是否隐藏选区高亮（WinForms 默认 true）。</summary>
        public bool HideSelection { get; set; } = true;
        #endregion

        #region 文本 / 光标
        private string _text = string.Empty;
        public string Text
        {
            get { return _text; }
            set
            {
                string v = value ?? string.Empty;
                if (v == _text) return;
                _text = v;
                if (_caretPosition > _text.Length) _caretPosition = _text.Length;
                _selectionAnchor = _caretPosition;
                _dirty = true;
                TextChanged?.Invoke(this, EventArgs.Empty);
                _onValueChanged?.Invoke(_text);
            }
        }

        private int _caretPosition = 0;
        /// <summary>当前光标（插入符号）位置。</summary>
        public int CaretPosition
        {
            get { return _caretPosition; }
            set
            {
                _caretPosition = Math.Max(0, Math.Min(_text.Length, value));
                _selectionAnchor = _caretPosition;
                _caretBlinkStart = Time.realtimeSinceStartup;
                _dirty = true;
            }
        }

        /// <summary>Winform 兼容：SelectionStart 即光标位置。</summary>
        public int SelectionStart
        {
            get { return _caretPosition; }
            set { CaretPosition = value; }
        }

        /// <summary>选区长度（shift+方向键 / 拖拽选择）。</summary>
        public int SelectionLength
        {
            get { return Math.Abs(_caretPosition - _selectionAnchor); }
            set
            {
                int len = Math.Max(0, value);
                _selectionAnchor = Math.Max(0, Math.Min(_text.Length, _caretPosition - len));
                _dirty = true;
            }
        }

        public bool HasSelection => SelectionLength > 0;

        /// <summary>内容建议尺寸（像素）：单行=文本宽+2*Padding，多行=文本高；用于自动撑高，类比 Text.preferredHeight。</summary>
        public Vector2 PreferredSize
        {
            get
            {
                Vector2 m = Measure(DisplayText());
                if (Multiline) return new Vector2(Math.Max(Width, m.x + 2 * Padding), Math.Max(Height, m.y + 2 * Padding));
                return new Vector2(m.x + 2 * Padding, Height);
            }
        }

        /// <summary>当前选中的文本（WinForms SelectedText）。</summary>
        public string SelectedText
        {
            get
            {
                if (!HasSelection) return string.Empty;
                int start = Math.Min(_caretPosition, _selectionAnchor);
                return _text.Substring(start, SelectionLength);
            }
            set
            {
                if (ReadOnly || !Enabled) return;
                DeleteSelection();
                if (string.IsNullOrEmpty(value)) return;
                RecordUndo();
                _text = _text.Insert(_caretPosition, value);
                _caretPosition += value.Length;
                _selectionAnchor = _caretPosition;
                _dirty = true;
                TextChanged?.Invoke(this, EventArgs.Empty);
                _onValueChanged?.Invoke(_text);
                ValueChanged?.Invoke();
            }
        }

        #region 撤销（单级，WinForms 兼容）
        private string _undoText;
        public bool CanUndo => _undoText != null;
        public void Undo()
        {
            if (_undoText == null) return;
            _text = _undoText;
            _undoText = null;
            _caretPosition = Math.Min(_caretPosition, _text.Length);
            _selectionAnchor = _caretPosition;
            _dirty = true;
            TextChanged?.Invoke(this, EventArgs.Empty);
            _onValueChanged?.Invoke(_text);
            ValueChanged?.Invoke();
        }
        private void RecordUndo()
        {
            if (ReadOnly) return;
            _undoText = _text;
        }
        #endregion
        #endregion

        #region 事件
        public event EventHandler TextChanged;
        public event Action GotFocus;
        public event Action LostFocus;
        public event Action<bool> TabPressed;     // shift 状态
        public event Action SubmitPressed;        // Enter
        public event Action CancelPressed;        // Escape
        public event Action<int> FunctionKeyPressed; // F1..F12
        public event Action AltEnterPressed;      // Alt+Enter
        public event Action ValueChanged;

        // 兼容旧调用：_onValueChanged?.Invoke(_text)
        private Action<string> _onValueChanged;
        public event Action<string> OnValueChanged
        {
            add { _onValueChanged += value; }
            remove { _onValueChanged -= value; }
        }
        #endregion

        #region 内部字段
        private int _selectionAnchor = 0;
        private bool _caretVisible = true;
        private float _caretBlinkStart = 0f;
        private bool _dirty = true;
        /// <summary>单行文本水平滚动偏移（像素）：文字向左移出可视区的距离，使光标始终可见（类比 WinForms TextBox）。</summary>
        private float _scrollOffset = 0f;
        /// <summary>最近一次绘制时文字内容的左起点（本地坐标，相对控件左边缘）；用于鼠标点击命中测试。</summary>
        private float _contentOriginX = 0f;
        #endregion

        #region 文本编辑
        public bool InsertChar(char c)
        {
            if (ReadOnly || !Enabled) return false;
            if (c < 0x20 && c != '\t') return false; // 忽略非打印控制字符

            RecordUndo();
            DeleteSelection();
            if (_text.Length >= MaxLength) return false;

            if (CharacterCasing == CharacterCasing.Upper) c = char.ToUpper(c);
            else if (CharacterCasing == CharacterCasing.Lower) c = char.ToLower(c);

            _text = _text.Insert(_caretPosition, c.ToString());
            _caretPosition++;
            _selectionAnchor = _caretPosition;
            _caretBlinkStart = Time.realtimeSinceStartup;
            _dirty = true;
            TextChanged?.Invoke(this, EventArgs.Empty);
            _onValueChanged?.Invoke(_text);
            ValueChanged?.Invoke();
            return true;
        }

        public void InsertText(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            foreach (char c in s) InsertChar(c);
        }

        public bool HandleBackspace()
        {
            if (ReadOnly || !Enabled) return false;
            RecordUndo();
            if (HasSelection) { DeleteSelection(); return true; }
            if (_caretPosition <= 0) return false;

            _text = _text.Remove(_caretPosition - 1, 1);
            _caretPosition--;
            _selectionAnchor = _caretPosition;
            _dirty = true;
            TextChanged?.Invoke(this, EventArgs.Empty);
            _onValueChanged?.Invoke(_text);
            ValueChanged?.Invoke();
            return true;
        }

        public bool HandleDelete()
        {
            if (ReadOnly || !Enabled) return false;
            RecordUndo();
            if (HasSelection) { DeleteSelection(); return true; }
            if (_caretPosition >= _text.Length) return false;

            _text = _text.Remove(_caretPosition, 1);
            _dirty = true;
            TextChanged?.Invoke(this, EventArgs.Empty);
            _onValueChanged?.Invoke(_text);
            ValueChanged?.Invoke();
            return true;
        }

        public void HandleHome(bool shift)
        {
            if (!shift) _selectionAnchor = 0;
            _caretPosition = 0;
            _caretBlinkStart = Time.realtimeSinceStartup;
            _dirty = true;
        }

        public void HandleEnd(bool shift)
        {
            if (!shift) _selectionAnchor = _text.Length;
            _caretPosition = _text.Length;
            _caretBlinkStart = Time.realtimeSinceStartup;
            _dirty = true;
        }

        public void HandleArrow(int dir, bool shift)
        {
            int target = _caretPosition + dir;
            // 若当前有选区且未按下 shift，先跳到选区端点（Winform 行为）
            if (!shift && HasSelection)
            {
                target = dir > 0 ? Math.Max(_caretPosition, _selectionAnchor)
                                 : Math.Min(_caretPosition, _selectionAnchor);
                _selectionAnchor = target;
                _caretPosition = target;
                _caretBlinkStart = Time.realtimeSinceStartup;
                _dirty = true;
                return;
            }
            _caretPosition = Math.Max(0, Math.Min(_text.Length, target));
            if (!shift) _selectionAnchor = _caretPosition;
            _caretBlinkStart = Time.realtimeSinceStartup;
            _dirty = true;
        }

        private void DeleteSelection()
        {
            if (!HasSelection) return;
            int start = Math.Min(_caretPosition, _selectionAnchor);
            int len = SelectionLength;
            _text = _text.Remove(start, len);
            _caretPosition = start;
            _selectionAnchor = start;
            _dirty = true;
        }

        public void Select(int start, int length)
        {
            start = Math.Max(0, Math.Min(_text.Length, start));
            length = Math.Max(0, Math.Min(_text.Length - start, length));
            _selectionAnchor = start;
            _caretPosition = start + length;
            _dirty = true;
        }

        public void SelectAll()
        {
            if (_text.Length == 0) return;
            _selectionAnchor = 0;
            _caretPosition = _text.Length;
            _dirty = true;
        }

        public void DeselectAll()
        {
            _selectionAnchor = _caretPosition;
            _dirty = true;
        }

        public void Clear()
        {
            if (_text.Length == 0) return;
            RecordUndo();
            _text = string.Empty;
            _caretPosition = 0;
            _selectionAnchor = 0;
            _dirty = true;
            TextChanged?.Invoke(this, EventArgs.Empty);
            _onValueChanged?.Invoke(_text);
            ValueChanged?.Invoke();
        }

        public void AppendText(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            RecordUndo();
            _caretPosition = _text.Length;
            _selectionAnchor = _caretPosition;
            foreach (char c in s) InsertChar(c);
        }
        #endregion

        #region 剪贴板
        public void Copy()
        {
            if (!HasSelection) return;
            int start = Math.Min(_caretPosition, _selectionAnchor);
            GUIUtility.systemCopyBuffer = _text.Substring(start, SelectionLength);
        }

        public void Cut()
        {
            if (ReadOnly) { Copy(); return; }
            Copy();
            RecordUndo();
            DeleteSelection();
        }

        public void Paste()
        {
            if (ReadOnly || !Enabled) return;
            string clip = GUIUtility.systemCopyBuffer;
            if (string.IsNullOrEmpty(clip)) return;
            RecordUndo();
            if (!Multiline) clip = clip.Replace("\r", "").Replace("\n", "");
            InsertText(clip);
        }
        #endregion

        #region 焦点
        public void SetFocus()
        {
            if (Focused) return;
            Focused = true;
            _caretBlinkStart = Time.realtimeSinceStartup;
            _dirty = true;
            GotFocus?.Invoke();
        }

        public void KillFocus()
        {
            if (!Focused) return;
            Focused = false;
            _dirty = true;
            LostFocus?.Invoke();
        }
        #endregion

        #region 输入（键盘由 KInputText_UnityLayer 在 OnGUI 的 KeyDown 事件驱动；IME 通过 compositionString 预览、e.character 提交）
        /// <summary>当前 IME 组合串（拼音预览），由 Update 每帧同步；绘制时附在文本末尾。</summary>
        public string Composition { get; private set; } = string.Empty;

        /// <summary>每帧由 Update 调用：同步 IME 组合串 + 光标闪烁。</summary>
        public void HandleKeyboardInput()
        {
            if (!Focused) { Composition = string.Empty; return; }
            // 同步 IME 组合串（仅用于预览显示；提交后的字符经 OnGUI 的 e.character 进入文本）。
            // 注意：必须每帧读取，因为 IME 组合过程中 compositionString 会持续变化。
            Composition = Input.compositionString;
            // 光标闪烁
            float t = (Time.realtimeSinceStartup - _caretBlinkStart) % 1f;
            _caretVisible = t < 0.5f;
        }

        /// <summary>由 OnGUI 的 EventType.KeyDown 驱动。e.character 同时包含大写字母与 IME 已提交字符，
        /// 比 Input.inputString 更可靠（Input.inputString 在 IME 激活时不会被填充）。</summary>
        public void HandleKeyEvent(Event e)
        {
            if (!Focused || !Enabled) return;
            // 正在 IME 组合时交还 OS 处理；最终字符会以 e.character 形式送达，无需在此拦截
            //（包括用 Enter/空格/数字 确认候选，都不会误触发 Submit/取消）。
            if (Input.compositionString.Length > 0) return;

            // 1) 可打印字符（含大写、IME 提交的中文等）
            char c = e.character;
            if (c != '\0' && c >= 0x20)
            {
                InsertChar(c);
                e.Use();
                return;
            }

            // 2) 组合键
            bool shift = e.shift;
            if (e.control || e.command)
            {
                switch (e.keyCode)
                {
                    case KeyCode.A: SelectAll(); e.Use(); return;
                    case KeyCode.X: Cut(); e.Use(); return;
                    case KeyCode.C: Copy(); e.Use(); return;
                    case KeyCode.V: Paste(); e.Use(); return;
                }
            }
            if (e.alt)
            {
                if (e.keyCode == KeyCode.Return) { AltEnterPressed?.Invoke(); e.Use(); return; }
                for (int f = 1; f <= 12; f++)
                    if (e.keyCode == (KeyCode)(KeyCode.F1 + (f - 1))) { FunctionKeyPressed?.Invoke(f); e.Use(); return; }
            }

            // 3) 特殊键
            switch (e.keyCode)
            {
                case KeyCode.Backspace: HandleBackspace(); e.Use(); break;
                case KeyCode.Delete: HandleDelete(); e.Use(); break;
                case KeyCode.LeftArrow: HandleArrow(-1, shift); e.Use(); break;
                case KeyCode.RightArrow: HandleArrow(1, shift); e.Use(); break;
                case KeyCode.Home: HandleHome(shift); e.Use(); break;
                case KeyCode.End: HandleEnd(shift); e.Use(); break;
                case KeyCode.Tab: TabPressed?.Invoke(shift); e.Use(); break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter: SubmitPressed?.Invoke(); e.Use(); break;
                case KeyCode.Escape: CancelPressed?.Invoke(); e.Use(); break;
            }
        }
        #endregion

        #region 鼠标：根据点击位置定位光标
        /// <summary>localX 为相对输入框左边缘的坐标（控件本地坐标）。点击命中测试会扣除当前水平滚动，
        /// 把屏幕点击位置换算成文字内容坐标，从而即使文本左右滚动也能正确落点。</summary>
        public void SetCaretFromLocalX(float localX)
        {
            float lx = localX - _contentOriginX;
            string display = DisplayText();
            int pos = 0;
            float best = float.MaxValue;
            for (int i = 0; i <= display.Length; i++)
            {
                float w = Measure(display.Substring(0, i)).x;
                float d = Math.Abs(w - lx);
                if (d < best) { best = d; pos = i; }
            }
            CaretPosition = pos;
        }

        public void OnMouseDown(float localX, bool doubleClick)
        {
            SetFocus();
            if (doubleClick) SelectAll();
            else SetCaretFromLocalX(localX);
        }
        #endregion

        #region 渲染
        private string DisplayText()
        {
            if (Password) return new string(PasswordChar, _text.Length);
            // 聚焦且正在 IME 组合时，把组合串（拼音预览）附在文本末尾一起显示
            if (Focused && !string.IsNullOrEmpty(Composition)) return _text + Composition;
            return _text;
        }

        private Vector2 Measure(string text)
        {
            if (string.IsNullOrEmpty(text)) return Vector2.zero;
            var anchor = TextAlignToAnchor(TextAlign);
            var hWrap = Multiline ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            return KText.MeasureText(text, Font, Mathf.RoundToInt(FontSize), FontStyle,
                1 << 20, anchor, hWrap, VerticalWrapMode.Overflow);
        }

        private TextAnchor TextAlignToAnchor(HorizontalAlignment align)
        {
            // 单行垂直居中，多行顶对齐（与选区/光标的整行高亮一致）
            bool middle = !Multiline;
            if (align == HorizontalAlignment.Right) return middle ? TextAnchor.MiddleRight : TextAnchor.UpperRight;
            if (align == HorizontalAlignment.Center) return middle ? TextAnchor.MiddleCenter : TextAnchor.UpperCenter;
            return middle ? TextAnchor.MiddleLeft : TextAnchor.UpperLeft;
        }

        /// <summary>
        /// 把输入框绘制到 BGRA32 字节缓冲（自下而上约定：row 0 = 底部）。
        /// (rx, ry, rw, rh) 为控件在缓冲中的区域，坐标同样自下而上（与 KText 原生缓冲一致）。
        /// 布局计算在"本地自上而下"坐标内进行，写入缓冲时自动转换为自下而上。
        /// </summary>
        #region 绘制辅助方法（原 Draw 内嵌套局部函数，抽出为正规方法）

        /// <summary>本地自上而下坐标 → 缓冲自下而上坐标 的矩形填充（纯 FillRect，不依赖 KText）。</summary>
        private void DrawFill(byte[] buffer, int bufW, int bufH, int rx, int ry, int rw, int rh,
            int tx, int ty, int tw, int th, Color32 col)
        {
            if (tw <= 0 || th <= 0) return;
            int bx = rx + tx;
            int by = ry + (rh - ty - th);
            int stride = bufW * 4;
            KTextCommon.FillRect(buffer, bufW, bufH, stride, bx, by, tw, th, col);
        }

        /// <summary>
        /// 本地自上而下坐标 → 缓冲自下而上坐标 的文本绘制。
        /// 用临时缓冲承载文本，再 CompositeBGRA 以 src-over 合成回主缓冲：
        /// 控件缓冲在此之前已画好背景/边框/选区高亮；DrawText 若直接写会覆盖这些像素，
        /// 先画到干净的临时缓冲、再按 alpha 合成，文字才能正确叠在背景之上而不破坏已有内容。
        /// 注：原生 KText.KText.DrawText 本身支持 stride + 裁剪区直接写入整张缓冲
        /// （见 KText_WinFormLayer.DrawText，全游戏文字都这么画），这里用临时缓冲仅为干净的 alpha 合成。
        /// </summary>
        private void DrawTextRegion(byte[] buffer, int bufW, int bufH, int rx, int ry, int rw, int rh,
            int tx, int ty, int tw, int th, string s, TextAnchor anchor,
            HorizontalWrapMode hWrap, VerticalWrapMode vWrap, Color32 col, int xOff = 0)
        {
            if (string.IsNullOrEmpty(s) || tw <= 0 || th <= 0) return;
            int bx = rx + tx;
            int by = ry + (rh - ty - th);
            int need = tw * th * 4;
            if (_textBuf == null || _textBuf.Length != need) _textBuf = new byte[need];
            Array.Clear(_textBuf, 0, need);

            // KText.KText.DrawText 按 KText 原生“自下而上”约定把文字写入临时缓冲（row 0 = 底部），
            // 与主缓冲朝向一致，CompositeBGRA 直接按行号对应合成，并把 RGBA 临时缓冲转成 BGRA 主缓冲。
            // xOff：水平滚动偏移（像素），把整行文字左移以露出右侧被裁掉的内容（单行横向滚动）。
            int glyphPixels = 0;
            try
            {
                KText.DrawText(_textBuf, tw, th, tw * 4, s, Font,
                    Mathf.RoundToInt(FontSize), FontStyle, xOff, 0, tw, th, col, anchor, hWrap, vWrap);
                for (int i = 3; i < need; i += 4) if (_textBuf[i] != 0) glyphPixels++;
            }
            catch (System.Exception ex)
            {
                // DrawText 若抛异常（如：OnGUI 上下文里 font atlas 读取失败），只跳过文字、保留背景/边框，
                // 避免异常冒泡到 GetTexture 导致 LoadRawTextureData 不执行、整帧纹理冻结在“空背景”那帧。
                if (!_diagLogged)
                {
                    Debug.LogError("[KInputText] DrawText 抛出异常（文字被跳过，背景仍显示）：\n" + ex);
                    _diagLogged = true;
                }
                return;
            }
            if (!_diagLogged)
            {
                Debug.Log("[KInputText] DrawText 字形像素数=" + glyphPixels + "，文本=\"" + s + "\"");
                _diagLogged = true;
            }
            if (glyphPixels == 0)
                Debug.LogWarning("[KInputText] DrawText 未产生任何字形像素：字体 atlas 可能未就绪（font.material.mainTexture 为空或 GetCharacterInfo 失败）。");

            CompositeBGRA(buffer, bufW, bufH, _textBuf, tw, th, bx, by);
        }

        #endregion

        public void Draw(byte[] buffer, int bufW, int bufH, int rx, int ry, int rw, int rh)
        {
            if (buffer == null || !Visible) return;

            // 背景（纯 FillRect，不依赖 KText，无条件先画，保证底框一定可见）
            if (BackgroundColor.a > 0.001f)
                DrawFill(buffer, bufW, bufH, rx, ry, rw, rh, 1, 1, rw - 2, rh - 2, BackgroundColor);

            // 边框（纯 FillRect，不依赖 KText）
            var border = Focused ? FocusedBorderColor : BorderColor;
            if (BorderStyle == 1 && BorderWidth > 0)
            {
                int bw = BorderWidth;
                DrawFill(buffer, bufW, bufH, rx, ry, rw, rh, 0, 0, rw, bw, border);          // 顶边
                DrawFill(buffer, bufW, bufH, rx, ry, rw, rh, 0, rh - bw, rw, bw, border);     // 底边
                DrawFill(buffer, bufW, bufH, rx, ry, rw, rh, 0, 0, bw, rh, border);          // 左边
                DrawFill(buffer, bufW, bufH, rx, ry, rw, rh, rw - bw, 0, bw, rh, border);     // 右边
            }

            int pad = Padding;
            // 文本区域（本地自上而下）：左内边距起，占满宽度、整高（垂直顶对齐）
            int textX = pad;
            int textY = 0;
            int textW = Math.Max(1, rw - pad * 2);
            int textH = rh;

            string display = DisplayText();

            // 计算文字水平偏移（WinForms TextAlign）
            TextAnchor anchor = TextAlignToAnchor(TextAlign);
            float textOffsetX;
            if (TextAlign == HorizontalAlignment.Right)
                textOffsetX = (rw - pad) - Measure(display).x;
            else if (TextAlign == HorizontalAlignment.Center)
                textOffsetX = pad + (textW - Measure(display).x) / 2f;
            else
                textOffsetX = textX;

            // 单行文本超出可视宽度时，水平滚动使光标始终可见（类比 WinForms TextBox）。
            // contentOriginX = 文字内容在本地坐标中的左起点；滚动后左起点向左移动 _scrollOffset。
            float contentOriginX = textOffsetX;
            if (!Multiline)
            {
                float contentWidth = Measure(display).x;
                float viewLeft = textX;
                float viewRight = textX + textW;
                if (contentWidth <= textW)
                {
                    _scrollOffset = 0;
                }
                else
                {
                    float maxScroll = contentWidth - textW;
                    _scrollOffset = Mathf.Clamp(_scrollOffset, 0f, maxScroll);

                    float caretContentX = Measure(display.Substring(0, Math.Min(_caretPosition, display.Length))).x;
                    float caretLocalX = textOffsetX - _scrollOffset + caretContentX;
                    float margin = 2f;
                    if (caretLocalX > viewRight - margin)
                        _scrollOffset = textOffsetX + caretContentX - (viewRight - margin);
                    else if (caretLocalX < viewLeft + margin)
                        _scrollOffset = textOffsetX + caretContentX - (viewLeft + margin);
                    _scrollOffset = Mathf.Clamp(_scrollOffset, 0f, maxScroll);
                }
                contentOriginX = textOffsetX - _scrollOffset;
            }
            _contentOriginX = contentOriginX;

            // 选区高亮（在文字下画底色）；未聚焦且 HideSelection 时隐藏。
            // 选区矩形裁剪到可视区 [textX, textX+textW]，避免滚动后高亮溢出边框。
            bool showSelection = Focused || !HideSelection;
            if (HasSelection && showSelection)
            {
                int start = Math.Min(_caretPosition, _selectionAnchor);
                start = Math.Max(0, Math.Min(start, display.Length));
                int selLen = Math.Max(0, Math.Min(SelectionLength, display.Length - start));
                string before = display.Substring(0, start);
                string sel = selLen > 0 ? display.Substring(start, selLen) : string.Empty;
                float x0 = contentOriginX + Measure(before).x;
                float w = Measure(sel).x;
                int selX0 = (int)x0;
                int selX1 = (int)(x0 + w);
                int viewL = textX;
                int viewR = textX + textW;
                selX0 = Math.Max(selX0, viewL);
                selX1 = Math.Min(selX1, viewR);
                int selW = Math.Max(0, selX1 - selX0);
                if (selW > 0)
                    DrawFill(buffer, bufW, bufH, rx, ry, rw, rh, selX0, 2, selW, rh - 4, SelectionBackColor);
            }

            // 文字（空且未聚焦时显示占位提示）
            bool placeholderMode = string.IsNullOrEmpty(display) && !Focused && !string.IsNullOrEmpty(Placeholder);
            if (placeholderMode)
            {
                var hWrap = Multiline ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
                DrawTextRegion(buffer, bufW, bufH, rx, ry, rw, rh, textX, textY, textW, textH,
                    Placeholder, anchor, hWrap, VerticalWrapMode.Overflow, PlaceholderColor);
            }
            else if (!string.IsNullOrEmpty(display))
            {
                var hWrap = Multiline ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
                // 单行：水平位置由我们自己用 contentOriginX 控制（给 DrawText 一个左偏移），
                // 因此强制用 Left 锚点（仅保留垂直对齐），避免与偏移叠加产生双重定位。
                // 多行：本就按宽度换行不会溢出，直接沿用对齐锚点，不加偏移（行为不变）。
                TextAnchor drawAnchor = Multiline ? anchor : TextAnchor.MiddleLeft;
                int xOff = Multiline ? 0 : Mathf.RoundToInt(contentOriginX - textX);
                DrawTextRegion(buffer, bufW, bufH, rx, ry, rw, rh, textX, textY, textW, textH,
                    display, drawAnchor, hWrap, VerticalWrapMode.Overflow, TextColor, xOff);
            }

            // 光标
            if (Focused && _caretVisible && Enabled)
            {
                string before = _caretPosition >= 0 && _caretPosition <= display.Length
                    ? display.Substring(0, _caretPosition) : display;
                float x = contentOriginX + Measure(before).x;
                x = Mathf.Clamp(x, textX, textX + textW - 1);
                int caretW = Math.Max(1, Mathf.RoundToInt(FontSize * 0.08f) + 1);
                DrawFill(buffer, bufW, bufH, rx, ry, rw, rh, (int)x, 2, caretW, rh - 4, CaretColor);
            }
        }
        #endregion

        #region 文本合成辅助
        // 文本临时缓冲（复用，避免每帧 GC）。DrawText 先把文字画到这张干净的临时缓冲，
        // 再用 CompositeBGRA 以 src-over 合成回主缓冲，从而把文字叠在已画好的背景/边框之上。
        private static byte[] _textBuf;
        // 文字绘制诊断开关：首次成功绘制或出错时各记录一次，避免每帧刷屏
        private static bool _diagLogged = false;

        /// <summary>
        /// 把 src（BGRA，KText 写入的是“自上而下”）以 src-over 方式合成到
        /// dst（BGRA，KInputText 主缓冲是“自下而上”）的 (ox,oy) 处。
        /// 因两侧 Y 方向相反，合成时按 (srcH-1-y) 翻转行，否则文字会上下颠倒。
        /// </summary>
        private static void CompositeBGRA(byte[] dst, int dstW, int dstH, byte[] src, int srcW, int srcH, int ox, int oy)
        {
            for (int y = 0; y < srcH; y++)
            {
                // 临时缓冲与主缓冲同为 KText 原生“自下而上”约定（row 0 = 底部），
                // 二者朝向一致，直接按行号对应（dy = oy + y），无需翻转 Y。
                // （之前误当“自上而下”做了 180° 翻转，导致文字上下颠倒。）
                int dy = oy + y;
                if ((uint)dy >= (uint)dstH) continue;
                int sRow = y * srcW;
                int dRow = dy * dstW;
                for (int x = 0; x < srcW; x++)
                {
                    int dx = ox + x;
                    if ((uint)dx >= (uint)dstW) continue;
                    int si = (sRow + x) * 4;
                    int sa = src[si + 3];
                    if (sa == 0) continue; // 透明像素：保留下方（背景）
                    int di = (dRow + dx) * 4;
                    float srcA = sa / 255f;
                    float dstA = dst[di + 3] / 255f;
                    float outA = srcA + dstA * (1f - srcA);
                    if (outA <= 0.0001f)
                    {
                        // 主缓冲是 BGRA（byte0=B），临时缓冲是 Color32 的 RGBA（byte0=R），需交换 R/B
                        dst[di] = src[si + 2]; dst[di + 1] = src[si + 1]; dst[di + 2] = src[si]; dst[di + 3] = (byte)sa;
                    }
                    else
                    {
                        // 同上：dst.B 取 src.B(src[si+2])，dst.R 取 src.R(src[si])，修正彩色文字色相翻转
                        dst[di]     = (byte)((src[si + 2] * srcA + dst[di]     * dstA * (1f - srcA)) / outA);
                        dst[di + 1] = (byte)((src[si + 1] * srcA + dst[di + 1] * dstA * (1f - srcA)) / outA);
                        dst[di + 2] = (byte)((src[si]     * srcA + dst[di + 2] * dstA * (1f - srcA)) / outA);
                        dst[di + 3] = (byte)(outA * 255f);
                    }
                }
            }
        }
        #endregion

        #region 例子用：输出到 Texture2D（BGRA32，自下而上，直接 LoadRawTextureData）
        private Texture2D _canvas;
        private byte[] _pixelBuffer;
        private int _width;
        private int _height;

        public int Width { get { return _width; } set { Resize(value, _height); } }
        public int Height { get { return _height; } set { Resize(_width, value); } }

        private void Resize(int w, int h)
        {
            w = Math.Max(1, w); h = Math.Max(1, h);
            if (w == _width && h == _height) return;
            _width = w; _height = h;
            _canvas = new Texture2D(_width, _height, TextureFormat.BGRA32, false);
            _canvas.filterMode = FilterMode.Point;
            // 半透明背景（BackgroundColor.a=0.85）需要按透明纹理做 alpha 混合，否则会被当成不透明/预乘错误。
            _canvas.alphaIsTransparency = true;
            // 关键：纹理尺寸变化时必须同步重建像素缓冲，否则 _pixelBuffer 长度与纹理不符，
            // LoadRawTextureData 会抛异常导致整帧不绘制（表现为“点击后背景消失”）。
            if (_pixelBuffer == null || _pixelBuffer.Length != _width * _height * 4)
            {
                _pixelBuffer = new byte[_width * _height * 4];
            }
            _dirty = true;
        }

        /// <summary>把输入框渲染到一张 BGRA32 纹理并返回（自下而上缓冲，无需行翻转）。</summary>
        public Texture2D GetTexture(int width, int height)
        {
            if (_canvas == null || _width != width || _height != height) Resize(width, height);
            if (_pixelBuffer != null) Array.Clear(_pixelBuffer, 0, _pixelBuffer.Length); // 透明清屏
            Draw(_pixelBuffer, _width, _height, 0, 0, _width, _height);
            _canvas.LoadRawTextureData(_pixelBuffer);
            _canvas.Apply(false);
            return _canvas;
        }

        /// <summary>已渲染的纹理（由每帧 GetTexture 更新）。OnGUI 的 Repaint 只负责把它画到屏幕，
        /// 不再在 GUI 绘制上下文里调用 DrawText，避免原生字体调用重入打断 IMGUI 绘制。</summary>
        public Texture2D Canvas { get { return _canvas; } }
        #endregion
    }
}
