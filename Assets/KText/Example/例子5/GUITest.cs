// ============================================================================
// GUITest.cs — 例子5：纯 Unity GUI (IMGUI) API 测试
// ============================================================================
//
// 本例与 KText 完全无关，仅用于测试 Unity IMGUI 的各种控件与文本相关 API：
//   - 单行 TextField
//   - 多行 TextArea
//   - 密码 PasswordField（可切换显示/隐藏）
//   - GUI.TextField / GUI.TextArea / GUI.PasswordField（绝对坐标版本）
//   - GUI.Label / GUI.Box / GUI.Button / GUI.Toggle / GUI.Toolbar 等文本显示
//   - GUIContent 与 tooltip
//
// ============================================================================

using UnityEngine;

namespace KText.Example
{
    [ExecuteInEditMode]
    public class GUITest : MonoBehaviour
    {
        private string _single = "单行文本框 TextField 测试";
        private string _multi = "多行文本框 TextArea 测试\n可以输入任意中英文混合内容\nGUI API Test 例子5";
        private string _password = "123456";
        private bool _showPassword;

        private string _guiSingle = "GUI.TextField 绝对坐标版本";
        private string _guiMulti = "GUI.TextArea 绝对坐标版本\n第二行内容";

        private int _toolbar = 0;
        private bool _toggle;
        private string _log = "点击按钮查看反馈";

        private void OnGUI()
        {
            // ---- 左列：GUILayout 流布局控件 ----
            GUILayout.BeginArea(new Rect(10, 10, 560, Screen.height - 20));
            GUILayout.Label("GUILayout 文本控件测试", MakeTitleStyle());
            GUILayout.Space(8);

            GUILayout.Label("单行 TextField：");
            _single = GUILayout.TextField(_single, GUILayout.Width(540), GUILayout.Height(28));

            GUILayout.Space(6);
            GUILayout.Label("多行 TextArea：");
            _multi = GUILayout.TextArea(_multi, GUILayout.Width(540), GUILayout.Height(90));

            GUILayout.Space(6);
            GUILayout.Label("密码 PasswordField：");
            if (_showPassword)
                _password = GUILayout.TextField(_password, GUILayout.Width(540), GUILayout.Height(28));
            else
                _password = GUILayout.PasswordField(_password, '*', GUILayout.Width(540), GUILayout.Height(28));
            _showPassword = GUILayout.Toggle(_showPassword, "显示密码");

            GUILayout.Space(8);
            GUILayout.Label($"字数：单行 {_single.Length} / 多行 {_multi.Length} / 密码 {_password.Length}");

            GUILayout.Space(10);
            GUILayout.Label("Toolbar / Toggle：");
            _toolbar = GUILayout.Toolbar(_toolbar, new[] { "Tab A", "Tab B", "Tab C" });
            _toggle = GUILayout.Toggle(_toggle, "启用选项");

            if (GUILayout.Button("点击我"))
                _log = $"按钮点击 @ {Time.realtimeSinceStartup:F2}s";
            GUILayout.Label(_log);

            // GUIContent 带 tooltip 的 Label
            GUILayout.Label(new GUIContent("带 Tooltip 的 Label（悬停查看）", "这是 tooltip 文本"));

            GUILayout.EndArea();

            // ---- 右列：GUI 绝对坐标控件 ----
            float rx = 590f;
            GUI.Label(new Rect(rx, 10, 560, 30), "GUI 绝对坐标控件测试", MakeTitleStyle());

            GUI.Label(new Rect(rx, 50, 560, 22), "GUI.TextField：");
            _guiSingle = GUI.TextField(new Rect(rx, 74, 540, 28), _guiSingle);

            GUI.Label(new Rect(rx, 116, 560, 22), "GUI.TextArea：");
            _guiMulti = GUI.TextArea(new Rect(rx, 140, 540, 70), _guiMulti);

            GUI.Label(new Rect(rx, 224, 560, 22), "GUI.PasswordField：");
            string pw = _showPassword
                ? GUI.TextField(new Rect(rx, 248, 540, 28), _password)
                : GUI.PasswordField(new Rect(rx, 248, 540, 28), _password, '*');
            if (!_showPassword) _password = pw;

            GUI.Box(new Rect(rx, 290, 540, 120),
                $"字数统计\n单行(Layout): {_single.Length}\n多行(Layout): {_multi.Length}\nGUI 单行: {_guiSingle.Length}\nGUI 多行: {_guiMulti.Length}");

            GUI.Label(new Rect(rx, 420, 560, 22),
                new GUIContent("GUI 带 tooltip 文本", "GUI.tooltip 演示"));
            GUI.Label(new Rect(rx, 446, 560, 20), "当前 Tooltip: " + GUI.tooltip);
        }

        private GUIStyle MakeTitleStyle()
        {
            var s = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
            };
            s.normal.textColor = new Color(0.6f, 0.85f, 1f);
            return s;
        }
    }
}
