# 托盘弹出窗口点击外部自动隐藏 — 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 左键点击托盘图标弹出主设置面板后，点击桌面/其他程序时面板自动隐藏到托盘；从主面板点开 Fans/Extra 等子窗口时不误隐藏。

**Architecture:** 改造现有 `Deactivate` 处理器 `SettingsForm_LostFocus`，用 `BeginInvoke` 异步延迟检查 `HasAnyFocus()`，焦点已离开整个窗口组时调 `HideAll()`。复用现有方法，不引入新依赖。

**Tech Stack:** C# / .NET 8 / WinForms / NotifyIcon

**关于测试：** 本项目无单元测试框架（仅 `GHelper.csproj` 一个 WinExe 项目，无 xunit/NUnit/MSTest 引用）。焦点/Deactivate 行为依赖真实的 Windows 消息泵，无法可靠单测。验证策略 = **编译通过 + 手动场景测试清单**（设计文档已列出）。

---

## 文件结构

- **修改：** `app/Settings.cs:637-640` — `SettingsForm_LostFocus` 方法（唯一的代码改动）
- **不新增文件**
- **不修改**：`Program.cs`（`SettingsToggle`/`TrayIcon_MouseClick` 无需改动，`HideAll()`/`HasAnyFocus()` 已存在且签名匹配）

## 复用的现有方法（只读参考，不要改）

- `Settings.cs:1586` `public void HideAll()` — 隐藏主面板并关闭所有已打开子窗口
- `Settings.cs:1612` `public bool HasAnyFocus(bool lostFocusCheck = false)` — 检查 6 个子窗口 + 自身的 `ContainsFocus`
- `Settings.cs:46` `static long lastLostFocus` — 已声明，`SettingsForm_LostFocus` 继续维护它（`HasAnyFocus(lostFocusCheck:true)` 仍依赖它）

---

### Task 1: 改造 `SettingsForm_LostFocus` 实现点击外部自动隐藏

**Files:**
- Modify: `app/Settings.cs:637-640`

- [ ] **Step 1: 替换 `SettingsForm_LostFocus` 方法体**

把 `app/Settings.cs:637-640` 当前内容：

```csharp
        private void SettingsForm_LostFocus(object? sender, EventArgs e)
        {
            lastLostFocus = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        }
```

替换为：

```csharp
        private void SettingsForm_LostFocus(object? sender, EventArgs e)
        {
            lastLostFocus = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            // 焦点离开整个窗口组（主面板 + 所有子窗口）时自动隐藏到托盘。
            // 用 BeginInvoke 延迟到消息泵下一轮：Deactivate 触发时，
            // 即将获得焦点的新窗口（如刚点开的 Fans 子窗口）尚未拿到焦点，
            // 直接检查 HasAnyFocus 会误判为「无焦点」而错误隐藏。
            BeginInvoke(new Action(() =>
            {
                if (!IsDisposed && Visible && !HasAnyFocus())
                    HideAll();
            }));
        }
```

关键点说明（实现时遵循）：
- `lastLostFocus` 赋值**保留** —— `HasAnyFocus(lostFocusCheck:true)` 的调用方仍依赖它（`Program.cs:422`）
- `BeginInvoke` 无延迟参数 = 排到当前消息队列尾，等当前焦点切换消息处理完
- `IsDisposed` 守卫：窗口可能正在关闭过程中
- `Visible` 守卫：避免对已隐藏窗口重复调 `HideAll`
- `HasAnyFocus()` 默认参数 `lostFocusCheck=false`，只查实时 `ContainsFocus` —— 正是我们想要的

- [ ] **Step 2: 编译验证**

Run:
```bash
cd "C:\Users\34235\Documents\Codespace\g-helper\app" && dotnet build GHelper.csproj -c Debug
```
Expected: `Build succeeded`，无错误，无新警告。

若报错 `BeginInvoke` 找不到 —— 不可能发生（`Form` 继承自 `Control`，`BeginInvoke(Delegate)` 是 `Control` 公开方法），但如果 IDE 提示歧义，用 `this.BeginInvoke(...)` 显式写法。

