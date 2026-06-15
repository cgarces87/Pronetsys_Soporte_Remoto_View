# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Remotely is a remote control + remote scripting solution built with .NET 8, Blazor Server, and SignalR Core. It has three runtime roles that communicate over SignalR: a **Server** (web app), an **Agent** (background service on managed devices), and a **Desktop** client (screen-casting app used for the actual remote-control session).

## Common commands

Run from the repo root unless noted. Solution file: `Remotely.sln`.

```bash
dotnet build Remotely.sln                 # Build everything
dotnet test                               # Run all test projects (this is what CI runs)
dotnet run --project Server               # Run the server (dev); agents connect to https://localhost:5001
```

Tests use **MSTest** (+ Moq + EF Core InMemory). To narrow scope:

```bash
dotnet test Tests/Server.Tests/Server.Tests.csproj          # One project
dotnet test --filter "FullyQualifiedName~AgentHubTests"     # One class
dotnet test --filter "Name=MethodName"                      # One method
```

EF Core migrations (PowerShell helpers in `Utilities/` — they apply to **all three** DB providers at once, which is required, see below):

```powershell
Utilities/Add-Migration.ps1 -MigrationName <Name>   # adds to Sqlite + SqlServer + PostgreSql contexts
Utilities/Update-Database.ps1
Utilities/Remove-Migration.ps1
```

Publishing clients/server: `Utilities/Publish.ps1` builds the Agent for win-x64/x86, linux-x64, osx-x64/arm64, builds the desktop clients, zips them into `Server/wwwroot/Content/`, and (with `-RID <rid> -OutDir <path>`) publishes the server.

## Development setup notes (from README)

- In **Development** environment: the Agent connects to `https://localhost:5001` using a pre-defined device ID, and the server assigns every connecting agent to the **first organization**. This lets you debug Server + Agent + Desktop together and see your device in the list.
- Visual Studio multi-project launch configs are in `Remotely.slnLaunch` / `Remotely.sln.startup.json` (e.g. "Server+Agent+Desktop", "Server+Agent").
- The Server project compiles TypeScript (`Microsoft.TypeScript.MSBuild`) and restores client-side JS libs via LibMan (`Server/libman.json`). The Shared project generates `.d.ts` files from select C# entities (DtsGenerator) so the TS code can share those types.

## Architecture

### Projects and their roles

- **Server** (`Microsoft.NET.Sdk.Web`, assembly `Remotely_Server`) — ASP.NET Core Blazor Server app. Hosts the SignalR hubs, the REST API (`Server/API/`), EF Core data layer, ASP.NET Identity auth, and the Razor/Blazor UI (`Server/Components/`, `Server/Pages/`). Minimal-hosting entry point is `Server/Program.cs`.
- **Agent** (`Remotely_Agent`) — background service (Windows Service / systemd) installed on managed devices. Maintains a SignalR connection to the server, executes scripts (embeds the PowerShell SDK), and launches the Desktop client when a remote-control session starts. Key service: `Agent/Services/AgentHubConnection.cs`.
- **Shared** — the contract layer referenced by nearly everything: EF entities (`Shared/Entities/`), DTOs, enums, and the **SignalR hub client interfaces** (`Shared/Interfaces/IAgentHubClient`, `IDesktopHubClient`, `IViewerHubClient`). Uses MessagePack for hub serialization.
- **Desktop.Shared** — the active cross-platform core of the desktop/remote-control client: screen capture, input injection, chat, and the SignalR connection to the desktop hub (`Desktop.Shared/Services/`).
- **Desktop.UI** — cross-platform **Avalonia 11** UI for the desktop client.
- **Desktop.Win** (`net8.0-windows`, WinExe, assembly `Remotely_Desktop`) — Windows entry point. Uses SharpDX (DXGI/Direct3D11) for screen capture and NAudio for audio. Its post-build step copies the output into `Agent/bin/.../Desktop/` so the Agent ships with the desktop client.
- **Desktop.Linux** — Linux entry point (Avalonia).
- **Desktop.Native** — P/Invoke native interop split into `Windows/` and `Linux/`.
- **Tests** — `Server.Tests` (MSTest), `Shared.Tests`, `Desktop.Win.Tests`, and `LoadTester` (a console load-testing tool).
- **Utilities** — PowerShell scripts for migrations, publishing, and command-list generation; bundled `signtool.exe`.

