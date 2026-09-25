using System.Threading.Tasks;
using Microsoft.Playwright;

namespace TransferToolRPA.Models
{
    public interface IAtendeNetFlow
    {
        void Inicializar(IPage page);
        Task NavegarParaTransferenciaAsync();
        Task PreencherOrigemDestinoAsync(string codigoOrigem, string codigoDestino);
        Task ConfigurarColunasValidadeAsync();
        Task FiltrarProdutoAsync(string codigo);
        Task<(int IndiceLote, double QuantidadeUsada)> SelecionarLotePorValidadeAsync(double quantidadeNecessaria);
        Task PreencherQuantidadeAsync(double quantidade);
        Task IncluirItemAsync();
        Task ConfirmarTransferenciaAsync();
        Task CapturarDiagnosticoAsync(string contexto);
    }
}