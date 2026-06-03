using System;
using System.Collections.Generic;

namespace TransferToolRPA.Models
{
    public record LoteDisponivel(
        int Index,
        DateTime Validade,
        double Quantidade
    );

    public static class LoteSelector
    {
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
    }
}
