using TransferToolRPA.Models;
using TransferToolRPA.Services;
using TransferToolRPA.ViewModels;
using Xunit;

namespace TransferToolRPA.Tests
{
    public class CargaQueueServiceTests
    {
        private static TransferenciaPayload Payload(int id, string destino) =>
            new(id, "2026-06-03", "10", destino, new[] { new PayloadItemEntrada(new[] { "2201" }, 5) });

        [Fact]
        public void EnqueueRange_CriaWrappersComStatusPendente()
        {
            var service = new CargaQueueService();

            service.EnqueueRange(new[] { Payload(1, "70"), Payload(2, "99") });

            Assert.Equal(2, service.Queue.Count);
            Assert.Equal(StatusCarga.Pendente, service.Queue[0].Status);
            Assert.Equal("70", service.Queue[0].Destino);
            Assert.Equal(1, service.Queue[0].QuantidadeItens);
        }

        [Fact]
        public void MoverParaOFim_MoveOItemParaOFinal()
        {
            var service = new CargaQueueService();
            service.EnqueueRange(new[] { Payload(1, "70"), Payload(2, "99"), Payload(3, "80") });

            service.MoverParaOFim(service.Queue[0]);

            Assert.Equal("99", service.Queue[0].Destino);
            Assert.Equal("80", service.Queue[1].Destino);
            Assert.Equal("70", service.Queue[2].Destino);
        }

        [Fact]
        public void Status_Set_DisparaMudancaDeCor()
        {
            var service = new CargaQueueService();
            service.Enqueue(Payload(1, "70"));

            var item = service.Queue[0];
            bool mudou = false;
            item.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(CargaItemViewModel.CorStatus)) mudou = true;
            };

            item.Status = StatusCarga.Concluida;

            Assert.True(mudou);
            Assert.Equal(StatusCarga.Concluida, item.Status);
        }
    }
}