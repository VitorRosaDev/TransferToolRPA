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
        private readonly ICargaQueueService _cargaQueueService;

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public ImportarCargaCommand(MainViewModel viewModel, IPayloadService payloadService, ILoggerService loggerService, ICargaQueueService cargaQueueService)
        {
            _viewModel = viewModel;
            _payloadService = payloadService;
            _loggerService = loggerService;
            _cargaQueueService = cargaQueueService;
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
                var payloads = _payloadService.CarregarEValidar(filePath);

                _cargaQueueService.EnqueueRange(payloads);

                _viewModel.ProgressoPercent = 0;
                _viewModel.ProgressoMensagem = $"{payloads.Length} transferência(s) carregada(s) com sucesso! Pronto para iniciar.";
                _loggerService.LogSuccess($"{payloads.Length} transferência(s) importada(s). Total de itens: {payloads.Sum(p => p.itens.Length)}.");
            }
            catch (Exception ex)
            {
                _viewModel.ProgressoPercent = 0;
                _viewModel.ProgressoMensagem = "Falha ao carregar o arquivo JSON.";
                _loggerService.LogError($"Falha na importação: {ex.Message}");
            }
        }
    }
}