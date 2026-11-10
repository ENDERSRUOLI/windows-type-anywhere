using System;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;

namespace WindowsTypeAnywhere;

/// <summary>由同目录 config.json 覆盖；缺失或损坏时回落到默认值。</summary>
internal sealed class AppConfig
{
    /// <summary>OnDemand = 全局热键唤起；Monitoring = 启用后每次键入前把中转框拉回前台。</summary>
    public string Mode { get; set; } = "OnDemand";
    public bool EnabledOnStart { get; set; } = true;

    /// <summary>监听模式是否在 IME 组合起始键上自动把中转框拉回前台；关闭后由唤起热键显式切换。</summary>
    public bool MonitoringAutoFocus { get; set; } = true;

    /// <summary>任意键或组合键，例如 "F12"、"Ctrl+Enter"、"Ctrl+Shift+Space"。</summary>
    public string SummonHotkey { get; set; } = "Ctrl+Shift+Space";
    public string CommitHotkey { get; set; } = "Ctrl+Enter";
    public string ToggleHotkey { get; set; } = "Ctrl+Shift+F12";
    public string PasteHotkey { get; set; } = "Ctrl+V";

    /// <summary>等待目标窗口成为前台的上限（事件驱动，非轮询）。</summary>
    public int ForegroundWaitMs { get; set; } = 400;

    /// <summary>可选的固定短延迟；设为 0 即完全不等待。不采用剪贴板变更监听。</summary>
    public int ClipboardDelayMs { get; set; } = 60;

    public bool RestoreClipboard { get; set; } = true;
    public int RestoreClipboardDelayMs { get; set; } = 1500;

    public bool RelayTopMost { get; set; } = true;
    public double RelayOpacity { get; set; } = 0.96;
    public int RelayWidth { get; set; } = 560;
    public int RelayHeight { get; set; } = 110;

    public bool IsMonitoring =>
        string.Equals(Mode, "Monitoring", StringComparison.OrdinalIgnoreCase);

    public static AppConfig Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                };
                var cfg = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(path), options);
                if (cfg != null) return cfg;
            }
        }
        catch
        {
            // 配置损坏时静默回落到默认值
        }
        return new AppConfig();
    }
}

/// <summary>一个「任意键或组合键」：修饰键位 + 主键虚拟键码。</summary>
internal sealed class HotkeySpec
{
    public uint Modifiers { get; }
    public uint Vk { get; }
    public string Display { get; }

    private HotkeySpec(uint modifiers, uint vk, string display)
    {
        Modifiers = modifiers;
        Vk = vk;
        Display = display;
    }

    public static HotkeySpec Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new FormatException("快捷键为空");

        uint mods = 0;
        string keyPart = null;

        foreach (var raw in text.Split('+'))
        {
            var p = raw.Trim();
            if (p.Length == 0) continue;
            switch (p.ToLowerInvariant())
            {
                case "ctrl":
                case "control": mods |= Native.MOD_CONTROL; break;
                case "alt":
                case "menu": mods |= Native.MOD_ALT; break;
                case "shift": mods |= Native.MOD_SHIFT; break;
                case "win":
                case "meta":
                case "super": mods |= Native.MOD_WIN; break;
                default: keyPart = p; break;
            }
        }

        if (keyPart == null)
            throw new FormatException("快捷键缺少主键: " + text);
        if (!Enum.TryParse<Keys>(keyPart, true, out var key))
            throw new FormatException("无法识别的按键: " + keyPart);

        return new HotkeySpec(mods, (uint)key, text);
    }
}
