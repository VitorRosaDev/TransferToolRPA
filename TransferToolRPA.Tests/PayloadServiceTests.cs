using System;
using TransferToolRPA.Models;
using Xunit;

namespace TransferToolRPA.Tests
{
    public class PayloadServiceTests
    {
        [Fact]
        public void Validar_PayloadValido_DeveConcluirSemExcecao()
        {
            // Arrange
            var itens = new[] { new PayloadItem("2201", 10.5, "2027-12-10") };
            var payload = new TransferenciaPayload(1, "2026-06-03", "10", "109", itens);

            // Act & Assert (não deve lançar exceção)
            PayloadValidator.Validar(payload);
        }

        [Fact]
        public void Validar_PayloadNulo_DeveLancarArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => PayloadValidator.Validar(null!));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        public void Validar_IdAppInvalido_DeveLancarArgumentException(int idAppInvalido)
        {
            // Arrange
            var itens = new[] { new PayloadItem("2201", 10, null) };
            var payload = new TransferenciaPayload(idAppInvalido, "2026-06-03", "10", "109", itens);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => PayloadValidator.Validar(payload));
            Assert.Contains("id_app", ex.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Validar_OrigemVazia_DeveLancarArgumentException(string origemVazia)
        {
            // Arrange
            var itens = new[] { new PayloadItem("2201", 10, null) };
            var payload = new TransferenciaPayload(1, "2026-06-03", origemVazia, "109", itens);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => PayloadValidator.Validar(payload));
            Assert.Contains("codigo_origem", ex.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Validar_DestinoVazio_DeveLancarArgumentException(string destinoVazio)
        {
            // Arrange
            var itens = new[] { new PayloadItem("2201", 10, null) };
            var payload = new TransferenciaPayload(1, "2026-06-03", "10", destinoVazio, itens);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => PayloadValidator.Validar(payload));
            Assert.Contains("codigo_destino", ex.Message);
        }

        [Fact]
        public void Validar_ItensNuloOuVazio_DeveLancarArgumentException()
        {
            // Arrange
            var payloadNulo = new TransferenciaPayload(1, "2026-06-03", "10", "109", null!);
            var payloadVazio = new TransferenciaPayload(1, "2026-06-03", "10", "109", Array.Empty<PayloadItem>());

            // Act & Assert
            Assert.Throws<ArgumentException>(() => PayloadValidator.Validar(payloadNulo));
            Assert.Throws<ArgumentException>(() => PayloadValidator.Validar(payloadVazio));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-10.5)]
        public void Validar_QuantidadeInvalida_DeveLancarArgumentException(double qtdInvalida)
        {
            // Arrange
            var itens = new[] { new PayloadItem("2201", qtdInvalida, null) };
            var payload = new TransferenciaPayload(1, "2026-06-03", "10", "109", itens);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => PayloadValidator.Validar(payload));
            Assert.Contains("quantidade", ex.Message);
        }
    }
}
