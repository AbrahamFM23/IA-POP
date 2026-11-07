using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;

using Forms = System.Windows.Forms;
using Drawing = System.Drawing;
using WpfApplication = System.Windows.Application;

namespace AI_Pop.App;

public class BubblePosition
{
    public double Left { get; set; }
    public double Top { get; set; }
}

public class AssistantWindowState
{
    public double Left { get; set; } = 100;
    public double Top { get; set; } = 100;
    public double Width { get; set; } = 1000;
    public double Height { get; set; } = 700;
}

public class AISettings
{
    public double GlobalOpacity { get; set; } = 1.0;
    public string GlobalTheme { get; set; } = "Glass oscuro";
    public Dictionary<string, BubblePosition> Positions { get; set; } = new();
    public Dictionary<string, AssistantWindowState> AssistantWindows { get; set; } = new();
}

public partial class App : WpfApplication
{
    private Forms.NotifyIcon? _trayIcon;
    private readonly List<MainWindow> _bubbles = new();
    private readonly Dictionary<string, AssistantWindow> _assistants = new();
    private AISettings _settings = new();

    private readonly string _settingsDirectory =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AI Pop");

    private string SettingsFile => Path.Combine(_settingsDirectory, "settings.json");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        LoadSettings();

        CreateBubble("ChatGPT", "◉", 40, 180);
        CreateBubble("Claude", "✳", 40, 255);
        CreateBubble("Gemini", "✦", 40, 330);

