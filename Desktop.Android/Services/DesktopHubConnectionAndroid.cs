using System.Threading.Channels;
using MessagePack;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection; // AddMessagePackProtocol
using Pronetsys.Shared.Enums;                   // PromptForAccessResult
using Pronetsys.Shared.Helpers;                 // DtoChunker
using Pronetsys.Shared.Models;
using Pronetsys.Shared.Models.Dtos;             // ScreenDataDto, DtoType, DtoWrapper

namespace Pronetsys.Desktop.Android.Services;

/// <summary>
/// Conexión al DesktopHub del servidor Pronetsys (mismo protocolo que el cliente de escritorio:
/// SignalR + MessagePack). Fase 0: conectar y obtener el ID de sesión. Fase 1: al conectarse un
/// técnico (RequestScreenCast, modo atendido), enviar el DTO ScreenData y transmitir los frames
/// JPEG capturados por MediaProjection usando el mismo formato binario que <c>ScreenCaster</c>.
/// </summary>
public class DesktopHubConnectionAndroid : IAsyncDisposable
{
    // Debe coincidir con el troceado del cliente de escritorio (ScreenCaster / DtoChunker).
    private const int ChunkSize = 50_000;

    private HubConnection? _connection;

    // Puente evento->stream: el capturer empuja JPEGs aquí; el IAsyncEnumerable los consume.
    private Channel<byte[]>? _frameChannel;

    private string _deviceName = "Android";
    private int _captureWidth;
    private int _captureHeight;

    public string SessionId { get; private set; } = string.Empty;
    public event Action<string>? StatusChanged;

    /// <summary>True cuando MediaProjection ya está capturando (dimensiones conocidas).</summary>
    public bool IsCapturing => _captureWidth > 0 && _captureHeight > 0;

    public async Task ConnectAsync(string serverUrl, string deviceName)
    {
        _deviceName = string.IsNullOrWhiteSpace(deviceName) ? "Android" : deviceName;

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
        await _connection.InvokeAsync("SendAttendedSessionInfo", _deviceName);

        StatusChanged?.Invoke("Listo. Comparte el ID con tu técnico.");
    }

    /// <summary>Lo llama <see cref="MainActivity"/> cuando arranca la captura MediaProjection.</summary>
    public void SetCaptureInfo(int width, int height)
    {
        _captureWidth = width;
        _captureHeight = height;
    }

