using System;
using System.IO;
using System.Windows.Input;
using TransferToolRPA.Services;
using TransferToolRPA.ViewModels;

namespace TransferToolRPA.Commands
{
    public class ImportarCargaCommand : ICommand
    {
        private readonly MainViewModel _viewModel;
        private readonly IPayloadService _payloadService;
        private readonly ILoggerService _loggerService;

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public ImportarCargaCommand(MainViewModel viewModel, IPayloadService payloadService, ILoggerService loggerService)
        {
            _viewModel = viewModel;
            _payloadService = payloadService;
            _loggerService = loggerService;
        }

        public bool CanExecute(object? parameter)
        {
            return !_viewModel.IsExecuting;
        }

        public void Execute(object? parameter)
        {
            string? filePath = parameter as string;
            if (string.IsNullOrWhiteSpace(filePath)) return;

            try
            {
                _viewModel.IsExecuting = false;
                _viewModel.JsonFilePath = filePath;

                _loggerService.Log($"Carregando payload JSON: {Path.GetFileName(filePath)}...");
                var payload = _payloadService.CarregarEValidar(filePath);

                _viewModel.SetPayload(payload);

                _viewModel.ProgressoPercent = 0;
                _viewModel.ProgressoMensagem = "Carga carregada com sucesso! Pronto para iniciar.";
                _loggerService.LogSuccess($"Carga importada com sucesso. ID da Carga: {payload.id_app}. Total de itens: {payload.itens.Length}.");
            }
            catch (Exception ex)
            {
                _viewModel.SetPayload(null);
                _viewModel.ProgressoPercent = 0;
                _viewModel.ProgressoMensagem = "Falha ao carregar o arquivo JSON.";
                _loggerService.LogError($"Falha na importação: {ex.Message}");
            }
        }
    }
}