        ShowBubbles();
        CreateTrayIcon();
    }

    private void CreateBubble(string name, string glyph, double defaultLeft, double defaultTop)
    {
        if (!_settings.Positions.TryGetValue(name, out BubblePosition? position))
        {
            position = new BubblePosition { Left = defaultLeft, Top = defaultTop };
        }

        var bubble = new MainWindow(
            name,
            glyph,
            System.Windows.Media.Brushes.White,
            position.Left,
            position.Top);

        bubble.SetGlobalOpacity(_settings.GlobalOpacity);
        bubble.PositionChanged += (_, _) => SaveSettings();
        bubble.ShowAllRequested = ShowBubbles;
        bubble.OpacityChanged = SetGlobalOpacity;
        bubble.OpenAssistantRequested = OpenAssistantWindow;
        _bubbles.Add(bubble);
    }

    private void OpenAssistantWindow(string name)
    {
        string url = name switch
        {
            "ChatGPT" => "https://chatgpt.com",
            "Claude" => "https://claude.ai",
            "Gemini" => "https://gemini.google.com",
            _ => "https://chatgpt.com"
        };

        if (_assistants.TryGetValue(name, out AssistantWindow? existing))
        {
            if (!existing.IsVisible)
                existing.Show();

            existing.Activate();
            return;
        }

        _settings.AssistantWindows.TryGetValue(name, out AssistantWindowState? savedState);

        var assistant = new AssistantWindow(
            name,
            url,
            _settings.GlobalOpacity,
            savedState,
            _settings.GlobalTheme);

        assistant.AssistantStateChanged += (_, _) => SaveSettings();
        _assistants[name] = assistant;
        assistant.Show();
    }

    private void CreateTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();

        var showItem = new Forms.ToolStripMenuItem("Mostrar todas las burbujas");
        var hideItem = new Forms.ToolStripMenuItem("Ocultar todas las burbujas");
        var opacityItem = new Forms.ToolStripMenuItem("Transparencia global");
        var themeItem = new Forms.ToolStripMenuItem("Tema global");
        var exitItem = new Forms.ToolStripMenuItem("Salir de AI Pop");

        showItem.Click += (_, _) => ShowBubbles();
        hideItem.Click += (_, _) => HideBubbles();

        AddOpacityOptions(opacityItem);
        AddThemeOptions(themeItem);

        exitItem.Click += (_, _) => ExitApplication();

        menu.Items.Add(showItem);
        menu.Items.Add(hideItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(opacityItem);
        menu.Items.Add(themeItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(exitItem);

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = Drawing.SystemIcons.Application,
            Text = "AI Pop",
            ContextMenuStrip = menu,
            Visible = true
        };

        _trayIcon.DoubleClick += (_, _) => ShowBubbles();
    }

    private void AddOpacityOptions(Forms.ToolStripMenuItem parent)
    {
        foreach (int value in new[] { 20, 40, 60, 80, 100 })
        {
            var item = new Forms.ToolStripMenuItem($"{value} %");
            double opacity = value / 100.0;
            item.Click += (_, _) => SetGlobalOpacity(opacity);
            parent.DropDownItems.Add(item);
        }
    }

    private void AddThemeOptions(Forms.ToolStripMenuItem parent)
    {
        foreach (string theme in new[] { "Glass oscuro", "Transparente", "Minimalista", "Fluent" })
        {
            var item = new Forms.ToolStripMenuItem(theme)
            {
                Checked = string.Equals(_settings.GlobalTheme, theme, StringComparison.OrdinalIgnoreCase),
                CheckOnClick = false
            };

            item.Click += (_, _) => SetGlobalTheme(theme);
            parent.DropDownItems.Add(item);
        }
    }

    private void SetGlobalOpacity(double opacity)
    {
        _settings.GlobalOpacity = Math.Clamp(opacity, 0.2, 1.0);

        foreach (var bubble in _bubbles)
            bubble.SetGlobalOpacity(_settings.GlobalOpacity);

        foreach (var assistant in _assistants.Values)
            assistant.SetGlobalOpacity(_settings.GlobalOpacity);

        SaveSettings();
    }

    private void SetGlobalTheme(string theme)
    {
        _settings.GlobalTheme = theme;

        foreach (var assistant in _assistants.Values)
            assistant.SetGlobalTheme(theme);

        SaveSettings();

        // Actualiza las marcas del menú al volverlo a abrir.
        if (_trayIcon?.ContextMenuStrip is Forms.ContextMenuStrip menu)
        {
            foreach (Forms.ToolStripItem entry in menu.Items)
            {
                if (entry is Forms.ToolStripMenuItem topItem &&
                    topItem.Text == "Tema global")
                {
                    foreach (Forms.ToolStripItem childEntry in topItem.DropDownItems)
                    {
                        if (childEntry is Forms.ToolStripMenuItem child)
                            child.Checked = child.Text == theme;
                    }
                }
            }
        }
    }

    private void ShowBubbles()
    {
        foreach (var bubble in _bubbles)
        {
            if (!bubble.IsVisible)
                bubble.Show();
        }
    }

    private void HideBubbles()
    {
        foreach (var bubble in _bubbles)
            bubble.Hide();
    }

    private void LoadSettings()
    {
        try
        {
            Directory.CreateDirectory(_settingsDirectory);

            if (File.Exists(SettingsFile))
            {
                string json = File.ReadAllText(SettingsFile);
                _settings = JsonSerializer.Deserialize<AISettings>(json) ?? new AISettings();
            }
        }
        catch
        {
            _settings = new AISettings();
        }

        _settings.Positions ??= new();
        _settings.AssistantWindows ??= new();

        if (string.IsNullOrWhiteSpace(_settings.GlobalTheme))
            _settings.GlobalTheme = "Glass oscuro";

        _settings.GlobalOpacity = Math.Clamp(_settings.GlobalOpacity, 0.2, 1.0);
    }

    private void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(_settingsDirectory);

            foreach (var bubble in _bubbles)
            {
                string name = bubble.Title.Replace("AI Pop - ", "");
                _settings.Positions[name] = new BubblePosition
                {
                    Left = bubble.Left,
                    Top = bubble.Top
                };
            }

            foreach (var assistant in _assistants)
                _settings.AssistantWindows[assistant.Key] = assistant.Value.GetCurrentState();

            string json = JsonSerializer.Serialize(
                _settings,
                new JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(SettingsFile, json);
        }
        catch
        {
            // Un error de guardado no debe cerrar AI Pop.
        }
    }

    private void ExitApplication()
    {
        SaveSettings();

        foreach (var bubble in _bubbles)
            bubble.AllowClose = true;

        foreach (var assistant in _assistants.Values)
            assistant.AllowClose = true;

        _trayIcon?.Dispose();
        _trayIcon = null;
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SaveSettings();
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
