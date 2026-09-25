using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using TransferToolRPA.Models;
using Xunit;

namespace TransferToolRPA.Tests
{
    /// <summary>
    /// Testes de orquestração do <see cref="AutomationEngine"/> sem depender de navegador:
    /// usa o fluxo mockado (IAtendeNetFlow) e chama ExecutarFluxoAsync diretamente.
    /// </summary>
    public class AutomationEngineTests
    {
        private readonly IProgress<ProgressoAutomacao> _progress;
        private readonly CancellationTokenSource _cts;
        private readonly IAtendeNetFlow _mockFlow;

        public AutomationEngineTests()
        {
            _progress = Substitute.For<IProgress<ProgressoAutomacao>>();
            _cts = new CancellationTokenSource();
            _mockFlow = Substitute.For<IAtendeNetFlow>();

            _mockFlow.FecharJanelasAbertasAsync().Returns(Task.CompletedTask);
            _mockFlow.NavegarParaTransferenciaAsync().Returns(Task.CompletedTask);
            _mockFlow.PreencherOrigemDestinoAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(Task.CompletedTask);
            _mockFlow.ConfigurarColunasValidadeAsync().Returns(Task.CompletedTask);
            _mockFlow.FiltrarProdutoAsync(Arg.Any<string>()).Returns(Task.FromResult(ResultadoFiltroProduto.Encontrado));
            _mockFlow.ObterLotesDisponiveisAsync(Arg.Any<string>()).Returns(Task.FromResult<IReadOnlyList<LoteGrade>>(new List<LoteGrade> { new(0, null, 100) }));
            _mockFlow.SelecionarLoteAsync(Arg.Any<LoteGrade>(), Arg.Any<string>()).Returns(Task.CompletedTask);
            _mockFlow.PreencherQuantidadeAsync(Arg.Any<double>()).Returns(Task.CompletedTask);
            _mockFlow.IncluirItemAsync().Returns(Task.CompletedTask);
            _mockFlow.ConfirmarTransferenciaAsync().Returns(Task.CompletedTask);
            _mockFlow.CapturarDiagnosticoAsync(Arg.Any<string>()).Returns(Task.CompletedTask);
        }

        private AutomationEngine CriarEngine(TransferenciaPayload payload)
            => new AutomationEngine(payload, _progress, _cts.Token, _mockFlow);

        private static DateTime D(int ano, int mes, int dia) => new DateTime(ano, mes, dia);

        private static TransferenciaPayload Payload(params PayloadItemEntrada[] itens)
            => new TransferenciaPayload(1, "2026-06-03", "10", "70", itens);

