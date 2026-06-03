using System;
using System.Text;

namespace TransferToolRPA.Services
{
    public class ObservableLoggerService : ILoggerService
    {
        private readonly StringBuilder _logBuilder = new StringBuilder();
        private readonly object _lockObject = new object();

        public event Action<string>? OnLogAdded;

        public string FullLog
        {
            get
            {
                lock (_lockObject)
                {
                    return _logBuilder.ToString();
                }
            }
        }

        public void Log(string message) => Append($"[INFO] {message}");
        public void LogSuccess(string message) => Append($"[SUCESSO] {message}");
        public void LogWarning(string message) => Append($"[AVISO] {message}");
        public void LogError(string message) => Append($"[ERRO] {message}");

        public void Clear()
        {
            lock (_lockObject)
            {
                _logBuilder.Clear();
            }
            OnLogAdded?.Invoke(string.Empty);
        }

        private void Append(string formattedMessage)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string line = $"[{timestamp}] {formattedMessage}\n";

            lock (_lockObject)
            {
                _logBuilder.Append(line);
            }

            OnLogAdded?.Invoke(line);
        }
    }
}
