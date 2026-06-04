using System;
using System.Windows;
using TransferToolRPA.Services;
using TransferToolRPA.ViewModels;

namespace TransferToolRPA
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Inicialização dos serviços (Injeção de Dependência Manual)
            ILoggerService loggerService = new ObservableLoggerService();
            IPayloadService payloadService = new PayloadService();
            IAutomationService automationService = new PlaywrightAutomationService();
            ICargaQueueService cargaQueueService = new CargaQueueService();

            // Inicialização da ViewModel passando as dependências do Clean Architecture
            var viewModel = new MainViewModel(payloadService, automationService, loggerService, cargaQueueService);

            // Inicialização da View injetando a ViewModel
            var mainWindow = new MainWindow(viewModel);
            mainWindow.Show();
        }
    }
}
