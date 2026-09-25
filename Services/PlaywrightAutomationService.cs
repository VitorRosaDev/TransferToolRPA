using System;
using System.Threading;
using System.Threading.Tasks;
using TransferToolRPA.Models;

namespace TransferToolRPA.Services
{
    public class PlaywrightAutomationService : IAutomationService
    {
        public async Task ExecutarAutomacaoAsync(
            TransferenciaPayload payload,
            IProgress<(string Mensagem, double Progresso)> progressReporter,
            CancellationToken cancellationToken)
        {
            var flow = new AtendeNetFlow(progressReporter, cancellationToken);
            var engine = new AutomationEngine(payload, progressReporter, cancellationToken, flow);
            await engine.ExecutarAsync();
        }
    }
}