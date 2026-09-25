using System;
using System.Threading;
using System.Threading.Tasks;
using TransferToolRPA.Models;

namespace TransferToolRPA.Services
{
    public interface IAutomationService
    {
        Task ExecutarAutomacaoAsync(
            TransferenciaPayload payload,
            IProgress<ProgressoAutomacao> progressReporter,
            CancellationToken cancellationToken
        );

        /// <summary>
        /// Encerra o navegador Chrome/Edge conectado via CDP (porta 9222). Se o
        /// encerramento via CDP nao funcionar, encerra o processo que usa a porta 9222.
        /// </summary>
        Task FecharNavegadorAsync();
    }
}
