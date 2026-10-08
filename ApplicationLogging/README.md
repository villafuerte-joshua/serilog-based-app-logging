# Yuehanxin.ApplicationLogging

Set up Serilog for .NET host apps with one call. It works with `IHostBuilder` and `IHostApplicationBuilder`, including `WebApplicationBuilder`.

Out of the box you get:

- Console logging, with exception stack traces tagged by activity ID
- Rolling JSON file logs, one file per day
- Minimum levels that differ between `Development` and other environments
- Extra log properties: `ActivityId`, `AppVersion`, `MachineName` and `EnvironmentName`
- Full override through the standard `Serilog` configuration section

## Requirements

- .NET 10 (`net10.0`)

## Installation

```bash
dotnet add package Yuehanxin.ApplicationLogging
```

Or add it to your project file:

```xml
<PackageReference Include="Yuehanxin.ApplicationLogging" Version="x.y.z" />
```

## Quick start

### 1. Add `LoggingOptions` to `appsettings.json`

```json
{
  "LoggingOptions": {
    "ApplicationName": "MyApp",
    "LogDirectory": "logs"
  }
}
```

### 2. Register the logger

**Minimal hosting (`WebApplication.CreateBuilder` / `Host.CreateApplicationBuilder`)**

```csharp
using ApplicationLogging;

var builder = WebApplication.CreateBuilder(args);

builder.UseApplicationLogging(builder.Configuration);

var app = builder.Build();
app.Run();
```

**Generic host (`Host.CreateDefaultBuilder`)**

```csharp
using ApplicationLogging;

var host = Host.CreateDefaultBuilder(args)
    .UseApplicationLogging()
    .Build();

await host.RunAsync();
```

With no arguments, settings are read from the host's own configuration: `appsettings.json`, `appsettings.{Environment}.json`, environment variables, command-line arguments and so on.

To read settings from a different configuration, pass it in. `LoggingOptions` and the `Serilog` section are then read only from that configuration, and the host's configuration is not used for logging:

```csharp
var host = Host.CreateDefaultBuilder(args)
    .UseApplicationLogging(myConfiguration)
    .Build();
```

### 3. Write logs

Use `ILogger<T>` from dependency injection as usual:

```csharp
public class OrderService(ILogger<OrderService> logger)
{
    public void Place(int orderId)
    {
        logger.LogInformation("Placing order {OrderId}", orderId);
    }
}
```

The static `Serilog.Log` logger is configured too, so `Log.Information(...)` also works.

## Configuration

### `LoggingOptions`

| Key | Required | Default | Description |
| --- | --- | --- | --- |
| `ApplicationName` | Yes | none | Used in the log file name. It is validated at startup, so the host will not start if it is missing. |
| `LogDirectory` | No | `logs` | Folder for the rolling JSON log files. A relative path is resolved against the process working directory. Set it to `""` to turn off file logging. |

The options are also registered with the options system, so you can inject `IOptions<LoggingOptions>` if you need them.

### Minimum levels

| Source | `Development` | Other environments |
| --- | --- | --- |
| Default | `Debug` | `Information` |
| `Microsoft.*` | `Information` | `Information` |
| `System.*` | (default) | `Warning` |
| `Microsoft.EntityFrameworkCore.Database.Command` | `Information` | `Warning` |

