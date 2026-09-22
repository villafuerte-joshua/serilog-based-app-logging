using Serilog.Core;
using Serilog.Events;
using System.Reflection;

namespace ApplicationLogging.Enrichers
{
    internal class AppVersionEnricher : ILogEventEnricher
    {
        private static readonly string AppVersion = ResolveAppVersion();

        private static string ResolveAppVersion()
        {
            return Assembly.GetEntryAssembly()?
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion
                ?? "unknown";
        }

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("AppVersion", AppVersion));
        }
    }
}
