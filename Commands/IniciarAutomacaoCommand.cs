using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using TransferToolRPA.Services;
using TransferToolRPA.ViewModels;

namespace TransferToolRPA.Commands
{
    public class IniciarAutomacaoCommand : ICommand
    {
        private readonly MainViewModel _viewModel;
        private readonly IAutomationService _automationService;
        private readonly ILoggerService _loggerService;
        private readonly ICargaQueueService _cargaQueueService;

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public IniciarAutomacaoCommand(MainViewModel viewModel, IAutomationService automationService, ILoggerService loggerService, ICargaQueueService cargaQueueService)
        {
            _viewModel = viewModel;
            _automationService = automationService;
            _loggerService = loggerService;
            _cargaQueueService = cargaQueueService;
        }

        public bool CanExecute(object? parameter)
        {
            return _viewModel.Payload != null && !_viewModel.IsExecuting;
        }

        public async void Execute(object? parameter)
        {
            if (_viewModel.Payload == null || _viewModel.IsExecuting) return;

            _viewModel.IsExecuting = true;
            _loggerService.Log("Iniciando automação do Atende.Net...");
            _viewModel.Cts = new CancellationTokenSource();

            var progress = new Progress<(string Mensagem, double Progresso)>(report =>
            {
                _viewModel.ProgressoMensagem = report.Mensagem;
                _viewModel.ProgressoPercent = report.Progresso;
                _loggerService.Log(report.Mensagem);
            });

            try
            {
                var token = _viewModel.Cts.Token;

                while (_cargaQueueService.Queue.Count > 0)
                {
                    token.ThrowIfCancellationRequested();

                    var payload = _cargaQueueService.Queue[0];
                    _loggerService.Log($"[FILA] Iniciando automação da carga para o destino: {payload.codigo_destino} (Cargas restantes: {_cargaQueueService.Queue.Count})...");

                    await Task.Run(async () => await _automationService.ExecutarAutomacaoAsync(payload, progress, token), token);

                    _loggerService.LogSuccess($"[FILA] Carga para o destino {payload.codigo_destino} concluída com sucesso.");

                    System.Windows.Application.Current.Dispatcher.Invoke(() => 
                    {
                        _cargaQueueService.Dequeue(out _);
                    });
                }

                _viewModel.ProgressoMensagem = "Fila de processamento concluída.";
                _viewModel.ProgressoPercent = 100;
            }
            catch (OperationCanceledException)
            {
                _viewModel.ProgressoMensagem = "Automação cancelada pelo operador.";
                _viewModel.ProgressoPercent = 0;
                _loggerService.LogWarning("Execução cancelada pelo operador.");
            }
            catch (Exception ex)
            {
                _viewModel.ProgressoMensagem = "Ocorreu uma falha durante a execução.";
                _loggerService.LogError($"Falha na automação: {ex.Message}");
                if (ex.InnerException != null)
                {
                    _loggerService.Log($"[DETALHES] {ex.InnerException.Message}");
                }
            }
            finally
            {
                _viewModel.IsExecuting = false;
                if (_viewModel.Cts != null)
                {
                    _viewModel.Cts.Dispose();
                    _viewModel.Cts = null;
                }
            }
        }
    }
}
