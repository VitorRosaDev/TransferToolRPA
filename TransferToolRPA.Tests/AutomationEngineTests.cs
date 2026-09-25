using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using TransferToolRPA.Models;
using Xunit;

namespace TransferToolRPA.Tests
{
    public class AutomationEngineTests
    {
        private readonly IProgress<(string Mensagem, double Progresso)> _progress;
        private readonly CancellationTokenSource _cts;
        private readonly IAtendeNetFlow _mockFlow;
        private readonly TransferenciaPayload _payload;

        public AutomationEngineTests()
        {
            _progress = Substitute.For<IProgress<(string Mensagem, double Progresso)>>();
            _cts = new CancellationTokenSource();
            _mockFlow = Substitute.For<IAtendeNetFlow>();

            _payload = new TransferenciaPayload(1, "2026-06-03", "10", "70", new[]
            {
                new PayloadItemEntrada(new[] { "2201" }, 10),
                new PayloadItemEntrada(new[] { "2218" }, 5)
            });
        }

        [Fact]
        public async Task ExecutarAsync_DeveChamarMetodosNaOrdemCorreta()
        {
            _mockFlow.NavegarParaTransferenciaAsync().Returns(Task.CompletedTask);
            _mockFlow.PreencherOrigemDestinoAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(Task.CompletedTask);
            _mockFlow.ConfigurarColunasValidadeAsync().Returns(Task.CompletedTask);
            _mockFlow.FiltrarProdutoAsync(Arg.Any<string>()).Returns(Task.CompletedTask);
            _mockFlow.SelecionarLotePorValidadeAsync(Arg.Any<double>()).Returns((0, 10.0));
            _mockFlow.PreencherQuantidadeAsync(Arg.Any<double>()).Returns(Task.CompletedTask);
            _mockFlow.IncluirItemAsync().Returns(Task.CompletedTask);
            _mockFlow.ConfirmarTransferenciaAsync().Returns(Task.CompletedTask);
            _mockFlow.CapturarDiagnosticoAsync(Arg.Any<string>()).Returns(Task.CompletedTask);

            var engine = new AutomationEngine(_payload, _progress, _cts.Token, _mockFlow);
            await engine.ExecutarAsync();

            await _mockFlow.Received(1).NavegarParaTransferenciaAsync();
            await _mockFlow.Received(1).PreencherOrigemDestinoAsync("10", "70");
            await _mockFlow.Received(1).ConfigurarColunasValidadeAsync();
            
            await _mockFlow.Received(2).FiltrarProdutoAsync(Arg.Any<string>());
            await _mockFlow.Received(2).SelecionarLotePorValidadeAsync(Arg.Any<double>());
            await _mockFlow.Received(2).PreencherQuantidadeAsync(Arg.Any<double>());
            await _mockFlow.Received(2).IncluirItemAsync();
            
            await _mockFlow.Received(1).ConfirmarTransferenciaAsync();
        }

        [Fact]
        public async Task ExecutarAsync_DeveReportarProgresso()
        {
            _mockFlow.NavegarParaTransferenciaAsync().Returns(Task.CompletedTask);
            _mockFlow.PreencherOrigemDestinoAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(Task.CompletedTask);
            _mockFlow.ConfigurarColunasValidadeAsync().Returns(Task.CompletedTask);
            _mockFlow.FiltrarProdutoAsync(Arg.Any<string>()).Returns(Task.CompletedTask);
            _mockFlow.SelecionarLotePorValidadeAsync(Arg.Any<double>()).Returns((0, 10.0));
            _mockFlow.PreencherQuantidadeAsync(Arg.Any<double>()).Returns(Task.CompletedTask);
            _mockFlow.IncluirItemAsync().Returns(Task.CompletedTask);
            _mockFlow.ConfirmarTransferenciaAsync().Returns(Task.CompletedTask);
            _mockFlow.CapturarDiagnosticoAsync(Arg.Any<string>()).Returns(Task.CompletedTask);

            var progressReports = new List<(string Mensagem, double Progresso)>();
            var progress = new Progress<(string Mensagem, double Progresso)>(r => progressReports.Add(r));

            var engine = new AutomationEngine(_payload, progress, _cts.Token, _mockFlow);
            await engine.ExecutarAsync();

            Assert.True(progressReports.Count > 0);
            Assert.Contains(progressReports, r => r.Mensagem.Contains("Conectando"));
            Assert.Contains(progressReports, r => r.Mensagem.Contains("Aba localizada"));
            Assert.Contains(progressReports, r => r.Mensagem.Contains("Inserindo item 1"));
            Assert.Contains(progressReports, r => r.Mensagem.Contains("Inserindo item 2"));
            Assert.Contains(progressReports, r => r.Mensagem.Contains("Processo concluído"));
        }

        [Fact]
        public async Task ExecutarAsync_QuandoCancellationTokenCancelado_DeveLancarOperationCanceledException()
        {
            _mockFlow.NavegarParaTransferenciaAsync().Returns(Task.Delay(1000));

            var cts = new CancellationTokenSource();
            cts.Cancel();

            var engine = new AutomationEngine(_payload, _progress, cts.Token, _mockFlow);

            await Assert.ThrowsAsync<OperationCanceledException>(() => engine.ExecutarAsync());
        }

