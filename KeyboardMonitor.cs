// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
//
// Copyright (c) 2026 ENDERSRUOLI
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Runtime.InteropServices;

namespace WindowsTypeAnywhere;

/// <summary>
/// 仅在安装期间存在的低级键盘钩子，用于两件事：
///  1) 支持「无修饰键的任意单键」作为全局快捷键；
///  2) 监听模式下，在键入发生前把中转框拉回前台（不吞掉按键，保留 IME 组合链）。
/// 禁用时立即 Uninstall，不留任何常驻监听。
/// </summary>
internal sealed class KeyboardMonitor : IDisposable
{
    private readonly Native.LowLevelKeyboardProc _proc;
    private IntPtr _hook = IntPtr.Zero;

    /// <summary>返回 true 表示吞掉该按键。</summary>
    public Func<uint, bool> OnKeyDown { get; set; }

    public KeyboardMonitor()
    {
        _proc = HookProc;   // 保持引用，防止委托被 GC 回收
    }

    public void Install()
    {
        if (_hook != IntPtr.Zero) return;
        _hook = Native.SetWindowsHookEx(
            Native.WH_KEYBOARD_LL, _proc, Native.GetModuleHandle(null), 0);
    }

    public void Uninstall()
    {
        if (_hook == IntPtr.Zero) return;
        Native.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode == Native.HC_ACTION)
        {
            int msg = wParam.ToInt32();
            if (msg == Native.WM_KEYDOWN || msg == Native.WM_SYSKEYDOWN)
            {
                var kb = Marshal.PtrToStructure<Native.KBDLLHOOKSTRUCT>(lParam);
                if (OnKeyDown != null && OnKeyDown(kb.vkCode))
                    return new IntPtr(1);
            }
        }
        return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose() => Uninstall();
}
