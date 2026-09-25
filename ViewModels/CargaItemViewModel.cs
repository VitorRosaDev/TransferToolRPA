using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using TransferToolRPA.Converters;
using TransferToolRPA.Models;

namespace TransferToolRPA.ViewModels
{
    /// <summary>
    /// Representa uma carga importada na fila, com o status de conclusão que define a
    /// cor de exibição (verde = ok, âmbar = parcial, vermelho = item não encontrado).
    /// </summary>
    public class CargaItemViewModel : INotifyPropertyChanged
    {
        private static readonly Brush CorPendente = BrushFactory.Criar("#E7E9EA");
        private static readonly Brush CorConcluida = BrushFactory.Criar("#00BA7C");
        private static readonly Brush CorParcial = BrushFactory.Criar("#FFB020");
        private static readonly Brush CorNaoEncontrado = BrushFactory.Criar("#F4212E");

        private StatusCarga _status = StatusCarga.Pendente;

        public CargaItemViewModel(TransferenciaPayload payload)
        {
            Payload = payload;
        }

        public TransferenciaPayload Payload { get; }

        public StatusCarga Status
        {
            get => _status;
            set
            {
                if (_status == value) return;
                _status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CorStatus));
            }
        }

        public Brush CorStatus => Status switch
        {
            StatusCarga.Concluida => CorConcluida,
            StatusCarga.Parcial => CorParcial,
            StatusCarga.ComNaoEncontrado => CorNaoEncontrado,
            _ => CorPendente
        };

        public string Destino => Payload.codigo_destino;
        public int QuantidadeItens => Payload.itens?.Length ?? 0;

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}