using System;
using System.IO;
using TransferToolRPA.Models;
using Xunit;

namespace TransferToolRPA.Tests
{
    public class PayloadServiceTests
    {
        [Fact]
        public void Validar_PayloadValido_DeveConcluirSemExcecao()
        {
            var itens = new[] { new PayloadItemEntrada(new[] { "2201" }, 10.5) };
            var payload = new TransferenciaPayload(1, "2026-06-03", "10", "109", itens);

            PayloadValidator.Validar(payload);
        }

        [Fact]
        public void Validar_PayloadNulo_DeveLancarArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => PayloadValidator.Validar(null!));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        public void Validar_IdAppInvalido_DeveLancarArgumentException(int idAppInvalido)
        {
            var itens = new[] { new PayloadItemEntrada(new[] { "2201" }, 10) };
            var payload = new TransferenciaPayload(idAppInvalido, "2026-06-03", "10", "109", itens);

            var ex = Assert.Throws<ArgumentException>(() => PayloadValidator.Validar(payload));
            Assert.Contains("id_app", ex.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Validar_OrigemVazia_DeveLancarArgumentException(string origemVazia)
        {
            var itens = new[] { new PayloadItemEntrada(new[] { "2201" }, 10) };
            var payload = new TransferenciaPayload(1, "2026-06-03", origemVazia, "109", itens);

            var ex = Assert.Throws<ArgumentException>(() => PayloadValidator.Validar(payload));
            Assert.Contains("codigo_origem", ex.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Validar_DestinoVazio_DeveLancarArgumentException(string destinoVazio)
        {
            var itens = new[] { new PayloadItemEntrada(new[] { "2201" }, 10) };
            var payload = new TransferenciaPayload(1, "2026-06-03", "10", destinoVazio, itens);

            var ex = Assert.Throws<ArgumentException>(() => PayloadValidator.Validar(payload));
            Assert.Contains("codigo_destino", ex.Message);
        }

        [Fact]
        public void Validar_ItensNuloOuVazio_DeveLancarArgumentException()
        {
            var payloadNulo = new TransferenciaPayload(1, "2026-06-03", "10", "109", null!);
            var payloadVazio = new TransferenciaPayload(1, "2026-06-03", "10", "109", Array.Empty<PayloadItemEntrada>());

            Assert.Throws<ArgumentException>(() => PayloadValidator.Validar(payloadNulo));
            Assert.Throws<ArgumentException>(() => PayloadValidator.Validar(payloadVazio));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-10.5)]
        public void Validar_QuantidadeInvalida_DeveLancarArgumentException(double qtdInvalida)
        {
            var itens = new[] { new PayloadItemEntrada(new[] { "2201" }, qtdInvalida) };
            var payload = new TransferenciaPayload(1, "2026-06-03", "10", "109", itens);

            var ex = Assert.Throws<ArgumentException>(() => PayloadValidator.Validar(payload));
            Assert.Contains("quantidade", ex.Message);
        }

        [Fact]
        public void Validar_CodigosArrayVazio_DeveLancarArgumentException()
        {
            var itens = new[] { new PayloadItemEntrada(Array.Empty<string>(), 10) };
            var payload = new TransferenciaPayload(1, "2026-06-03", "10", "109", itens);

            var ex = Assert.Throws<ArgumentException>(() => PayloadValidator.Validar(payload));
            Assert.Contains("código de produto ausente", ex.Message);
        }

        [Fact]
        public void Validar_CodigoPrimeiroVazio_DeveLancarArgumentException()
        {
            var itens = new[] { new PayloadItemEntrada(new[] { "" }, 10) };
            var payload = new TransferenciaPayload(1, "2026-06-03", "10", "109", itens);

            var ex = Assert.Throws<ArgumentException>(() => PayloadValidator.Validar(payload));
            Assert.Contains("código de produto vazio", ex.Message);
        }

        [Fact]
        public void Validar_CodigoComCaracteresSuspeitos_DeveLancarArgumentException()
        {
            var itens = new[] { new PayloadItemEntrada(new[] { "2201<script>" }, 10) };
            var payload = new TransferenciaPayload(1, "2026-06-03", "10", "109", itens);

            var ex = Assert.Throws<ArgumentException>(() => PayloadValidator.Validar(payload));
            Assert.Contains("caracteres inválidos", ex.Message);
        }

        [Fact]
        public void Validar_QuantidadeExcedeLimite_DeveLancarArgumentException()
        {
            var itens = new[] { new PayloadItemEntrada(new[] { "2201" }, 1000001) };
            var payload = new TransferenciaPayload(1, "2026-06-03", "10", "109", itens);

            var ex = Assert.Throws<ArgumentException>(() => PayloadValidator.Validar(payload));
            Assert.Contains("1.000.000", ex.Message);
        }

        [Fact]
        public void ValidarTodas_ArrayValido_DeveConcluirSemExcecao()
        {
            var payload1 = new TransferenciaPayload(1, "2026-06-03", "10", "70", new[] { new PayloadItemEntrada(new[] { "2201" }, 10) });
            var payload2 = new TransferenciaPayload(2, "2026-06-03", "10", "73", new[] { new PayloadItemEntrada(new[] { "2218" }, 5) });

            PayloadValidator.ValidarTodas(new[] { payload1, payload2 });
        }

        [Fact]
        public void ValidarTodas_ArrayNulo_DeveLancarArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => PayloadValidator.ValidarTodas(null!));
        }

        [Fact]
        public void ValidarTodas_ArrayVazio_DeveLancarArgumentException()
        {
            var ex = Assert.Throws<ArgumentException>(() => PayloadValidator.ValidarTodas(Array.Empty<TransferenciaPayload>()));
            Assert.Contains("Nenhuma transferência", ex.Message);
        }

        [Fact]
        public void CarregarDeArquivo_ArquivoNaoExiste_DeveLancarFileNotFoundException()
        {
            Assert.Throws<FileNotFoundException>(() => PayloadValidator.CarregarDeArquivo("arquivo_inexistente.json"));
        }

        [Fact]
        public void ExpandirItens_RetornaItensInternosCorretos()
        {
            var payload = new TransferenciaPayload(1, "2026-06-03", "10", "70", new[]
            {
                new PayloadItemEntrada(new[] { "25510" }, 8),
                new PayloadItemEntrada(new[] { "2201" }, 120)
            });

            var expandidos = PayloadValidator.ExpandirItens(payload).ToArray();

            Assert.Equal(2, expandidos.Length);
            Assert.Equal("25510", expandidos[0].Codigo);
            Assert.Equal(8, expandidos[0].Quantidade);
            Assert.Equal("2201", expandidos[1].Codigo);
            Assert.Equal(120, expandidos[1].Quantidade);
        }

        [Fact]
        public void ExpandirItens_PayloadNulo_RetornaVazio()
        {
            var expandidos = PayloadValidator.ExpandirItens(null!);
            Assert.Empty(expandidos);
        }
    }
}