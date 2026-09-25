using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using NSubstitute;
using TransferToolRPA.Models;
using Xunit;

namespace TransferToolRPA.Tests
{
    public class AtendeNetFlowLogicTests
    {
        private readonly AtendeNetFlow _flow;
        private readonly IProgress<(string Mensagem, double Progresso)> _progress;
        private readonly CancellationToken _cancellationToken;

        public AtendeNetFlowLogicTests()
        {
            _progress = Substitute.For<IProgress<(string Mensagem, double Progresso)>>();
            _cancellationToken = CancellationToken.None;
            _flow = new AtendeNetFlow(_progress, _cancellationToken);
        }

        [Fact]
        public void SelecionarMelhorLoteComCompletamento_QuantidadeSuficienteNoPrimeiro_DeveRetornarPrimeiro()
        {
            var lotes = new List<AtendeNetFlowTestHelper.LoteInfo>
            {
                new AtendeNetFlowTestHelper.LoteInfo(0, new DateTime(2027, 6, 15), 100, null),
                new AtendeNetFlowTestHelper.LoteInfo(1, new DateTime(2027, 12, 31), 200, null)
            };

            var resultado = AtendeNetFlowTestHelper.SelecionarMelhorLoteComCompletamento(lotes, 50);

            Assert.Equal(0, resultado.IndiceLote);
            Assert.Equal(50, resultado.QuantidadeUsada);
        }

        [Fact]
        public void SelecionarMelhorLoteComCompletamento_QuantidadeInsuficienteNoPrimeiro_DeveUsarQuantidadeDisponivel()
        {
            var lotes = new List<AtendeNetFlowTestHelper.LoteInfo>
            {
                new AtendeNetFlowTestHelper.LoteInfo(0, new DateTime(2027, 6, 15), 30, null),
                new AtendeNetFlowTestHelper.LoteInfo(1, new DateTime(2027, 12, 31), 100, null)
            };

            var resultado = AtendeNetFlowTestHelper.SelecionarMelhorLoteComCompletamento(lotes, 50);

            Assert.Equal(0, resultado.IndiceLote);
            Assert.Equal(30, resultado.QuantidadeUsada);
        }

        [Fact]
        public void SelecionarMelhorLoteComCompletamento_QuantidadeExataNoPrimeiro_DeveRetornarTotal()
        {
            var lotes = new List<AtendeNetFlowTestHelper.LoteInfo>
            {
                new AtendeNetFlowTestHelper.LoteInfo(0, new DateTime(2027, 6, 15), 50, null),
                new AtendeNetFlowTestHelper.LoteInfo(1, new DateTime(2027, 12, 31), 100, null)
            };

            var resultado = AtendeNetFlowTestHelper.SelecionarMelhorLoteComCompletamento(lotes, 50);

            Assert.Equal(0, resultado.IndiceLote);
            Assert.Equal(50, resultado.QuantidadeUsada);
        }

        [Fact]
        public void SelecionarMelhorLoteComCompletamento_MultiplosLotesMesmaValidade_DeveEscolherMenorQuantidade()
        {
            var lotes = new List<AtendeNetFlowTestHelper.LoteInfo>
            {
                new AtendeNetFlowTestHelper.LoteInfo(0, new DateTime(2027, 6, 15), 100, null),
                new AtendeNetFlowTestHelper.LoteInfo(1, new DateTime(2027, 6, 15), 50, null),
                new AtendeNetFlowTestHelper.LoteInfo(2, new DateTime(2027, 6, 15), 75, null)
            };

            var resultado = AtendeNetFlowTestHelper.SelecionarMelhorLoteComCompletamento(lotes, 40);

            Assert.Equal(1, resultado.IndiceLote);
            Assert.Equal(40, resultado.QuantidadeUsada);
        }

        [Fact]
        public void SelecionarMelhorLoteComCompletamento_ListaVazia_DeveLancarExcecao()
        {
            var lotes = new List<AtendeNetFlowTestHelper.LoteInfo>();

            Assert.Throws<InvalidOperationException>(() => AtendeNetFlowTestHelper.SelecionarMelhorLoteComCompletamento(lotes, 50));
        }

        [Fact]
        public void SelecionarMelhorLoteComCompletamento_TodosLotesSemQuantidade_DeveLancarExcecao()
        {
            var lotes = new List<AtendeNetFlowTestHelper.LoteInfo>
            {
                new AtendeNetFlowTestHelper.LoteInfo(0, new DateTime(2027, 6, 15), 0, null),
                new AtendeNetFlowTestHelper.LoteInfo(1, new DateTime(2027, 12, 31), 0, null)
            };

            Assert.Throws<InvalidOperationException>(() => AtendeNetFlowTestHelper.SelecionarMelhorLoteComCompletamento(lotes, 50));
        }
    }

    public static class AtendeNetFlowTestHelper
    {
        public static (int IndiceLote, double QuantidadeUsada) SelecionarMelhorLoteComCompletamento(List<LoteInfo> lotes, double quantidadeNecessaria)
        {
            var lotesOrdenados = lotes.OrderBy(l => l.Validade).ThenBy(l => l.Quantidade).ToList();
            var lotesValidos = lotesOrdenados.Where(l => l.Quantidade > 0.001).ToList();
            if (lotesValidos.Count == 0)
            {
                throw new InvalidOperationException("Nenhum lote com quantidade disponível encontrado.");
            }

            var melhorLote = lotesValidos[0];
            double quantidadeUsada = Math.Min(melhorLote.Quantidade, quantidadeNecessaria);

            return (melhorLote.Indice, quantidadeUsada);
        }

        public record LoteInfo(int Indice, DateTime Validade, double Quantidade, ILocator Linha);
    }
}