using MessagePack;
using Pronetsys.Shared.Helpers;      // DtoChunker
using Pronetsys.Shared.Models.Dtos;  // DtoWrapper, DtoType, *Dto

namespace Pronetsys.Desktop.Android.Services;

/// <summary>
/// Fase 2 — traduce los DTOs de entrada del técnico (que llegan por el DesktopHub en
/// <c>SendDtoToClient</c>) a gestos del <see cref="RemoteControlAccessibilityService"/>.
///
/// El visor envía coordenadas en porcentaje (0..1) de la pantalla. Un click del técnico llega
/// como <c>MouseDown</c>+<c>MouseUp</c>: si no hubo desplazamiento es un toque; si lo hubo, es un
/// arrastre/deslizamiento. La rueda del ratón se traduce a un scroll vertical.
///
/// Android no permite inyectar pulsaciones de teclado a otras apps, así que texto/teclas quedan
/// fuera de alcance (se podría hacer texto vía nodo enfocado + ACTION_SET_TEXT en un incremento
/// posterior). Requiere que el usuario habilite el servicio de accesibilidad en Ajustes.
/// </summary>
public class AndroidInputHandler
{
    // Umbral (px) por debajo del cual un down->up se considera toque y no arrastre.
    private const double TapThresholdPx = 15.0;

    private int _width;
    private int _height;

    // Estado del gesto en curso (entre MouseDown y MouseUp).
    private double _downX, _downY;
    private long _downAtMs;
    private bool _isDown;

    public void SetScreenSize(int width, int height)
    {
        _width = width;
        _height = height;
    }

    public void Handle(byte[] message)
    {
        var svc = RemoteControlAccessibilityService.Instance;
        if (svc is null || _width == 0 || _height == 0)
        {
            // El servicio de accesibilidad aún no está habilitado: ignorar la entrada.
            return;
        }

        DtoWrapper wrapper;
        try
        {
            wrapper = MessagePackSerializer.Deserialize<DtoWrapper>(message);
        }
        catch
        {
            return;
        }

        switch (wrapper.DtoType)
        {
            case DtoType.Tap:
                if (DtoChunker.TryComplete<TapDto>(wrapper, out var tap) && tap is not null)
                {
                    svc.Tap(ToX(tap.PercentX), ToY(tap.PercentY));
                }
                break;

            case DtoType.MouseDown:
                if (DtoChunker.TryComplete<MouseDownDto>(wrapper, out var down) && down is not null)
                {
                    _downX = down.PercentX;
                    _downY = down.PercentY;
                    _downAtMs = NowMs();
                    _isDown = true;
                }
                break;

            case DtoType.MouseUp:
                if (DtoChunker.TryComplete<MouseUpDto>(wrapper, out var up) && up is not null)
                {
                    HandleMouseUp(svc, up.PercentX, up.PercentY);
                }
                break;

            case DtoType.MouseWheel:
                if (DtoChunker.TryComplete<MouseWheelDto>(wrapper, out var wheel) && wheel is not null)
                {
                    Scroll(svc, wheel.DeltaY);
                }
                break;

            // MouseMove entre down y up no se aplica: el arrastre se resuelve en MouseUp con los
            // extremos (Android no expone un "mover el puntero" fuera de un gesto continuo).
            default:
                break;
        }
    }

    private void HandleMouseUp(RemoteControlAccessibilityService svc, double upPercentX, double upPercentY)
    {
        if (!_isDown)
        {
            // Up sin down previo: tratarlo como toque simple.
            svc.Tap(ToX(upPercentX), ToY(upPercentY));
            return;
        }

        _isDown = false;

        var dxPx = Math.Abs(upPercentX - _downX) * _width;
        var dyPx = Math.Abs(upPercentY - _downY) * _height;
        var distance = Math.Sqrt(dxPx * dxPx + dyPx * dyPx);

        if (distance < TapThresholdPx)
        {
            svc.Tap(ToX(upPercentX), ToY(upPercentY));
        }
        else
        {
            var durationMs = Math.Clamp(NowMs() - _downAtMs, 50, 2000);
            svc.Swipe(ToX(_downX), ToY(_downY), ToX(upPercentX), ToY(upPercentY), durationMs);
        }
    }

    private void Scroll(RemoteControlAccessibilityService svc, double deltaY)
    {
        if (deltaY == 0)
        {
            return;
        }

        // Deslizamiento vertical desde el centro. Rueda hacia abajo (deltaY>0) -> el dedo sube
        // (el contenido baja), como en un scroll táctil natural.
        var cx = _width / 2f;
        var cy = _height / 2f;
        var amount = _height * 0.25f;
        var endY = cy - Math.Sign(deltaY) * amount;
        svc.Swipe(cx, cy, cx, endY, 200);
    }

    private float ToX(double percentX) => (float)(percentX * _width);
    private float ToY(double percentY) => (float)(percentY * _height);
    private static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
