using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace TransferToolRPA.Models
{
    /// <summary>
    /// Resultado de uma consulta de produto por código na grade de lotes.
    /// "NaoEncontrado" abrange tanto código inexistente quanto produto sem estoque
    /// (grade vazia após filtrar) — ambos devem ser pulados com log vermelho.
    /// </summary>
    public enum ResultadoFiltroProduto
    {
        Encontrado,
        NaoEncontrado
    }

    /// <summary>
    /// Lote lido da grade de lotes. A validade é nula quando o produto é não
    /// perecível e a coluna vem vazia no Atende.Net (prioriza-se então a quantidade).
    /// </summary>
    public record LoteGrade(int Indice, DateTime? Validade, double Quantidade);

    public interface IAtendeNetFlow
    {
        void Inicializar(IPage page);

        /// <summary>
        /// Etapa zero: fecha todas as janelas abertas no Atende.Net antes de iniciar,
        /// evitando que janelas residuais (consulta, estoque etc.) interfiram no fluxo.
        /// </summary>
        Task FecharJanelasAbertasAsync();

        Task NavegarParaTransferenciaAsync();
        Task PreencherOrigemDestinoAsync(string codigoOrigem, string codigoDestino);
        Task ConfigurarColunasValidadeAsync();

        /// <summary>Filtra o produto pelo código e informa se a grade carregou resultados.</summary>
        Task<ResultadoFiltroProduto> FiltrarProdutoAsync(string codigo);

        /// <summary>
        /// Lê os lotes da grade filtrada por <paramref name="codigo"/> (validade pode ser
        /// nula). Somente linhas cujo codigo do produto confere sao retornadas - descarta
        /// linhas velhas de outro produto que ainda estejam no grid durante a transicao.
        /// </summary>
        Task<IReadOnlyList<LoteGrade>> ObterLotesDisponiveisAsync(string codigo);

        /// <summary>
        /// Seleciona um lote específico na grade filtrada, localizando a linha por
        /// (validade + quantidade) para não depender de índice posicional frágil.
        /// </summary>
        Task SelecionarLoteAsync(LoteGrade lote, string codigo);

        Task PreencherQuantidadeAsync(double quantidade);
        Task IncluirItemAsync();
        Task ConfirmarTransferenciaAsync();
        Task CapturarDiagnosticoAsync(string contexto);
    }
}