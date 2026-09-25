using System;

namespace TransferToolRPA.Services
{
    public interface ILoggerService
    {
        event Action<LogEntry>? OnLogAdded;
        string FullLog { get; }
        void Log(string message);
        void LogSuccess(string message);
        void LogWarning(string message);
        void LogError(string message);
        void Clear();
    }
}
