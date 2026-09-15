using System.ComponentModel.DataAnnotations;

namespace ApplicationLogging
{
    public class LoggingOptions
    {
        [Required]
        public string ApplicationName { get; set; } = null!;
        public string LogDirectory { get; set; } = "logs";
    }
}