- [ ] **Step 3: 提交**

```bash
git add app/Settings.cs
git commit -m "Auto-hide settings panel when focus leaves the window group

SettingsForm_LostFocus (Deactivate handler) previously only recorded the
lastLostFocus timestamp. Now it also hides the window group via HideAll()
when no child window (Fans/Extra/Updates/Matrix/Handheld/MouseSettings)
retains focus, using BeginInvoke to defer the HasAnyFocus check past the
focus-transition message so opening a child window is not mistaken for
losing focus."
```

---

### Task 2: 手动场景验证（无代码改动，按清单逐项确认）

**Files:** 无（运行应用验证）

构建并运行应用需要 Windows 桌面环境。若当前环境无法运行 GUI，跳过本 Task 并在交付说明中标注「需用户在 Windows 上验证」。

- [ ] **Step 1: 发布 Debug 单文件版用于测试**

```bash
cd "C:\Users\34235\Documents\Codespace\g-helper\app" && dotnet publish GHelper.csproj -c Debug -r win-x64 --self-contained false -p:PublishSingleFile=true
```
Expected: 生成 `bin\Debug\net8.0-windows\win-x64\publish\GHelper.exe`

- [ ] **Step 2: 运行并逐项验证以下场景**

启动 `GHelper.exe`，左键托盘图标弹出主面板，验证：

| # | 场景 | 期望结果 | 通过？ |
|---|------|----------|--------|
| 1 | 主面板可见 → 点击桌面空白处 | 主面板隐藏 | ☐ |
| 2 | 主面板可见 → 点击任务栏其他程序 | 主面板隐藏 | ☐ |
| 3 | 主面板可见 → 点击 Fans 按钮 | 主面板**不**隐藏，Fans 窗口出现 | ☐ |
| 4 | 场景 3 后，Fans + 主面板可见 → 点击桌面 | 两者**都**隐藏 | ☐ |
| 5 | 主面板可见 → 点击 Extra 按钮 | 主面板不隐藏，Extra 出现 | ☐ |
| 6 | 主面板可见 → 右键面板内调出上下文菜单 | 菜单正常显示，面板**不**立即消失 | ☐ |
| 7 | 开启 topmost（Extra 设置里）→ 主面板可见 → 点击桌面 | 主面板隐藏（符合用户选择「全部触发」） | ☐ |
| 8 | 左键托盘图标快速弹出→隐藏→再弹出 | 正常切换，不卡死、不残留 | ☐ |

- [ ] **Step 3: 记录验证结果**

若全部通过 → 进入 Task 3。
若场景 6 失败（右键菜单导致面板立即消失）→ 这是已预见的风险，进入 Task 3 的回退分支（Step 2）。
若其他场景失败 → 回到 Task 1 检查实现，用 `systematic-debugging` 排查。

---

### Task 3: 处理右键上下文菜单误触发（条件性 —— 仅当 Task 2 场景 6 失败时执行）

**Files:**
- Modify: `app/Settings.cs:637-...`（Task 1 改动处）

**前提：** 只有 Task 2 场景 6「右键菜单弹出导致面板立即消失」失败时才执行本 Task。若场景 6 通过，跳过本 Task，直接 Task 4。

- [ ] **Step 1: 确认失败现象**

右键主面板 → `ContextMenuStrip` 弹出 → 主面板在菜单显示瞬间消失（菜单变成无父窗口的孤儿菜单或一并消失）。

原因：`ContextMenuStrip` 不是 `Control` 子类，它弹出时主面板 `ContainsFocus` 可能为 false，且它不在 `HasAnyFocus()` 检查的子窗口列表里。

- [ ] **Step 2: 加上下文菜单可见性判断**

定位上下文菜单字段名。当前类中（`Settings.cs:23`）：

```csharp
ContextMenuStrip contextMenuStrip = new CustomContextMenu();
```