    /// <summary>Lo llama el capturer (evento FrameEncoded) por cada JPEG listo.</summary>
    public void PushFrame(byte[] jpeg)
    {
        // Si no hay stream activo, el frame se descarta silenciosamente.
        _frameChannel?.Writer.TryWrite(jpeg);
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
            _frameChannel?.Writer.TryComplete();
        });

        // Modo ATENDIDO (el nuestro): el técnico entró el ID -> el servidor pide el cast.
        // Fase 3 añadirá el diálogo de consentimiento antes de empezar a transmitir.
        _connection.On<string, string, bool, Guid>("RequestScreenCast",
            async (viewerId, requesterName, notifyUser, streamId) =>
            {
                await BeginScreenCastAsync(viewerId, requesterName, streamId);
            });

        // Modo desatendido (no aplica al agente atendido, pero se cablea por robustez).
        _connection.On<string, string, bool, Guid>("GetScreenCast",
            async (viewerId, requesterName, notifyUser, streamId) =>
            {
                await BeginScreenCastAsync(viewerId, requesterName, streamId);
            });

        // Fase 2: DTOs entrantes (eventos de entrada del técnico) -> aplicar por accesibilidad.
        _connection.On<byte[], string>("SendDtoToClient",
            (dtoWrapper, viewerConnectionId) =>
            {
                // TODO Fase 2: deserializar el DTO y ejecutar el gesto/tecla correspondiente.
            });

        _connection.On<string>("ViewerDisconnected", viewerId =>
        {
            StatusChanged?.Invoke("El técnico cerró la sesión.");
            _frameChannel?.Writer.TryComplete();
        });

        // Consentimiento atendido: el servidor invoca y ESPERA una respuesta.
        _connection.On<RemoteControlAccessRequest, PromptForAccessResult>("PromptForAccess",
            accessRequest =>
            {
                // TODO Fase 3: mostrar el diálogo real y devolver la decisión del usuario.
                return Task.FromResult(new PromptForAccessResult());
            });
    }

    // Envía ScreenData al viewer y luego transmite los frames hasta que la sesión termina.
    private async Task BeginScreenCastAsync(string viewerId, string requesterName, Guid streamId)
    {
        if (_connection is null)
        {
            return;
        }

        if (!IsCapturing)
        {
            StatusChanged?.Invoke(
                "El técnico se conectó, pero aún no compartes la pantalla. Toca \"Compartir pantalla\".");
            return;
        }

        var who = string.IsNullOrWhiteSpace(requesterName) ? "El técnico" : requesterName;
        StatusChanged?.Invoke($"{who} conectado. Transmitiendo pantalla…");

        // Canal nuevo por sesión; se descarta el frame más viejo para no acumular latencia.
        _frameChannel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(2)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

        // 1) ScreenData: el viewer dimensiona su lienzo con esto.
        var screenData = new ScreenDataDto
        {
            MachineName = _deviceName,
            DisplayNames = new[] { "Android" },
            SelectedDisplay = "Android",
            ScreenWidth = _captureWidth,
            ScreenHeight = _captureHeight
        };
        await SendDtoToViewerAsync(screenData, DtoType.ScreenData, viewerId);

        // 2) Stream de frames (bloquea hasta que el canal se completa: Disconnect/ViewerDisconnected).
        try
        {
            await _connection.SendAsync("SendDesktopStream", StreamFramesAsync(), streamId);
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke("Transmisión finalizada: " + ex.Message);
        }
    }

    // Serializa el DTO -> DtoWrapper (troceado) -> MessagePack -> SendDtoToViewer.
    private async Task SendDtoToViewerAsync<T>(T dto, DtoType dtoType, string viewerId)
    {
        if (_connection is null)
        {
            return;
        }

        foreach (var wrapper in DtoChunker.ChunkDto(dto, dtoType, chunkSize: ChunkSize))
        {
            var serialized = MessagePackSerializer.Serialize(wrapper);
            await _connection.SendAsync("SendDtoToViewer", serialized, viewerId);
        }
    }

    // Consume el canal de JPEGs y emite cada frame en el formato binario del ScreenCaster,
    // troceado en piezas de 50 000 bytes.
    private async IAsyncEnumerable<byte[]> StreamFramesAsync()
    {
        var channel = _frameChannel;
        if (channel is null)
        {
            yield break;
        }

        while (await channel.Reader.WaitToReadAsync())
        {
            while (channel.Reader.TryRead(out var jpeg))
            {
                foreach (var chunk in BuildFrameChunks(jpeg))
                {
                    yield return chunk;
                }
            }
        }
    }

    // [int32 jpegLen][int32 left][int32 top][int32 width][int32 height][int64 unixMs][jpeg]
    // (capturamos pantalla completa, así que left=top=0 y width/height = dimensiones de captura).
    private IEnumerable<byte[]> BuildFrameChunks(byte[] jpeg)
    {
        var ms = new MemoryStream();
        var writer = new BinaryWriter(ms);
        writer.Write(jpeg.Length);
        writer.Write(0);                // left
        writer.Write(0);                // top
        writer.Write(_captureWidth);    // width
        writer.Write(_captureHeight);   // height
        writer.Write(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        writer.Write(jpeg);
        writer.Flush();

        return ms.ToArray().Chunk(ChunkSize);
    }

    public async ValueTask DisposeAsync()
    {
        _frameChannel?.Writer.TryComplete();
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
        GC.SuppressFinalize(this);
    }
}
