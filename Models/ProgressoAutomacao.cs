namespace TransferToolRPA.Models
{
    /// <summary>
    /// Severidade de uma linha de log. Permite que a camada de apresentação (WPF)
    /// pinte a mensagem conforme a criticidade (Erro = vermelho, Aviso = âmbar).
    /// </summary>
    public enum NivelLog
    {
        Info,
        Sucesso,
        Aviso,
        Erro
    }

    /// <summary>
    /// Desfecho da transferência de um item. Usado para colorir a carga na fila:
    /// "NaoEncontrado" deixa a carga vermelha e "Parcial" deixa âmbar/laranja.
    /// </summary>
    public enum ResultadoItemTransferencia
    {
        Nenhum,
        NaoEncontrado,
        Parcial
    }

    /// <summary>
    /// Status de conclusão de uma carga importada (define a cor na fila).
    /// </summary>
    public enum StatusCarga
    {
        Pendente,
        Concluida,
        Parcial,
        ComNaoEncontrado
    }

    /// <summary>
    /// Evento de progresso reportado pelo fluxo de automação. O <see cref="Nivel"/>
    /// carrega a severidade para que a UI/logger escolha a cor de exibição, e
    /// <see cref="Resultado"/> identifica o desfecho de cada item.
    /// </summary>
    public record ProgressoAutomacao(
        string Mensagem,
        double Percentual,
        NivelLog Nivel = NivelLog.Info,
        ResultadoItemTransferencia Resultado = ResultadoItemTransferencia.Nenhum);
}