把 Task 1 写入的 `SettingsForm_LostFocus` 改为：

```csharp
        private void SettingsForm_LostFocus(object? sender, EventArgs e)
        {
            lastLostFocus = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            // 焦点离开整个窗口组（主面板 + 所有子窗口）时自动隐藏到托盘。
            // 用 BeginInvoke 延迟到消息泵下一轮：Deactivate 触发时，
            // 即将获得焦点的新窗口（如刚点开的 Fans 子窗口）尚未拿到焦点，
            // 直接检查 HasAnyFocus 会误判为「无焦点」而错误隐藏。
            BeginInvoke(new Action(() =>
            {
                if (IsDisposed || !Visible)
                    return;
                // 右键上下文菜单弹出时主面板会 Deactivate，但菜单仍属于本窗口交互
                if (contextMenuStrip.Visible)
                    return;
                if (!HasAnyFocus())
                    HideAll();
            }));
        }
```

新增的 `contextMenuStrip.Visible` 检查把右键菜单打开期间排除在「失焦隐藏」之外。

- [ ] **Step 3: 编译验证**

```bash
cd "C:\Users\34235\Documents\Codespace\g-helper\app" && dotnet build GHelper.csproj -c Debug
```
Expected: `Build succeeded`

- [ ] **Step 4: 重新跑 Task 2 场景 6 + 场景 1-5**

确认场景 6 修复且未破坏其他场景。

- [ ] **Step 5: 提交**

```bash
git add app/Settings.cs
git commit -m "Exclude open context menu from auto-hide trigger

The panel's ContextMenuStrip is not a Control and is not covered by
HasAnyFocus, so opening it would otherwise Deactivate the panel and
trigger HideAll. Skip hiding while contextMenuStrip.Visible."
```

---

### Task 4: 收尾

**Files:** 无代码改动

- [ ] **Step 1: 最终编译确认**

```bash
cd "C:\Users\34235\Documents\Codespace\g-helper\app" && dotnet build GHelper.csproj -c Release
```
Expected: `Build succeeded`

- [ ] **Step 2: 确认工作树状态干净**

```bash
git status
```
Expected: 工作树干净（所有改动已提交），分支 `feature/audio-device-switcher`。

- [ ] **Step 3: 交付说明**

向用户报告：
- 改了哪个方法、几行
- Task 2 验证清单结果（或标注「需用户在 Windows 验证」）
- 是否触发了 Task 3 的回退分支

---

## Self-Review

**1. Spec 覆盖：** 设计文档要求「点击外部隐藏 + 保留子窗口 + 复用 HasAnyFocus/HideAll + BeginInvoke 延迟 + 处理右键菜单风险」。
- 点击外部隐藏 → Task 1 ✅
- 保留子窗口（点 Fans 不隐藏）→ Task 1 用 `HasAnyFocus()` ✅
- BeginInvoke 延迟 → Task 1 ✅
- 右键菜单风险 → Task 3 条件性处理 ✅
- 测试计划 6 个场景 → Task 2 ✅

**2. Placeholder 扫描：** 无 TBD/TODO。Task 3 已给出完整代码（不依赖 Task 2 的实际结果填充）。场景 6 的回退是明确分支，不是占位。

**3. 类型/命名一致性：**
- `HasAnyFocus()` 默认参数 `lostFocusCheck=false` —— 与 `Settings.cs:1612` 签名一致 ✅
- `HideAll()` 无参 —— 与 `Settings.cs:1586` 一致 ✅
- `contextMenuStrip` 字段名 —— 与 `Settings.cs:23` 一致 ✅
- `lastLostFocus` —— 与 `Settings.cs:46` 声明一致 ✅
- `BeginInvoke(new Action(() => {...}))` —— `Control.BeginInvoke(Delegate)` 标准用法 ✅

**4. 风险确认：** 计划明确标注无测试框架、验证靠手动清单，且 Task 3 为已预见的右键菜单问题准备了具体回退代码。
