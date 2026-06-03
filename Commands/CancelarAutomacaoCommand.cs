using System;
using System.Windows.Input;
using TransferToolRPA.Services;
using TransferToolRPA.ViewModels;

namespace TransferToolRPA.Commands
{
    public class CancelarAutomacaoCommand : ICommand
    {
        private readonly MainViewModel _viewModel;
        private readonly ILoggerService _loggerService;

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public CancelarAutomacaoCommand(MainViewModel viewModel, ILoggerService loggerService)
        {
            _viewModel = viewModel;
            _loggerService = loggerService;
        }

        public bool CanExecute(object? parameter)
        {
            return _viewModel.IsExecuting;
        }

        public void Execute(object? parameter)
        {
            if (_viewModel.Cts != null && !_viewModel.Cts.IsCancellationRequested)
            {
                _loggerService.LogWarning("Cancelamento solicitado pelo operador. Interrompendo atividades...");
                _viewModel.Cts.Cancel();
            }
        }
    }
}
