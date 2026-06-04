using System;
using System.Windows;
using System.Windows.Input;
using TransferToolRPA.Models;
using TransferToolRPA.Services;
using TransferToolRPA.ViewModels;

namespace TransferToolRPA.Commands
{
    public class ExcluirCargaCommand : ICommand
    {
        private readonly MainViewModel _viewModel;
        private readonly ILoggerService _loggerService;
        private readonly ICargaQueueService _cargaQueueService;

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public ExcluirCargaCommand(MainViewModel viewModel, ILoggerService loggerService, ICargaQueueService cargaQueueService)
        {
            _viewModel = viewModel;
            _loggerService = loggerService;
            _cargaQueueService = cargaQueueService;
        }

        public bool CanExecute(object? parameter)
        {
            return !_viewModel.IsExecuting && parameter is TransferenciaPayload;
        }

        public void Execute(object? parameter)
        {
            if (parameter is not TransferenciaPayload payload) return;

            var result = MessageBox.Show(
                $"Tem certeza que deseja remover a carga para a escola de destino \"{payload.codigo_destino}\"?",
                "Confirmar Exclusão",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _cargaQueueService.Remove(payload);
                _loggerService.Log($"Carga para {payload.codigo_destino} excluída da fila pelo operador.");
            }
        }
    }
}
