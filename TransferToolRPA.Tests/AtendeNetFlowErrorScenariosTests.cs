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
    /// Testes de cenários de erro/limite do fluxo usando a página mockada — sem navegador real.
    /// </summary>
    public class AtendeNetFlowErrorScenariosTests
    {
        private readonly IProgress<ProgressoAutomacao> _progress;
        private readonly IPage _page;
        private readonly ILocator _gradeLocator;
        private readonly AtendeNetFlow _flow;

        public AtendeNetFlowErrorScenariosTests()
        {
            _progress = Substitute.For<IProgress<ProgressoAutomacao>>();
            _page = Substitute.For<IPage>();
            _gradeLocator = Substitute.For<ILocator>();

            // Janelas de tempo pequenas para os testes não ficarem lentos.
            _flow = new AtendeNetFlow(
                _progress,
                CancellationToken.None,
                dumpDir: Path.Combine(Path.GetTempPath(), "TransferToolRPA.Tests"),
                timeoutGradeMs: 800,
                janelaGradeVaziaMs: 100);
            _flow.Inicializar(_page);
        }

        [Fact]
        public async Task FiltrarProdutoAsync_GradeVaziaEstavel_RetornaNaoEncontrado()
        {
            _page.Locator(AtendeNetSelectors.FiltroProduto.InputFiltro).Returns(_gradeLocator);
            _gradeLocator.FillAsync(Arg.Any<string>()).Returns(Task.CompletedTask);
            _page.Locator(AtendeNetSelectors.GradeLotes.Linhas).Returns(_gradeLocator);
            _gradeLocator.AllAsync().Returns(Task.FromResult<IReadOnlyList<ILocator>>(Array.Empty<ILocator>()));

            var resultado = await _flow.FiltrarProdutoAsync("999999");

            Assert.Equal(ResultadoFiltroProduto.NaoEncontrado, resultado);
        }

        [Fact]
        public async Task ObterLotesDisponiveisAsync_GradeVazia_RetornaListaVazia()
        {
            _page.Locator(AtendeNetSelectors.GradeLotes.Linhas).Returns(_gradeLocator);
            _page.Locator(AtendeNetSelectors.GradeLotes.LinhasFallback).Returns(_gradeLocator);
            _gradeLocator.AllAsync().Returns(Task.FromResult<IReadOnlyList<ILocator>>(Array.Empty<ILocator>()));

            var lotes = await _flow.ObterLotesDisponiveisAsync("999999");

            Assert.Empty(lotes);
        }

        [Fact]
        public async Task ObterLotesDisponiveisAsync_QuantidadeInvalida_LidaComoZero()
        {
            var linha1 = Substitute.For<ILocator>();

            _page.Locator(AtendeNetSelectors.GradeLotes.Linhas).Returns(_gradeLocator);
            _gradeLocator.AllAsync().Returns(Task.FromResult<IReadOnlyList<ILocator>>(new[] { linha1 }));
            linha1.Locator(AtendeNetSelectors.GradeLotes.CelulaCodigoProduto).InnerTextAsync().Returns("2201");
            linha1.Locator(AtendeNetSelectors.GradeLotes.CelulaValidade).InnerTextAsync().Returns("15/06/2027");
            linha1.Locator(AtendeNetSelectors.GradeLotes.CelulaQuantidade).InnerTextAsync().Returns(string.Empty);

            var lotes = await _flow.ObterLotesDisponiveisAsync("2201");

            Assert.Single(lotes);
            Assert.Equal(0, lotes[0].Quantidade);
        }

        [Fact]
        public async Task SelecionarLoteAsync_LoteInexistenteNaGrade_DeveLancarInvalidOperationException()
        {
            var linha1 = Substitute.For<ILocator>();

            _page.Locator(AtendeNetSelectors.GradeLotes.Linhas).Returns(_gradeLocator);
            _gradeLocator.AllAsync().Returns(Task.FromResult<IReadOnlyList<ILocator>>(new[] { linha1 }));
            linha1.Locator(AtendeNetSelectors.GradeLotes.CelulaCodigoProduto).InnerTextAsync().Returns("2201");
            linha1.Locator(AtendeNetSelectors.GradeLotes.CelulaValidade).InnerTextAsync().Returns("15/06/2027");
            linha1.Locator(AtendeNetSelectors.GradeLotes.CelulaQuantidade).InnerTextAsync().Returns("50,00000");

            // Pede um lote que não existe na grade filtrada.
            var loteInexistente = new LoteGrade(0, new DateTime(2030, 1, 1), 999);

            await Assert.ThrowsAsync<InvalidOperationException>(() => _flow.SelecionarLoteAsync(loteInexistente, "2201"));
        }

        [Fact]
        public async Task FiltrarProdutoAsync_MensagemNaoEncontradoResidual_NaoAbortaConsultaComResultado()
        {
            // Regressão: logo após clicar em "Consultar", o grid ainda exibe o estado da
            // consulta anterior — inclusive o "Registro não encontrado" de um código que
            // realmente não tinha estoque. Essa mensagem residual NÃO pode fazer a consulta
            // atual (que TEM resultado) ser tratada como "não encontrado".
            var mensagem = Substitute.For<ILocator>();
            var linha = Substitute.For<ILocator>();

            _page.Locator(AtendeNetSelectors.FiltroProduto.InputFiltro).Returns(_gradeLocator);
            _gradeLocator.FillAsync(Arg.Any<string>()).Returns(Task.CompletedTask);
            _page.Locator(AtendeNetSelectors.GradeLotes.Linhas).Returns(_gradeLocator);

            // 1ª leitura: grade ainda vazia (estado residual da consulta anterior).
            // 2ª leitura: o lote do produto filtrado já carregou.
            _gradeLocator.AllAsync().Returns(
                Task.FromResult<IReadOnlyList<ILocator>>(Array.Empty<ILocator>()),
                Task.FromResult<IReadOnlyList<ILocator>>(new[] { linha }));

            // Mensagem "Registro não encontrado" residual continua visível no grid.
            _page.Locator(AtendeNetSelectors.GradeLotes.MensagemNaoEncontrado).Returns(mensagem);
            mensagem.CountAsync().Returns(1);

            linha.Locator(AtendeNetSelectors.GradeLotes.CelulaCodigoProduto).InnerTextAsync().Returns("29285");

            var resultado = await _flow.FiltrarProdutoAsync("29285");

            Assert.Equal(ResultadoFiltroProduto.Encontrado, resultado);
        }
    }
}