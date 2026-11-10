// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
//
// Copyright (c) 2026 ENDERSRUOLI
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace WindowsTypeAnywhere;

/// <summary>
/// 接收 WM_HOTKEY 的隐藏窗口。这里刻意创建普通顶层窗口（不设 HWND_MESSAGE 父窗口，
/// 也不设 WS_VISIBLE），确保注册热键后 WM_HOTKEY 一定能到达 WndProc。
/// </summary>
internal sealed class HotkeyWindow : NativeWindow
{
    public event Action<int> HotkeyPressed;

    public HotkeyWindow()
    {
        var cp = new CreateParams { Caption = "WindowsTypeAnywhere.HotkeySink" };
        CreateHandle(cp);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Native.WM_HOTKEY)
            HotkeyPressed?.Invoke(m.WParam.ToInt32());
        base.WndProc(ref m);
    }

    public void Destroy() => DestroyHandle();
}

/// <summary>
/// RegisterHotKey 只负责带修饰键的组合键；无修饰键的任意单键由 KeyboardMonitor 处理，
/// 因为不带修饰键的全局热键会吞掉该键在其它程序中的正常使用。
/// </summary>
internal sealed class HotkeyManager : IDisposable
{
    private readonly HotkeyWindow _window = new HotkeyWindow();
    private readonly Dictionary<int, Action> _actions = new Dictionary<int, Action>();
    private int _nextId = 1;

    public IntPtr WindowHandle => _window.Handle;

    public HotkeyManager()
    {
        _window.HotkeyPressed += id =>
        {
            if (_actions.TryGetValue(id, out var action)) action();
        };
    }

    public bool TryRegister(HotkeySpec spec, Action action)
    {
        if (spec.Modifiers == 0) return false;
        int id = _nextId++;
        if (!Native.RegisterHotKey(_window.Handle, id,
                spec.Modifiers | Native.MOD_NOREPEAT, spec.Vk))
            return false;
        _actions[id] = action;
        return true;
    }

    public void Clear()
    {
        foreach (var id in _actions.Keys)
            Native.UnregisterHotKey(_window.Handle, id);
        _actions.Clear();
    }

    public void Dispose()
    {
        Clear();
        _window.Destroy();
    }
}
