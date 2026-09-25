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
        private string _logOutput = string.Empty;
        private bool _isExecuting = false;

        public ObservableCollection<TransferenciaPayload> CargasImportadas => _cargaQueueService.Queue;
        public TransferenciaPayload? Payload => CargasImportadas.FirstOrDefault();
        public CancellationTokenSource? Cts { get; set; }

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

        public string LogOutput
        {
            get => _logOutput;
            set { _logOutput = value; OnPropertyChanged(); }
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

            _loggerService.OnLogAdded += line =>
            {
                if (string.IsNullOrEmpty(line))
                {
                    LogOutput = string.Empty;
                }
                else
                {
                    LogOutput += line;
                }
            };

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

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}