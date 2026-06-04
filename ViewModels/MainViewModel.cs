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

        private string _jsonFilePath = string.Empty;
        private double _progressoPercent = 0;
        private string _progressoMensagem = "Aguardando importação de arquivo JSON...";
        private string _logOutput = string.Empty;
        private bool _isExecuting = false;
        
        public ObservableCollection<TransferenciaPayload> CargasImportadas { get; } = new();
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
            ILoggerService loggerService)
        {
            _loggerService = loggerService;

            // Inscreve a propriedade LogOutput para reagir ao serviço de Logs
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

            CargasImportadas.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(Payload));
                CommandManager.InvalidateRequerySuggested();
            };

            ImportarJsonCommand = new ImportarCargaCommand(this, payloadService, loggerService);
            IniciarAutomacaoCommand = new IniciarAutomacaoCommand(this, automationService, loggerService);
            CancelarAutomacaoCommand = new CancelarAutomacaoCommand(this, loggerService);
            ExcluirCargaCommand = new ExcluirCargaCommand(this, loggerService);
            AbrirNavegadorCommand = new AbrirNavegadorCommand(this, loggerService);

            _loggerService.Log("Aplicativo inicializado. Pronto para receber cargas do TransferTool Mobile.");
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
