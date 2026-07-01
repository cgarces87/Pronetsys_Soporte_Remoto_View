using Android.App;
using Android.Content;
using Android.OS;

namespace Pronetsys.Desktop.Android.Services;

/// <summary>
/// Fase 1/3 — servicio en primer plano requerido para usar MediaProjection en Android 10+.
/// Muestra una notificación persistente ("sesión de soporte activa") mientras se comparte la
/// pantalla, y le da al usuario un control claro para detenerla.
/// </summary>
[Service(Exported = false, ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeMediaProjection)]
public class ScreenCaptureForegroundService : Service
{
    private const string ChannelId = "pronetsys_remote_support";
    private const int NotificationId = 4020;

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        StartForeground(NotificationId, BuildNotification());
        return StartCommandResult.Sticky;
    }

    private Notification BuildNotification()
    {
        var mgr = (NotificationManager)GetSystemService(NotificationService)!;
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channel = new NotificationChannel(ChannelId, "Soporte remoto", NotificationImportance.Low);
            mgr.CreateNotificationChannel(channel);
        }

        return new Notification.Builder(this, ChannelId)
            .SetContentTitle("Pronetsys Asistencia Remota")
            .SetContentText("Sesión de soporte activa: tu pantalla se está compartiendo.")
            .SetOngoing(true)
            .Build();

        // TODO Fase 3: agregar una acción "Detener" que finalice la captura y el servicio.
    }
}
