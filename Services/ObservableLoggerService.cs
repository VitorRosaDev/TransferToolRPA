using System;
using System.IO;
using System.Text;
using TransferToolRPA.Models;

namespace TransferToolRPA.Services
{
    /// <summary>
    /// Logger do aplicativo: mantém o histórico em memória (para o console da UI) e o
    /// persiste em arquivo para rastreabilidade. O arquivo é diário, em
    /// %LOCALAPPDATA%\TransferToolRPA\Logs\TransferToolRPA-YYYY-MM-DD.log.
    /// A gravação em disco é best-effort: falhas de I/O nunca derrubam a automação.
    /// </summary>
    public class ObservableLoggerService : ILoggerService
    {
        private readonly StringBuilder _logBuilder = new StringBuilder();
        private readonly object _lockObject = new object();

        public event Action<LogEntry>? OnLogAdded;

        /// <summary>
        /// Caminho do arquivo de log persistido (nulo/vazio quando não foi possível
        /// criar o diretório de logs — nesse caso o app segue apenas em memória).
        /// </summary>
        public string? CaminhoArquivoLog { get; }

        public ObservableLoggerService(string? logDirectory = null)
        {
            CaminhoArquivoLog = ResolverCaminhoLog(logDirectory);

            RegistrarCabecalhoSessao();
        }

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

        public void Log(string message) => Append("INFO", message, NivelLog.Info);
        public void LogSuccess(string message) => Append("SUCESSO", message, NivelLog.Sucesso);
        public void LogWarning(string message) => Append("AVISO", message, NivelLog.Aviso);
        public void LogError(string message) => Append("ERRO", message, NivelLog.Erro);

        public void Clear()
        {
            lock (_lockObject)
            {
                _logBuilder.Clear();
            }

            OnLogAdded?.Invoke(new LogEntry(string.Empty, NivelLog.Info));
        }

        private void Append(string prefixo, string message, NivelLog nivel)
        {
            message = SanitizarParaLinhaUnica(message);

            string hora = DateTime.Now.ToString("HH:mm:ss");
            string line = $"[{hora}] [{prefixo}] {message}\n";

            lock (_lockObject)
            {
                _logBuilder.Append(line);
            }

            // O arquivo é diário, mas pode reunir mais de uma sessão — por isso a linha
            // persistida inclui a data completa (a do console mantém só a hora).
            PersistirLinha($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{prefixo}] {message}");

            OnLogAdded?.Invoke(new LogEntry(line, nivel));
        }

        /// <summary>
        /// Remove quebras de linha do texto para evitar que uma mensagem forje
        /// linhas falsas no log (log injection).
        /// </summary>
        private static string SanitizarParaLinhaUnica(string texto)
        {
            return texto
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }

        private void PersistirLinha(string linha)
        {
            if (string.IsNullOrEmpty(CaminhoArquivoLog)) return;

            try
            {
                File.AppendAllText(CaminhoArquivoLog, linha + Environment.NewLine);
            }
            catch
            {
                // Persistência é acessória: nunca deve interromper a automação.
            }
        }

        private void RegistrarCabecalhoSessao()
        {
            PersistirLinha(string.Empty);
            PersistirLinha($"===== Sessão iniciada em {DateTime.Now:yyyy-MM-dd HH:mm:ss} =====");
        }

        private static string? ResolverCaminhoLog(string? logDirectory)
        {
            string dir = string.IsNullOrWhiteSpace(logDirectory)
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "TransferToolRPA",
                    "Logs")
                : logDirectory;

            try
            {
                Directory.CreateDirectory(dir);
            }
            catch
            {
                return null;
            }

            return Path.Combine(dir, $"TransferToolRPA-{DateTime.Now:yyyy-MM-dd}.log");
        }
    }
}