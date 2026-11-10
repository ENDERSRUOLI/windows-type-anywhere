// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
//
// Copyright (c) 2026 ENDERSRUOLI
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsTypeAnywhere;

/// <summary>
/// 把中转框的文本经剪贴板送进目标窗口。
/// 全程不注册任何剪贴板变更监听：唯一的等待是事件驱动的前台切换确认 + 一次可选短延迟。
/// </summary>
internal sealed class TextTransfer
{
    private readonly AppConfig _config;
    private readonly ForegroundTracker _tracker;
    private readonly HotkeySpec _paste;

    public TextTransfer(AppConfig config, ForegroundTracker tracker, HotkeySpec paste)
    {
        _config = config;
        _tracker = tracker;
        _paste = paste;
    }

    public async Task<bool> TransferAsync(string text, IntPtr target)
    {
        if (string.IsNullOrEmpty(text)) return false;
        if (target == IntPtr.Zero || !Native.IsWindow(target)) return false;

        IDataObject previous = _config.RestoreClipboard ? ClipboardSnapshot.Capture() : null;
        bool sent = false;
        try
        {
            if (!SetClipboardText(text)) return false;

            // (1) 必须显式切回目标：隐藏中转框时 Windows 会把焦点丢给 Z 序里的下一个窗口
            ForegroundHelper.ForceForeground(target);
            if (!await _tracker.WaitForForegroundAsync(target, _config.ForegroundWaitMs))
                return false;

            // (2) 可选的固定短延迟（"魔法数字"，可设为 0）。无轮询、无剪贴板监听。
            if (_config.ClipboardDelayMs > 0)
                await Task.Delay(_config.ClipboardDelayMs);

            // (3) 扫描码注入粘贴键：RDP/WSLg 主要按扫描码传输键盘输入，
            //     把合成好的 Unicode 字符当按键注入并不可靠（部分应用如 Qt 明确不接受）。
            InputSender.SendChord(_paste);
            sent = true;
            return true;
        }
        finally
        {
            // 传输结束即收尾：不注册任何剪贴板监听。
            // 成功时延迟恢复，避免远端还没取走数据就把剪贴板改回原内容；失败时立即恢复。
            if (previous != null)
            {
                if (sent && _config.RestoreClipboardDelayMs > 0)
                    _ = RestoreLaterAsync(previous, _config.RestoreClipboardDelayMs);
                else
                    ClipboardSnapshot.Restore(previous);
            }
        }
    }

    private static bool SetClipboardText(string text)
    {
        for (int i = 0; i < 3; i++)
        {
            try
            {
                Clipboard.SetText(text, TextDataFormat.UnicodeText);
                return true;
            }
            catch (ExternalException)
            {
                Thread.Sleep(20);   // 剪贴板被占用时，仅在失败路径短暂等待
            }
        }
        return false;
    }

    private static async Task RestoreLaterAsync(IDataObject previous, int delayMs)
    {
        await Task.Delay(delayMs).ConfigureAwait(true);
        ClipboardSnapshot.Restore(previous);
    }
}

internal static class ClipboardSnapshot
{
    /// <summary>把当前剪贴板各格式立即取成快照，避免原持有者随后改写导致延迟渲染失效。</summary>
    public static IDataObject Capture()
    {
        try
        {
            IDataObject src = Clipboard.GetDataObject();
            if (src == null) return null;
            var copy = new DataObject();
            foreach (string format in src.GetFormats())
            {
                try
                {
                    object data = src.GetData(format);
                    if (data != null) copy.SetData(format, data);
                }
                catch
                {
                    // 单个格式不可用则跳过
                }
            }
            return copy;
        }
        catch
        {
            return null;
        }
    }

    public static void Restore(IDataObject snapshot)
    {
        try
        {
            if (snapshot != null) Clipboard.SetDataObject(snapshot, true);
        }
        catch
        {
            // 恢复失败不影响已完成的主流程
        }
    }
}

internal static class ForegroundHelper
{
    public static bool ForceForeground(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !Native.IsWindow(hwnd)) return false;
        if (Native.GetForegroundWindow() == hwnd) return true;

        // 仅在目标最小化时恢复；对已最大化的窗口调用 SW_RESTORE 会把它还原成普通大小
        if (Native.IsIconic(hwnd)) Native.ShowWindow(hwnd, Native.SW_RESTORE);

        uint myThread = Native.GetCurrentThreadId();
        IntPtr fg = Native.GetForegroundWindow();
        uint fgThread = fg == IntPtr.Zero ? 0 : Native.GetWindowThreadProcessId(fg, out uint _);
        bool attached = fgThread != 0 && fgThread != myThread &&
                        Native.AttachThreadInput(myThread, fgThread, true);
        try
        {
            Native.SetForegroundWindow(hwnd);
        }
        finally
        {
            if (attached) Native.AttachThreadInput(myThread, fgThread, false);
        }
        return Native.GetForegroundWindow() == hwnd;
    }
}

internal static class InputSender
{
    public static void SendChord(HotkeySpec spec)
    {
        var mods = new List<uint>();
        if ((spec.Modifiers & Native.MOD_CONTROL) != 0) mods.Add(Native.VK_CONTROL);
        if ((spec.Modifiers & Native.MOD_ALT) != 0) mods.Add(Native.VK_MENU);
        if ((spec.Modifiers & Native.MOD_SHIFT) != 0) mods.Add(Native.VK_SHIFT);
        if ((spec.Modifiers & Native.MOD_WIN) != 0) mods.Add(Native.VK_LWIN);

        foreach (uint vk in mods) SendScan(vk, false);
        SendScan(spec.Vk, false);
        SendScan(spec.Vk, true);
        for (int i = mods.Count - 1; i >= 0; i--) SendScan(mods[i], true);
    }

    private static void SendScan(uint vk, bool keyUp)
    {
        uint scan = Native.MapVirtualKey(vk, Native.MAPVK_VK_TO_VSC);
        uint flags = Native.KEYEVENTF_SCANCODE;
        if (keyUp) flags |= Native.KEYEVENTF_KEYUP;
        if (IsExtended(vk)) flags |= Native.KEYEVENTF_EXTENDEDKEY;

        var input = new Native.INPUT
        {
            type = Native.INPUT_KEYBOARD,
            u = new Native.INPUTUNION
            {
                ki = new Native.KEYBDINPUT
                {
                    wVk = 0,                       // KEYEVENTF_SCANCODE 时 wVk 必须为 0
                    wScan = (ushort)scan,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };
        Native.SendInput(1, new Native.INPUT[] { input }, Marshal.SizeOf<Native.INPUT>());
    }

    private static bool IsExtended(uint vk)
    {
        switch (vk)
        {
            case Native.VK_UP:
            case Native.VK_DOWN:
            case Native.VK_LEFT:
            case Native.VK_RIGHT:
            case Native.VK_INSERT:
            case Native.VK_DELETE:
            case Native.VK_HOME:
            case Native.VK_END:
            case Native.VK_PRIOR:
            case Native.VK_NEXT:
            case Native.VK_NUMLOCK:
            case Native.VK_DIVIDE:
            case Native.VK_RCONTROL:
            case Native.VK_RMENU:
            case Native.VK_LWIN:
            case Native.VK_RWIN:
                return true;
            default:
                return false;
        }
    }
}
