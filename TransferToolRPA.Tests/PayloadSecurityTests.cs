using System;
using TransferToolRPA.Models;
using Xunit;

namespace TransferToolRPA.Tests
{
    public class PayloadSecurityTests
    {
        [Theory]
        [InlineData("10; DROP TABLE depositos;")]
        [InlineData("10\nSystem.Console.WriteLine()")]
        [InlineData("10<script>alert(1)</script>")]
        public void Validar_CodigoOrigemComCaracteresInvalidos_DeveLancarArgumentException(string origemInjetada)
        {
            // Arrange
            var itens = new[] { new PayloadItem("2201", 10, null) };
            var payload = new TransferenciaPayload(1, "2026-06-03", origemInjetada, "109", itens);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => PayloadValidator.Validar(payload));
            Assert.Contains("codigo_origem", ex.Message);
        }

        [Theory]
        [InlineData("../../../etc/passwd")]
        [InlineData(@"..\..\Windows\System32")]
        public void Validar_CodigoDestinoComPathTraversal_DeveLancarArgumentException(string destinoTraversal)
        {
            // Arrange
            var itens = new[] { new PayloadItem("2201", 10, null) };
            var payload = new TransferenciaPayload(1, "2026-06-03", "10", destinoTraversal, itens);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => PayloadValidator.Validar(payload));
            Assert.Contains("codigo_destino", ex.Message);
        }

        [Fact]
        public void Validar_CodigoProdutoGigante_DeveLancarArgumentException()
        {
            // Arrange
            string codigoGigante = new string('A', 100); // Excede o limite de 50 caracteres
            var itens = new[] { new PayloadItem(codigoGigante, 10, null) };
            var payload = new TransferenciaPayload(1, "2026-06-03", "10", "109", itens);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => PayloadValidator.Validar(payload));
            Assert.Contains("código do produto", ex.Message.ToLower());
        }

        [Theory]
        [InlineData(1000001)] // Limite é 1.000.000
        [InlineData(5000000)]
        public void Validar_QuantidadeExorbitante_DeveLancarArgumentException(double quantidadeExorbitante)
        {
            // Arrange
            var itens = new[] { new PayloadItem("2201", quantidadeExorbitante, null) };
            var payload = new TransferenciaPayload(1, "2026-06-03", "10", "109", itens);

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => PayloadValidator.Validar(payload));
            Assert.Contains("excede o limite máximo", ex.Message);
        }
    }
}
