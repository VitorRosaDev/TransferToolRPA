using System;
using System.Threading;
using System.Threading.Tasks;
using TransferToolRPA.Models;

namespace TransferToolRPA.Services
{
    public class PlaywrightAutomationService : IAutomationService
    {
        private const string CdpUrl = "http://127.0.0.1:9222";

        public async Task ExecutarAutomacaoAsync(
            TransferenciaPayload payload,
            IProgress<ProgressoAutomacao> progressReporter,
            CancellationToken cancellationToken)
        {
            var flow = new AtendeNetFlow(progressReporter, cancellationToken);
            var engine = new AutomationEngine(payload, progressReporter, cancellationToken, flow);
            await engine.ExecutarAsync();
        }

        /// <summary>
        /// Encerra o navegador conectado via CDP (porta 9222). Faz o close via CDP e, como
        /// garantia extra, se a porta continuar ativa, encerra o processo do navegador.
        /// </summary>
        public async Task FecharNavegadorAsync()
        {
            try
            {
                string? wsUrl = await ObterWebSocketUrlAsync();
                if (!string.IsNullOrEmpty(wsUrl))
                {
                    using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
                    var browser = await playwright.Chromium.ConnectOverCDPAsync(wsUrl);

                    // Envia Browser.close explicitamente: encerra o processo do Chrome,
                    // independentemente da semantica padrao do CloseAsync em conexoes CDP.
                    var cdp = await browser.NewBrowserCDPSessionAsync();
                    await cdp.SendAsync("Browser.close");

                    try { await browser.CloseAsync(); } catch { }
                }
            }
            catch
            {
                // Sem navegador ou falha na conexao: tenta o fallback abaixo.
            }

            await Task.Delay(800);

            if (await Porta9222AtivaAsync())
            {
                EncerrarProcessosDaPorta9222();
            }
        }

        private static async Task<string?> ObterWebSocketUrlAsync()
        {
            using var httpClient = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            string json = await httpClient.GetStringAsync($"{CdpUrl}/json/version");
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("webSocketDebuggerUrl", out var wsProp))
            {
                string? url = wsProp.GetString();
                if (!string.IsNullOrEmpty(url)) return url.Replace("localhost", "127.0.0.1");
            }
            return null;
        }

        private static async Task<bool> Porta9222AtivaAsync()
        {
            try
            {
                using var httpClient = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(2) };
                var resposta = await httpClient.GetAsync($"{CdpUrl}/json/version");
                return resposta.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Encerra os processos de navegador que usam a porta 9222. O PID e obtido via
        /// "netstat -ano" (ultima coluna das linhas com :9222), sem dependencias externas.
        /// </summary>
        private static void EncerrarProcessosDaPorta9222()
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c netstat -ano")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var processo = System.Diagnostics.Process.Start(psi);
                if (processo == null) return;

                string saida = processo.StandardOutput.ReadToEnd();
                processo.WaitForExit(3000);

                var pids = new System.Collections.Generic.HashSet<int>();

                foreach (var linha in saida.Split(new[] { "\n" }, StringSplitOptions.None))
                {
                    if (!linha.Contains(":9222")) continue;

                    var partes = linha.Trim().Split(new[] { " ", "\t" }, StringSplitOptions.RemoveEmptyEntries);
                    if (partes.Length > 0 && int.TryParse(partes[partes.Length - 1], out int pid) && pid > 0)
                    {
                        pids.Add(pid);
                    }
                }

                foreach (int pid in pids)
                {
                    try
                    {
                        System.Diagnostics.Process.GetProcessById(pid).Kill(entireProcessTree: true);
                    }
                    catch { }
                }
            }
            catch { }
        }
    }
}