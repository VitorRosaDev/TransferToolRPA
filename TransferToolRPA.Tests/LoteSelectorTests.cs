using System;
using System.Collections.Generic;
using TransferToolRPA.Models;
using Xunit;

namespace TransferToolRPA.Tests
{
    public class LoteSelectorTests
    {
        [Fact]
        public void SelecionarMelhorLote_ListaNula_DeveRetornarNulo()
        {
            // Act
            var resultado = LoteSelector.SelecionarMelhorLote(null!);

            // Assert
            Assert.Null(resultado);
        }

        [Fact]
        public void SelecionarMelhorLote_ListaVazia_DeveRetornarNulo()
        {
            // Arrange
            var lotes = new List<LoteDisponivel>();

            // Act
            var resultado = LoteSelector.SelecionarMelhorLote(lotes);

            // Assert
            Assert.Null(resultado);
        }

        [Fact]
        public void SelecionarMelhorLote_LoteUnico_DeveRetornarOLoteUnico()
        {
            // Arrange
            var lote = new LoteDisponivel(0, DateTime.Now.AddDays(10), 100);
            var lotes = new List<LoteDisponivel> { lote };

            // Act
            var resultado = LoteSelector.SelecionarMelhorLote(lotes);

            // Assert
            Assert.NotNull(resultado);
            Assert.Equal(lote, resultado);
        }

        [Fact]
        public void SelecionarMelhorLote_ValidadesDiferentes_DeveEscolherValidadeMaisAntiga()
        {
            // Arrange
            var validadeAntiga = DateTime.Today.AddDays(5);
            var validadeNova = DateTime.Today.AddDays(15);

            var loteNovo = new LoteDisponivel(0, validadeNova, 10);
            var loteAntigo = new LoteDisponivel(1, validadeAntiga, 100); // Maior quantidade, mas validade mais curta
            var lotes = new List<LoteDisponivel> { loteNovo, loteAntigo };

            // Act
            var resultado = LoteSelector.SelecionarMelhorLote(lotes);

            // Assert
            Assert.NotNull(resultado);
            Assert.Equal(loteAntigo, resultado);
            Assert.Equal(1, resultado.Index);
        }

        [Fact]
        public void SelecionarMelhorLote_ValidadesIguais_DeveDesempatarPelaMenorQuantidade()
        {
            // Arrange
            var validadeMesma = DateTime.Today.AddDays(10);

            var loteMaiorQtd = new LoteDisponivel(0, validadeMesma, 150);
            var loteMenorQtd = new LoteDisponivel(1, validadeMesma, 50); // Mesma validade, menor quantidade
            var lotes = new List<LoteDisponivel> { loteMaiorQtd, loteMenorQtd };

            // Act
            var resultado = LoteSelector.SelecionarMelhorLote(lotes);

            // Assert
            Assert.NotNull(resultado);
            Assert.Equal(loteMenorQtd, resultado);
            Assert.Equal(1, resultado.Index);
        }

        [Fact]
        public void SelecionarMelhorLote_MultiplosLotesEmpatadosEmQuantidadeEValidade_DeveRetornarOPrimeiroInserido()
        {
            // Arrange
            var validadeComum = DateTime.Today.AddDays(10);
            var lote1 = new LoteDisponivel(0, validadeComum, 100);
            var lote2 = new LoteDisponivel(1, validadeComum, 100); // Mesmo saldo
            var lote3 = new LoteDisponivel(2, validadeComum, 100); // Mesmo saldo
            var lotes = new List<LoteDisponivel> { lote2, lote1, lote3 };

            // Act
            var resultado = LoteSelector.SelecionarMelhorLote(lotes);

            // Assert
            Assert.NotNull(resultado);
            Assert.Equal(lote2, resultado); // Primeiro lote da lista sob empate total
        }

        [Fact]
        public void SelecionarMelhorLote_FormatosDeDataUTCePlano_DeveEscolherValidadeMaisAntiga()
        {
            // Arrange
            var validadeAntiga = DateTime.Parse("2026-06-01");
            var validadeNova = DateTime.Parse("2026-06-15");

            var loteNovo = new LoteDisponivel(0, validadeNova, 10);
            var loteAntigo = new LoteDisponivel(1, validadeAntiga, 100);
            var lotes = new List<LoteDisponivel> { loteNovo, loteAntigo };

            // Act
            var resultado = LoteSelector.SelecionarMelhorLote(lotes);

            // Assert
            Assert.NotNull(resultado);
            Assert.Equal(loteAntigo, resultado);
            Assert.Equal(1, resultado.Index);
        }
        // --- Ordenação / planejamento (validade x quantidade, multi-código, sem validade) ---

        [Fact]
        public void OrdenarLotes_MesmaValidade_ColocaMenorQuantidadePrimeiro()
        {
            var lotes = new List<LoteCandidato>
            {
                new("A", 0, new DateTime(2027, 6, 15), 100),
                new("A", 0, new DateTime(2027, 6, 15), 50),
                new("A", 0, new DateTime(2027, 6, 15), 75)
            };

            var ordenados = LoteSelector.OrdenarLotes(lotes);

            Assert.Equal(50, ordenados[0].Quantidade);
            Assert.Equal(75, ordenados[1].Quantidade);
            Assert.Equal(100, ordenados[2].Quantidade);
        }

        [Fact]
        public void OrdenarLotes_LoteSemValidade_VaiPorUltimo()
        {
            var lotes = new List<LoteCandidato>
            {
                new("A", 0, null, 5),
                new("A", 0, new DateTime(2028, 1, 1), 100)
            };

            var ordenados = LoteSelector.OrdenarLotes(lotes);

            Assert.NotNull(ordenados[0].Validade);
            Assert.Null(ordenados[1].Validade);
        }

