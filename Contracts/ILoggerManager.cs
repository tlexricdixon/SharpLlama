namespace Contracts;

public interface ILoggerManager
{
    void LogDebug(string message);
    void LogError(string message);
    void LogInfo(string message, params object[] args);
    void LogWarning(string message);
}