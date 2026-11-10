// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
//
// Copyright (c) 2026 ENDERSRUOLI
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Drawing;
using System.Windows.Forms;

namespace WindowsTypeAnywhere;

/// <summary>
/// 中转文本框：一个能获得真实焦点的原生 WinForms TextBox（原生 EDIT 控件）。
/// Windows 输入法(TSF/IMM32) 必须挂在这种原生输入上下文上 —— 这是整个方案的核心约束，
/// 因此本窗体必须是 Windows 原生窗口，而不能是 WSLg 里的 GTK/Qt 窗口。
/// </summary>
internal sealed class RelayForm : Form
{
    private readonly TextBox _input;
    private readonly Label _hint;

    public RelayForm(AppConfig config, string commitHint)
    {
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        TopMost = config.RelayTopMost;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(Math.Max(240, config.RelayWidth), Math.Max(70, config.RelayHeight));
        Opacity = Math.Min(1.0, Math.Max(0.3, config.RelayOpacity));
        Text = "中转文本框 · Windows Type Anywhere";
        BackColor = SystemColors.Window;

        _hint = new Label
        {
            Dock = DockStyle.Top,
            Height = 24,
            AutoSize = false,
            Padding = new Padding(6, 4, 6, 0),
            ForeColor = SystemColors.GrayText,
            Text = "用 Windows 输入法在此键入 · " + commitHint + " 发送 · Esc 取消"
        };

        _input = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            AcceptsReturn = true,
            AcceptsTab = false,
            WordWrap = true,
            ScrollBars = ScrollBars.Vertical,
            BorderStyle = BorderStyle.None,
            Font = new Font("Microsoft YaHei UI", 12f)
            // 不设置 ImeMode：保留用户系统输入法的默认行为
        };
        _input.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                Hide();
                e.Handled = true;
            }
        };

        // 先加 Fill 再加 Top：WinForms 按 z 序逆序停靠，Top 先占位、Fill 占剩余空间
        Controls.Add(_input);
        Controls.Add(_hint);

        Shown += (s, e) => FocusInput();
    }

    public void FocusInput() => _input.Focus();

    public string ReadAndClear()
    {
        string text = _input.Text;
        _input.Clear();
        return text;
    }

    public void SetText(string text) => _input.Text = text ?? string.Empty;

    /// <summary>放在目标窗口所在屏幕的工作区底部，避免遮住正在编辑的文本框。</summary>
    public void PlaceNear(IntPtr targetHwnd)
    {
        Screen screen = Screen.PrimaryScreen;
        try
        {
            if (targetHwnd != IntPtr.Zero && Native.GetWindowRect(targetHwnd, out Native.RECT r))
            {
                var rect = new Rectangle(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
                screen = Screen.FromRectangle(rect) ?? Screen.PrimaryScreen;
            }
        }
        catch
        {
            screen = Screen.PrimaryScreen;
        }
        Rectangle area = screen.WorkingArea;
        Location = new Point(
            area.Left + (area.Width - Width) / 2,
            area.Bottom - Height - 8);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;   // 保留 HWND，避免下次 Show 时句柄失效
            Hide();
            return;
        }
        base.OnFormClosing(e);
    }
}
