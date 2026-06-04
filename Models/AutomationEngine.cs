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

        public AutomationEngine(
            TransferenciaPayload payload, 
            IProgress<(string Mensagem, double Progresso)> progressReporter,
            CancellationToken cancellationToken)
        {
            _payload = payload;
            _progressReporter = progressReporter;
            _cancellationToken = cancellationToken;
        }

        public async Task ExecutarAsync()
        {
            using var playwright = await Playwright.CreateAsync();
            
            ReportProgress("Conectando ao navegador Chrome/Edge ativo (porta 9222)...", 5);
            
            string cdpUrl = "http://127.0.0.1:9222";
            string? wsUrl = null;
            bool conectado = false;

            // Tentativa 1: Conectar a uma instância já rodando
            try
            {
                wsUrl = await ObterWebSocketUrlAsync(cdpUrl);
                conectado = !string.IsNullOrEmpty(wsUrl);
            }
            catch
            {
                // Silencioso, tentará iniciar o navegador na sequência
            }

            if (!conectado)
            {
                ReportProgress("Navegador não encontrado. Tentando iniciar o Chrome/Edge com depuração...", 7);
                
                try
                {
                    if (NavegadorHelper.IniciarNavegadorComDepuracao())
                    {
                        // Aguarda e tenta se conectar até 6 vezes (total de 6 segundos)
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
                            catch
                            {
                                // Continua tentando no próximo segundo
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ReportProgress($"[AVISO] Falha ao tentar disparar o processo do navegador: {ex.Message}", 7);
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

            ReportProgress("Aba localizada! Iniciando o fluxo no Atende.Net...", 10);

            // PASSO 4: Clicar em "Transferências" e escolher a opção "Incluir transferência"
            ReportProgress("Acessando tela de inclusão de transferência...", 15);
            
            // Clica no botão de dropdown "Transferências" (nome de rotina 543 no Atende.Net)
            await page.ClickAsync("span.estrutura_botao_acao[name='543'], span.estrutura_botao_acao:has-text('Transferências')");
            
            // Clica em "Incluir" ou "Incluir transferência" (ação 102) no menu flutuante que surge
            await page.ClickAsync(".div_context_flutuante span[name='102'], .div_context_flutuante span:has-text('Incluir'), span[name='102']");

            // Aguarda a tela de inclusão carregar (esperando o seletor do depósito de origem ou autocomplete do Atende.Net)
            await page.WaitForSelectorAsync("input[name='saida_depcodigo'], input[name='deposito_origem'], #origem_codigo");

            _cancellationToken.ThrowIfCancellationRequested();

            // PASSO 5: Preencher depósito de origem e destino
            ReportProgress($"Preenchendo Origem: {_payload.codigo_origem} e Destino: {_payload.codigo_destino}...", 25);
            
            var inputOrigem = page.Locator("input[name='saida_depcodigo'], input[name='deposito_origem'], #origem_codigo");
            await inputOrigem.FillAsync(_payload.codigo_origem);
            await inputOrigem.PressAsync("Tab");
            
            // Pequeno delay para a requisição de validação AJAX do depósito
            await Task.Delay(500); 
            
            var inputDestino = page.Locator("input[name='entrada_depcodigo'], input[name='deposito_destino'], #destino_codigo");
            await inputDestino.FillAsync(_payload.codigo_destino);
            await inputDestino.PressAsync("Tab");
            await Task.Delay(500);

            _cancellationToken.ThrowIfCancellationRequested();

            int totalItens = _payload.itens.Length;
            double progressoBase = 30.0;
            double progressoPorItem = 60.0 / totalItens;

            // Iterar sobre os itens a transferir
            for (int i = 0; i < totalItens; i++)
            {
                var item = _payload.itens[i];
                double progressoAtual = progressoBase + (i * progressoPorItem);
                
                ReportProgress($"Inserindo item {i + 1} de {totalItens}: {item.codigo} (Qtd: {item.quantidade})...", progressoAtual);

                _cancellationToken.ThrowIfCancellationRequested();

                // PASSO 6: Digitar código do produto
                var inputProduto = page.Locator("input[name='prdcodigo'], input[name='codigo_produto'], #produto_codigo");
                await inputProduto.FillAsync(item.codigo);
                await inputProduto.PressAsync("Enter");

                // Aguarda o resultado da consulta do produto aparecer no grid/tabela
                await page.WaitForSelectorAsync("table#tabela-lotes, table.grid-lotes, div.resultado-busca");

                _cancellationToken.ThrowIfCancellationRequested();

                // PASSO 7: Configurar consulta para marcar coluna de Validade (caso não esteja visível)
                // O robô abre a engrenagem, marca validade, aplica e fecha
                try
                {
                    // Tenta clicar na engrenagem. Se já estiver visível a coluna de validade, ignora falha silenciosamente
                    if (await page.Locator("button.btn-config-consulta, #btn-configurar").IsVisibleAsync())
                    {
                        await page.ClickAsync("button.btn-config-consulta, #btn-configurar");
                        await page.CheckAsync("input[type='checkbox'][name='validade'], #chk-validade");
                        await page.ClickAsync("button#btn-aplicar, button:has-text('Aplicar Colunas'), button:has-text('Aplicar')");
                        await page.ClickAsync("button.btn-close, button:has-text('Fechar')");
                    }
                }
                catch (Exception)
                {
                    // Log de aviso mas continua a execução
                    ReportProgress($"[AVISO] Falha ao configurar colunas do produto {item.codigo}. Prosseguindo...", progressoAtual);
                }

                _cancellationToken.ThrowIfCancellationRequested();

                // PASSO 8: Selecionar a validade mais curta
                // 1º Critério: Validade mais próxima (curta)
                // 2º Critério (desempate): Menor quantidade disponível
                ReportProgress($"Avaliando validades e quantidades disponíveis para o item {item.codigo}...", progressoAtual + (progressoPorItem * 0.4));
                
                // Mapeia todas as linhas correspondentes aos lotes de validade na tabela
                var rows = await page.Locator("table#tabela-lotes tbody tr, table.grid-lotes tbody tr").AllAsync();
                
                if (rows.Count == 0)
                {
                    throw new InvalidOperationException($"Nenhum lote ou estoque disponível encontrado para o produto {item.codigo}.");
                }

                var lotesList = new System.Collections.Generic.List<LoteDisponivel>();

                // Leitura dinâmica de cabeçalhos para encontrar os índices corretos de Validade e Estoque/Saldo
                var headers = await page.Locator("table#tabela-lotes th, table.grid-lotes th, .conteudo_grid th").AllInnerTextsAsync();
                int validadeColIndex = -1;
                int quantidadeColIndex = -1;

                for (int idx = 0; idx < headers.Count; idx++)
                {
                    string hText = headers[idx].ToLower();
                    if (hText.Contains("validade")) validadeColIndex = idx;
                    else if (hText.Contains("estoque") || hText.Contains("saldo") || hText.Contains("dispon")) quantidadeColIndex = idx;
                }

                // Fallbacks padrão caso os nomes de cabeçalho não sejam mapeados
                if (validadeColIndex == -1) validadeColIndex = 2;
                if (quantidadeColIndex == -1) quantidadeColIndex = 3;

                for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
                {
                    var row = rows[rowIndex];
                    var colunas = await row.Locator("td").AllInnerTextsAsync();
                    
                    if (colunas.Count > Math.Max(validadeColIndex, quantidadeColIndex))
                    {
                        string txtValidade = colunas[validadeColIndex]; // Ex: "10/12/2027"
                        string txtQuantidade = colunas[quantidadeColIndex]; // Ex: "150" ou "10,0"

                        if (DateTime.TryParse(txtValidade, out DateTime validadeParsed))
                        {
                            double.TryParse(txtQuantidade.Replace(",", "."), out double quantidadeParsed);
                            lotesList.Add(new LoteDisponivel(rowIndex, validadeParsed, quantidadeParsed));
                        }
                    }
                }

                var melhorLote = LoteSelector.SelecionarMelhorLote(lotesList);
                if (melhorLote == null)
                {
                    throw new InvalidOperationException($"Nenhum lote com validade válida encontrado para o produto {item.codigo}.");
                }

                // Clica no lote escolhido para selecioná-lo
                await rows[melhorLote.Index].ClickAsync();

                // Inserir a quantidade do produto a ser transferida
                await page.FillAsync("input[name='quantidade_transferir'], #quantidade_item", item.quantidade.ToString());

                // Clica em "Incluir" para jogar no carrinho da transferência
                await page.ClickAsync("button#btn-incluir, button:has-text('Incluir'), input[value='Incluir']");

                // Aguarda o item aparecer na lista de itens incluídos (carrinho de transferência)
                await page.WaitForSelectorAsync("table#tabela-incluidos, table.grid-itens-incluidos");

                ReportProgress($"Item {item.codigo} incluído com sucesso!", progressoAtual + (progressoPorItem * 0.9));
            }

            _cancellationToken.ThrowIfCancellationRequested();

            // PASSO 10: Ao final da lista, clicar no botão "Confirmar"
            ReportProgress("Finalizando lista. Clicando em Confirmar transferência...", 92);
            await page.ClickAsync("button#btn-confirmar, button:has-text('Confirmar'), button:has-text('Gravar')");

            // Verifica se aparece pop-up pedindo confirmação do sistema
            try
            {
                var popupConfirmar = page.Locator("button:has-text('Sim'), span:has-text('Sim'), button.btn-confirm-popup");
                // Espera de forma assíncrona por até 3 segundos que o botão de confirmação esteja visível
                await popupConfirmar.WaitForAsync(new LocatorWaitForOptions 
                { 
                    State = WaitForSelectorState.Visible, 
                    Timeout = 3000 
                });
                await popupConfirmar.ClickAsync();
            }
            catch (Exception)
            {
                // Pop-up não apareceu, o que é esperado em alguns casos conforme o passo 10
            }

            ReportProgress("Processo concluído com sucesso!", 100);
        }

        private void ReportProgress(string mensagem, double progresso)
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