        private List<string> CodigosFiltrados() => _mockFlow.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IAtendeNetFlow.FiltrarProdutoAsync))
            .Select(c => (string)c.GetArguments()[0]!)
            .ToList();

        private List<LoteGrade> LotesSelecionados() => _mockFlow.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IAtendeNetFlow.SelecionarLoteAsync))
            .Select(c => (LoteGrade)c.GetArguments()[0]!)
            .ToList();

        private List<double> QuantidadesPreenchidas() => _mockFlow.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IAtendeNetFlow.PreencherQuantidadeAsync))
            .Select(c => (double)c.GetArguments()[0]!)
            .ToList();

        [Fact]
        public async Task ExecutarFluxoAsync_DeveChamarMetodosNaOrdemCorreta()
        {
            await CriarEngine(Payload(
                new PayloadItemEntrada(new[] { "2201" }, 10),
                new PayloadItemEntrada(new[] { "2218" }, 5))).ExecutarFluxoAsync();

            await _mockFlow.Received(1).NavegarParaTransferenciaAsync();
            await _mockFlow.Received(1).PreencherOrigemDestinoAsync("10", "70");
            await _mockFlow.Received(1).ConfigurarColunasValidadeAsync();
            await _mockFlow.Received(2).FiltrarProdutoAsync(Arg.Any<string>());
            await _mockFlow.Received(2).SelecionarLoteAsync(Arg.Any<LoteGrade>(), Arg.Any<string>());
            await _mockFlow.Received(2).PreencherQuantidadeAsync(Arg.Any<double>());
            await _mockFlow.Received(2).IncluirItemAsync();
            await _mockFlow.Received(1).ConfirmarTransferenciaAsync();
        }

        [Fact]
        public async Task ExecutarFluxoAsync_MultiplosCodigos_AgregaEOrdenaGlobalmente()
        {
            // Item com quantidade 10 e códigos "8875, 12494".
            // 8875: 20/10/2027 qty12 e 10/10/2027 qty2; 12494: 15/10/2027 qty2.
            // Ordem esperada: 8875(10/10) 2 -> 12494(15/10) 2 -> 8875(20/10) 6.
            _mockFlow.ObterLotesDisponiveisAsync(Arg.Any<string>()).Returns(
                Task.FromResult<IReadOnlyList<LoteGrade>>(new List<LoteGrade>
                {
                    new(0, D(2027, 10, 20), 12),
                    new(0, D(2027, 10, 10), 2)
                }),
                Task.FromResult<IReadOnlyList<LoteGrade>>(new List<LoteGrade>
                {
                    new(0, D(2027, 10, 15), 2)
                }));

            await CriarEngine(Payload(new PayloadItemEntrada(new[] { "8875, 12494" }, 10))).ExecutarFluxoAsync();

            Assert.Equal(new[] { "8875", "12494", "8875", "12494", "8875" }, CodigosFiltrados());

            Assert.Equal(
                new DateTime?[] { D(2027, 10, 10), D(2027, 10, 15), D(2027, 10, 20) },
                LotesSelecionados().Select(l => l.Validade).ToArray());

            Assert.Equal(new[] { 2.0, 2.0, 6.0 }, QuantidadesPreenchidas().ToArray());

            await _mockFlow.Received(3).IncluirItemAsync();
            await _mockFlow.Received(1).ConfirmarTransferenciaAsync();
        }

        [Fact]
        public async Task ExecutarFluxoAsync_CodigoNaoEncontrado_PulaItemEIncluiOsDemais()
        {
            var payload = Payload(
                new PayloadItemEntrada(new[] { "000000" }, 5),   // não encontrado
                new PayloadItemEntrada(new[] { "2201" }, 3));    // encontrado

            _mockFlow.FiltrarProdutoAsync("000000").Returns(Task.FromResult(ResultadoFiltroProduto.NaoEncontrado));
            _mockFlow.FiltrarProdutoAsync("2201").Returns(Task.FromResult(ResultadoFiltroProduto.Encontrado));

            await CriarEngine(payload).ExecutarFluxoAsync();

            // O item inexistente não gera inclusão; o segundo item é incluído normalmente.
            await _mockFlow.Received(1).IncluirItemAsync();
            await _mockFlow.Received(1).ConfirmarTransferenciaAsync();

            // Item pulado deve gerar log VERMELHO (NivelLog.Erro).
            _progress.Received().Report(Arg.Is<ProgressoAutomacao>(p => p.Nivel == NivelLog.Erro && p.Resultado == ResultadoItemTransferencia.NaoEncontrado));
        }

        [Fact]
        public async Task ExecutarFluxoAsync_EstoqueInsuficiente_LogaParcialEMantemFluxo()
        {
            // Pede 5, mas só existem 2 + 2 lotes disponíveis.
            _mockFlow.ObterLotesDisponiveisAsync(Arg.Any<string>()).Returns(
                Task.FromResult<IReadOnlyList<LoteGrade>>(new List<LoteGrade>
                {
                    new(0, D(2027, 6, 15), 2),
                    new(0, D(2027, 6, 16), 2)
                }));

            await CriarEngine(Payload(new PayloadItemEntrada(new[] { "2201" }, 5))).ExecutarFluxoAsync();

            await _mockFlow.Received(2).IncluirItemAsync();
            await _mockFlow.Received(1).ConfirmarTransferenciaAsync();
            Assert.Equal(new[] { 2.0, 2.0 }, QuantidadesPreenchidas().ToArray());

            // Quantidade parcial deve gerar log ÂMBAR (NivelLog.Aviso).
            _progress.Received().Report(Arg.Is<ProgressoAutomacao>(p => p.Nivel == NivelLog.Aviso && p.Resultado == ResultadoItemTransferencia.Parcial));
        }

        [Fact]
        public async Task ExecutarFluxoAsync_DeveReportarProgressoFinal()
        {
            await CriarEngine(Payload(new PayloadItemEntrada(new[] { "2201" }, 10))).ExecutarFluxoAsync();

            _progress.Received().Report(Arg.Is<ProgressoAutomacao>(p => p.Percentual == 100 && p.Nivel == NivelLog.Sucesso));
        }

        [Fact]
        public async Task ExecutarFluxoAsync_Cancelado_LancaOperationCanceledException()
        {
            _cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => CriarEngine(Payload(new PayloadItemEntrada(new[] { "2201" }, 10))).ExecutarFluxoAsync());
        }

        [Fact]
        public async Task ExecutarFluxoAsync_DeveFecharJanelasAbertasAntesDeNavegar()
        {
            await CriarEngine(Payload(new PayloadItemEntrada(new[] { "2201" }, 10))).ExecutarFluxoAsync();

            await _mockFlow.Received(1).FecharJanelasAbertasAsync();
        }
    }
}