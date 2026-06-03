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

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public IniciarAutomacaoCommand(MainViewModel viewModel, IAutomationService automationService, ILoggerService loggerService)
        {
            _viewModel = viewModel;
            _automationService = automationService;
            _loggerService = loggerService;
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
                var payload = _viewModel.Payload;
                var token = _viewModel.Cts.Token;

                await Task.Run(async () => await _automationService.ExecutarAutomacaoAsync(payload, progress, token), token);

                _loggerService.LogSuccess("Automação concluída sem erros.");
                _viewModel.SetPayload(null); // Limpa o payload processado
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
