using System;
using System.Windows;
using System.Windows.Input;
using TransferToolRPA.Models;
using TransferToolRPA.Services;
using TransferToolRPA.ViewModels;

namespace TransferToolRPA.Commands
{
    public class AbrirNavegadorCommand : ICommand
    {
        private readonly MainViewModel _viewModel;
        private readonly ILoggerService _loggerService;

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public AbrirNavegadorCommand(MainViewModel viewModel, ILoggerService loggerService)
        {
            _viewModel = viewModel;
            _loggerService = loggerService;
        }

        public bool CanExecute(object? parameter)
        {
            return !_viewModel.IsExecuting;
        }

        public void Execute(object? parameter)
        {
            _loggerService.Log("Iniciando navegador com perfil de depuração habilitado...");

            bool sucesso = NavegadorHelper.IniciarNavegadorComDepuracao();
            if (sucesso)
            {
                _loggerService.LogSuccess("Navegador iniciado com sucesso na porta 9222.");
                _loggerService.Log("✅ Chrome aberto na tela de login do Atende.Net.");
                _loggerService.Log("👉 Faça login manualmente e, após acessar o sistema, clique em 'Iniciar RPA'.");
            }
            else
            {
                _loggerService.LogError("Não foi possível localizar o Google Chrome ou Microsoft Edge no sistema.");
                MessageBox.Show(
                    "Não foi possível iniciar o navegador automaticamente.\n" +
                    "Certifique-se de que o Google Chrome ou Microsoft Edge está instalado nos caminhos padrões do Windows.",
                    "Erro ao Iniciar Navegador",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
