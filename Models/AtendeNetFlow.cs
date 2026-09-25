using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace TransferToolRPA.Models
{
    public class AtendeNetFlow : IAtendeNetFlow
    {
        private const int TimeoutGradePadraoMs = ConfiguracaoAutomacao.TimeoutGradeMs;
        private const int JanelaGradeVaziaPadraoMs = ConfiguracaoAutomacao.JanelaGradeVaziaMs;

        private IPage? _page;
        private readonly IProgress<ProgressoAutomacao> _progressReporter;
        private readonly CancellationToken _cancellationToken;
        private readonly string _dumpDir;
        private readonly int _timeoutGradeMs;
        private readonly int _janelaGradeVaziaMs;

        /// <summary>
        /// Quantidade de itens já presente no carrinho de transferência na última
        /// verificação. Usado para detectar o incremento após cada "Incluir".
        /// </summary>
        private int _ultimoRowcountCarrinho;

        public AtendeNetFlow(
            IProgress<ProgressoAutomacao> progressReporter,
            CancellationToken cancellationToken,
            string? dumpDir = null,
            int timeoutGradeMs = TimeoutGradePadraoMs,
            int janelaGradeVaziaMs = JanelaGradeVaziaPadraoMs)
        {
            _progressReporter = progressReporter;
            _cancellationToken = cancellationToken;
            _dumpDir = dumpDir ?? ObterDiretorioDiagnosticoPadrao();
            _timeoutGradeMs = timeoutGradeMs;
            _janelaGradeVaziaMs = janelaGradeVaziaMs;
        }

        /// <summary>
        /// Diretório padrão para artefatos de diagnóstico (screenshot/HTML), na pasta do
        /// usuário — substitui o antigo caminho fixo de máquina de desenvolvimento.
        /// </summary>
        private static string ObterDiretorioDiagnosticoPadrao()
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TransferToolRPA",
                "Diagnostico");

            try
            {
                Directory.CreateDirectory(dir);
                DiagnosticoHelper.LimparAntigos(dir, ConfiguracaoAutomacao.DiasRetencaoDiagnostico);
            }
            catch
            {
                // Sem permissão de escrita: CapturarDiagnosticoAsync fará fallback para o temp.
            }

            return dir;
        }

        public void Inicializar(IPage page)
        {
            _page = page ?? throw new ArgumentNullException(nameof(page));
            _ultimoRowcountCarrinho = 0;

            // Teto para QUALQUER operacao do Playwright nesta pagina: evita travar
            // indefinidamente em grids que ainda estao carregando/re-renderizando.
            try { _page.SetDefaultTimeout(ConfiguracaoAutomacao.TimeoutPadraoPaginaMs); } catch { }
        }

        /// <summary>
        /// Etapa zero: fecha todas as janelas abertas do Atende.Net (abas em
        /// "Janelas Abertas") antes de iniciar o fluxo, para nao haver janelas
        /// residuais interferindo (ex.: janela de consulta atras da inclusao).
        /// </summary>
        public async Task FecharJanelasAbertasAsync()
        {
            var page = GetPage();
            await ReportAsync("Etapa zero: fechando janelas abertas...", 11);

            var botoesFechar = page.Locator(AtendeNetSelectors.Navegacao.BotaoFecharJanela);

            // Cada clique fecha uma janela e re-renderiza a barra de abas; re-consultamos
            // a contagem a cada iteracao. Limite de seguranca para nao entrar em loop infinito.
            for (int i = 0; i < ConfiguracaoAutomacao.MaxJanelasParaFechar; i++)
            {
                _cancellationToken.ThrowIfCancellationRequested();

                int quantidade = await botoesFechar.CountAsync();
                if (quantidade == 0) break;

                await botoesFechar.First.ClickAsync();
                await Task.Delay(ConfiguracaoAutomacao.DelayFecharJanelaMs, _cancellationToken);
            }

            await ReportAsync("Janelas abertas verificadas/fechadas.", 11);
        }

        public async Task NavegarParaTransferenciaAsync()
        {
            var page = GetPage();
            await ReportAsync("Navegando para tela de transferências...", 12);

            await page.GotoAsync(AtendeNetSelectors.UrlSistema, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = ConfiguracaoAutomacao.TimeoutNavegacaoMs
            });

            await ClicarComRetryAsync(() => page.ClickAsync(AtendeNetSelectors.Navegacao.MenuMovimento));
            await ClicarComRetryAsync(() => page.ClickAsync(AtendeNetSelectors.Navegacao.MenuTransferencia));
            await ClicarComRetryAsync(() => page.ClickAsync(AtendeNetSelectors.Navegacao.BotaoOutrasOpcoes));
            await ClicarComRetryAsync(() => page.ClickAsync(AtendeNetSelectors.Navegacao.MenuIncluirTransferencia));

            await AguardarJanelaInclusaoAsync();
        }

        public async Task PreencherOrigemDestinoAsync(string codigoOrigem, string codigoDestino)
        {
            var page = GetPage();
            await ReportAsync($"Preenchendo Origem: {codigoOrigem} e Destino: {codigoDestino}...", 22);

            var campoOrigem = page.Locator(AtendeNetSelectors.OrigemDestino.CampoOrigem).First;
            await PreencherComRetryAsync(campoOrigem, codigoOrigem);
            await campoOrigem.PressAsync("Tab");
            await Task.Delay(ConfiguracaoAutomacao.DelayAposTabMs);

            var campoDestino = page.Locator(AtendeNetSelectors.OrigemDestino.CampoDestinoIndex1);
            if (await campoDestino.CountAsync() == 0)
            {
                campoDestino = page.Locator(AtendeNetSelectors.OrigemDestino.CampoDestinoFallback);
            }
            await PreencherComRetryAsync(campoDestino, codigoDestino);
            await campoDestino.PressAsync("Tab");
            await Task.Delay(ConfiguracaoAutomacao.DelayAposTabMs);
        }

        public async Task ConfigurarColunasValidadeAsync()
        {
            var page = GetPage();
            await ReportAsync("Configurando coluna de Validade...", 28);

            try
            {
                var botaoConfigurar = page.Locator(AtendeNetSelectors.ConfiguracaoColunas.BotaoConfigurar);
                if (await botaoConfigurar.IsVisibleAsync())
                {
                    await ClicarComRetryAsync(() => botaoConfigurar.ClickAsync());
                    await ClicarComRetryAsync(() => page.ClickAsync(AtendeNetSelectors.ConfiguracaoColunas.CheckboxValidade));
                    await ClicarComRetryAsync(() => page.ClickAsync(AtendeNetSelectors.ConfiguracaoColunas.BotaoAplicarColunas));
                    await ClicarComRetryAsync(() => page.ClickAsync(AtendeNetSelectors.ConfiguracaoColunas.BotaoFecharConfig));
                }
            }
            catch (Exception ex)
            {
                await ReportAsync($"[AVISO] Falha ao configurar colunas: {ex.Message}. Prosseguindo...", 28);
            }
        }

        public async Task<ResultadoFiltroProduto> FiltrarProdutoAsync(string codigo)
        {
            var page = GetPage();
            await ReportAsync($"Filtrando produto: {codigo}...", 35);

            var inputFiltro = page.Locator(AtendeNetSelectors.FiltroProduto.InputFiltro);
            await PreencherComRetryAsync(inputFiltro, codigo);
            await ClicarComRetryAsync(() => page.ClickAsync(AtendeNetSelectors.FiltroProduto.BotaoConsultar));

            return await AguardarGradeResultadosAsync(codigo);
        }

        public async Task<IReadOnlyList<LoteGrade>> ObterLotesDisponiveisAsync(string codigo)
        {
            var lotes = await LerLotesAsync(codigo);
            return lotes.Select(l => new LoteGrade(l.Indice, l.Validade, l.Quantidade)).ToList();
        }

        public async Task SelecionarLoteAsync(LoteGrade lote, string codigo)
        {
            await ReportAsync("Selecionando lote...", 45);
            _cancellationToken.ThrowIfCancellationRequested();

            // 1) Le a grade exigindo que as linhas sejam do codigo correto.
            var alvo = LocalizarLote(await LerLotesAsync(codigo), lote);

            // 2) Se nao achou (grade ainda em transicao/conteudo velho), re-filtra o
            //    codigo e rele UMA vez antes de desistir.
            if (alvo == null)
            {
                await ReportAsync("[AVISO] Lote nao localizado; re-filtrando o produto e relendo a grade...", 45, NivelLog.Aviso);
                await FiltrarProdutoAsync(codigo);
                _cancellationToken.ThrowIfCancellationRequested();
                alvo = LocalizarLote(await LerLotesAsync(codigo), lote);
            }

            if (alvo == null)
            {
                await CapturarDiagnosticoAsync($"Lote nao encontrado na grade (codigo={codigo}, validade={lote.Validade:dd/MM/yyyy}, qtd={lote.Quantidade})");
                throw new InvalidOperationException(
                    $"Lote nao encontrado na grade apos filtrar (codigo={codigo}, validade={lote.Validade:dd/MM/yyyy}, quantidade={lote.Quantidade}).");
            }

            // O clique no lote dispara um AJAX que preenche os campos de detalhe
            // (Quantidade Disponivel, precos). Sem esperar, o preenchimento da
            // quantidade ocorria antes desse AJAX e era sobrescrito/limpo.
            await alvo.Linha.Locator(AtendeNetSelectors.GradeLotes.CelulaValidade)
                .ClickAsync(new LocatorClickOptions { Timeout = ConfiguracaoAutomacao.TimeoutPadraoPaginaMs });
            await AguardarSelecaoLoteAsync();
        }

        private static LoteInfo? LocalizarLote(List<LoteInfo> lotes, LoteGrade lote)
        {
            // Prefere o indice lido na filtragem (distingue lotes com mesma validade e
            // mesma quantidade) e cai para o matching por (validade + quantidade).
            return lotes.FirstOrDefault(l =>
                    l.Indice == lote.Indice &&
                    l.Validade == lote.Validade &&
                    Math.Abs(l.Quantidade - lote.Quantidade) < 0.001)
                ?? lotes.FirstOrDefault(l =>
                    l.Validade == lote.Validade &&
                    Math.Abs(l.Quantidade - lote.Quantidade) < 0.001);
        }

        public async Task PreencherQuantidadeAsync(double quantidade)
        {
            var page = GetPage();
            var inputQuantidade = page.Locator(AtendeNetSelectors.Quantidade.InputQuantidade);
            if (await inputQuantidade.CountAsync() == 0)
            {
                inputQuantidade = page.Locator(AtendeNetSelectors.Quantidade.InputQuantidadeAria);
            }

            await PreencherComRetryAsync(inputQuantidade, quantidade.ToString(new CultureInfo("pt-BR")));
        }

        public async Task IncluirItemAsync()
        {
            var page = GetPage();
            var botaoIncluir = page.Locator(AtendeNetSelectors.IncluirItem.BotaoIncluir);
            if (await botaoIncluir.CountAsync() == 0)
            {
                botaoIncluir = page.Locator(AtendeNetSelectors.IncluirItem.BotaoIncluirFallback);
            }
            await ClicarComRetryAsync(() => botaoIncluir.ClickAsync());

            await AguardarItemNoCarrinhoAsync();
        }

        public async Task ConfirmarTransferenciaAsync()
        {
            var page = GetPage();
            await ReportAsync("Confirmando transferência...", 92);

            var botaoConfirmar = page.Locator(AtendeNetSelectors.Confirmacao.BotaoConfirmar);
            if (await botaoConfirmar.CountAsync() == 0)
            {
                botaoConfirmar = page.Locator(AtendeNetSelectors.Confirmacao.BotaoConfirmarFallback);
            }
            await ClicarComRetryAsync(() => botaoConfirmar.ClickAsync());

            await ConfirmarModalSeExistirAsync();
        }

        public async Task CapturarDiagnosticoAsync(string contexto)
        {
            var page = GetPage();
            try
            {
                string dumpDir = _dumpDir;
                if (!System.IO.Directory.Exists(dumpDir))
                {
                    dumpDir = System.IO.Path.GetTempPath();
                }

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string screenshotPath = System.IO.Path.Combine(dumpDir, $"diag_{timestamp}.png");
                await page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshotPath, FullPage = true });

                string htmlPath = System.IO.Path.Combine(dumpDir, $"diag_{timestamp}.html");
                string html = await page.ContentAsync();
                await System.IO.File.WriteAllTextAsync(htmlPath, html);

                var framesInfo = new List<string>();
                foreach (var frame in page.Frames)
                {
                    framesInfo.Add($"Frame: {frame.Name} | URL: {frame.Url}");
                    try
                    {
                        string frameHtmlPath = System.IO.Path.Combine(dumpDir, $"diag_frame_{frame.Name ?? "unnamed"}_{timestamp}.html");
                        string frameHtml = await frame.ContentAsync();
                        await System.IO.File.WriteAllTextAsync(frameHtmlPath, frameHtml);
                    }
                    catch { }
                }

                await ReportAsync($"[DIAGNÓSTICO] {contexto} | Screenshot: {screenshotPath} | HTML: {htmlPath} | Frames: {framesInfo.Count}", 0);
            }
            catch (Exception ex)
            {
                await ReportAsync($"[DIAGNÓSTICO] Erro ao capturar diagnóstico: {ex.Message}", 0);
            }
        }

        private IPage GetPage()
        {
            return _page ?? throw new InvalidOperationException("AtendeNetFlow não foi inicializado. Chame Inicializar(IPage) primeiro.");
        }

        private async Task AguardarJanelaInclusaoAsync()
        {
            var page = GetPage();
            await ReportAsync("Aguardando carregamento da tela de inclusão...", 18);

            var timeout = ConfiguracaoAutomacao.TimeoutJanelaInclusaoMs;
            var inicio = DateTime.Now;

            while (DateTime.Now - inicio < TimeSpan.FromMilliseconds(timeout))
            {
                _cancellationToken.ThrowIfCancellationRequested();

                var frames = page.Frames.Where(f => f.Url.Contains("atende.net") || f.Url.Contains("conteudo_")).ToList();
                foreach (var frame in frames)
                {
                    try
                    {
                        var campoOrigem = frame.Locator(AtendeNetSelectors.OrigemDestino.CampoOrigem);
                        if (await campoOrigem.CountAsync() > 0 && await campoOrigem.First.IsVisibleAsync())
                        {
                            return;
                        }
                    }
                    catch { }
                }

                await Task.Delay(ConfiguracaoAutomacao.DelayPollJanelaMs);
            }

            await CapturarDiagnosticoAsync("Timeout aguardando janela de inclusão");
            throw new TimeoutException("Janela de inclusão de transferência não carregou dentro do tempo esperado.");
        }

        /// <summary>
        /// Aguarda a grade refletir o produto filtrado. Retorna "Encontrado" quando a
        /// primeira linha já corresponde ao código; "NaoEncontrado" quando a grade permanece
        /// vazia por um tempo estável (a consulta não trouxe lotes) ou quando o tempo máximo
        /// expira sem confirmação. A janela de estabilidade evita que a mensagem residual
        /// "Registro não encontrado" de uma consulta anterior seja confundida com o resultado
        /// da consulta atual (que ainda está carregando).
        /// </summary>
        private async Task<ResultadoFiltroProduto> AguardarGradeResultadosAsync(string codigo)
        {
            var page = GetPage();
            var inicio = DateTime.Now;
            DateTime? vazioDesde = null;

            while (DateTime.Now - inicio < TimeSpan.FromMilliseconds(_timeoutGradeMs))
            {
                _cancellationToken.ThrowIfCancellationRequested();

                // Só considera pronto quando a grade já refletir o produto filtrado
                // (evita ler um conjunto não filtrado enquanto o AJAX ainda processa).
                var linhas = await page.Locator(AtendeNetSelectors.GradeLotes.Linhas).AllAsync();

                if (linhas.Count > 0)
                {
                    vazioDesde = null;

                    string primeiroCodigo = await LinhaTextoAsync(linhas[0], AtendeNetSelectors.GradeLotes.CelulaCodigoProduto);
                    if (string.Equals(primeiroCodigo, codigo, StringComparison.OrdinalIgnoreCase))
                    {
                        return ResultadoFiltroProduto.Encontrado;
                    }
                }
                else
                {
                    // Grade vazia AGORA pode ser transitorio: logo apos clicar em
                    // "Consultar", o grid ainda exibe o estado da consulta anterior
                    // (inclusive o "Registro nao encontrado" residual de um codigo que
                    // de fato nao existia). So se declara "nao encontrado" apos a grade
                    // permanecer vazia por um tempo estavel — o suficiente para o AJAX da
                    // consulta atual concluir e re-renderizar.
                    vazioDesde ??= DateTime.Now;
                    if (DateTime.Now - vazioDesde >= TimeSpan.FromMilliseconds(_janelaGradeVaziaMs))
                    {
                        return ResultadoFiltroProduto.NaoEncontrado;
                    }
                }

                await Task.Delay(ConfiguracaoAutomacao.DelayPollGradeMs);
            }

            await CapturarDiagnosticoAsync($"Timeout aguardando grade de lotes filtrada (código: {codigo})");
            await ReportAsync($"[AVISO] A grade não confirmou o código {codigo} em {_timeoutGradeMs}ms. Item tratado como não encontrado.", 0, NivelLog.Aviso);
            return ResultadoFiltroProduto.NaoEncontrado;
        }

        /// <summary>
        /// Lê o texto (sem espaços) de uma célula de uma linha da grade; retorna vazio
        /// se a célula não existir (evita exceções em linhas incompletas/transitórias).
        /// </summary>
        private async Task<string> LinhaTextoAsync(ILocator linha, string seletor)
        {
            try
            {
                var texto = await linha.Locator(seletor).InnerTextAsync();
                return texto?.Trim() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Aguarda o término do AJAX disparado ao selecionar um lote, detectado pelo
        /// preenchimento do campo "Quantidade Disponível" (somente leitura) com um
        /// valor maior que zero.
        /// </summary>
        private async Task AguardarSelecaoLoteAsync()
        {
            var page = GetPage();
            var timeout = ConfiguracaoAutomacao.TimeoutSelecaoLoteMs;
            var inicio = DateTime.Now;

            while (DateTime.Now - inicio < TimeSpan.FromMilliseconds(timeout))
            {
                _cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var campo = page.Locator(AtendeNetSelectors.Quantidade.CampoQuantidadeDisponivel);
                    if (await campo.CountAsync() > 0)
                    {
                        string valor = await LerValorCampoAsync(campo);
                        if (double.TryParse(valor, NumberStyles.Number, new CultureInfo("pt-BR"), out double qtd) && qtd > 0)
                        {
                            return;
                        }
                    }
                }
                catch
                {
                    // Campo ainda não disponível; aguarda o próximo ciclo.
                }

                await Task.Delay(ConfiguracaoAutomacao.DelayPollGradeMs);
            }

            await CapturarDiagnosticoAsync("Timeout aguardando seleção do lote");
            throw new TimeoutException("Lote não foi selecionado (Quantidade Disponível não preenchida).");
        }

        /// <summary>
        /// Lê o valor atual de um campo de entrada, tentando o valor corrente e, em
        /// seguida, o atributo "value" (útil para campos somente leitura).
        /// </summary>
        private async Task<string> LerValorCampoAsync(ILocator campo)
        {
            try
            {
                return (await campo.InputValueAsync())?.Trim() ?? string.Empty;
            }
            catch
            {
                try
                {
                    return (await campo.GetAttributeAsync("value"))?.Trim() ?? string.Empty;
                }
                catch
                {
                    return string.Empty;
                }
            }
        }

        private async Task AguardarItemNoCarrinhoAsync()
        {
            var page = GetPage();
            var timeout = ConfiguracaoAutomacao.TimeoutCarrinhoMs;
            var inicio = DateTime.Now;
            int rowcountEsperado = _ultimoRowcountCarrinho + 1;

            while (DateTime.Now - inicio < TimeSpan.FromMilliseconds(timeout))
            {
                _cancellationToken.ThrowIfCancellationRequested();

                // O Atende.Net não atualiza o aria-rowcount do carrinho (fica "0" mesmo
                // com itens incluídos, e o aria-rowindex vira "NaN"). Conta-se então as
                // linhas de dados reais (classe "linha_dados") em vez do atributo ARIA.
                var linhas = page.Locator(AtendeNetSelectors.CarrinhoItens.Linhas);
                int n = await linhas.CountAsync();
                if (n >= rowcountEsperado)
                {
                    _ultimoRowcountCarrinho = n;
                    return;
                }

                await Task.Delay(ConfiguracaoAutomacao.DelayPollCarrinhoMs);
            }

            await CapturarDiagnosticoAsync("Timeout aguardando item no carrinho");
            throw new TimeoutException("Item não apareceu no carrinho de transferência após clicar em Incluir.");
        }

        private async Task ConfirmarModalSeExistirAsync()
        {
            var page = GetPage();
            try
            {
                var botaoSim = page.Locator(AtendeNetSelectors.Confirmacao.BotaoSimModal);
                await botaoSim.WaitForAsync(new LocatorWaitForOptions
                {
                    State = WaitForSelectorState.Visible,
                    Timeout = ConfiguracaoAutomacao.TimeoutModalMs
                });
                await ClicarComRetryAsync(() => botaoSim.ClickAsync());
            }
            catch
            {
                // Modal não apareceu, ok
            }
        }

        /// <summary>
        /// Lê as linhas da grade de lotes. A validade é opcional (nula quando a coluna
        /// vem vazia) — produtos não perecíveis passam a ser considerados, usando apenas
        /// a quantidade como critério. Quando um código é informado (codigoFiltro),
        /// linhas de outro produto são descartadas (protege contra grade em transição).
        /// </summary>
        private async Task<List<LoteInfo>> LerLotesAsync(string? codigoFiltro = null)
        {
            var page = GetPage();
            var linhas = await page.Locator(AtendeNetSelectors.GradeLotes.Linhas).AllAsync();
            if (linhas.Count == 0)
            {
                linhas = await page.Locator(AtendeNetSelectors.GradeLotes.LinhasFallback).AllAsync();
            }

            var lotes = new List<LoteInfo>();

            for (int rowIndex = 0; rowIndex < linhas.Count; rowIndex++)
            {
                _cancellationToken.ThrowIfCancellationRequested();

                var linha = linhas[rowIndex];

                // Quando um código é informado, descarta linhas de OUTRO produto — evita
                // usar um lote "velho" que ainda esteja no grid durante a transição do
                // filtro (causa do erro "Lote não encontrado").
                if (!string.IsNullOrEmpty(codigoFiltro))
                {
                    string codigoLinha = await LinhaTextoAsync(linha, AtendeNetSelectors.GradeLotes.CelulaCodigoProduto);
                    if (!string.Equals(codigoLinha, codigoFiltro, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }

                string txtValidade = await LinhaTextoAsync(linha, AtendeNetSelectors.GradeLotes.CelulaValidade);
                string txtQuantidade = await LinhaTextoAsync(linha, AtendeNetSelectors.GradeLotes.CelulaQuantidade);

                DateTime? validade = null;
                if (DateTime.TryParse(txtValidade, new CultureInfo("pt-BR"), DateTimeStyles.None, out DateTime validadeParsed))
                {
                    validade = validadeParsed;
                }

                double.TryParse(txtQuantidade, NumberStyles.Number, new CultureInfo("pt-BR"), out double quantidade);

                // Mantém a linha mesmo sem validade; linhas sem quantidade ficam com 0 e
                // são descartadas ao montar o plano (LoteSelector).
                lotes.Add(new LoteInfo(rowIndex, validade, quantidade, linha));
            }

            return lotes;
        }

        private async Task ClicarComRetryAsync(Func<Task> acao, int maxTentativas = ConfiguracaoAutomacao.MaxTentativasRetry, int delayBaseMs = ConfiguracaoAutomacao.DelayRetryCliqueMs)
        {
            Exception? ultimaExcecao = null;

            for (int tentativa = 1; tentativa <= maxTentativas; tentativa++)
            {
                _cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await acao();
                    return;
                }
                catch (Exception ex)
                {
                    ultimaExcecao = ex;
                    await ReportAsync($"[RETRY {tentativa}/{maxTentativas}] Falha ao clicar: {ex.Message}. Aguardando {delayBaseMs * tentativa}ms...", 0);

                    if (tentativa < maxTentativas)
                    {
                        await Task.Delay(delayBaseMs * tentativa, _cancellationToken);
                    }
                }
            }

            throw new InvalidOperationException($"Falha após {maxTentativas} tentativas: {ultimaExcecao?.Message}", ultimaExcecao);
        }

        private async Task PreencherComRetryAsync(ILocator locator, string valor, int maxTentativas = ConfiguracaoAutomacao.MaxTentativasRetry, int delayBaseMs = ConfiguracaoAutomacao.DelayRetryPreenchimentoMs)
        {
            Exception? ultimaExcecao = null;

            for (int tentativa = 1; tentativa <= maxTentativas; tentativa++)
            {
                _cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await locator.FillAsync(valor);
                    return;
                }
                catch (Exception ex)
                {
                    ultimaExcecao = ex;
                    await ReportAsync($"[RETRY {tentativa}/{maxTentativas}] Falha ao preencher: {ex.Message}. Aguardando {delayBaseMs * tentativa}ms...", 0);

                    if (tentativa < maxTentativas)
                    {
                        await Task.Delay(delayBaseMs * tentativa, _cancellationToken);
                    }
                }
            }

            throw new InvalidOperationException($"Falha ao preencher após {maxTentativas} tentativas: {ultimaExcecao?.Message}", ultimaExcecao);
        }

        private async Task ReportAsync(string mensagem, double progresso, NivelLog nivel = NivelLog.Info)
        {
            _progressReporter?.Report(new ProgressoAutomacao(mensagem, Math.Clamp(progresso, 0, 100), nivel));
            await Task.CompletedTask;
        }

        private record LoteInfo(int Indice, DateTime? Validade, double Quantidade, ILocator Linha);
    }
}
