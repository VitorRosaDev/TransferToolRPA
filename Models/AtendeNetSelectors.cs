using Microsoft.Playwright;

namespace TransferToolRPA.Models
{
    public static class AtendeNetSelectors
    {
        public const string UrlSistema = "https://alvorada.atende.net/atendenet#!/sistema/28";

        /// <summary>
        /// Seletor da janela ATIVA (a que está no topo). No Atende.Net cada janela
        /// (#janela_*) recebe a classe "janela_ipm_ativa" quando está em foco e
        /// "janela_ipm_desativa" quando está em segundo plano. Usado para desambiguar
        /// seletores que existem em mais de uma janela aberta simultaneamente
        /// (ex.: a janela de consulta fica atrás da janela de inclusão).
        /// </summary>
        public const string JanelaAtiva = "[id^=\"janela_\"].janela_ipm_ativa";

        public static class Navegacao
        {
            public static readonly string MenuMovimento = "#estrutura_menu_sistema li:nth-of-type(2) span";
            public static readonly string MenuTransferencia = "#estrutura_container_sistema li:nth-of-type(4) span";
            public static readonly string BotaoOutrasOpcoes = "aside.area_acoes > div:nth-of-type(1) span.drop_down";
            public static readonly string MenuIncluirTransferencia = "#context_menu tr:nth-of-type(1) span > span";
            // Botao "X" (title=Fechar) de cada aba em "Janelas Abertas" — usado na etapa
            // zero para fechar as janelas abertas antes de iniciar a transferencia.
            public static readonly string BotaoFecharJanela = "#estrutura_janelas_abertas span.tab_close";
        }

        public static class OrigemDestino
        {
            public static readonly string CampoOrigem = "aside input.campo-numerico";
            public static readonly string CampoDestinoIndex1 = "aside input.campo-numerico >> nth=1";
            public static readonly string CampoDestinoFallback = "span:nth-of-type(2) span:nth-of-type(1) input.campo-numerico";
        }

        public static class ConfiguracaoColunas
        {
            public static readonly string BotaoConfigurar = JanelaAtiva + " > div.area_total_janela > div span:nth-of-type(5) > input";
            public static readonly string CheckboxValidade = "li:nth-of-type(6) li:nth-of-type(1) > a > ins.jstree-checkbox";
            public static readonly string BotaoAplicarColunas = "button:nth-of-type(4)";
            public static readonly string BotaoFecharConfig = "button:nth-of-type(5)";
        }

        public static class FiltroProduto
        {
            public static readonly string InputFiltro = JanelaAtiva + " aside td:nth-of-type(3) > input";
            public static readonly string InputFiltroAria = "aria/Primeiro valor para o filtro sobre o campo Código[role=\"textbox\"]";
            public static readonly string BotaoConsultar = JanelaAtiva + " aside span[name=\"consultar\"][title=\"Consultar\"]";
        }

        public static class GradeLotes
        {
            // Linhas de DADOS reais: exclui a linha de "sem resultados" (que traz um
            // <div class="mensagem_sistema">Registro não encontrado</div> sem celulas
            // nomecoluna). Sem isso, o grid vazio contava 1 "linha" e a deteccao de vazio
            // nunca disparava.
            public static readonly string Linhas = "[data-subcontexto-id=\"subcontexto_dados_tela_consulta_estoque\"] tbody tr:has(td[nomecoluna=\"prdcodigo\"])";
            public static readonly string LinhasFallback = "table.grid-lotes tbody tr:has(td[nomecoluna=\"prdcodigo\"])";
            // Células identificadas pelo atributo "nomecoluna" (estável mesmo se as
            // colunas forem reordenadas). O seletor posicional antigo (td:nth-of-type)
            // quebrava quando a grade carregava um conjunto de dados não filtrado.
            public static readonly string CelulaCodigoProduto = "td[nomecoluna=\"prdcodigo\"]";
            public static readonly string CelulaValidade = "td[nomecoluna=\"estdatavalidade\"]";
            public static readonly string CelulaQuantidade = "td[nomecoluna=\"estquantidade\"]";
            // Mensagem de "sem resultados" DENTRO do grid de lotes (a linha vazia traz um
            // <div class="mensagem_sistema">Registro não encontrado</div>). Escopada ao grid
            // para nao casar com mensagens/rodapes de outras telas ou janelas.
            public static readonly string MensagemNaoEncontrado = "[data-subcontexto-id=\"subcontexto_dados_tela_consulta_estoque\"] div.mensagem_sistema";
        }

        public static class Quantidade
        {
            public static readonly string InputQuantidade = "input[name=\"quantidade_transferir\"]";
            public static readonly string InputQuantidadeAria = "input[aria-label=\"Quantidade a Transferir\"]";
            public static readonly string CampoQuantidadeDisponivel = "input[name=\"quantidade_disponivel\"]";
        }

        public static class IncluirItem
        {
            public static readonly string BotaoIncluir = "button[name=\"botao_incluir\"]";
            public static readonly string BotaoIncluirFallback = "button:has-text('Incluir')";
        }

        public static class Confirmacao
        {
            public static readonly string BotaoConfirmar = "button.estrutura_botao_colorido";
            public static readonly string BotaoConfirmarFallback = "button:has-text('Confirmar')";
            public static readonly string BotaoSimModal = "div.estrutura_modal_container footer > button:nth-of-type(1)";
            public static readonly string BotaoSimModalFallback = "button:has-text('Sim')";
        }

        public static class CarrinhoItens
        {
            public static readonly string TabelaIncluidos = "[data-subcontexto-id=\"subcontexto_dados_grid_itens_transferencia\"]";
            // Linhas de dados reais do carrinho (classe "linha_dados"). O Atende.Net NÃO
            // atualiza o aria-rowcount do carrinho (fica "0" mesmo com itens incluídos),
            // então a detecção conta as linhas em vez de ler o atributo ARIA.
            public static readonly string Linhas = "[data-subcontexto-id=\"subcontexto_dados_grid_itens_transferencia\"] tbody tr.linha_dados";
        }
    }
}
