namespace TransferToolRPA.Models
{
    /// <summary>
    /// Centraliza os "magic numbers" de tempo, retentativas e limites da automação
    /// num único ponto, facilitando o ajuste operacional e a revisão de segurança.
    /// </summary>
    public static class ConfiguracaoAutomacao
    {
        // --- Conexão CDP / navegador ---
        public const int TimeoutHttpCdpSegundos = 2;
        public const int MaxTentativasConexaoCdp = 6;
        public const int DelayReconexaoCdpMs = 1000;
        public const int DelayAposBrowserCloseMs = 800;
        public const int TimeoutNetstatMs = 3000;

        // --- Fluxo Atende.Net: timeouts de espera ---
        public const int TimeoutPadraoPaginaMs = 10000;
        public const int TimeoutNavegacaoMs = 30000;
        public const int TimeoutJanelaInclusaoMs = 20000;
        public const int TimeoutGradeMs = 15000;
        public const int JanelaGradeVaziaMs = 4000;
        public const int TimeoutCarrinhoMs = 10000;
        public const int TimeoutSelecaoLoteMs = 10000;
        public const int TimeoutModalMs = 3000;

        // --- Fluxo Atende.Net: delays de polling e interação ---
        public const int DelayPollJanelaMs = 500;
        public const int DelayPollGradeMs = 200;
        public const int DelayPollCarrinhoMs = 300;
        public const int DelayAposTabMs = 500;
        public const int DelayFecharJanelaMs = 300;

        // --- Retentativas ---
        public const int MaxTentativasRetry = 3;
        public const int DelayRetryCliqueMs = 500;
        public const int DelayRetryPreenchimentoMs = 300;

        // --- Limites de segurança ---
        public const int MaxJanelasParaFechar = 20;
    }
}