        [Fact]
        public async Task ExecutarAsync_QuandoFlowLancaExcecao_DeveCapturarDiagnosticoERelancar()
        {
            var excecaoEsperada = new InvalidOperationException("Erro simulado");
            _mockFlow.NavegarParaTransferenciaAsync().Returns(Task.FromException(excecaoEsperada));
            _mockFlow.CapturarDiagnosticoAsync(Arg.Any<string>()).Returns(Task.CompletedTask);

            var engine = new AutomationEngine(_payload, _progress, _cts.Token, _mockFlow);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.ExecutarAsync());
            Assert.Equal("Erro simulado", ex.Message);
            
            await _mockFlow.Received(1).CapturarDiagnosticoAsync(Arg.Is<string>(s => s.Contains("Erro simulado")));
        }

        [Fact]
        public async Task ExecutarAsync_ComMultiplosItens_DeveProcessarTodosSequencialmente()
        {
            var payloadMultiplos = new TransferenciaPayload(1, "2026-06-03", "10", "70", new[]
            {
                new PayloadItemEntrada(new[] { "2201" }, 10),
                new PayloadItemEntrada(new[] { "2218" }, 5),
                new PayloadItemEntrada(new[] { "38355" }, 2)
            });

            _mockFlow.NavegarParaTransferenciaAsync().Returns(Task.CompletedTask);
            _mockFlow.PreencherOrigemDestinoAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(Task.CompletedTask);
            _mockFlow.ConfigurarColunasValidadeAsync().Returns(Task.CompletedTask);
            _mockFlow.FiltrarProdutoAsync(Arg.Any<string>()).Returns(Task.CompletedTask);
            _mockFlow.SelecionarLotePorValidadeAsync(Arg.Any<double>()).Returns((0, 10.0));
            _mockFlow.PreencherQuantidadeAsync(Arg.Any<double>()).Returns(Task.CompletedTask);
            _mockFlow.IncluirItemAsync().Returns(Task.CompletedTask);
            _mockFlow.ConfirmarTransferenciaAsync().Returns(Task.CompletedTask);
            _mockFlow.CapturarDiagnosticoAsync(Arg.Any<string>()).Returns(Task.CompletedTask);

            var engine = new AutomationEngine(payloadMultiplos, _progress, _cts.Token, _mockFlow);
            await engine.ExecutarAsync();

            await _mockFlow.Received(3).FiltrarProdutoAsync(Arg.Any<string>());
            await _mockFlow.Received(3).SelecionarLotePorValidadeAsync(Arg.Any<double>());
            await _mockFlow.Received(3).PreencherQuantidadeAsync(Arg.Any<double>());
            await _mockFlow.Received(3).IncluirItemAsync();
        }

        [Fact]
        public async Task ExecutarAsync_DevePassarCodigosCorretosParaFiltrarProduto()
        {
            var payload = new TransferenciaPayload(1, "2026-06-03", "10", "70", new[]
            {
                new PayloadItemEntrada(new[] { "2201" }, 10),
                new PayloadItemEntrada(new[] { "2218" }, 5)
            });

            _mockFlow.NavegarParaTransferenciaAsync().Returns(Task.CompletedTask);
            _mockFlow.PreencherOrigemDestinoAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(Task.CompletedTask);
            _mockFlow.ConfigurarColunasValidadeAsync().Returns(Task.CompletedTask);
            _mockFlow.FiltrarProdutoAsync(Arg.Any<string>()).Returns(Task.CompletedTask);
            _mockFlow.SelecionarLotePorValidadeAsync(Arg.Any<double>()).Returns((0, 10.0));
            _mockFlow.PreencherQuantidadeAsync(Arg.Any<double>()).Returns(Task.CompletedTask);
            _mockFlow.IncluirItemAsync().Returns(Task.CompletedTask);
            _mockFlow.ConfirmarTransferenciaAsync().Returns(Task.CompletedTask);
            _mockFlow.CapturarDiagnosticoAsync(Arg.Any<string>()).Returns(Task.CompletedTask);

            var engine = new AutomationEngine(payload, _progress, _cts.Token, _mockFlow);
            await engine.ExecutarAsync();

            await _mockFlow.Received(1).FiltrarProdutoAsync("2201");
            await _mockFlow.Received(1).FiltrarProdutoAsync("2218");
        }

        [Fact]
        public async Task ExecutarAsync_DevePassarQuantidadesCorretasParaSelecionarLote()
        {
            _mockFlow.NavegarParaTransferenciaAsync().Returns(Task.CompletedTask);
            _mockFlow.PreencherOrigemDestinoAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(Task.CompletedTask);
            _mockFlow.ConfigurarColunasValidadeAsync().Returns(Task.CompletedTask);
            _mockFlow.FiltrarProdutoAsync(Arg.Any<string>()).Returns(Task.CompletedTask);
            _mockFlow.SelecionarLotePorValidadeAsync(Arg.Any<double>()).Returns((0, 10.0));
            _mockFlow.PreencherQuantidadeAsync(Arg.Any<double>()).Returns(Task.CompletedTask);
            _mockFlow.IncluirItemAsync().Returns(Task.CompletedTask);
            _mockFlow.ConfirmarTransferenciaAsync().Returns(Task.CompletedTask);
            _mockFlow.CapturarDiagnosticoAsync(Arg.Any<string>()).Returns(Task.CompletedTask);

            var engine = new AutomationEngine(_payload, _progress, _cts.Token, _mockFlow);
            await engine.ExecutarAsync();

            await _mockFlow.Received(1).SelecionarLotePorValidadeAsync(10);
            await _mockFlow.Received(1).SelecionarLotePorValidadeAsync(5);
        }
    }
}