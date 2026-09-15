using Serilog.Core;
using Serilog.Events;
using System.Diagnostics;

namespace ApplicationLogging
{
    internal class ActivityIdEnricher : ILogEventEnricher
    {
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            var activityId = Activity.Current?.Id ?? "-";
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ActivityId", activityId));
        }
    }
}
