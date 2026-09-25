using TransferToolRPA.Models;

namespace TransferToolRPA.Services
{
    /// <summary>
    /// Linha de log pronta para exibição. <see cref="Texto"/> já contém o timestamp e o
    /// prefixo de severidade; <see cref="Nivel"/> define a cor na UI. <see cref="Nome"/>
    /// fica reservado para quando o TransferToolMobile passar a enviar a descrição do
    /// produto no payload (hoje permanece nulo).
    /// </summary>
    public record LogEntry(string Texto, NivelLog Nivel, string? Nome = null);
}