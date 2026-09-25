using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace TransferToolRPA.Models
{
    public class AutomationEngine
    {
        private readonly TransferenciaPayload _payload;
        private readonly IProgress<(string Mensagem, double Progresso)> _progressReporter;
        private readonly CancellationToken _cancellationToken;
        private readonly IAtendeNetFlow _flow;

        public AutomationEngine(
            TransferenciaPayload payload,
            IProgress<(string Mensagem, double Progresso)> progressReporter,
            CancellationToken cancellationToken,
            IAtendeNetFlow flow)
        {
            _payload = payload;
            _progressReporter = progressReporter;
            _cancellationToken = cancellationToken;
            _flow = flow;
        }

        public async Task ExecutarAsync()
        {
            // Garante que driver (node.exe) e browsers sejam resolvidos a partir do diretório
            // da aplicação. Idempotente: normalmente já foi aplicado em App.OnStartup.
            PlaywrightPathResolver.Configure();

            using var playwright = await Playwright.CreateAsync();

            Report("Conectando ao navegador Chrome/Edge ativo (porta 9222)...", 5);

            string cdpUrl = "http://127.0.0.1:9222";
            string? wsUrl = null;
            bool conectado = false;

            try
            {
                wsUrl = await ObterWebSocketUrlAsync(cdpUrl);
                conectado = !string.IsNullOrEmpty(wsUrl);
            }
            catch
            {
            }

            if (!conectado)
            {
                Report("Navegador não encontrado. Tentando iniciar o Chrome/Edge com depuração...", 7);

                try
                {
                    if (NavegadorHelper.IniciarNavegadorComDepuracao())
                    {
                        for (int i = 0; i < 6; i++)
                        {
                            _cancellationToken.ThrowIfCancellationRequested();
                            await Task.Delay(1000);
                            try
                            {
                                wsUrl = await ObterWebSocketUrlAsync(cdpUrl);
                                if (!string.IsNullOrEmpty(wsUrl))
                                {
                                    conectado = true;
                                    break;
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Report($"[AVISO] Falha ao tentar disparar o processo do navegador: {ex.Message}", 7);
                }
            }

            if (!conectado || string.IsNullOrEmpty(wsUrl))
            {
                throw new InvalidOperationException(
                    "Não foi possível conectar ao navegador ativo na porta 9222. " +
                    "Certifique-se de que o Google Chrome ou Edge foi iniciado com a flag de depuração habilitada: " +
                    "--remote-debugging-port=9222\nSe o navegador já estiver aberto, feche todas as abas pessoais e processos do Chrome e tente novamente.");
            }

            IBrowser browser;
            try
            {
                browser = await playwright.Chromium.ConnectOverCDPAsync(wsUrl);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Falha ao conectar via protocolo de depuração remota do Playwright. " +
                    "Certifique-se de que o navegador está aberto e acessível na porta 9222.", ex);
            }

            var context = browser.Contexts.FirstOrDefault()
                ?? throw new InvalidOperationException("Nenhum contexto de navegador ativo encontrado.");

            var page = context.Pages.FirstOrDefault(p => p.Url.Contains("atende.net"))
                ?? context.Pages.FirstOrDefault(p => !p.Url.StartsWith("chrome://") && !p.Url.StartsWith("chrome-extension://"))
                ?? context.Pages.FirstOrDefault()
                ?? throw new InvalidOperationException("Nenhuma aba activa encontrada no navegador.");

            _cancellationToken.ThrowIfCancellationRequested();

            Report("Aba localizada! Iniciando o fluxo no Atende.Net...", 10);

            _flow.Inicializar(page);

            try
            {
                await _flow.NavegarParaTransferenciaAsync();

                await _flow.PreencherOrigemDestinoAsync(_payload.codigo_origem, _payload.codigo_destino);

                await _flow.ConfigurarColunasValidadeAsync();

                int totalItens = _payload.itens.Length;
                double progressoBase = 30.0;
                double progressoPorItem = 60.0 / totalItens;

                for (int i = 0; i < totalItens; i++)
                {
                    var item = _payload.itens[i];
                    string codigo = item.codigos[0];
                    double quantidade = item.quantidade;
                    double progressoAtual = progressoBase + (i * progressoPorItem);

                    Report($"Inserindo item {i + 1} de {totalItens}: {codigo} (Qtd: {quantidade})...", progressoAtual);

                    _cancellationToken.ThrowIfCancellationRequested();

                    await _flow.FiltrarProdutoAsync(codigo);

                    var loteResultado = await _flow.SelecionarLotePorValidadeAsync(quantidade);
                    double quantidadeUsada = loteResultado.QuantidadeUsada;

                    await _flow.PreencherQuantidadeAsync(quantidadeUsada);

                    await _flow.IncluirItemAsync();

                    Report($"Item {codigo} incluído com sucesso!", progressoAtual + (progressoPorItem * 0.9));
                }

                _cancellationToken.ThrowIfCancellationRequested();

                await _flow.ConfirmarTransferenciaAsync();

                Report("Processo concluído com sucesso!", 100);
            }
            catch (Exception ex)
            {
                await _flow.CapturarDiagnosticoAsync($"Erro durante execução: {ex.Message}");
                throw;
            }
        }

        private void Report(string mensagem, double progresso)
        {
            _progressReporter.Report((mensagem, Math.Clamp(progresso, 0, 100)));
        }

        private async Task<string?> ObterWebSocketUrlAsync(string cdpUrl)
        {
            using var httpClient = new System.Net.Http.HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(2);
            string json = await httpClient.GetStringAsync($"{cdpUrl}/json/version");
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
    }
}