> **Desktop client pipeline:** `Desktop.Shared` (cross-platform core: capture, input, SignalR) → `Desktop.UI` (Avalonia UI) → `Desktop.Win` / `Desktop.Linux` (per-OS entry points), with `Desktop.Native` providing the P/Invoke layer. Put desktop changes in `Desktop.Shared`/`Desktop.UI`; only touch a per-OS head (`Desktop.Win`/`Desktop.Linux`) when the change is genuinely OS-specific (native capture/input). The porting surface for a new OS is the set of abstractions in `Desktop.Shared/Abstractions/` (e.g. `IScreenCapturer`, `IKeyboardMouseInput`, `IAudioCapturer`, `IClipboardService`), each implemented per platform and registered in that platform's `Startup/IServiceCollectionExtensions.cs`.

### SignalR communication (the core of the system)

Three hubs are mapped in `Server/Program.cs`, each with a strongly-typed client interface in `Shared/Interfaces/`:

| Hub (server-side) | Route | Connects | Client interface |
|---|---|---|---|
| `AgentHub` | `/hubs/service` | Server ↔ Agent | `IAgentHubClient` |
| `DesktopHub` | `/hubs/desktop` | Server ↔ Desktop client (screen caster) | `IDesktopHubClient` |
| `ViewerHub` | `/hubs/viewer` | Server ↔ browser viewer (the technician in the web UI) | `IViewerHubClient` |

A remote-control session flows: the browser **Viewer** asks the server, the server tells the **Agent** (over AgentHub) to launch the **Desktop** client, and the Desktop client streams its screen back through the server to the Viewer. When changing a hub method, update both the hub class in `Server/Hubs/` and the corresponding interface in `Shared/Interfaces/` so the contract stays in sync.

Server-side connection/session state lives in singleton caches (`AgentHubSessionCache`, `RemoteControlSessionCache`, `DesktopStreamCache`) registered in `Program.cs`.

### Data layer & configuration

- EF Core with **three interchangeable DB providers** selected at runtime by `ApplicationOptions.DbProvider` (`SQLite` default, `SQLServer`, `PostgreSQL`). Each has its own DbContext (`Server/Data/SqliteDbContext.cs`, etc., all deriving from `AppDb`) and its own migrations folder under `Server/Migrations/{Sqlite,SqlServer,PostgreSql}`. **Any schema change must be migrated for all three** — this is why the migration scripts run `dotnet ef` three times.
- Migrations are applied automatically at startup (`appDb.Database.MigrateAsync()` in `Program.cs`).
- **Only the DB provider, connection strings, and ASP.NET port are configured via environment variables** (prefixed `Remotely_`, e.g. `Remotely_ApplicationOptions__DbProvider`, `Remotely_ConnectionStrings__SQLite`; port via `ASPNETCORE_HTTP_PORTS`). **All other settings** (CORS origins, known proxies, 2FA enforcement, data retention, SMTP, theme, etc.) are stored in the database and edited at runtime on the **Server Config** page — read in code via the app settings (`SettingsModel`), not appsettings.json.

### Multi-tenancy & auth

Data is grouped into **Organizations** (users, devices, scripts). The first registered account becomes both server admin and org admin; afterwards self-registration is disabled unless `MaxOrganizationCount` is raised. Authorization uses custom policies/handlers in `Server/Auth/` (`TwoFactorRequired`, `OrganizationAdminRequired`, `ServerAdminRequired`). The REST API authenticates via an `X-Api-Key` header (`[ApiKeyId]:[ApiSecret]`); API surface is browsable at `/swagger`.

### Conventions

- `Nullable` is enabled solution-wide (`Directory.Build.props`); `ImplicitUsings` is on for the Server and several Desktop projects (see `Usings.cs` / `GlobalUsings.cs` for the global using sets).
- Root namespaces follow `Remotely.<ProjectName>`.
