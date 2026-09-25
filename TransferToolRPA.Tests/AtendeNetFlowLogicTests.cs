using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using NSubstitute;
using TransferToolRPA.Models;
using Xunit;

namespace TransferToolRPA.Tests
{
    /// <summary>
    /// Testes de comportamento positivo do fluxo (leitura de lotes e seleção de lote)
    /// usando a página mockada — sem navegador real.
    /// </summary>
    public class AtendeNetFlowLogicTests
    {
        private readonly IProgress<ProgressoAutomacao> _progress;
        private readonly IPage _page;
        private readonly ILocator _gradeLocator;
        private readonly AtendeNetFlow _flow;

        public AtendeNetFlowLogicTests()
        {
            _progress = Substitute.For<IProgress<ProgressoAutomacao>>();
            _page = Substitute.For<IPage>();
            _gradeLocator = Substitute.For<ILocator>();

            _flow = new AtendeNetFlow(
                _progress,
                CancellationToken.None,
                dumpDir: Path.Combine(Path.GetTempPath(), "TransferToolRPA.Tests"),
                timeoutGradeMs: 800,
                janelaGradeVaziaMs: 100);
            _flow.Inicializar(_page);
        }

        [Fact]
        public async Task FiltrarProdutoAsync_PrimeiraLinhaComCodigo_RetornaEncontrado()
        {
            var linha1 = Substitute.For<ILocator>();

            _page.Locator(AtendeNetSelectors.FiltroProduto.InputFiltro).Returns(_gradeLocator);
            _gradeLocator.FillAsync(Arg.Any<string>()).Returns(Task.CompletedTask);
            _page.Locator(AtendeNetSelectors.GradeLotes.Linhas).Returns(_gradeLocator);
            _gradeLocator.AllAsync().Returns(Task.FromResult<IReadOnlyList<ILocator>>(new[] { linha1 }));
            linha1.Locator(AtendeNetSelectors.GradeLotes.CelulaCodigoProduto).InnerTextAsync().Returns("2201");

            var resultado = await _flow.FiltrarProdutoAsync("2201");

            Assert.Equal(ResultadoFiltroProduto.Encontrado, resultado);
        }

        [Fact]
        public async Task ObterLotesDisponiveisAsync_LoteComValidade_LerValidadeEQuantidade()
        {
            var linha1 = Substitute.For<ILocator>();

            _page.Locator(AtendeNetSelectors.GradeLotes.Linhas).Returns(_gradeLocator);
            _gradeLocator.AllAsync().Returns(Task.FromResult<IReadOnlyList<ILocator>>(new[] { linha1 }));
            linha1.Locator(AtendeNetSelectors.GradeLotes.CelulaCodigoProduto).InnerTextAsync().Returns("2201");
            linha1.Locator(AtendeNetSelectors.GradeLotes.CelulaValidade).InnerTextAsync().Returns("15/06/2027");
            linha1.Locator(AtendeNetSelectors.GradeLotes.CelulaQuantidade).InnerTextAsync().Returns("1.100,00000");

            var lotes = await _flow.ObterLotesDisponiveisAsync("2201");

            Assert.Single(lotes);
            Assert.Equal(new DateTime(2027, 6, 15), lotes[0].Validade);
            Assert.Equal(1100, lotes[0].Quantidade);
        }

        [Fact]
        public async Task ObterLotesDisponiveisAsync_LoteSemValidade_IncluiComValidadeNula()
        {
            var linha1 = Substitute.For<ILocator>();

            _page.Locator(AtendeNetSelectors.GradeLotes.Linhas).Returns(_gradeLocator);
            _gradeLocator.AllAsync().Returns(Task.FromResult<IReadOnlyList<ILocator>>(new[] { linha1 }));
            linha1.Locator(AtendeNetSelectors.GradeLotes.CelulaCodigoProduto).InnerTextAsync().Returns("2201");
            linha1.Locator(AtendeNetSelectors.GradeLotes.CelulaValidade).InnerTextAsync().Returns(string.Empty);
            linha1.Locator(AtendeNetSelectors.GradeLotes.CelulaQuantidade).InnerTextAsync().Returns("10,00000");

            var lotes = await _flow.ObterLotesDisponiveisAsync("2201");

            Assert.Single(lotes);
            Assert.Null(lotes[0].Validade);
            Assert.Equal(10, lotes[0].Quantidade);
        }

        [Fact]
        public async Task SelecionarLoteAsync_LoteExistente_SelecionaSemErro()
        {
            var linha1 = Substitute.For<ILocator>();
            var campoQtd = Substitute.For<ILocator>();

            _page.Locator(AtendeNetSelectors.GradeLotes.Linhas).Returns(_gradeLocator);
            _gradeLocator.AllAsync().Returns(Task.FromResult<IReadOnlyList<ILocator>>(new[] { linha1 }));
            linha1.Locator(AtendeNetSelectors.GradeLotes.CelulaCodigoProduto).InnerTextAsync().Returns("2201");
            linha1.Locator(AtendeNetSelectors.GradeLotes.CelulaValidade).InnerTextAsync().Returns("15/06/2027");
            linha1.Locator(AtendeNetSelectors.GradeLotes.CelulaQuantidade).InnerTextAsync().Returns("50,00000");

            _page.Locator(AtendeNetSelectors.Quantidade.CampoQuantidadeDisponivel).Returns(campoQtd);
            campoQtd.CountAsync().Returns(1);
            campoQtd.InputValueAsync().Returns("50,00000");

            await _flow.SelecionarLoteAsync(new LoteGrade(0, new DateTime(2027, 6, 15), 50), "2201");

            await linha1.Locator(AtendeNetSelectors.GradeLotes.CelulaValidade).Received(1)
                .ClickAsync(Arg.Any<LocatorClickOptions>());
        }

