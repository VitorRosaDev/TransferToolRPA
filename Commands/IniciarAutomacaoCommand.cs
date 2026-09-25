using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using TransferToolRPA.Models;
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

        /// <summary>
        /// Encaminha a mensagem de progresso ao logger com o nível adequado, para que o
        /// console pinte a linha com a cor correspondente (Erro=vermelho, Aviso=âmbar).
        /// </summary>
        private void RegistrarNoLog(ProgressoAutomacao report)
        {
            switch (report.Nivel)
            {
                case NivelLog.Sucesso:
                    _loggerService.LogSuccess(report.Mensagem);
                    break;
                case NivelLog.Aviso:
                    _loggerService.LogWarning(report.Mensagem);
                    break;
                case NivelLog.Erro:
                    _loggerService.LogError(report.Mensagem);
                    break;
                default:
                    _loggerService.Log(report.Mensagem);
                    break;
            }
        }

        /// <summary>
        /// Ao final do fluxo, pergunta se o operador deseja fechar o Chrome da sessao
        /// (porta 9222). Sim encerra o navegador; Nao mantem a janela e exibe um lembrete.
        /// </summary>
        private async Task PerguntarSobreFechamentoDoNavegadorAsync()
        {
            var resposta = MessageBox.Show(
                "TRANSFERÊNCIA FINALIZADA!\n\n" +
                "É recomendável fechar a janela do Chrome utilizada nesta sessão.\n\n" +
                "Fechar janela?",
                "TransferTool RPA",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (resposta == MessageBoxResult.Yes)
            {
                _loggerService.Log("Fechando a janela do Chrome (porta 9222)...");

                try
                {
                    await _automationService.FecharNavegadorAsync();
                    _loggerService.LogSuccess("Janela do Chrome encerrada.");
                }
                catch (Exception ex)
                {
                    _loggerService.LogWarning($"Não foi possível fechar o Chrome automaticamente: {ex.Message}");
                }
            }
            else
            {
                MessageBox.Show(
                    "Não se esqueça de fechar a janela do Chrome utilizada nesta sessão.",
                    "TransferTool RPA",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
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

            try
            {
                var token = _viewModel.Cts.Token;
                int pendentes = _cargaQueueService.Queue.Count;

                while (pendentes > 0)
                {
                    token.ThrowIfCancellationRequested();

                    var item = _cargaQueueService.Queue[0];
                    string destino = item.Payload.codigo_destino;
                    _loggerService.Log($"[FILA] Iniciando automação da carga para o destino: {destino} (Cargas restantes: {pendentes})...");

                    bool teveNaoEncontrado = false;
                    bool teveParcial = false;

                    var progress = new Progress<ProgressoAutomacao>(report =>
                    {
                        _viewModel.ProgressoMensagem = report.Mensagem;
                        _viewModel.ProgressoPercent = report.Percentual;

                        if (report.Resultado == ResultadoItemTransferencia.NaoEncontrado)
                        {
                            teveNaoEncontrado = true;
                        }
                        else if (report.Resultado == ResultadoItemTransferencia.Parcial)
                        {
                            teveParcial = true;
                        }

                        RegistrarNoLog(report);
                    });

                    try
                    {
                        await Task.Run(async () => await _automationService.ExecutarAutomacaoAsync(item.Payload, progress, token), token);

                        StatusCarga status = teveNaoEncontrado
                            ? StatusCarga.ComNaoEncontrado
                            : teveParcial ? StatusCarga.Parcial : StatusCarga.Concluida;

                        item.Status = status;
                        _loggerService.LogSuccess($"[FILA] Carga para o destino {destino} concluída.");

                        System.Windows.Application.Current.Dispatcher.Invoke(() => _cargaQueueService.MoverParaOFim(item));
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception)
                    {
                        item.Status = StatusCarga.ComNaoEncontrado;
                        throw;
                    }

                    pendentes--;
                }

                _viewModel.ProgressoMensagem = "Fila de processamento concluída.";
                _viewModel.ProgressoPercent = 100;

                await PerguntarSobreFechamentoDoNavegadorAsync();
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
