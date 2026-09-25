using System;
using System.IO;

namespace TransferToolRPA.Models
{
    /// <summary>
    /// Utilitários de manutenção da pasta de diagnóstico do RPA (screenshot/HTML).
    /// </summary>
    public static class DiagnosticoHelper
    {
        /// <summary>
        /// Remove arquivos de diagnóstico ("diag_*") com mais de <paramref name="diasRetencao"/>
        /// dias, limitando o acúmulo de dados sensíveis (DOM/sessão) em disco. Best-effort:
        /// falhas de I/O ou permissão são ignoradas e nunca interrompem a automação.
        /// </summary>
        public static void LimparAntigos(string diretorio, int diasRetencao)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(diretorio) || !Directory.Exists(diretorio)) return;

                var limite = DateTime.Now.AddDays(-diasRetencao);

                foreach (var arquivo in Directory.EnumerateFiles(diretorio, "diag_*"))
                {
                    try
                    {
                        if (File.GetLastWriteTime(arquivo) < limite)
                        {
                            File.Delete(arquivo);
                        }
                    }
                    catch
                    {
                        // Arquivo em uso ou sem permissão: ignora e segue.
                    }
                }
            }
            catch
            {
                // Manutenção é acessória; nunca deve interromper a automação.
            }
        }
    }
}
