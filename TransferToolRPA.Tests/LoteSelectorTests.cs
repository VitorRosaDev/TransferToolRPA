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
    }
}
