using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using TransferToolRPA.Models;

namespace TransferToolRPA.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private string _jsonFilePath = string.Empty;
        private string _origem = "-";
        private string _destino = "-";
        private int _totalItens = 0;
        private double _progressoPercent = 0;
        private string _progressoMensagem = "Aguardando importação de arquivo JSON...";
        private string _logOutput = string.Empty;
        private bool _isExecuting = false;
        private TransferenciaPayload? _payload;
        private CancellationTokenSource? _cts;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string JsonFilePath
        {
            get => _jsonFilePath;
            set { _jsonFilePath = value; OnPropertyChanged(); }
        }

        public string Origem
        {
            get => _origem;
            set { _origem = value; OnPropertyChanged(); }
        }

        public string Destino
        {
            get => _destino;
            set { _destino = value; OnPropertyChanged(); }
        }

        public int TotalItens
        {
            get => _totalItens;
            set { _totalItens = value; OnPropertyChanged(); }
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

        public MainViewModel()
        {
            ImportarJsonCommand = new RelayCommand(param => ImportarJson(param as string));
            IniciarAutomacaoCommand = new RelayCommand(async _ => await IniciarAutomacaoAsync(), _ => _payload != null && !IsExecuting);
            CancelarAutomacaoCommand = new RelayCommand(_ => CancelarAutomacao(), _ => IsExecuting);

            Log("Aplicativo inicializado. Pronto para receber cargas do TransferTool Mobile.");
        }

        public void ImportarJson(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return;

            try
            {
                IsExecuting = false;
                JsonFilePath = filePath;
                
                Log($"Carregando payload JSON: {Path.GetFileName(filePath)}...");
                _payload = PayloadValidator.CarregarDeArquivo(filePath);

                Origem = _payload.codigo_origem;
                Destino = _payload.codigo_destino;
                TotalItens = _payload.itens.Length;

                ProgressoPercent = 0;
                ProgressoMensagem = "Carga carregada com sucesso! Pronto para iniciar.";
                Log($"[SUCESSO] Carga importada com sucesso. ID da Carga: {_payload.id_app}. Total de itens: {TotalItens}.");
            }
            catch (Exception ex)
            {
                _payload = null;
                Origem = "-";
                Destino = "-";
                TotalItens = 0;
                ProgressoPercent = 0;
                ProgressoMensagem = "Falha ao carregar o arquivo JSON.";
                Log($"[ERRO] Falha na importação: {ex.Message}");
            }
        }

        private async Task IniciarAutomacaoAsync()
        {
            if (_payload == null) return;

            IsExecuting = true;
            Log("Iniciando automação do Atende.Net...");
            _cts = new CancellationTokenSource();

            var progress = new Progress<(string Mensagem, double Progresso)>(report =>
            {
                ProgressoMensagem = report.Mensagem;
                ProgressoPercent = report.Progresso;
                Log(report.Mensagem);
            });

            try
            {
                var engine = new AutomationEngine(_payload, progress, _cts.Token);
                await Task.Run(async () => await engine.ExecutarAsync(), _cts.Token);

                Log("[SUCESSO] Automação concluída sem erros.");
                _payload = null; // Limpa o payload processado
            }
            catch (OperationCanceledException)
            {
                ProgressoMensagem = "Automação cancelada pelo operador.";
                ProgressoPercent = 0;
                Log("[AVISO] Execução cancelada pelo operador.");
            }
            catch (Exception ex)
            {
                ProgressoMensagem = "Ocorreu uma falha durante a execução.";
                Log($"[ERRO CRÍTICO] Falha na automação: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Log($"[DETALHES] {ex.InnerException.Message}");
                }
            }
            finally
            {
                IsExecuting = false;
                _cts.Dispose();
                _cts = null;
            }
        }

        private void CancelarAutomacao()
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                Log("Cancelamento solicitado pelo operador. Interrompendo atividades...");
                _cts.Cancel();
            }
        }

        private void Log(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogOutput += $"[{timestamp}] {message}\n";
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    // Implementação simples de ICommand para manter o padrão MVVM desacoplado
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute(parameter);
        public void Execute(object? parameter) => _execute(parameter);
    }
}
