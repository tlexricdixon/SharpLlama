using Microsoft.Extensions.Logging;
using SharpLlama.Contracts;


namespace SharpLlama.LoggerService
{
    public class LoggerManager(ILogger<LoggerManager> logger) : ILoggerManager
    {
        private readonly ILogger<LoggerManager> _logger = logger;

        public void LogDebug(string message) => _logger.LogDebug("{Message}", message);
        public void LogError(string message) => _logger.LogError("{Message}", message);
        public void LogInfo(string message, params object[] args) => _logger.LogInformation(message, args);
        public void LogWarning(string message) => _logger.LogWarning("{Message}", message);
    }
}
