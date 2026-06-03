using TransferToolRPA.Models;

namespace TransferToolRPA.Services
{
    public interface IPayloadService
    {
        TransferenciaPayload CarregarEValidar(string filePath);
    }
}
