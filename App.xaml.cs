using System;
using System.Windows;
using TransferToolRPA.Models;
using TransferToolRPA.Services;
using TransferToolRPA.ViewModels;

namespace TransferToolRPA
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // CRÍTICO: resolve e aplica os caminhos do Playwright (driver + browsers) ANTES de
            // qualquer uso do Playwright. Em publish self-contained sem single-file, o diretório
            // base é o da própria aplicação e o driver (node.exe) fica em ".playwright\node\win32_x64".
            var playwrightPaths = PlaywrightPathResolver.Configure();

            // Inicialização dos serviços (Injeção de Dependência Manual)
            var loggerConcreto = new ObservableLoggerService();
            ILoggerService loggerService = loggerConcreto;
            IPayloadService payloadService = new PayloadService();
            IAutomationService automationService = new PlaywrightAutomationService();
            ICargaQueueService cargaQueueService = new CargaQueueService();

            if (!string.IsNullOrEmpty(loggerConcreto.CaminhoArquivoLog))
            {
                loggerService.Log($"Log persistido em: {loggerConcreto.CaminhoArquivoLog}");
            }

            ReportarDiagnosticoPlaywright(loggerService, playwrightPaths);

            // Inicialização da ViewModel passando as dependências do Clean Architecture
            var viewModel = new MainViewModel(payloadService, automationService, loggerService, cargaQueueService);

            // Inicialização da View injetando a ViewModel
            var mainWindow = new MainWindow(viewModel);
            mainWindow.Show();
        }

        /// <summary>
        /// Reporta no console de logs o resultado da resolução dos caminhos do Playwright,
        /// facilitando o diagnóstico de problemas de deploy (driver/browsers ausentes).
        /// </summary>
        private static void ReportarDiagnosticoPlaywright(ILoggerService logger, PlaywrightPaths paths)
        {
            logger.Log($"Diretório base da aplicação: {paths.BaseDirectory}");

            if (paths.DriverFound)
            {
                logger.Log($"Driver do Playwright localizado: {paths.DriverExecutable}");
                logger.Log($"{PlaywrightPathResolver.DriverPathVariable}={paths.BaseDirectory}");
            }
            else
            {
                logger.LogWarning(
                    $"Driver do Playwright (node.exe) não encontrado em \"{paths.DriverExecutable}\". " +
                    "A automação falhará com 'Driver not found'. Refaça o publish garantindo que a pasta " +
                    ".playwright (com node\\win32_x64\\node.exe) seja copiada ao lado do executável.");
            }

            if (paths.BrowsersFound)
            {
                logger.Log($"Browsers do Playwright localizados em: {paths.PlaywrightDirectory}");
                logger.Log($"{PlaywrightPathResolver.BrowsersPathVariable}={paths.PlaywrightDirectory}");
            }
            else
            {
                logger.LogWarning(
                    $"Browsers do Playwright não encontrados em \"{paths.PlaywrightDirectory}\". " +
                    "Isso não impede a conexão via CDP a um navegador já aberto, mas impede abrir um " +
                    "navegador gerenciado pelo Playwright.");
            }
        }
    }
}

