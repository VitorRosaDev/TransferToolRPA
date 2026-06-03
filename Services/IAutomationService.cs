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
            IProgress<(string Mensagem, double Progresso)> progressReporter, 
            CancellationToken cancellationToken
        );
    }
}
