using Android.App;
using Android.Content;
using Android.OS;

namespace Pronetsys.Desktop.Android.Services;

/// <summary>
/// Fase 1/3 — servicio en primer plano requerido para usar MediaProjection en Android 10+.
/// Muestra una notificación persistente mientras se comparte la pantalla, refleja el estado de la
/// sesión (en espera / técnico conectado) y ofrece una acción "Detener" para cortar la captura,
/// dándole al usuario un control claro y siempre visible.
/// </summary>
[Service(Exported = false, ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeMediaProjection)]
public class ScreenCaptureForegroundService : Service
{
    public const string StopAction = "com.pronetsys.asistenciaremota.STOP_SHARING";

    private const string ChannelId = "pronetsys_remote_support";
    private const int NotificationId = 4020;

    private static ScreenCaptureForegroundService? _instance;

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (intent?.Action == StopAction)
        {
            // El usuario tocó "Detener" en la notificación: cortar captura y bajar el servicio.
            MainActivity.Instance?.StopScreenCapture();
            StopForeground(StopForegroundFlags.Remove);
            StopSelf();
            return StartCommandResult.NotSticky;
        }

        _instance = this;
        StartForeground(NotificationId, BuildNotification("Sesión de soporte activa: listo para compartir."));
        return StartCommandResult.Sticky;
    }

    public override void OnDestroy()
    {
        _instance = null;
        base.OnDestroy();
    }

    /// <summary>Actualiza el texto de la notificación persistente (estado de la sesión).</summary>
    public static void UpdateStatus(string text)
    {
        var self = _instance;
        if (self is null)
        {
            return;
        }

        var mgr = (NotificationManager?)self.GetSystemService(NotificationService);
        mgr?.Notify(NotificationId, self.BuildNotification(text));
    }

    private Notification BuildNotification(string contentText)
    {
        var mgr = (NotificationManager)GetSystemService(NotificationService)!;
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channel = new NotificationChannel(ChannelId, "Soporte remoto", NotificationImportance.Low);
            mgr.CreateNotificationChannel(channel);
        }

        // Acción "Detener": reenvía un intent con StopAction a este mismo servicio.
        var stopIntent = new Intent(this, typeof(ScreenCaptureForegroundService));
        stopIntent.SetAction(StopAction);
        var flags = Build.VERSION.SdkInt >= BuildVersionCodes.S
            ? PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent
            : PendingIntentFlags.UpdateCurrent;
        var stopPending = PendingIntent.GetService(this, 0, stopIntent, flags);

        return new Notification.Builder(this, ChannelId)
            .SetContentTitle("Pronetsys Asistencia Remota")
            .SetContentText(contentText)
            .SetOngoing(true)
            .AddAction(new Notification.Action.Builder(null, "Detener", stopPending).Build())
            .Build();
    }
}
