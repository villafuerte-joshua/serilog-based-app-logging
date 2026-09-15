# Yuehanxin.ApplicationLogging

Shared Serilog-based application logging setup for `IHostBuilder` and `IHostApplicationBuilder`.

## Installation

```bash
dotnet add package Yuehanxin.ApplicationLogging
```

## Usage

```csharp
// IHostBuilder (generic host)
host.UseApplicationLogging(configuration);

// IHostApplicationBuilder (minimal hosting model)
builder.UseApplicationLogging(configuration);
```

## Configuration

Add a `LoggingOptions` section to your `appsettings.json`:

```json
{
  "LoggingOptions": {
    "ApplicationName": "MyApp",
    "LogDirectory": "logs"
  }
}
```