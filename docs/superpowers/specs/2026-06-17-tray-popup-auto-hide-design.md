# 托盘弹出窗口点击外部自动隐藏

**日期：** 2026-06-17
**分支：** feature/audio-device-switcher
**状态：** 待审批

## 问题

左键点击托盘图标弹出主设置面板后，点击桌面或其他程序时，面板不会自动隐藏，停留在桌面如同普通窗口。

## 当前行为（代码现状）

- `Program.cs:454` `TrayIcon_MouseClick` → 左键调用 `SettingsToggle(trayClick: true)`
- `Program.cs:416` `SettingsToggle` → `settingsForm.Show()` / `HideAll()`
- `Settings.cs:131` 已订阅 `Deactivate += SettingsForm_LostFocus`
- `Settings.cs:637` `SettingsForm_LostFocus` **目前仅记录 `lastLostFocus` 时间戳，不隐藏窗口**
- `Settings.cs:1612` `HasAnyFocus()` 已检查所有子窗口（Fans/Extra/Updates/Matrix/Handheld/MouseSettings）+ 自身的 `ContainsFocus`
- 子窗口通过 `AddOwnedForm` 关联（如 `Settings.cs:1199`）

## 目标行为

点击桌面/其他程序区域时，主面板（及任何已打开的子窗口）自动隐藏到托盘。

**保留的边界：** 从主面板点开 Fans/Extra 等子窗口时，主面板不隐藏 —— 只有当焦点转移到「窗口组之外」时才隐藏整个组。

## 设计

### 核心机制

改造 `SettingsForm_LostFocus`（Deactivate 处理器）：失去焦点后异步检查是否还有任何子窗口持有焦点，若无则隐藏整个窗口组。

### 实现

修改 `Settings.cs:637-640`：

```csharp
private void SettingsForm_LostFocus(object? sender, EventArgs e)
{
    lastLostFocus = DateTimeOffset.Now.ToUnixTimeMilliseconds();

    // 焦点转移到窗口组外时自动隐藏。
    // 用 BeginInvoke 延迟到消息泵下一轮：Deactivate 在旧窗口触发时，
    // 新窗口（子窗口/菜单）尚未获得焦点，直接检查会得到错误结果。
    BeginInvoke(new Action(() =>
    {
        if (!IsDisposed && Visible && !HasAnyFocus())
            HideAll();
    }));
}
```

### 关键技术点

1. **`BeginInvoke` 延迟的必要性：** WinForms 焦点切换顺序是「旧窗口 Deactivate → 新窗口 Activated」。直接在 Deactivate 内调 `HasAnyFocus()` 会因新窗口尚未拿到焦点而误判为「无焦点」从而隐藏。`BeginInvoke` 把检查推到当前消息处理结束后，此时焦点状态已稳定。

2. **`HasAnyFocus()` 复用：** 现有方法已检查所有 6 个子窗口 + 自身的 `ContainsFocus`，正好表达「窗口组是否仍持有焦点」。从主面板点开 Fans → Fans `ContainsFocus` 为 true → 不隐藏。点击桌面 → 全部 `ContainsFocus` 为 false → 隐藏。

3. **`HideAll()` 已处理子窗口：** `Settings.cs:1586` 的 `HideAll()` 会关闭所有打开的子窗口并隐藏主面板。

### 不采用其他方案的理由

- **Win32 `WM_ACTIVATE` 消息处理：** 更精准但侵入 `WndProc`（已混有电源/任务栏消息），改动大。
- **全局鼠标钩子：** 过度工程，`Deactivate` 已足够覆盖「点击其他区域」语义。

## 边界与已验证项

| 场景 | 行为 | 验证 |
|------|------|------|
| 点击桌面 | 隐藏 | `Deactivate` + `HasAnyFocus()` false |
| 点击其他程序 | 隐藏 | 同上 |
| 从主面板点开 Fans | 不隐藏 | Fans `ContainsFocus` true |
| Fans 打开后点击桌面 | 全部隐藏 | `HideAll()` 关闭子窗口 |
| 启动时 `SettingsToggle(false)` | 正常显示 | `ShowAll()` 会 Activate，Deactivate 不触发 |
| `topmost` 置顶模式 | 也会隐藏 | 符合用户选择「全部触发」 |
| 右键上下文菜单 | 实测验证 | 菜单打开时若误触发，加 `Form.ActiveForm` 判断 |

## 测试计划

1. 左键托盘弹出 → 点击桌面 → 应隐藏
2. 左键托盘弹出 → 点击任务栏其他程序 → 应隐藏
3. 左键托盘弹出 → 点 Fans 按钮 → 主面板不隐藏，Fans 出现
4. Fans 打开后 → 点击桌面 → 主面板 + Fans 都隐藏
5. 开启 topmost → 点击桌面 → 应隐藏（验证用户选择）
6. 右键面板上下文菜单 → 菜单不应导致面板立即消失

## 涉及文件

- `app/Settings.cs`：修改 `SettingsForm_LostFocus`（约 6-10 行改动）

## 风险

低。改动局限于一个事件处理器，复用现有 `HasAnyFocus()`/`HideAll()`。若右键菜单导致误触发，回退方案是加额外 `Form.ActiveForm`/`contextMenuStrip.Visible` 判断。