        [Fact]
        public async Task SelecionarLoteAsync_DoisLotesComMesmaValidadeEQuantidade_UsaIndiceParaDesambiguar()
        {
            var linha0 = Substitute.For<ILocator>();
            var linha1 = Substitute.For<ILocator>();
            var campoQtd = Substitute.For<ILocator>();

            _page.Locator(AtendeNetSelectors.GradeLotes.Linhas).Returns(_gradeLocator);
            _gradeLocator.AllAsync().Returns(Task.FromResult<IReadOnlyList<ILocator>>(new[] { linha0, linha1 }));

            foreach (var linha in new[] { linha0, linha1 })
            {
                linha.Locator(AtendeNetSelectors.GradeLotes.CelulaCodigoProduto).InnerTextAsync().Returns("2201");
                linha.Locator(AtendeNetSelectors.GradeLotes.CelulaValidade).InnerTextAsync().Returns("15/06/2027");
                linha.Locator(AtendeNetSelectors.GradeLotes.CelulaQuantidade).InnerTextAsync().Returns("5,00000");
            }

            _page.Locator(AtendeNetSelectors.Quantidade.CampoQuantidadeDisponivel).Returns(campoQtd);
            campoQtd.CountAsync().Returns(1);
            campoQtd.InputValueAsync().Returns("5,00000");

            // Pede o lote na 2ª linha (mesma validade/quantidade da 1ª).
            await _flow.SelecionarLoteAsync(new LoteGrade(1, new DateTime(2027, 6, 15), 5), "2201");

            await linha1.Locator(AtendeNetSelectors.GradeLotes.CelulaValidade).Received(1)
                .ClickAsync(Arg.Any<LocatorClickOptions>());
            await linha0.Locator(AtendeNetSelectors.GradeLotes.CelulaValidade).DidNotReceive()
                .ClickAsync(Arg.Any<LocatorClickOptions>());
        }

        [Fact]
        public async Task FecharJanelasAbertasAsync_DeveFecharTodasAsJanelasAbertas()
        {
            var botoes = Substitute.For<ILocator>();
            var botao = Substitute.For<ILocator>();

            // A contagem cai 3 -> 2 -> 1 -> 0 conforme cada janela e fechada.
            var contagens = new Queue<int>(new[] { 3, 2, 1, 0 });
            botoes.CountAsync().Returns(_ => Task.FromResult(contagens.Count > 0 ? contagens.Dequeue() : 0));
            botoes.First.Returns(botao);

            _page.Locator(AtendeNetSelectors.Navegacao.BotaoFecharJanela).Returns(botoes);

            await _flow.FecharJanelasAbertasAsync();

            await botao.Received(3).ClickAsync(Arg.Any<LocatorClickOptions>());
        }

        [Fact]
        public async Task ObterLotesDisponiveisAsync_DescartaLinhasDeOutroCodigo()
        {
            var linha2201 = Substitute.For<ILocator>();
            var linhaVelha = Substitute.For<ILocator>();

            _page.Locator(AtendeNetSelectors.GradeLotes.Linhas).Returns(_gradeLocator);
            _gradeLocator.AllAsync().Returns(Task.FromResult<IReadOnlyList<ILocator>>(new[] { linha2201, linhaVelha }));

            linha2201.Locator(AtendeNetSelectors.GradeLotes.CelulaCodigoProduto).InnerTextAsync().Returns("2201");
            linha2201.Locator(AtendeNetSelectors.GradeLotes.CelulaValidade).InnerTextAsync().Returns("10/08/2027");
            linha2201.Locator(AtendeNetSelectors.GradeLotes.CelulaQuantidade).InnerTextAsync().Returns("156,00000");

            // Linha "velha" de OUTRO produto (ex.: sobra do filtro anterior).
            linhaVelha.Locator(AtendeNetSelectors.GradeLotes.CelulaCodigoProduto).InnerTextAsync().Returns("9999");
            linhaVelha.Locator(AtendeNetSelectors.GradeLotes.CelulaValidade).InnerTextAsync().Returns("01/02/2027");
            linhaVelha.Locator(AtendeNetSelectors.GradeLotes.CelulaQuantidade).InnerTextAsync().Returns("6,00000");

            var lotes = await _flow.ObterLotesDisponiveisAsync("2201");

            Assert.Single(lotes);
            Assert.Equal(new DateTime(2027, 8, 10), lotes[0].Validade);
            Assert.Equal(156, lotes[0].Quantidade);
        }
    }
}