// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
//
// Copyright (c) 2026 ENDERSRUOLI
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace WindowsTypeAnywhere;

/// <summary>托盘常驻与状态编排。程序无主窗口，退出时显式 ExitThread。</summary>
internal sealed class TrayAppContext : ApplicationContext
{
    private readonly AppConfig _config;
    private readonly ForegroundTracker _tracker;
    private readonly HotkeyManager _hotkeys;
    private readonly KeyboardMonitor _monitor;
    private readonly RelayForm _relay;
    private readonly TextTransfer _transfer;
    private readonly NotifyIcon _tray;

    private readonly List<KeyValuePair<HotkeySpec, Action>> _modifierless =
        new List<KeyValuePair<HotkeySpec, Action>>();
    private readonly List<string> _registrationErrors = new List<string>();

    private bool _enabled;
    private bool _monitoring;
    private bool _transferring;

    private ToolStripMenuItem _enabledItem;
    private ToolStripMenuItem _modeItem;

    public TrayAppContext()
    {
        _config = AppConfig.Load(Path.Combine(AppContext.BaseDirectory, "config.json"));
        _monitoring = _config.IsMonitoring;
        _enabled = _config.EnabledOnStart;

        HotkeySpec commitSpec = Parse("CommitHotkey", _config.CommitHotkey);
        HotkeySpec summonSpec = Parse("SummonHotkey", _config.SummonHotkey);
        HotkeySpec toggleSpec = Parse("ToggleHotkey", _config.ToggleHotkey);
        HotkeySpec pasteSpec = Parse("PasteHotkey", _config.PasteHotkey);

        _tracker = new ForegroundTracker();
        _hotkeys = new HotkeyManager();
        _tracker.RegisterOwnWindow(_hotkeys.WindowHandle);

        _relay = new RelayForm(_config, _config.CommitHotkey);
        _tracker.RegisterOwnWindow(_relay.Handle);   // 强制创建 HWND 并登记为「自己的窗口」

        _transfer = new TextTransfer(_config, _tracker, pasteSpec);
        _monitor = new KeyboardMonitor();
        _monitor.OnKeyDown = OnLowLevelKeyDown;

        Register(commitSpec, Commit);
        Register(summonSpec, Summon);
        Register(toggleSpec, ToggleEnabled);

        string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.2.2";
        _tray = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Windows Type Anywhere v" + version,
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };
        _tray.DoubleClick += (s, e) => Summon();

        ApplyEnabledState();
        UpdateMenu();

