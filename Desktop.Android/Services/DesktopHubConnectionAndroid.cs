using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection; // AddMessagePackProtocol
using Pronetsys.Shared.Enums;                   // PromptForAccessResult
using Pronetsys.Shared.Models;

namespace Pronetsys.Desktop.Android.Services;

/// <summary>
/// Conexión mínima al DesktopHub del servidor Pronetsys (mismo protocolo que el cliente
/// de escritorio: SignalR + MessagePack). Fase 0: conectar y obtener el ID de sesión.
/// Los métodos que el servidor invoca en el cliente (IDesktopHubClient) están como TODO
/// y se implementan en las fases 1-3 (captura, control, consentimiento).
/// </summary>
public class DesktopHubConnectionAndroid : IAsyncDisposable
{
    private HubConnection? _connection;

    public string SessionId { get; private set; } = string.Empty;
    public event Action<string>? StatusChanged;

    public async Task ConnectAsync(string serverUrl, string deviceName)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl($"{serverUrl.TrimEnd('/')}/hubs/desktop")
            .AddMessagePackProtocol()
            .WithAutomaticReconnect()
            .Build();

        RegisterClientHandlers();

        await _connection.StartAsync();
        StatusChanged?.Invoke("Conectado. Obteniendo ID de sesión…");

        // Métodos del servidor (DesktopHub) que usa el cliente de escritorio atendido.
        SessionId = await _connection.InvokeAsync<string>("GetSessionID");
        await _connection.InvokeAsync("SendAttendedSessionInfo", deviceName);

        StatusChanged?.Invoke("Listo. Comparte el ID con tu técnico.");
    }

    private void RegisterClientHandlers()
    {
        if (_connection is null)
        {
            return;
        }

        // ---- IDesktopHubClient: lo que el servidor invoca en este cliente ----

        _connection.On<string>("Disconnect", reason =>
        {
            StatusChanged?.Invoke($"Desconectado: {reason}");
        });

        // Fase 3: el técnico solicita ver la pantalla -> mostrar PromptForAccess y,
        // si el usuario acepta, empezar la captura (Fase 1).
        _connection.On<string, string, bool, Guid>("RequestScreenCast",
            (viewerId, requesterName, notifyUser, streamId) =>
            {
                // TODO Fase 3: mostrar diálogo de consentimiento; luego BeginScreenCasting.
            });

        _connection.On<string, string, bool, Guid>("GetScreenCast",
            (viewerId, requesterName, notifyUser, streamId) =>
            {
                // TODO Fase 1: iniciar MediaProjection + stream de frames al viewer.
            });

        // Fase 2: DTOs entrantes (eventos de entrada del técnico) -> aplicar por accesibilidad.
        _connection.On<byte[], string>("SendDtoToClient",
            (dtoWrapper, viewerConnectionId) =>
            {
                // TODO Fase 2: deserializar el DTO y ejecutar el gesto/tecla correspondiente.
            });

        _connection.On<string>("ViewerDisconnected", viewerId =>
        {
            // TODO: si no quedan viewers, detener la captura.
        });

        // Consentimiento atendido: el servidor invoca y ESPERA una respuesta.
        _connection.On<RemoteControlAccessRequest, PromptForAccessResult>("PromptForAccess",
            accessRequest =>
            {
                // TODO Fase 3: mostrar el diálogo real y devolver la decisión del usuario.
                return Task.FromResult(new PromptForAccessResult());
            });
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
        GC.SuppressFinalize(this);
    }
}
