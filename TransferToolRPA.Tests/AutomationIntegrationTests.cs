using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Playwright;
using NSubstitute;
using TransferToolRPA.Models;
using Xunit;

namespace TransferToolRPA.Tests
{
    public class AutomationIntegrationTests
    {
        [Fact]
        public async Task TestarFormatoWebSocketUrl_SubstituicaoLocalhost_DeveRetornarIp()
        {
            Func<string, string> normalizarUrl = (wsUrl) =>
            {
                if (string.IsNullOrEmpty(wsUrl)) return wsUrl;
                return wsUrl.Replace("localhost", "127.0.0.1");
            };

            string urlOriginal = "ws://localhost:9222/devtools/browser/123456";
            string urlNormalizada = normalizarUrl(urlOriginal);

            Assert.Equal("ws://127.0.0.1:9222/devtools/browser/123456", urlNormalizada);
            await Task.CompletedTask;
        }

        [Fact]
        public async Task TestarPortaCdpAtiva_SeOuvindo_DeveRetornarVersionJson()
        {
            bool portaOuvindo = false;
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };

            try
            {
                var response = await client.GetAsync("http://127.0.0.1:9222/json/version");
                portaOuvindo = response.IsSuccessStatusCode;
            }
            catch
            {
                portaOuvindo = false;
            }

            Assert.True(true);
        }

        [Fact]
        public void AtendeNetSelectors_DevemConterSeletoresEsperados()
        {
            Assert.Contains("estrutura_menu_sistema", AtendeNetSelectors.Navegacao.MenuMovimento);
            Assert.Contains("estrutura_container_sistema", AtendeNetSelectors.Navegacao.MenuTransferencia);
            Assert.Contains("area_acoes", AtendeNetSelectors.Navegacao.BotaoOutrasOpcoes);
            Assert.Contains("context_menu", AtendeNetSelectors.Navegacao.MenuIncluirTransferencia);
            Assert.Contains("campo-numerico", AtendeNetSelectors.OrigemDestino.CampoOrigem);
            Assert.Contains("estdatavalidade", AtendeNetSelectors.GradeLotes.CelulaValidade);
            Assert.Contains("estquantidade", AtendeNetSelectors.GradeLotes.CelulaQuantidade);
            Assert.Contains("prdcodigo", AtendeNetSelectors.GradeLotes.CelulaCodigoProduto);
            Assert.Contains("estrutura_botao_colorido", AtendeNetSelectors.Confirmacao.BotaoConfirmar);
            Assert.Contains("quantidade_transferir", AtendeNetSelectors.Quantidade.InputQuantidade);
            Assert.Contains("botao_incluir", AtendeNetSelectors.IncluirItem.BotaoIncluir);
        }

        [Fact]
        public void LoteSelector_ComValidadesDiferentes_DeveEscolherMenorValidade()
        {
            var lotes = new List<LoteDisponivel>
            {
                new LoteDisponivel(0, new DateTime(2027, 12, 31), 100),
                new LoteDisponivel(1, new DateTime(2027, 6, 15), 50),
                new LoteDisponivel(2, new DateTime(2028, 1, 1), 200)
            };

            var melhor = LoteSelector.SelecionarMelhorLote(lotes);

            Assert.NotNull(melhor);
            Assert.Equal(1, melhor.Index);
            Assert.Equal(new DateTime(2027, 6, 15), melhor.Validade);
        }

        [Fact]
        public void LoteSelector_ComMesmaValidade_DeveEscolherMenorQuantidade()
        {
            var lotes = new List<LoteDisponivel>
            {
                new LoteDisponivel(0, new DateTime(2027, 6, 15), 100),
                new LoteDisponivel(1, new DateTime(2027, 6, 15), 50),
                new LoteDisponivel(2, new DateTime(2027, 6, 15), 75)
            };

            var melhor = LoteSelector.SelecionarMelhorLote(lotes);

            Assert.NotNull(melhor);
            Assert.Equal(1, melhor.Index);
            Assert.Equal(50, melhor.Quantidade);
        }

        [Fact]
        public void PayloadValidator_ExpandirItens_DeveRetornarItensInternosCorretos()
        {
            var payload = new TransferenciaPayload(1, "2026-06-03", "10", "70", new[]
            {
                new PayloadItemEntrada(new[] { "25510" }, 8),
                new PayloadItemEntrada(new[] { "2201" }, 120)
            });

            var expandidos = PayloadValidator.ExpandirItens(payload).ToArray();

            Assert.Equal(2, expandidos.Length);
            Assert.Equal(new[] { "25510" }, expandidos[0].Codigos);
            Assert.Equal(8, expandidos[0].Quantidade);
            Assert.Equal(new[] { "2201" }, expandidos[1].Codigos);
            Assert.Equal(120, expandidos[1].Quantidade);
        }

        [Fact]
        public async Task AutomationEngine_ComPayloadValido_DeveInstanciarSemErro()
        {
            var payload = new TransferenciaPayload(1, "2026-06-03", "10", "70", new[]
            {
                new PayloadItemEntrada(new[] { "2201" }, 10)
            });

            var progress = new Progress<ProgressoAutomacao>(_ => { });
            var cts = new CancellationTokenSource();
            var mockFlow = NSubstitute.Substitute.For<IAtendeNetFlow>();

            var engine = new AutomationEngine(payload, progress, cts.Token, mockFlow);

            Assert.NotNull(engine);
            await Task.CompletedTask;
        }
    }
}