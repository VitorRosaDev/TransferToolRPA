using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace TransferToolRPA.Models
{
    public class AutomationEngine
    {
        private readonly TransferenciaPayload _payload;
        private readonly IProgress<ProgressoAutomacao> _progressReporter;
        private readonly CancellationToken _cancellationToken;
        private readonly IAtendeNetFlow _flow;

        public AutomationEngine(
            TransferenciaPayload payload,
            IProgress<ProgressoAutomacao> progressReporter,
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

            await ExecutarFluxoAsync();
        }

        /// <summary>
        /// Executa o fluxo de transferência propriamente dito, assumindo que o navegador
        /// já está conectado e o fluxo inicializado. Separado de <see cref="ExecutarAsync"/>
        /// para permitir testes determinísticos (sem abrir/conectar um navegador).
        /// </summary>
        public async Task ExecutarFluxoAsync()
        {
            try
            {
                await _flow.FecharJanelasAbertasAsync();

                await _flow.NavegarParaTransferenciaAsync();

                await _flow.PreencherOrigemDestinoAsync(_payload.codigo_origem, _payload.codigo_destino);

                await _flow.ConfigurarColunasValidadeAsync();

                int totalItens = _payload.itens.Length;
                double progressoBase = 30.0;
                double progressoPorItem = 60.0 / totalItens;

                for (int i = 0; i < totalItens; i++)
                {
                    var item = _payload.itens[i];
                    double progressoAtual = progressoBase + (i * progressoPorItem);
                    string codigosTexto = string.Join(", ", PayloadValidator.NormalizarCodigos(item.codigos));

                    Report($"Inserindo item {i + 1} de {totalItens}: {codigosTexto} (Qtd: {item.quantidade})...", progressoAtual);

                    _cancellationToken.ThrowIfCancellationRequested();

                    await ProcessarItemAsync(item, progressoAtual, progressoPorItem);
                }

                _cancellationToken.ThrowIfCancellationRequested();

                await _flow.ConfirmarTransferenciaAsync();

                Report("Processo concluído com sucesso!", 100, NivelLog.Sucesso);
            }
            catch (Exception ex)
            {
                await _flow.CapturarDiagnosticoAsync($"Erro durante execução: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Processa um item da carga: consulta cada código, agrega todos os lotes numa única
        /// lista, planeja (validade x quantidade, com completamento entre lotes/códigos) e
        /// executa as inclusões. Itens sem nenhum lote (código inexistente ou sem estoque)
        /// são pulados com log vermelho; quantidades parciais geram log âmbar.
        /// </summary>
        private async Task ProcessarItemAsync(PayloadItemEntrada item, double progresso, double progressoPorItem)
        {
            var codigos = PayloadValidator.NormalizarCodigos(item.codigos);
            double quantidade = item.quantidade;

            // 1) Consulta cada código e agrega TODOS os lotes numa única lista.
            //    "codigoFiltradoAtual" acompanha qual código está na grade, evitando
            //    re-filtragens desnecessárias na etapa de inclusão (passo 4).
            var candidatos = new List<LoteCandidato>();
            string? codigoFiltradoAtual = null;
            foreach (var codigo in codigos)
            {
                _cancellationToken.ThrowIfCancellationRequested();

                var resultado = await _flow.FiltrarProdutoAsync(codigo);
                if (resultado != ResultadoFiltroProduto.Encontrado)
                {
                    Report($"[AVISO] Código {codigo} sem lotes/estoque (Qtd: {quantidade}).", progresso, NivelLog.Aviso);
                    continue;
                }

                codigoFiltradoAtual = codigo;

                var lotes = await _flow.ObterLotesDisponiveisAsync(codigo);
                foreach (var lote in lotes)
                {
                    candidatos.Add(new LoteCandidato(codigo, lote.Indice, lote.Validade, lote.Quantidade));
                }
            }

            string codigosTexto = string.Join(", ", codigos);

            // 2) Nenhum lote em nenhum código => Eventualidade 2 (pular + log vermelho).
            if (candidatos.Count == 0)
            {
                Report($"Item NÃO adicionado — código não localizado: código(s) {codigosTexto}, quantidade {quantidade}.", progresso, NivelLog.Erro, ResultadoItemTransferencia.NaoEncontrado);
                return;
            }

            // 3) Planeja a transferência (validade x quantidade, completando entre lotes/códigos).
            var plano = LoteSelector.PlanejarTransferencia(candidatos, quantidade);

            if (plano.Inclusoes.Count == 0)
            {
                Report($"Item NÃO adicionado — sem quantidade disponível: código(s) {codigosTexto}, quantidade {quantidade}.", progresso, NivelLog.Erro, ResultadoItemTransferencia.NaoEncontrado);
                return;
            }

            // 4) Executa as inclusões, re-filtrando somente quando o código muda.
            foreach (var inclusao in plano.Inclusoes)
            {
                _cancellationToken.ThrowIfCancellationRequested();

                if (!string.Equals(codigoFiltradoAtual, inclusao.Lote.Codigo, StringComparison.OrdinalIgnoreCase))
                {
                    await _flow.FiltrarProdutoAsync(inclusao.Lote.Codigo);
                    codigoFiltradoAtual = inclusao.Lote.Codigo;
                }

                await _flow.SelecionarLoteAsync(
                    new LoteGrade(inclusao.Lote.IndiceNaGrade, inclusao.Lote.Validade, inclusao.Lote.Quantidade),
                    inclusao.Lote.Codigo);
                await _flow.PreencherQuantidadeAsync(inclusao.Quantidade);
                await _flow.IncluirItemAsync();
            }

            // 5) Quantidade parcial => Eventualidade 5 (log âmbar e segue o fluxo).
            if (plano.QuantidadeFaltante > 0.001)
            {
                Report(
                    $"Item com quantidade PARCIAL: código(s) {codigosTexto} — transferido {plano.QuantidadeTransferida} de {quantidade} (faltaram {plano.QuantidadeFaltante}).",
                    progresso + (progressoPorItem * 0.9),
                    NivelLog.Aviso,
                    ResultadoItemTransferencia.Parcial);
            }
            else
            {
                Report(
                    $"Item {codigosTexto} incluído com sucesso ({plano.QuantidadeTransferida} un.).",
                    progresso + (progressoPorItem * 0.9),
                    NivelLog.Sucesso);
            }
        }

        private void Report(string mensagem, double progresso, NivelLog nivel = NivelLog.Info, ResultadoItemTransferencia resultado = ResultadoItemTransferencia.Nenhum)
        {
            _progressReporter.Report(new ProgressoAutomacao(mensagem, Math.Clamp(progresso, 0, 100), nivel, resultado));
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