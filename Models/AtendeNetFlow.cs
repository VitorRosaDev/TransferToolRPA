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
        private IPage? _page;
        private readonly IProgress<(string Mensagem, double Progresso)> _progressReporter;
        private readonly CancellationToken _cancellationToken;
        private readonly string _dumpDir;

        /// <summary>
        /// Quantidade de itens já presente no carrinho de transferência na última
        /// verificação. Usado para detectar o incremento após cada "Incluir".
        /// </summary>
        private int _ultimoRowcountCarrinho;

        public AtendeNetFlow(
            IProgress<(string Mensagem, double Progresso)> progressReporter,
            CancellationToken cancellationToken,
            string? dumpDir = null)
        {
            _progressReporter = progressReporter;
            _cancellationToken = cancellationToken;
            _dumpDir = dumpDir ?? ObterDiretorioDiagnosticoPadrao();
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
        }

        public async Task NavegarParaTransferenciaAsync()
        {
            var page = GetPage();
            await ReportAsync("Navegando para tela de transferências...", 12);

            await page.GotoAsync(AtendeNetSelectors.UrlSistema, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = 30000
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
            await Task.Delay(500);

            var campoDestino = page.Locator(AtendeNetSelectors.OrigemDestino.CampoDestinoIndex1);
            if (await campoDestino.CountAsync() == 0)
            {
                campoDestino = page.Locator(AtendeNetSelectors.OrigemDestino.CampoDestinoFallback);
            }
            await PreencherComRetryAsync(campoDestino, codigoDestino);
            await campoDestino.PressAsync("Tab");
            await Task.Delay(500);
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

        public async Task FiltrarProdutoAsync(string codigo)
        {
            var page = GetPage();
            await ReportAsync($"Filtrando produto: {codigo}...", 35);

            var inputFiltro = page.Locator(AtendeNetSelectors.FiltroProduto.InputFiltro);
            await PreencherComRetryAsync(inputFiltro, codigo);
            await ClicarComRetryAsync(() => page.ClickAsync(AtendeNetSelectors.FiltroProduto.BotaoConsultar));

            await AguardarGradeResultadosAsync(codigo);
        }

        public async Task<(int IndiceLote, double QuantidadeUsada)> SelecionarLotePorValidadeAsync(double quantidadeNecessaria)
        {
            await ReportAsync("Selecionando lote por validade mais curta...", 45);

            var lotes = await ObterLotesDisponiveisAsync();
            if (lotes.Count == 0)
            {
                throw new InvalidOperationException("Nenhum lote disponível encontrado na grade.");
            }

            var (indiceLote, quantidadeUsada) = SelecionarMelhorLoteComCompletamento(lotes, quantidadeNecessaria);

            // Usa o locator da linha já armazenado em LoteInfo, em vez de re-buscar a
            // grade e indexar por posição — o re-fetch podia divergir do snapshot usado
            // para montar "lotes" e causar IndexOutOfRange (bloqueio anterior).
            var melhorLote = lotes.First(l => l.Indice == indiceLote);
            await melhorLote.Linha.Locator(AtendeNetSelectors.GradeLotes.CelulaValidade).ClickAsync();

            // O clique no lote dispara um AJAX que preenche os campos de detalhe
            // (Quantidade Disponível, preços). Sem esperar, o preenchimento da
            // quantidade ocorria antes desse AJAX e era sobrescrito/limpo.
            await AguardarSelecaoLoteAsync();

            return (indiceLote, quantidadeUsada);
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

            var timeout = 20000;
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

                await Task.Delay(500);
            }

            await CapturarDiagnosticoAsync("Timeout aguardando janela de inclusão");
            throw new TimeoutException("Janela de inclusão de transferência não carregou dentro do tempo esperado.");
        }

        private async Task AguardarGradeResultadosAsync(string codigo)
        {
            var page = GetPage();
            var timeout = 20000;
            var inicio = DateTime.Now;

            while (DateTime.Now - inicio < TimeSpan.FromMilliseconds(timeout))
            {
                _cancellationToken.ThrowIfCancellationRequested();

                // Só considera pronto quando a grade já refletir o produto filtrado
                // (evita ler um conjunto não filtrado enquanto o AJAX ainda processa).
                var linhas = await page.Locator(AtendeNetSelectors.GradeLotes.Linhas).AllAsync();
                if (linhas.Count > 0)
                {
                    string primeiroCodigo = await LinhaTextoAsync(linhas[0], AtendeNetSelectors.GradeLotes.CelulaCodigoProduto);
                    if (string.Equals(primeiroCodigo, codigo, StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }
                }

                await Task.Delay(300);
            }

            await CapturarDiagnosticoAsync("Timeout aguardando grade de lotes filtrada");
            throw new TimeoutException($"Grade de lotes não carregou o produto {codigo} após filtrar.");
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
            var timeout = 10000;
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

                await Task.Delay(200);
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
            var timeout = 10000;
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

                await Task.Delay(300);
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
                    Timeout = 3000
                });
                await ClicarComRetryAsync(() => botaoSim.ClickAsync());
            }
            catch
            {
                // Modal não apareceu, ok
            }
        }

        private async Task<List<LoteInfo>> ObterLotesDisponiveisAsync()
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
                var linha = linhas[rowIndex];
                string txtValidade = await LinhaTextoAsync(linha, AtendeNetSelectors.GradeLotes.CelulaValidade);
                string txtQuantidade = await LinhaTextoAsync(linha, AtendeNetSelectors.GradeLotes.CelulaQuantidade);

                if (DateTime.TryParse(txtValidade, new CultureInfo("pt-BR"), DateTimeStyles.None, out DateTime validade))
                {
                    double.TryParse(txtQuantidade, NumberStyles.Number, new CultureInfo("pt-BR"), out double quantidade);
                    lotes.Add(new LoteInfo(rowIndex, validade, quantidade, linha));
                }
            }

            return lotes.OrderBy(l => l.Validade).ThenBy(l => l.Quantidade).ToList();
        }

        private (int IndiceLote, double QuantidadeUsada) SelecionarMelhorLoteComCompletamento(List<LoteInfo> lotes, double quantidadeNecessaria)
        {
            var lotesValidos = lotes.Where(l => l.Quantidade > 0.001).ToList();
            if (lotesValidos.Count == 0)
            {
                throw new InvalidOperationException("Nenhum lote com quantidade disponível encontrado.");
            }

            var melhorLote = lotesValidos[0]; // Já ordenado por Validade ASC, Quantidade ASC
            double quantidadeUsada = Math.Min(melhorLote.Quantidade, quantidadeNecessaria);

            return (melhorLote.Indice, quantidadeUsada);
        }

        private async Task ClicarComRetryAsync(Func<Task> acao, int maxTentativas = 3, int delayBaseMs = 500)
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

        private async Task PreencherComRetryAsync(ILocator locator, string valor, int maxTentativas = 3, int delayBaseMs = 300)
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

        private async Task ReportAsync(string mensagem, double progresso)
        {
            _progressReporter?.Report((mensagem, Math.Clamp(progresso, 0, 100)));
            await Task.CompletedTask;
        }

        private record LoteInfo(int Indice, DateTime Validade, double Quantidade, ILocator Linha);
    }
}
