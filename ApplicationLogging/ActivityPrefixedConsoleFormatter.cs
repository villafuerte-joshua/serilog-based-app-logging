using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Display;

namespace ApplicationLogging
{
    internal class ActivityPrefixedConsoleFormatter : ITextFormatter
    {
        private const string LineTemplate = "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {ActivityId} {Message:lj}";
        private readonly MessageTemplateTextFormatter lineFormatter = new(LineTemplate);

        public void Format(LogEvent logEvent, TextWriter output)
        {
            lineFormatter.Format(logEvent, output);
            output.Write(Environment.NewLine);

            if (logEvent.Exception is null)
            {
                return;
            }

            var activityId = GetActivityId(logEvent);
            var exceptionLines = logEvent.Exception.ToString().Split(Environment.NewLine);

            foreach (var line in exceptionLines)
            {
                output.Write($"[{activityId}] {line}");
                output.Write(Environment.NewLine);
            }
        }

        private static string GetActivityId(LogEvent logEvent)
        {
            if (logEvent.Properties.TryGetValue("ActivityId", out var value) &&
                value is ScalarValue { Value: string activityId })
            {
                return activityId;
            }

            return "-";
        }
    }
}
