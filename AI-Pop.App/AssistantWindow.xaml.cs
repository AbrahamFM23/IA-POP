using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;

using Microsoft.Web.WebView2.Core;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;
using MediaSolidColorBrush = System.Windows.Media.SolidColorBrush;
using System.Windows.Media.Effects;

namespace AI_Pop.App;

public partial class AssistantWindow : Window
{
    private readonly string _assistantName;
    private readonly string _assistantUrl;
    private bool _initialized;
    private bool _transparentMode;
    private string? _transparentScriptId;

    private readonly DispatcherTimer _saveTimer;
    private readonly DispatcherTimer _hoverTimer;

    public bool AllowClose { get; set; }

    public event EventHandler? AssistantStateChanged;

    public AssistantWindow(
        string assistantName,
        string assistantUrl,
        double opacity,
        AssistantWindowState? savedState = null,
        string theme = "Glass oscuro")
    {
        InitializeComponent();

        _assistantName = assistantName;
        _assistantUrl = assistantUrl;

        Title = $"AI Pop - {assistantName}";
        WindowStartupLocation = WindowStartupLocation.Manual;

        if (savedState != null)
        {
            Width = Math.Max(400, savedState.Width);
            Height = Math.Max(300, savedState.Height);
            Left = savedState.Left;
            Top = savedState.Top;
        }

        _saveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(400)
        };

        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            AssistantStateChanged?.Invoke(this, EventArgs.Empty);
        };

        _hoverTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(350)
        };

        _hoverTimer.Tick += (_, _) =>
        {
            _hoverTimer.Stop();

            if (!IsMouseOver)
                HoverToolbar.Visibility = Visibility.Collapsed;
        };

        LocationChanged += (_, _) => ScheduleSave();
        SizeChanged += (_, _) => ScheduleSave();
        Loaded += AssistantWindow_Loaded;
        Closing += AssistantWindow_Closing;

        SetGlobalTheme(theme);
        SetGlobalOpacity(opacity);
    }

    private void ScheduleSave()
    {
        if (!_initialized)
            return;

        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private async void AssistantWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        if (_initialized)
            return;

        _initialized = true;

        try
        {
            // IMPORTANTE: se conserva la ruta original para proteger las sesiones.
            string userDataFolder = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "AI Pop",
                "WebView2",
                _assistantName);

            Directory.CreateDirectory(userDataFolder);

            var environment =
                await CoreWebView2Environment.CreateAsync(
                    userDataFolder: userDataFolder);

            // En modo transparente, WebView2 deja transparente su propio fondo.
            // Esto no elimina por sí solo los fondos CSS definidos por cada sitio.
            Browser.DefaultBackgroundColor = _transparentMode
                ? System.Drawing.Color.Transparent
                : System.Drawing.Color.FromArgb(255, 18, 20, 24);

            await Browser.EnsureCoreWebView2Async(environment);

            // Se registra antes de navegar para que el estilo esté disponible
            // desde el primer render cuando el tema elegido sea Transparente.
            if (_transparentMode)
            {
                _transparentScriptId =
                    await Browser.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
                        TransparentPageStyleScript);
            }

            Browser.Source = new Uri(_assistantUrl);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"No se pudo iniciar {_assistantName}.\n\n{ex.Message}",
                "AI Pop",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private const string TransparentPageStyleScript = """
        (() => {
            const styleId = 'ai-pop-transparent-theme';

            const css = `
                html,
                body,
                body > div,
                #root,
                #app,
                main,
                [role="main"] {
                    background-color: transparent !important;
                    background-image: none !important;
                }
            `;

            const setTransparent = (enabled) => {
                let style = document.getElementById(styleId);

                if (enabled) {
                    if (!style) {
                        style = document.createElement('style');
                        style.id = styleId;
                        style.textContent = css;
                        (document.head || document.documentElement)
                            .appendChild(style);
                    }
                } else if (style) {
                    style.remove();
                }
            };

            window.__aiPopSetTransparent = setTransparent;
            setTransparent(true);

            if (!window.__aiPopTransparentObserver) {
                const observer = new MutationObserver(() => {
                    if (window.__aiPopTransparentEnabled &&
                        !document.getElementById(styleId)) {
                        setTransparent(true);
                    }
                });

                observer.observe(document.documentElement, {
                    childList: true,
                    subtree: true
                });

                window.__aiPopTransparentObserver = observer;
            }

            window.__aiPopTransparentEnabled = true;
        })();
        """;

    private async void UpdateWebViewTransparency(bool enabled)
    {
        if (Browser.CoreWebView2 == null)
            return;

        if (enabled)
        {
            Browser.DefaultBackgroundColor = System.Drawing.Color.Transparent;

            if (string.IsNullOrEmpty(_transparentScriptId))
            {
                _transparentScriptId =
                    await Browser.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
                        TransparentPageStyleScript);
            }

            await Browser.CoreWebView2.ExecuteScriptAsync(
                "window.__aiPopSetTransparent?.(true); window.__aiPopTransparentEnabled = true;");
        }
        else
        {
            Browser.DefaultBackgroundColor =
                System.Drawing.Color.FromArgb(255, 18, 20, 24);

            await Browser.CoreWebView2.ExecuteScriptAsync(
                "window.__aiPopTransparentEnabled = false; window.__aiPopSetTransparent?.(false);");

            if (!string.IsNullOrEmpty(_transparentScriptId))
            {
                Browser.CoreWebView2.RemoveScriptToExecuteOnDocumentCreated(
                    _transparentScriptId);
                _transparentScriptId = null;
            }
        }
    }

    public void SetGlobalTheme(string theme)
    {
        _transparentMode =
            string.Equals(
                theme,
                "Transparente",
                StringComparison.OrdinalIgnoreCase);

        ThemeLabel.Text = $"AI Pop · {theme}";

        switch (theme)
        {
            case "Transparente":
                Background = MediaBrushes.Transparent;
                RootGrid.Background = MediaBrushes.Transparent;
                HoverToolbar.Background =
                    new MediaSolidColorBrush(
                        MediaColor.FromArgb(150, 15, 17, 20));

                // Si WebView2 ya está inicializado, cambiar su fondo en caliente.
                UpdateWebViewTransparency(true);
                break;

            case "Minimalista":
                Background =
                    new MediaSolidColorBrush(
                        MediaColor.FromRgb(25, 27, 30));
                RootGrid.Background =
                    new MediaSolidColorBrush(
                        MediaColor.FromRgb(25, 27, 30));
                HoverToolbar.Background =
                    new MediaSolidColorBrush(
                        MediaColor.FromRgb(34, 36, 40));
                SetWebViewOpaqueBackground();
                break;

            case "Fluent":
                Background =
                    new MediaSolidColorBrush(
                        MediaColor.FromRgb(31, 35, 42));
                RootGrid.Background =
                    new MediaSolidColorBrush(
                        MediaColor.FromRgb(31, 35, 42));
                HoverToolbar.Background =
                    new MediaSolidColorBrush(
                        MediaColor.FromArgb(225, 45, 50, 60));
                SetWebViewOpaqueBackground();
                break;

            default: // Glass oscuro
                Background =
                    new MediaSolidColorBrush(
                        MediaColor.FromRgb(18, 20, 24));
                RootGrid.Background =
                    new MediaSolidColorBrush(
                        MediaColor.FromRgb(18, 20, 24));
                HoverToolbar.Background =
                    new MediaSolidColorBrush(
                        MediaColor.FromArgb(235, 29, 32, 38));
                SetWebViewOpaqueBackground();
                break;
        }
    }

    private void SetWebViewOpaqueBackground()
    {
        UpdateWebViewTransparency(false);
    }

    public void SetGlobalOpacity(double opacity)
    {
        // La opacidad afecta a toda la ventana, incluido WebView2 y su contenido.
        double normalizedOpacity = Math.Clamp(opacity, 0.2, 1.0);
        Opacity = normalizedOpacity;

        // Al bajar la opacidad, aplicamos un difuminado progresivo.
        // 100 % = nítido. 20 % = difuminado más perceptible.
        double blurRadius = (1.0 - normalizedOpacity) * 3.0;

        RootGrid.Effect = blurRadius <= 0.05
            ? null
            : new BlurEffect
            {
                Radius = blurRadius,
                KernelType = KernelType.Gaussian,
                RenderingBias = RenderingBias.Performance
            };
    }

    public AssistantWindowState GetCurrentState()
    {
        Rect bounds = WindowState == WindowState.Normal
            ? new Rect(Left, Top, Width, Height)
            : RestoreBounds;

        return new AssistantWindowState
        {
            Left = bounds.Left,
            Top = bounds.Top,
            Width = Math.Max(400, bounds.Width),
            Height = Math.Max(300, bounds.Height)
        };
    }

    private void RootGrid_MouseEnter(
        object sender,
        System.Windows.Input.MouseEventArgs e)
    {
        _hoverTimer.Stop();
        HoverToolbar.Visibility = Visibility.Visible;
    }

    private void RootGrid_MouseLeave(
        object sender,
        System.Windows.Input.MouseEventArgs e)
    {
        _hoverTimer.Stop();
        _hoverTimer.Start();
    }

    private void HideButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        Hide();
    }

    private void AssistantWindow_Closing(
        object? sender,
        CancelEventArgs e)
    {
        if (!AllowClose)
        {
            e.Cancel = true;
            _saveTimer.Stop();
            _hoverTimer.Stop();

            AssistantStateChanged?.Invoke(
                this,
                EventArgs.Empty);

            Hide();
        }
    }
}