        if (_registrationErrors.Count > 0)
            _tray.ShowBalloonTip(4000, "快捷键注册失败",
                string.Join("\n", _registrationErrors), ToolTipIcon.Warning);
    }

    private HotkeySpec Parse(string name, string value)
    {
        try
        {
            return HotkeySpec.Parse(value);
        }
        catch (Exception ex)
        {
            _registrationErrors.Add(name + ": " + ex.Message);
            return HotkeySpec.Parse("Ctrl+Enter");
        }
    }

    private void Register(HotkeySpec spec, Action action)
    {
        if (spec.Modifiers == 0)
            _modifierless.Add(new KeyValuePair<HotkeySpec, Action>(spec, action));
        else if (!_hotkeys.TryRegister(spec, action))
            _registrationErrors.Add(spec.Display + " 被其它程序占用");
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        _enabledItem = new ToolStripMenuItem("启用");
        _enabledItem.Click += (s, e) => ToggleEnabled();

        _modeItem = new ToolStripMenuItem("模式");
        _modeItem.Click += (s, e) => ToggleMode();

        var show = new ToolStripMenuItem("显示中转框");
        show.Click += (s, e) => Summon();

        var edit = new ToolStripMenuItem("编辑配置");
        edit.Click += (s, e) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo(
                    Path.Combine(AppContext.BaseDirectory, "config.json"))
                { UseShellExecute = true });
            }
            catch
            {
                // 打开失败不影响托盘其它功能
            }
        };

        var reload = new ToolStripMenuItem("重新载入配置");
        reload.Click += (s, e) => Application.Restart();

        var exit = new ToolStripMenuItem("退出");
        exit.Click += (s, e) => Exit();

        menu.Items.AddRange(new ToolStripItem[]
        {
            _enabledItem,
            _modeItem,
            new ToolStripSeparator(),
            show,
            edit,
            reload,
            new ToolStripSeparator(),
            exit
        });
        return menu;
    }

    private void UpdateMenu()
    {
        _enabledItem.Text = _enabled ? "状态：已启用（点击禁用）" : "状态：已禁用（点击启用）";
        _enabledItem.Checked = _enabled;
        _modeItem.Text = _monitoring ? "模式：监听（点击切为按需）" : "模式：按需（点击切为监听）";
    }

    private void ToggleEnabled()
    {
        _enabled = !_enabled;
        ApplyEnabledState();
        UpdateMenu();
    }

    private void ToggleMode()
    {
        _monitoring = !_monitoring;
        ApplyEnabledState();
        UpdateMenu();
    }

    private void ApplyEnabledState()
    {
        if (_enabled)
        {
            // 只在真正需要时安装低级键盘钩子：监听模式自动聚焦，或配置了无修饰键的单键热键。
            if ((_monitoring && _config.MonitoringAutoFocus) || _modifierless.Count > 0)
                _monitor.Install();
            else
                _monitor.Uninstall();

            if (_monitoring)
            {
                _relay.PlaceNear(_tracker.LastTarget);
                _relay.Show();
                if (_config.MonitoringAutoFocus)
                {
                    ForegroundHelper.ForceForeground(_relay.Handle);
                    _relay.FocusInput();
                }
            }
        }
        else
        {
            _monitor.Uninstall();
            _relay.Hide();
        }
    }

    private async void Summon()
    {
        if (!_enabled) return;
        IntPtr target = _tracker.LastTarget;   // 必须在 Show 之前取，否则会被自己的窗口覆盖
        _relay.PlaceNear(target);
        _relay.Show();
        ForegroundHelper.ForceForeground(_relay.Handle);
        _relay.FocusInput();

        // WSLg/RDP 窗口会在焦点切换后异步抢回前台，首次显示时尤其明显；
        // 短延迟后复核一次，必要时再切一次，避免中转框可见却收不到键入。
        await Task.Delay(80);
        if (Native.GetForegroundWindow() != _relay.Handle)
        {
            ForegroundHelper.ForceForeground(_relay.Handle);
            _relay.FocusInput();
        }
    }

    private bool OnLowLevelKeyDown(uint vk)
    {
        if (!_enabled) return false;
        uint mods = CurrentModifiers();

        // (1) 无修饰键的任意单键：投递到消息队列异步执行，避免在钩子回调里做耗时操作
        if (mods == 0)
        {
            foreach (var pair in _modifierless)
            {
                if (pair.Key.Vk == vk)
                {
                    Action action = pair.Value;
                    _relay.BeginInvoke(action);
                    return true;   // 吞掉，避免该键被键入到文本框里
                }
            }
        }

        // (2) 监听模式自动聚焦：只在中文 IME 组合的起始键上把中转框拉回前台。
        //     CapsLock 开启时输入法固定输出大写英文，不会进入组合态，此时不触发；
        //     数字、符号、空格、小键盘、编辑键、功能键与 Ctrl|Alt|Win 组合留在目标窗口。
        if (_monitoring && _config.MonitoringAutoFocus && !_transferring &&
            ShouldAutoFocus(vk) &&
            (mods & (Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_WIN)) == 0 &&
            Native.GetForegroundWindow() != _relay.Handle)
        {
            if (!_relay.Visible) _relay.Show();
            ForegroundHelper.ForceForeground(_relay.Handle);
            return true;   // 吞掉触发切换的键，避免它被键入到目标窗口
        }
        return false;
    }

    /// <summary>
    /// 以中文拼音输入法为参考模型的自动聚焦判定：CapsLock 开启时用户只输入大写英文，
    /// 不会唤起输入法，因此整个自动聚焦都不触发。其它语言的输入法可能需要另定规则。
    /// </summary>
    private static bool ShouldAutoFocus(uint vk)
    {
        if (Control.IsKeyLocked(Keys.CapsLock)) return false;
        return IsTextInputKey(vk);
    }

    /// <summary>只有 IME 组合的起始键才需要中转框接管；键盘上已有的字符可直接送进目标窗口。</summary>
    private static bool IsTextInputKey(uint vk)
    {
        if (vk >= 0x41 && vk <= 0x5A) return true;   // A-Z：拼音等输入法依靠字母键开始组合
        return vk == 0xE5;                           // VK_PROCESSKEY，IME 处理中的按键
    }

    private static uint CurrentModifiers()
    {
        uint m = 0;
        if ((Native.GetKeyState(Native.VK_CONTROL) & 0x8000) != 0) m |= Native.MOD_CONTROL;
        if ((Native.GetKeyState(Native.VK_MENU) & 0x8000) != 0) m |= Native.MOD_ALT;
        if ((Native.GetKeyState(Native.VK_SHIFT) & 0x8000) != 0) m |= Native.MOD_SHIFT;
        if ((Native.GetKeyState(Native.VK_LWIN) & 0x8000) != 0 ||
            (Native.GetKeyState(Native.VK_RWIN) & 0x8000) != 0) m |= Native.MOD_WIN;
        return m;
    }

    private async void Commit()
    {
        if (!_enabled || _transferring) return;

        string text = _relay.ReadAndClear();
        if (string.IsNullOrEmpty(text))
        {
            if (!_monitoring) _relay.Hide();
            return;
        }

        IntPtr target = _tracker.LastTarget;
        if (target == IntPtr.Zero || !Native.IsWindow(target))
        {
            _relay.SetText(text);   // 没有可用目标，把字还回去，不丢内容
            return;
        }

        _transferring = true;
        try
        {
            bool ok = await _transfer.TransferAsync(text, target);
            if (!ok) _relay.SetText(text);
        }
        catch
        {
            _relay.SetText(text);
        }
        finally
        {
            if (_monitoring)
            {
                // 注入的粘贴键还在目标窗口消息队列里，稍后把焦点交还目标窗口；
                // 这段时间保持 _transferring，避免 LL 钩子提前抢焦点。
                await Task.Delay(Math.Max(80, _config.ClipboardDelayMs));
                if (Native.IsWindow(target))
                    ForegroundHelper.ForceForeground(target);
            }
            else
            {
                _relay.Hide();
            }
            _transferring = false;
        }
    }

    private void Exit()
    {
        _tray.Visible = false;
        _tray.Dispose();
        _monitor.Uninstall();
        _hotkeys.Dispose();
        _tracker.Dispose();
        _relay.Dispose();
        ExitThread();
    }
}
