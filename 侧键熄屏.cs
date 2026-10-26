// ============================================================
// 侧键熄屏 v2 —— 自定义黑屏按键 + 屏蔽 Windows 键
//
// 功能：
//   · 触发黑屏的按键可自定义：鼠标侧键，或任意键盘键 / 组合键（如 Ctrl+Alt+K）
//   · 按下触发键 → 全屏纯黑（隐藏光标，移动鼠标不恢复）；再按一次 → 恢复
//   · 运行时屏蔽 Windows 键（防止黑屏/使用中误弹开始菜单，可在设置里关闭）
//   · 常驻系统托盘，右键菜单：设置 / 退出
//
// 配置：程序同目录 Config.ini（自动生成），改配置即时生效
//
// 编译（Windows 自带编译器）：
//   "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo \
//     /target:winexe /codepage:65001 /optimize \
//     /reference:System.Windows.Forms.dll /reference:System.Drawing.dll \
//     /out:侧键熄屏.exe 侧键熄屏.cs
// ============================================================

using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace SideKeyBlack
{
    // ---------------- 配置 ----------------
    public class Config
    {
        public string TriggerType = "mouse";   // mouse | key
        public int    MouseButton  = 1;        // 1=侧键1 2=侧键2
        public int    KeyCode      = 0;        // 键盘触发主键 vkCode
        public Keys   KeyMod       = 0;        // 键盘触发修饰键
        public bool   BlockWin     = true;     // 屏蔽 Windows 键

        private static string FilePath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config.ini");
        }

        public static Config Load()
        {
            var c = new Config();
            string p = FilePath();
            if (!File.Exists(p)) return c;
            try
            {
                foreach (string line in File.ReadAllLines(p))
                {
                    string s = line.Trim();
                    if (s.Length == 0 || s.StartsWith(";") || s.StartsWith("#")) continue;
                    int eq = s.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = s.Substring(0, eq).Trim().ToLower();
                    string v = s.Substring(eq + 1).Trim();
                    switch (k)
                    {
                        case "trigger_type": c.TriggerType = v; break;
                        case "mouse_button": int.TryParse(v, out c.MouseButton); break;
                        case "key_code":     int.TryParse(v, out c.KeyCode); break;
                        case "key_mod":      int m; if (int.TryParse(v, out m)) c.KeyMod = (Keys)m; break;
                        case "block_win":    c.BlockWin = v != "0"; break;
                    }
                }
            }
            catch { }
            return c;
        }

        public void Save()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("; 侧键熄屏配置（保存后即时生效）");
                sb.AppendLine("; trigger_type: mouse=鼠标侧键  key=键盘按键");
                sb.AppendLine("trigger_type=" + TriggerType);
                sb.AppendLine("mouse_button=" + MouseButton);
                sb.AppendLine("key_code=" + KeyCode);
                sb.AppendLine("key_mod=" + (int)KeyMod);
                sb.AppendLine("; block_win: 1=黑屏时屏蔽Win键  0=不屏蔽");
                sb.AppendLine("block_win=" + (BlockWin ? 1 : 0));
                File.WriteAllText(FilePath(), sb.ToString(), new UTF8Encoding(false));
            }
            catch { }
        }
    }

    // ---------------- 全局低层鼠标钩子 ----------------
    public class GlobalMouseHook
    {
        private const int WH_MOUSE_LL = 14;
        private const int WM_XBUTTONDOWN = 0x020B;
        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        public event Action<int> XButtonDown;
        private HookProc _proc;
        private GCHandle _handle;
        private IntPtr _hook = IntPtr.Zero;
        private long _last = 0;

        public void Install()
        {
            if (_hook != IntPtr.Zero) return;
            _proc = Callback;
            _handle = GCHandle.Alloc(_proc);
            _hook = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(null), 0);
        }
        public void Uninstall()
        {
            if (_hook != IntPtr.Zero) { UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; }
            if (_handle.IsAllocated) _handle.Free();
        }
        private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam.ToInt32() == WM_XBUTTONDOWN)
            {
                int mouseData = Marshal.ReadInt32(lParam, 8);
                int button = (mouseData >> 16) & 0xFFFF;
                if (button == 1 || button == 2)
                {
                    long now = DateTime.UtcNow.Ticks / 10000;
                    if (now - _last > 200) { _last = now; if (XButtonDown != null) XButtonDown(button); }
                }
            }
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }
    }

    // ---------------- 全局低层键盘钩子 ----------------
    public class GlobalKeyboardHook
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int VK_LWIN = 0x5B, VK_RWIN = 0x5C;
        private const int VK_CTRL = 0x11, VK_ALT = 0x12, VK_SHIFT = 0x10;

        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        public event Action<int> KeyPressed;       // 触发键按下（vkCode）
        public bool BlockWin = true;

        private HookProc _proc;
        private GCHandle _handle;
        private IntPtr _hook = IntPtr.Zero;
        private long _last = 0;

        public void Install()
        {
            if (_hook != IntPtr.Zero) return;
            _proc = Callback;
            _handle = GCHandle.Alloc(_proc);
            _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
        }
        public void Uninstall()
        {
            if (_hook != IntPtr.Zero) { UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; }
            if (_handle.IsAllocated) _handle.Free();
        }

        public static bool IsKeyDown(int vk)
        {
            return (GetAsyncKeyState(vk) & 0x8000) != 0;
        }

        private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                int vk = Marshal.ReadInt32(lParam, 0);   // KBDLLHOOKSTRUCT.vkCode

                // 屏蔽 Windows 键（左/右 Win）
                if (BlockWin && (vk == VK_LWIN || vk == VK_RWIN))
                {
                    return new IntPtr(1);   // 吞掉，不传给系统
                }

                if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                {
                    if (vk != VK_CTRL && vk != VK_ALT && vk != VK_SHIFT)
                    {
                        long now = DateTime.UtcNow.Ticks / 10000;
                        if (now - _last > 200) { _last = now; if (KeyPressed != null) KeyPressed(vk); }
                    }
                }
            }
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }
    }

    // ---------------- 全屏纯黑窗口 ----------------
    public class BlackScreen : Form
    {
        public bool AllowClose = false;
        public BlackScreen()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Bounds = Screen.PrimaryScreen.Bounds;
            BackColor = Color.Black;
            KeyPreview = true;
        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Alt | Keys.F4)) return true;
            return base.ProcessCmdKey(ref msg, keyData);
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!AllowClose) { e.Cancel = true; return; }
            base.OnFormClosing(e);
        }
    }

    // ---------------- 设置窗口 ----------------
    public class SettingsForm : Form
    {
        private readonly Config _cfg;
        private RadioButton _rbMouse, _rbKey;
        private ComboBox _cmbButton;
        private TextBox _txtKey;
        private CheckBox _chkWin;

        public SettingsForm(Config cfg)
        {
            _cfg = cfg;
            Text = "侧键熄屏 · 设置";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(420, 330);
            Font = new Font("Microsoft YaHei", 10F);

            var gb = new GroupBox { Text = "黑屏触发按键", Location = new Point(14, 14), Size = new Size(392, 220) };

            _rbMouse = new RadioButton { Text = "鼠标侧键", Location = new Point(18, 28), AutoSize = true };
            _rbKey = new RadioButton { Text = "键盘按键（自定义）", Location = new Point(18, 68), AutoSize = true };

            var lblBtn = new Label { Text = "侧键:", Location = new Point(40, 54), AutoSize = true };
            _cmbButton = new ComboBox { Location = new Point(90, 50), Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbButton.Items.Add("侧键1（前）");
            _cmbButton.Items.Add("侧键2（后）");

            var lblKey = new Label { Text = "触发键:", Location = new Point(40, 102), AutoSize = true };
            _txtKey = new TextBox { Location = new Point(90, 98), Width = 250, ReadOnly = true,
                                    Text = "点击此处后按下你的按键…" };
            var hint = new Label { Text = "可单键（如 F8）或组合键（如 Ctrl+Alt+K）", Location = new Point(40, 130), AutoSize = true, ForeColor = Color.DimGray };

            _chkWin = new CheckBox { Text = "黑屏时屏蔽 Windows 键（防止黑屏时误触）", Location = new Point(18, 172), AutoSize = true };

            gb.Controls.AddRange(new Control[] { _rbMouse, _rbKey, lblBtn, _cmbButton, lblKey, _txtKey, hint, _chkWin });
            Controls.Add(gb);

            var btnOk = new Button { Text = "保存", Location = new Point(150, 248), Width = 100 };
            var btnCancel = new Button { Text = "取消", Location = new Point(262, 248), Width = 90 };
            btnOk.Click += (s, e) => SaveAndClose();
            btnCancel.Click += (s, e) => Close();
            Controls.AddRange(new Control[] { btnOk, btnCancel });

            // 按键捕获
            _txtKey.GotFocus += (s, e) => _txtKey.Text = "按下你的触发键…";
            _txtKey.KeyDown += OnKeyDown;

            // 回显当前配置
            _rbMouse.Checked = _cfg.TriggerType != "key";
            _rbKey.Checked = _cfg.TriggerType == "key";
            _cmbButton.SelectedIndex = (_cfg.MouseButton == 2) ? 1 : 0;
            _chkWin.Checked = _cfg.BlockWin;
            if (_cfg.KeyCode != 0)
                _txtKey.Text = KeyName(_cfg.KeyMod, (Keys)_cfg.KeyCode);
            SyncControls();
            _rbMouse.CheckedChanged += (s, e) => SyncControls();
            _rbKey.CheckedChanged += (s, e) => SyncControls();
        }

        private void SyncControls()
        {
            _cmbButton.Enabled = _rbMouse.Checked;
            _txtKey.Enabled = _rbKey.Checked;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            Keys code = e.KeyCode;
            if (code == Keys.ControlKey || code == Keys.Alt || code == Keys.ShiftKey ||
                code == Keys.Menu || code == Keys.LWin || code == Keys.RWin)
                return;
            _cfg.TriggerType = "key";
            _cfg.KeyCode = (int)code;
            _cfg.KeyMod = e.Modifiers;
            _txtKey.Text = KeyName(e.Modifiers, code);
            e.SuppressKeyPress = true;
            _rbKey.Checked = true;
        }

        private void SaveAndClose()
        {
            if (_rbMouse.Checked) { _cfg.TriggerType = "mouse"; _cfg.MouseButton = _cmbButton.SelectedIndex == 1 ? 2 : 1; }
            else if (_cfg.KeyCode != 0) { _cfg.TriggerType = "key"; }
            _cfg.BlockWin = _chkWin.Checked;
            _cfg.Save();
            Close();
        }

        private static string KeyName(Keys mod, Keys code)
        {
            var sb = new StringBuilder();
            if ((mod & Keys.Control) != 0) sb.Append("Ctrl+");
            if ((mod & Keys.Alt) != 0) sb.Append("Alt+");
            if ((mod & Keys.Shift) != 0) sb.Append("Shift+");
            sb.Append(code == 0 ? "(无)" : code.ToString());
            return sb.ToString();
        }
    }

    // ---------------- 主程序（托盘） ----------------
    public class AppForm : Form
    {
        private readonly Config _cfg = Config.Load();
        private readonly GlobalMouseHook _mouse = new GlobalMouseHook();
        private readonly GlobalKeyboardHook _key = new GlobalKeyboardHook();
        private BlackScreen _bs;
        private NotifyIcon _notify;

        public AppForm()
        {
            Text = "侧键熄屏";
            ShowInTaskbar = false;
            WindowState = FormWindowState.Minimized;

            _key.BlockWin = false;   // 默认不屏蔽，仅黑屏时启用

            _notify = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Visible = true,
                Text = "侧键熄屏：按触发键黑屏，再按恢复"
            };
            var menu = new ContextMenuStrip();
            menu.Items.Add("设置…", null, (s, e) => OpenSettings());
            menu.Items.Add("说明：按触发键黑屏 / 再按恢复", null, null);
            menu.Items.Add("-", null, null);
            menu.Items.Add("退出", null, (s, e) => Application.Exit());
            _notify.ContextMenuStrip = menu;

            _mouse.XButtonDown += (b) => { try { BeginInvoke(new Action(() => OnMouseSide(b))); } catch { } };
            _key.KeyPressed += (vk) => { try { BeginInvoke(new Action(() => OnKeyPress(vk))); } catch { } };
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            _mouse.Install();
            _key.Install();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _mouse.Uninstall();
            _key.Uninstall();
            if (_notify != null) { _notify.Visible = false; _notify.Dispose(); }
            base.OnFormClosed(e);
        }

        private void OnMouseSide(int button)
        {
            if (_cfg.TriggerType == "mouse" && button == _cfg.MouseButton) Toggle();
        }

        private void OnKeyPress(int vk)
        {
            if (_cfg.TriggerType != "key") return;
            if (vk != _cfg.KeyCode) return;
            if (!ModMatch()) return;
            Toggle();
        }

        private bool ModMatch()
        {
            bool ctrl = GlobalKeyboardHook.IsKeyDown(0x11);
            bool alt = GlobalKeyboardHook.IsKeyDown(0x12);
            bool shift = GlobalKeyboardHook.IsKeyDown(0x10);
            return ctrl == ((_cfg.KeyMod & Keys.Control) != 0) &&
                   alt == ((_cfg.KeyMod & Keys.Alt) != 0) &&
                   shift == ((_cfg.KeyMod & Keys.Shift) != 0);
        }

        private void OpenSettings()
        {
            var dlg = new SettingsForm(_cfg);
            dlg.ShowDialog();
        }

        private void Toggle()
        {
            if (_bs == null)
            {
                _key.BlockWin = _cfg.BlockWin;   // 黑屏时屏蔽 Win 键
                _bs = new BlackScreen();
                _bs.Show();
                Cursor.Hide();
            }
            else
            {
                _key.BlockWin = false;           // 恢复后解除屏蔽
                _bs.AllowClose = true;
                _bs.Close();
                _bs.Dispose();
                _bs = null;
                Cursor.Show();
            }
        }
    }

    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new AppForm());
        }
    }
}