These are applied before the `Serilog` configuration section is read, so `Serilog:MinimumLevel` overrides them:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning"
      }
    }
  }
}
```

## Default sinks

When `Serilog:WriteTo` is **not** configured, two sinks are added for you.

### Console

Output template:

```text
[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {ActivityId} {Message:lj}
```

Each line of an exception stack trace is prefixed with the activity ID, so you can match traces to the request that caused them, even when requests overlap:

```text
[2026-10-08 14:03:11 INF] 00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01 Placing order 42
[2026-10-08 14:03:11 ERR] 00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01 Failed to place order 42
[00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01] System.InvalidOperationException: Out of stock
[00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01]    at OrderService.Place(Int32 orderId) ...
```

### Rolling JSON file

- Path: `{LogDirectory}/{ApplicationName}-log-{yyyyMMdd}.json`, for example `logs/MyApp-log-20261008.json`
- Format: Serilog `JsonFormatter`, with every property on each event
- Rolls daily, and also when a file reaches 1 GB (`MyApp-log-20261008_001.json`)
- Keeps the 31 most recent files (Serilog's default)

## Overriding the sinks

If `Serilog:WriteTo` has **any** entry, both default sinks are turned off and only your sinks are used. You do not get one default sink plus yours: if you still want the console, list it yourself.

The minimum levels and all enrichers below still apply.

The `Console` and `File` sinks come with this package, so you can use them without installing anything else:

```json
{
  "LoggingOptions": {
    "ApplicationName": "MyApp"
  },
  "Serilog": {
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {AppVersion} {ActivityId} {SourceContext}: {Message:lj}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/myapp-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 14,
          "outputTemplate": "{Timestamp:o} [{Level:u3}] [{EnvironmentName}@{MachineName}] v{AppVersion} {ActivityId} {SourceContext}: {Message:lj}{NewLine}{Exception}"
        }
      }
    ]
  }
}
```

For structured JSON files, use a formatter instead of `outputTemplate`. Every enriched property is written automatically:

```json
{
  "Name": "File",
  "Args": {
    "path": "logs/myapp-.json",
    "rollingInterval": "Day",
    "formatter": "Serilog.Formatting.Compact.CompactJsonFormatter, Serilog.Formatting.Compact"
  }
}
```

To use other sinks (Seq, Application Insights, Elasticsearch and so on), install the sink package and add it to `WriteTo`.

## Properties you can use in `outputTemplate`

### Added by this library

| Property | Value | Example |
| --- | --- | --- |
| `{ActivityId}` | `Activity.Current.Id`: the W3C trace ID of the current request or operation. `-` when there is no current activity. | `00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01` |
| `{AppVersion}` | `AssemblyInformationalVersion` of your app's entry assembly. `unknown` if it cannot be read. | `1.4.2+9f1c2ab` |
| `{MachineName}` | `Environment.MachineName` | `WEB-01` |
| `{EnvironmentName}` | The `ASPNETCORE_ENVIRONMENT` variable, else `DOTNET_ENVIRONMENT`, else `Production` | `Staging` |

Notes:

- `AppVersion` comes from the `<Version>` (or `<InformationalVersion>`) of **your** application, not of this package. The .NET SDK appends the git commit (`+9f1c2ab...`) by default. To leave it out, add `<IncludeSourceRevisionInInformationalVersion>false</IncludeSourceRevisionInInformationalVersion>` to your app's project file.
- `EnvironmentName` is read from environment variables only. If you set the environment another way (for example `--environment Staging` on the command line), this value can differ from `IHostEnvironment.EnvironmentName`.
- The library adds these properties only when the event does not already have them, so a value you push yourself takes priority.

### Built-in Serilog and ASP.NET Core properties

| Property | Description |
| --- | --- |
| `{Timestamp}` | Event time, for example `{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}` |
| `{Level}` | Log level, for example `{Level:u3}` gives `INF` |
| `{Message}` | Rendered message. `{Message:lj}` prints strings without quotes and objects as JSON. |
| `{Exception}` | Exception and stack trace (empty if there is none) |
| `{NewLine}` | Line break |
| `{Properties}` | All properties not already used in the template |
| `{SourceContext}` | Logger category, for example the `T` in `ILogger<T>` |
| `{EventId}` | `EventId` passed to `ILogger` |
| `{RequestId}`, `{RequestPath}`, `{ConnectionId}` | Added by ASP.NET Core inside a request scope |

### Your own properties

`Enrich.FromLogContext()` is turned on, so properties from logging scopes and `LogContext` can be used in templates too:

```csharp
using (logger.BeginScope(new Dictionary<string, object> { ["TenantId"] = tenantId }))
{
    logger.LogInformation("Processing batch");
}

// or, with Serilog directly
using (Serilog.Context.LogContext.PushProperty("TenantId", tenantId))
{
    Log.Information("Processing batch");
}
```

```text
"outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {TenantId} {Message:lj}{NewLine}{Exception}"
```

You can also add static properties through configuration:

```json
{
  "Serilog": {
    "Properties": {
      "Team": "Payments"
    }
  }
}
```

## Notes

- With `IHostApplicationBuilder`, the logger is created as soon as `UseApplicationLogging` runs. Call it after you add any configuration sources it needs to read.
- The logger is disposed and flushed when the host shuts down.

## Changelog

See [CHANGELOG.md](https://github.com/villafuerte-joshua/serilog-based-app-logging/blob/main/CHANGELOG.md). The changelog is also included in the package.

## License

MIT
