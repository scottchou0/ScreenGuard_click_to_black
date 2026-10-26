# 侧键熄屏

按一下鼠标侧键（或你自定义的按键），屏幕**完全黑屏**；再按一次，恢复正常。用于一键息屏/锁屏、离开时快速黑屏。

## 功能

- **自定义黑屏触发键**：鼠标侧键（侧键1/侧键2），或任意键盘键 / 组合键（如 `Ctrl+Alt+K`）。
- **完全黑屏**：全屏纯黑窗口 + 隐藏光标，黑屏期间移动鼠标也不会点亮，只能靠触发键恢复。
- **黑屏时屏蔽 Windows 键**：防止黑屏状态下误按 Win 键唤醒/切走；恢复正常后解除屏蔽。
- **常驻托盘**：右下角图标运行，右键菜单「设置 / 退出」，占用极小。

## 使用

1. 双击 `侧键熄屏.exe` 启动，程序驻留系统托盘。
2. 默认触发键是**鼠标侧键1**：按一下 → 屏幕黑屏；再按一下 → 恢复。
3. 想改触发键：右键托盘图标 → **设置**：
   - 选「鼠标侧键」，再选 侧键1 / 侧键2；
   - 或选「键盘按键」，点触发键输入框 → 直接按想用的键（支持组合键如 `Ctrl+Alt+K`）；
   - 勾选「黑屏时屏蔽 Windows 键」控制是否屏蔽；
   - 点「保存」即时生效。
4. 退出：右键托盘图标 → **退出**。

## 配置

程序同目录自动生成/读取 `Config.ini`，可直接编辑（保存后即时生效）：

```ini
; 侧键熄屏配置（保存后即时生效）
; trigger_type: mouse=鼠标侧键  key=键盘按键
trigger_type=mouse
mouse_button=1
key_code=0
key_mod=0
; block_win: 1=黑屏时屏蔽Win键  0=不屏蔽
block_win=1
```

| 字段 | 说明 |
|---|---|
| `trigger_type` | `mouse` 用鼠标侧键；`key` 用键盘按键 |
| `mouse_button` | `1`=侧键1  `2`=侧键2 |
| `key_code` | 键盘触发主键的虚拟键码（十进制） |
| `key_mod` | 键盘触发修饰键（`Ctrl=131072` `Alt=65536` `Shift=65536` 等，按位组合） |
| `block_win` | `1` 黑屏时屏蔽 Win 键，`0` 不屏蔽 |

> 推荐用界面设置，避免手改虚拟键码出错。

## 编译

本机无需安装 SDK，用 .NET Framework 自带的编译器即可：

```bat
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo ^
  /target:winexe /codepage:65001 /optimize ^
  /reference:System.Windows.Forms.dll /reference:System.Drawing.dll ^
  /out:侧键熄屏.exe 侧键熄屏.cs
```

## 目录结构

```
侧键熄屏/
├── 侧键熄屏.exe      # 可直接运行的程序
├── 侧键熄屏.cs        # 源码（C# / WinForms）
└── README.md          # 本说明
```

## 技术实现（简介）

- 全局低层鼠标钩子 `WH_MOUSE_LL`：监听鼠标侧键（`WM_XBUTTONDOWN`，取 mouseData 高 16 位得侧键编号）。
- 全局低层键盘钩子 `WH_KEYBOARD_LL`：检测自定义键盘触发键、拦截 Win 键（黑屏时）。
- 黑屏 = 全屏无边框置顶纯黑窗口 + `Cursor.Hide()`；屏蔽 `Alt+F4`，防止被意外关闭。
- 配置存于同目录 `Config.ini`（`System.IO` 读写，无需第三方依赖）。