        [Fact]
        public void OrdenarLotes_ListaNula_RetornaVazia()
        {
            Assert.Empty(LoteSelector.OrdenarLotes(null));
        }

        [Fact]
        public void PlanejarTransferencia_QuantidadeSuficienteNoPrimeiro_UsaApenasEle()
        {
            var lotes = new List<LoteCandidato>
            {
                new("A", 0, new DateTime(2027, 6, 15), 100),
                new("A", 0, new DateTime(2027, 12, 31), 200)
            };

            var plano = LoteSelector.PlanejarTransferencia(lotes, 50);

            Assert.Single(plano.Inclusoes);
            Assert.Equal(50, plano.Inclusoes[0].Quantidade);
            Assert.Equal(50, plano.QuantidadeTransferida);
            Assert.Equal(0, plano.QuantidadeFaltante);
        }

        [Fact]
        public void PlanejarTransferencia_EstoqueInsuficienteNoPrimeiro_CompletaComProximo()
        {
            var lotes = new List<LoteCandidato>
            {
                new("A", 0, new DateTime(2027, 6, 15), 30),
                new("A", 0, new DateTime(2027, 12, 31), 100)
            };

            var plano = LoteSelector.PlanejarTransferencia(lotes, 50);

            Assert.Equal(2, plano.Inclusoes.Count);
            Assert.Equal(30, plano.Inclusoes[0].Quantidade);
            Assert.Equal(20, plano.Inclusoes[1].Quantidade);
            Assert.Equal(50, plano.QuantidadeTransferida);
            Assert.Equal(0, plano.QuantidadeFaltante);
        }

        [Fact]
        public void PlanejarTransferencia_MultiplosCodigos_OrdenaGlobalmenteECompleta()
        {
            // Exemplo do usuário: 10 unidades.
            // Código A: 20/10/2027 qty12 e 10/10/2027 qty2; Código B: 15/10/2027 qty2.
            // Esperado: A(10/10) 2 -> B(15/10) 2 -> A(20/10) 6.
            var lotes = new List<LoteCandidato>
            {
                new("A", 0, new DateTime(2027, 10, 20), 12),
                new("A", 0, new DateTime(2027, 10, 10), 2),
                new("B", 0, new DateTime(2027, 10, 15), 2)
            };

            var plano = LoteSelector.PlanejarTransferencia(lotes, 10);

            Assert.Equal(3, plano.Inclusoes.Count);
            Assert.Equal("A", plano.Inclusoes[0].Lote.Codigo);
            Assert.Equal(new DateTime(2027, 10, 10), plano.Inclusoes[0].Lote.Validade);
            Assert.Equal(2, plano.Inclusoes[0].Quantidade);

            Assert.Equal("B", plano.Inclusoes[1].Lote.Codigo);
            Assert.Equal(2, plano.Inclusoes[1].Quantidade);

            Assert.Equal("A", plano.Inclusoes[2].Lote.Codigo);
            Assert.Equal(new DateTime(2027, 10, 20), plano.Inclusoes[2].Lote.Validade);
            Assert.Equal(6, plano.Inclusoes[2].Quantidade);

            Assert.Equal(10, plano.QuantidadeTransferida);
            Assert.Equal(0, plano.QuantidadeFaltante);
        }

        [Fact]
        public void PlanejarTransferencia_EstoqueTotalMenorQueOPedido_RetornaFaltante()
        {
            var lotes = new List<LoteCandidato>
            {
                new("A", 0, new DateTime(2027, 6, 15), 2),
                new("B", 0, new DateTime(2027, 6, 16), 2)
            };

            var plano = LoteSelector.PlanejarTransferencia(lotes, 5);

            Assert.Equal(2, plano.Inclusoes.Count);
            Assert.Equal(4, plano.QuantidadeTransferida);
            Assert.Equal(1, plano.QuantidadeFaltante);
        }

        [Fact]
        public void PlanejarTransferencia_SomenteLotesSemValidade_UsaMenorQuantidadePrimeiro()
        {
            var lotes = new List<LoteCandidato>
            {
                new("A", 0, null, 50),
                new("A", 0, null, 5)
            };

            var plano = LoteSelector.PlanejarTransferencia(lotes, 10);

            Assert.Equal(2, plano.Inclusoes.Count);
            Assert.Equal(5, plano.Inclusoes[0].Quantidade);
            Assert.Equal(5, plano.Inclusoes[1].Quantidade);
            Assert.Equal(10, plano.QuantidadeTransferida);
        }

        [Fact]
        public void PlanejarTransferencia_ListaVazia_RetornaFaltanteTotal()
        {
            var plano = LoteSelector.PlanejarTransferencia(new List<LoteCandidato>(), 50);

            Assert.Empty(plano.Inclusoes);
            Assert.Equal(0, plano.QuantidadeTransferida);
            Assert.Equal(50, plano.QuantidadeFaltante);
        }

        [Fact]
        public void PlanejarTransferencia_TodosLotesSemQuantidade_RetornaFaltanteTotal()
        {
            var lotes = new List<LoteCandidato>
            {
                new("A", 0, new DateTime(2027, 6, 15), 0),
                new("A", 0, new DateTime(2027, 12, 31), 0)
            };

            var plano = LoteSelector.PlanejarTransferencia(lotes, 50);

            Assert.Empty(plano.Inclusoes);
            Assert.Equal(50, plano.QuantidadeFaltante);
        }

        // __FIM_LOTE__
    }
}
