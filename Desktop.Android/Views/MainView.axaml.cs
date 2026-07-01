using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Pronetsys.Desktop.Android.Services;

namespace Pronetsys.Desktop.Android.Views;

public partial class MainView : UserControl
{
    // TODO: hacerlo configurable (o incrustarlo en el APK como en el cliente de escritorio).
    private const string ServerUrl = "https://asistencia.pronetsys.com.co";

    private readonly DesktopHubConnectionAndroid _hub = new();

    /// <summary>
    /// Hub de la vista activa, para que <see cref="MainActivity"/> le entregue los frames
    /// capturados por MediaProjection (creada fuera del árbol de Avalonia).
    /// </summary>
    public static DesktopHubConnectionAndroid? ActiveHub { get; private set; }

    /// <summary>Vista activa, para que la Activity pueda refrescar su estado (p. ej. al reanudar).</summary>
    public static MainView? Current { get; private set; }

    public MainView()
    {
        InitializeComponent();

        ActiveHub = _hub;
        Current = this;

        _hub.StatusChanged += message =>
            Dispatcher.UIThread.Post(() => StatusText.Text = message);

        _hub.SessionActiveChanged += active =>
            Dispatcher.UIThread.Post(() => SessionIndicator.IsVisible = active);

        StartButton.Click += OnStartClicked;
        AccessibilityButton.Click += OnAccessibilityClicked;

        // Fase 0: al abrir la app, conectar y mostrar el ID de sesión.
        _ = StartAsync();
        RefreshAccessibilityState();
    }

    private void OnAccessibilityClicked(object? sender, RoutedEventArgs e)
    {
        MainActivity.Instance?.OpenAccessibilitySettings();
    }

    /// <summary>La Activity llama esto al reanudar (p. ej. tras volver de Ajustes).</summary>
    public void RefreshFromActivity()
    {
        Dispatcher.UIThread.Post(RefreshAccessibilityState);
    }

    // Refleja si el control táctil ya está habilitado (servicio de accesibilidad activo).
    private void RefreshAccessibilityState()
    {
        var enabled = MainActivity.Instance?.IsAccessibilityEnabled() == true;
        AccessibilityButton.Content = enabled ? "Control táctil habilitado ✓" : "Habilitar control táctil";
        AccessibilityHint.IsVisible = !enabled;
    }

    private async void OnStartClicked(object? sender, RoutedEventArgs e)
    {
        // Fase 1 (prueba): pedir consentimiento y empezar a capturar (los frames se registran
        // en logcat). El envío por el hub se cablea en el siguiente incremento.
        MainActivity.Instance?.RequestScreenCapture();
        await StartAsync();
    }

    private async Task StartAsync()
    {
        try
        {
            StatusText.Text = "Conectando…";
            var deviceName = global::Android.OS.Build.Model ?? "Android";
            await _hub.ConnectAsync(ServerUrl, deviceName);
            SessionIdText.Text = FormatSessionId(_hub.SessionId);
        }
        catch (Exception ex)
        {
            StatusText.Text = "Error: " + ex.Message;
        }
    }

    // Formatea "123456789" -> "123 456 789" (igual que el cliente de escritorio).
    private static string FormatSessionId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return "— — —";
        }
        return string.Join(" ", Regex.Matches(id, ".{1,3}").Select(m => m.Value));
    }
}
