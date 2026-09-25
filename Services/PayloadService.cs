using TransferToolRPA.Models;

namespace TransferToolRPA.Services
{
    public class PayloadService : IPayloadService
    {
        public TransferenciaPayload[] CarregarEValidar(string filePath)
        {
            return PayloadValidator.CarregarDeArquivo(filePath);
        }
    }
}