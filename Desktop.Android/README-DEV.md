# Desktop.Android — cliente de soporte atendido para Android (Fase 0: scaffold)

Cabeza **Avalonia.Android** (single-view) del cliente de soporte atendido de Pronetsys.
Es el equivalente Android del cliente portátil `Pronetsys_Desktop.exe`: el usuario abre la
app, obtiene un **ID de sesión**, un técnico se conecta desde `/Viewer`, y el usuario
**acepta** la conexión. Se conecta al **`DesktopHub`** del servidor (no al `AgentHub`), así
que **no requiere cambios en el servidor**.

> ⚠️ **Estado: scaffold de Fase 0.** Este código define la estructura, los permisos y los
> esqueletos de los servicios Android. **Aún NO compila sin el toolchain de Android** y varios
> métodos son `TODO`. El objetivo de la Fase 0 es: *que arranque y conecte al `DesktopHub`.*

---

## 1. Requisitos del entorno de build (una sola vez)

Este proyecto es `net8.0-android`. En la máquina de desarrollo necesitas:

```powershell
# 1) Workload de Android para .NET
dotnet workload install android

# 2) JDK 17 (Microsoft OpenJDK) y Android SDK.
#    Lo más simple: instalar Visual Studio 2022 con la carga
#    ".NET Multi-platform App UI development" (trae JDK 17 + Android SDK + emulador),
#    o Android Studio (SDK + AVD) y apuntar las variables:
#      ANDROID_HOME / ANDROID_SDK_ROOT -> ...\Android\Sdk
#      JAVA_HOME -> ...\Microsoft\jdk-17...
```

Para **probar** necesitas un **emulador (AVD)** o un **dispositivo físico** con *Depuración USB*
activada (conectado por `adb`).

## 2. Compilar y desplegar

```powershell
# Compilar el APK (Debug)
dotnet build Desktop.Android/Desktop.Android.csproj -c Debug

# Compilar + firmar APK (Release) -> bin/Release/net8.0-android/*-Signed.apk
dotnet publish Desktop.Android/Desktop.Android.csproj -c Release

# Instalar en un dispositivo/emulador conectado
adb install -r bin/Release/net8.0-android/com.pronetsys.asistenciaremota-Signed.apk
```

> Este proyecto **no está agregado a `Pronetsys.sln`** a propósito, para no exigir el
> workload de Android a quienes solo compilan el servidor/escritorio. Ábrelo por separado
> o agrégalo a una solución aparte (`Pronetsys.Android.sln`).

## 3. Arquitectura y reúso

| Capa | Reúso |
|---|---|
| `Shared` (interfaces `IDesktopHubClient`/`IViewerHubClient`, DTOs, enums, MessagePack) | ✅ Referenciado directo |
| `Desktop.Shared` (ScreenCaster, Viewer, DesktopHubConnection de escritorio) | ❌ Hoy NO se referencia: arrastra `SkiaSharp.Views.Desktop.Common` + `Desktop.Native` (P/Invoke Win/Linux/mac). Ver §6. |
| `Desktop.UI` (ventanas Avalonia) | ❌ No aplica (Android es single-view). UI propia mínima aquí. |

Por eso, en la Fase 0 la app implementa su **propia** conexión mínima al `DesktopHub`
(`Services/DesktopHubConnectionAndroid.cs`) usando los contratos de `Shared`.

## 4. Estructura de archivos

```
Desktop.Android/
  Desktop.Android.csproj          # net8.0-android + Avalonia.Android + Shared
  Properties/AndroidManifest.xml  # permisos: MediaProjection, foreground service, accesibilidad, internet
  MainActivity.cs                 # AvaloniaMainActivity<App>
  App.axaml(.cs)                  # ISingleViewApplicationLifetime -> MainView
  Views/MainView.axaml(.cs)       # UI mínima: estado + ID de sesión + botón
  Services/
    DesktopHubConnectionAndroid.cs   # conexión SignalR mínima al /hubs/desktop  (Fase 0)
    AndroidScreenCapturer.cs         # MediaProjection + VirtualDisplay + ImageReader (Fase 1)
    ScreenCaptureForegroundService.cs# foreground service para MediaProjection (Fase 1)
    RemoteControlAccessibilityService.cs # input por gestos (Fase 2)
    AndroidSessionIndicator.cs       # notificación "sesión activa" (Fase 3)
  Startup/ServiceCollectionExtensions.cs # DI
```

## 5. Hoja de ruta (TODO por fase)

- **Fase 0 (este scaffold):** arrancar la app, conectar al `DesktopHub`, obtener y mostrar el
  **ID de sesión**. Sin captura ni control. → *Valida que el stack .NET/SignalR corre en Android.*
- **Fase 1 — Captura:** `AndroidScreenCapturer` con MediaProjection → frames JPEG (SkiaSharp) →
  enviar por el hub (protocolo `SendDtoToViewer`). Vista remota de solo lectura.
- **Fase 2 — Control:** `RemoteControlAccessibilityService` (`dispatchGesture`): tap, swipe,
  scroll; texto básico. El usuario habilita el servicio de accesibilidad una vez.
- **Fase 3 — UX atendida:** diálogo *PromptForAccess* ("¿Permitir que X controle tu equipo?"),
  foreground service + indicador de sesión, onboarding de accesibilidad.
- **Fase 4 — Pulido:** reconexión, ajuste de codificación/FPS (o H.264 vía `MediaCodec`),
  portapapeles, firma del APK, enlace de descarga en la página Descargas del servidor.

## 6. Reutilizar `Desktop.Shared` (opcional, más adelante)

Para reusar `ScreenCaster`/`Viewer` en vez de reimplementar el protocolo, hay que **desacoplar
`Desktop.Shared` del escritorio** sin romper los clientes actuales (¡están en producción!):
1. Multi-target `Desktop.Shared` a `net8.0;net8.0-android` (o extraer un `Desktop.Core` portátil).
2. Sustituir `SkiaSharp.Views.Desktop.Common` por SkiaSharp core / `SkiaSharp.Views.Android`.
3. Quitar la dependencia dura a `Desktop.Native` (sólo cargarla en los heads de escritorio).
4. Hacer `UiDispatcher` agnóstico del *lifetime* (single-view vs classic desktop).

Hasta hacer eso, **no referenciar `Desktop.Shared` desde Android** (romperá el build).

## 7. Limitaciones conocidas (Android)

- Control por **Accesibilidad = gestos**, no inyección de teclas de hardware; texto limitado.
- **MediaProjection pide permiso en cada sesión** (atendido). Algunos OEM exigen re-consentir.
- **Google Play restringe apps de Accesibilidad** → distribuir por sideload/MDM/empresarial.
- Rendimiento: codificación en el dispositivo; posible optimización a H.264 por hardware.
