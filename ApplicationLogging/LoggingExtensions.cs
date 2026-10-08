using ApplicationLogging.Enrichers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;

namespace ApplicationLogging
{
    public static class LoggingExtensions
    {
        /// <summary>
        /// Use application logging for host builders
        /// </summary>
        /// <param name="host"></param>
        /// <param name="configuration"></param>
        /// <returns></returns>
        public static IHostBuilder UseApplicationLogging(
            this IHostBuilder host,
            IConfiguration configuration)
        {
            host.ConfigureServices((context, services) =>
            {
                RegisterLoggingOptions(services, configuration);
            });

            host.UseSerilog((context, services, loggerConfiguration) =>
            {
                ConfigureBaseLogger(context.HostingEnvironment, loggerConfiguration, context.Configuration);

                var loggingOptions = GetLoggingOptions(configuration);
                InitializeLogDir(loggerConfiguration, loggingOptions, configuration);
            });

            return host;
        }

        /// <summary>
        /// Use application logging for host application builder
        /// </summary>
        /// <param name="host"></param>
        /// <param name="configuration"></param>
        /// <returns></returns>
        public static IHostApplicationBuilder UseApplicationLogging(
            this IHostApplicationBuilder host,
            IConfiguration configuration)
        {
            RegisterLoggingOptions(host.Services, configuration);

            var loggingOptions = configuration.GetSection("LoggingOptions").Get<LoggingOptions>() ?? new LoggingOptions();

            var loggerConfiguration = ConfigureBaseLogger(host.Environment, new LoggerConfiguration(), configuration);
            InitializeLogDir(loggerConfiguration, loggingOptions, configuration);

            Log.Logger = loggerConfiguration.CreateLogger();
            host.Services.AddSerilog(dispose: true);

            return host;
        }

        /// <summary>
        /// Helpers to check if any sinks are configured in the Serilog configuration section
        /// </summary>
        /// <param name="configuration"></param>
        /// <returns></returns>
        private static bool HasConfiguredSinks(IConfiguration configuration) => configuration.GetSection("Serilog:WriteTo").GetChildren().Any();

        /// <summary>
        /// Configure the base logger with common settings for all environments
        /// </summary>
        /// <param name="environment"></param>
        /// <param name="loggerConfiguration"></param>
        /// <param name="configuration"></param>
        /// <returns></returns>
        private static LoggerConfiguration ConfigureBaseLogger(IHostEnvironment environment, LoggerConfiguration loggerConfiguration, IConfiguration configuration)
        {
            if (environment.IsDevelopment())
            {
                loggerConfiguration
                    .MinimumLevel.Debug()
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Information);
            }
            else
            {
                loggerConfiguration
                    .MinimumLevel.Information()
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                    .MinimumLevel.Override("System", LogEventLevel.Warning)
                    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning);
            }
            loggerConfiguration
                .ReadFrom.Configuration(configuration)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithEnvironmentName()
                .Enrich.With<ActivityIdEnricher>()
                .Enrich.With<AppVersionEnricher>();

            if (!HasConfiguredSinks(configuration))
            {
                loggerConfiguration.WriteTo.Console(new ActivityPrefixedConsoleFormatter());
            }

            return loggerConfiguration;
        }

        /// <summary>
        /// Initialize the log directory and configure file logging if a log directory is specified in the logging options
        /// </summary>
        /// <param name="loggerConfiguration"></param>
        /// <param name="loggingOptions"></param>
        private static void InitializeLogDir(LoggerConfiguration loggerConfiguration, LoggingOptions loggingOptions, IConfiguration configuration)
        {
            if (HasConfiguredSinks(configuration) || string.IsNullOrEmpty(loggingOptions.LogDirectory))
                return;

            loggerConfiguration.WriteTo.File(
                new JsonFormatter(),
                Path.Combine(loggingOptions.LogDirectory, $"{loggingOptions.ApplicationName}-log-.json"),
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true);
        }
        /// <summary>
        /// Register the logging options in the service collection and bind them to the configuration section "LoggingOptions"
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        private static void RegisterLoggingOptions(IServiceCollection services, IConfiguration configuration)
        {
            services.AddOptions<LoggingOptions>()
                .Bind(configuration.GetSection("LoggingOptions"))
                .ValidateDataAnnotations()
                .ValidateOnStart();
        }
        /// <summary>
        /// Get the logging options from the configuration section "LoggingOptions" or return a new instance of LoggingOptions if not found
        /// </summary>
        /// <param name="configuration"></param>
        /// <returns></returns>
        private static LoggingOptions GetLoggingOptions(IConfiguration configuration)
        {
            return configuration.GetSection("LoggingOptions").Get<LoggingOptions>() ?? new LoggingOptions();
        }
    }
}
