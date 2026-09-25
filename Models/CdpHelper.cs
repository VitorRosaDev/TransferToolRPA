using System;
using System.Threading.Tasks;

namespace TransferToolRPA.Models
{
    /// <summary>
    /// Constantes e utilitários de conexão com o Chrome/Edge via CDP
    /// (Chrome DevTools Protocol). Centraliza a porta de depuração e a descoberta
    /// do WebSocket, eliminando a duplicação entre o motor de automação e o
    /// serviço de encerramento do navegador.
    /// </summary>
    public static class CdpHelper
    {
        public const int PortaDepuracao = 9222;
        public const string CdpUrl = "http://127.0.0.1:9222";

        /// <summary>
        /// Consulta a URL do WebSocket de depuração em <c>/json/version</c>.
        /// Retorna nulo quando o navegador não está acessível na porta de depuração.
        /// </summary>
        public static async Task<string?> ObterWebSocketUrlAsync()
        {
            try
            {
                using var httpClient = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(ConfiguracaoAutomacao.TimeoutHttpCdpSegundos) };
                string json = await httpClient.GetStringAsync($"{CdpUrl}/json/version");
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("webSocketDebuggerUrl", out var wsProp))
                {
                    string? url = wsProp.GetString();
                    if (!string.IsNullOrEmpty(url))
                    {
                        return url.Replace("localhost", "127.0.0.1");
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
