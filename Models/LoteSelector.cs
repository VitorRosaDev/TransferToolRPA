using System;
using System.Collections.Generic;
using System.Linq;

namespace TransferToolRPA.Models
{
    public record LoteDisponivel(
        int Index,
        DateTime Validade,
        double Quantidade
    );

    /// <summary>
    /// Lote candidato agregado para uma transferência. Pode vir de códigos diferentes
    /// (ex.: mesmo produto adquirido em licitações distintas).
    /// </summary>
    public record LoteCandidato(
        string Codigo,
        int IndiceNaGrade,
        DateTime? Validade,
        double Quantidade
    );

    /// <summary>Uma inclusão planejada: de qual lote retirar e quanto transferir.</summary>
    public record InclusaoPlanejada(
        LoteCandidato Lote,
        double Quantidade
    );

    /// <summary>
    /// Plano de transferência de um item. Pode conter várias inclusões (completamento
    /// entre lotes/códigos) e informa quanto ficou faltando quando o estoque não cobre
    /// a quantidade pedida.
    /// </summary>
    public record PlanoTransferencia(
        IReadOnlyList<InclusaoPlanejada> Inclusoes,
        double QuantidadeTransferida,
        double QuantidadeFaltante
    );

    public static class LoteSelector
    {
        private const double Tolerancia = 0.001;

        /// <summary>
        /// Seleciona o melhor lote de acordo com as regras:
        /// 1º Validade mais antiga (curta) primeiro.
        /// 2º Em caso de empate de validade, o lote com menor quantidade física em estoque.
        /// </summary>
        public static LoteDisponivel? SelecionarMelhorLote(IEnumerable<LoteDisponivel> lotes)
        {
            if (lotes == null) return null;

            LoteDisponivel? melhorLote = null;

            foreach (var lote in lotes)
            {
                if (melhorLote == null)
                {
                    melhorLote = lote;
                    continue;
                }

                if (lote.Validade < melhorLote.Validade)
                {
                    melhorLote = lote;
                }
                else if (lote.Validade == melhorLote.Validade && lote.Quantidade < melhorLote.Quantidade)
                {
                    melhorLote = lote;
                }
            }

            return melhorLote;
        }

        /// <summary>
        /// Ordena os lotes candidatos aplicando o critério logístico:
        /// 1º lotes COM validade, por validade mais curta e, em empate, menor quantidade;
        /// 2º lotes SEM validade (não perecíveis) ao final, por menor quantidade.
        /// Lotes sem quantidade disponível são descartados.
        /// </summary>
        public static List<LoteCandidato> OrdenarLotes(IEnumerable<LoteCandidato>? lotes)
        {
            if (lotes == null) return new List<LoteCandidato>();

            return lotes
                .Where(l => l.Quantidade > Tolerancia)
                .OrderBy(l => l.Validade.HasValue ? 0 : 1)
                .ThenBy(l => l.Validade ?? DateTime.MaxValue)
                .ThenBy(l => l.Quantidade)
                .ToList();
        }

        /// <summary>
        /// Monta o plano de transferência de um item: percorre os lotes ordenados
        /// (validade x quantidade) consumindo de cada um o máximo necessário até atingir
        /// a quantidade pedida — completando entre lotes/códigos se preciso. Ao final
        /// informa a quantidade transferida e quanto ficou faltando (0 se foi atingida).
        /// </summary>
        public static PlanoTransferencia PlanejarTransferencia(IEnumerable<LoteCandidato>? lotes, double quantidadeNecessaria)
        {
            var inclusoes = new List<InclusaoPlanejada>();

            if (quantidadeNecessaria <= Tolerancia)
            {
                return new PlanoTransferencia(inclusoes, 0, 0);
            }

            double restante = quantidadeNecessaria;

            foreach (var lote in OrdenarLotes(lotes))
            {
                if (restante <= Tolerancia) break;

                double usar = Math.Min(lote.Quantidade, restante);
                if (usar <= Tolerancia) continue;

                inclusoes.Add(new InclusaoPlanejada(lote, usar));
                restante -= usar;
            }

            double transferida = quantidadeNecessaria - Math.Max(restante, 0);
            return new PlanoTransferencia(inclusoes, transferida, Math.Max(restante, 0));
        }
    }
}
