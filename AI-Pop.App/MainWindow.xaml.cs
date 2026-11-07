using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

using MediaBrush = System.Windows.Media.Brush;
using WpfMenuItem = System.Windows.Controls.MenuItem;
using WpfContextMenu = System.Windows.Controls.ContextMenu;
using WpfSeparator = System.Windows.Controls.Separator;

namespace AI_Pop.App;

public partial class MainWindow : Window
{
    public bool AllowClose { get; set; }

    public event EventHandler? PositionChanged;

    public Action? ShowAllRequested { get; set; }

    public Action<double>? OpacityChanged { get; set; }

    public Action<string>? OpenAssistantRequested { get; set; }

    private readonly string _assistantName;

    private readonly DispatcherTimer _clickTimer;

    public MainWindow(
        string assistantName,
        string glyph,
        MediaBrush accent,
        double left,
        double top)
    {
        InitializeComponent();

        _assistantName = assistantName;

        Title = $"AI Pop - {assistantName}";

        BubbleIcon.Text = glyph;

        BubbleIcon.Foreground =
            System.Windows.Media.Brushes.White;

        BubbleBorder.BorderBrush =
            System.Windows.Media.Brushes.Transparent;

        ToolTip = $"{assistantName} - AI Pop";

        Left = left;
        Top = top;

        _clickTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(
                System.Windows.Forms.SystemInformation.DoubleClickTime)
        };

        _clickTimer.Tick += (_, _) =>
        {
            _clickTimer.Stop();
            Hide();
        };

        CreateContextMenu();

        Closing += Bubble_Closing;
    }

    private void BubbleBorder_MouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.ClickCount >= 2)
        {
            _clickTimer.Stop();
            OpenAssistant();
            e.Handled = true;
            return;
        }

        double initialLeft = Left;
        double initialTop = Top;

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            return;
        }

        double movement = Math.Abs(Left - initialLeft)
                        + Math.Abs(Top - initialTop);

        if (movement > 2)
        {
            _clickTimer.Stop();
            PositionChanged?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            _clickTimer.Stop();
            _clickTimer.Start();
        }

        e.Handled = true;
    }

    private void OpenAssistant()
    {
        OpenAssistantRequested?.Invoke(_assistantName);
    }

    public void SetGlobalOpacity(double opacity)
    {
        Opacity = Math.Clamp(opacity, 0.2, 1.0);
    }

    private void CreateContextMenu()
    {
        var menu = new WpfContextMenu();

        var openItem = new WpfMenuItem
        {
            Header = "Abrir asistente"
        };

        openItem.Click += (_, _) => OpenAssistant();

        var hideItem = new WpfMenuItem
        {
            Header = "Ocultar esta burbuja"
        };

        hideItem.Click += (_, _) => Hide();

        var showItem = new WpfMenuItem
        {
            Header = "Mostrar todas las burbujas"
        };

        showItem.Click += (_, _) => ShowAllRequested?.Invoke();

        var opacityMenu = new WpfMenuItem
        {
            Header = "Transparencia global"
        };

        AddOpacityOptions(opacityMenu);

        menu.Items.Add(openItem);
        menu.Items.Add(hideItem);
        menu.Items.Add(new WpfSeparator());
        menu.Items.Add(showItem);
        menu.Items.Add(opacityMenu);

        BubbleBorder.ContextMenu = menu;
    }

    private void AddOpacityOptions(WpfMenuItem parent)
    {
        foreach (int value in new[] { 20, 40, 60, 80, 100 })
        {
            var item = new WpfMenuItem
            {
                Header = $"{value} %"
            };

            double opacity = value / 100.0;

            item.Click += (_, _) =>
                OpacityChanged?.Invoke(opacity);

            parent.Items.Add(item);
        }
    }

    private void Bubble_Closing(
        object? sender,
        CancelEventArgs e)
    {
        if (!AllowClose)
        {
            e.Cancel = true;
            Hide();
        }
    }
}