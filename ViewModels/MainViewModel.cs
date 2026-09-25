using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Input;
using TransferToolRPA.Commands;
using TransferToolRPA.Models;
using TransferToolRPA.Services;

namespace TransferToolRPA.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly ILoggerService _loggerService;
        private readonly ICargaQueueService _cargaQueueService;

        private string _jsonFilePath = string.Empty;
        private double _progressoPercent = 0;
        private string _progressoMensagem = "Aguardando importação de arquivo JSON...";
        private bool _isExecuting = false;

        public ObservableCollection<CargaItemViewModel> CargasImportadas => _cargaQueueService.Queue;
        public TransferenciaPayload? Payload => CargasImportadas.FirstOrDefault(c => c.Status == StatusCarga.Pendente)?.Payload;
        public CancellationTokenSource? Cts { get; set; }

        /// <summary>Console de logs em tempo real, com cor por severidade (NivelLog).</summary>
        public ObservableCollection<LogEntry> LogEntries { get; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        public string JsonFilePath
        {
            get => _jsonFilePath;
            set { _jsonFilePath = value; OnPropertyChanged(); }
        }

        public double ProgressoPercent
        {
            get => _progressoPercent;
            set { _progressoPercent = value; OnPropertyChanged(); }
        }

        public string ProgressoMensagem
        {
            get => _progressoMensagem;
            set { _progressoMensagem = value; OnPropertyChanged(); }
        }

        public bool IsExecuting
        {
            get => _isExecuting;
            set
            {
                _isExecuting = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public ICommand ImportarJsonCommand { get; }
        public ICommand IniciarAutomacaoCommand { get; }
        public ICommand CancelarAutomacaoCommand { get; }
        public ICommand ExcluirCargaCommand { get; }
        public ICommand AbrirNavegadorCommand { get; }

        public MainViewModel(
            IPayloadService payloadService,
            IAutomationService automationService,
            ILoggerService loggerService,
            ICargaQueueService cargaQueueService)
        {
            _loggerService = loggerService;
            _cargaQueueService = cargaQueueService;

            _loggerService.OnLogAdded += AplicarLog;

            _cargaQueueService.OnQueueChanged += () =>
            {
                OnPropertyChanged(nameof(Payload));
                OnPropertyChanged(nameof(CargasImportadas));
                CommandManager.InvalidateRequerySuggested();
            };

            ImportarJsonCommand = new ImportarCargaCommand(this, payloadService, loggerService, _cargaQueueService);
            IniciarAutomacaoCommand = new IniciarAutomacaoCommand(this, automationService, loggerService, _cargaQueueService);
            CancelarAutomacaoCommand = new CancelarAutomacaoCommand(this, loggerService);
            ExcluirCargaCommand = new ExcluirCargaCommand(this, loggerService, _cargaQueueService);
            AbrirNavegadorCommand = new AbrirNavegadorCommand(this, loggerService);

            _loggerService.Log("Aplicativo inicializado. Pronto para receber cargas do TransferTool Mobile.");
        }

        /// <summary>
        /// Adiciona uma linha ao console de logs, garantindo execução na thread da UI
        /// (a automação pode reportar de threads de fundo). Um entry vazio sinaliza Clear.
        /// </summary>
        private void AplicarLog(LogEntry entry)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(() => AplicarLogNaColecao(entry));
            }
            else
            {
                AplicarLogNaColecao(entry);
            }
        }

        private void AplicarLogNaColecao(LogEntry entry)
        {
            try
            {
                if (string.IsNullOrEmpty(entry.Texto))
                {
                    LogEntries.Clear();
                    return;
                }

                LogEntries.Add(entry);
            }
            catch
            {
                // Exibição de log é não-crítica: nunca deve derrubar a aplicação.
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}