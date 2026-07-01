using Android.AccessibilityServices;
using Android.Graphics;
using Android.Views.Accessibility;

namespace Pronetsys.Desktop.Android.Services;

/// <summary>
/// Fase 2 — inyección de entrada. Android no permite inyectar toques a otras apps salvo con
/// un Servicio de Accesibilidad. Aquí se traducen los eventos de entrada del técnico (que
/// llegan por el hub) a gestos con <c>DispatchGesture</c>. El usuario debe habilitar este
/// servicio una vez en Ajustes → Accesibilidad.
/// </summary>
public class RemoteControlAccessibilityService : AccessibilityService
{
    public static RemoteControlAccessibilityService? Instance { get; private set; }

    protected override void OnServiceConnected()
    {
        base.OnServiceConnected();
        Instance = this;
    }

    // No consumimos eventos de accesibilidad; solo inyectamos gestos.
    public override void OnAccessibilityEvent(AccessibilityEvent? e) { }
    public override void OnInterrupt() { }

    /// <summary>Toque simple en coordenadas de pantalla.</summary>
    public void Tap(float x, float y)
    {
        using var path = new Path();
        path.MoveTo(x, y);
        var gesture = new GestureDescription.Builder()
            .AddStroke(new GestureDescription.StrokeDescription(path, 0, 50))
            .Build();
        DispatchGesture(gesture, callback: null, handler: null);
    }

    /// <summary>Deslizar/scroll de (x1,y1) a (x2,y2) en <paramref name="durationMs"/>.</summary>
    public void Swipe(float x1, float y1, float x2, float y2, long durationMs)
    {
        using var path = new Path();
        path.MoveTo(x1, y1);
        path.LineTo(x2, y2);
        var gesture = new GestureDescription.Builder()
            .AddStroke(new GestureDescription.StrokeDescription(path, 0, Math.Max(1, durationMs)))
            .Build();
        DispatchGesture(gesture, callback: null, handler: null);
    }

    // TODO Fase 2: LongPress, texto (via nodo enfocado / ACTION_SET_TEXT), botones Atrás/Inicio
    // (PerformGlobalAction(GlobalActionBack / GlobalActionHome)).
}
