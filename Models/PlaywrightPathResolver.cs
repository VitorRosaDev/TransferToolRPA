using System;
using System.IO;
using System.Linq;

namespace TransferToolRPA.Models
{
    /// <summary>
    /// Centraliza a resolução e a configuração dos caminhos usados pelo Playwright.
    ///
    /// O Playwright usa DUAS variáveis de ambiente distintas e independentes:
    ///  - <see cref="DriverPathVariable"/> deve apontar para o diretório que CONTÉM a
    ///    subpasta ".playwright" (o Playwright procura
    ///    "&lt;valor&gt;\.playwright\node\win32_x64\node.exe").
    ///  - <see cref="BrowsersPathVariable"/> deve apontar para o diretório que CONTÉM os
    ///    browsers baixados (ex.: "chromium-1117", "ffmpeg-1009") — isto é, a própria
    ///    pasta ".playwright".
    ///
    /// Este resolver é idempotente e deve rodar antes de qualquer uso do Playwright
    /// (em App.OnStartup e, defensivamente, no início de AutomationEngine.ExecutarAsync).
    /// </summary>
    public static class PlaywrightPathResolver
    {
        public const string DriverPathVariable = "PLAYWRIGHT_DRIVER_SEARCH_PATH";
        public const string BrowsersPathVariable = "PLAYWRIGHT_BROWSERS_PATH";

        internal const string PlaywrightFolderName = ".playwright";
        internal const string ChromiumFolderPattern = "chromium-*";

        /// <summary>Diretório base da aplicação (onde reside o executável/host).</summary>
        public static string GetBaseDirectory() => AppContext.BaseDirectory;

        /// <summary>
        /// Resolve os caminhos a partir de <paramref name="baseDirectory"/> (ou de
        /// <see cref="AppContext.BaseDirectory"/> quando nulo). Função pura, sem efeitos
        /// colaterais, o que a torna testável de forma isolada.
        /// </summary>
        public static PlaywrightPaths Resolve(string? baseDirectory = null)
        {
            string baseDir = string.IsNullOrWhiteSpace(baseDirectory)
                ? AppContext.BaseDirectory
                : baseDirectory!;

            string playwrightDirectory = Path.Combine(baseDir, PlaywrightFolderName);
            string driverDirectory = Path.Combine(playwrightDirectory, "node", "win32_x64");
            string driverExecutable = Path.Combine(driverDirectory, "node.exe");

            bool driverFound = File.Exists(driverExecutable);
            bool browsersFound = Directory.Exists(playwrightDirectory)
                && Directory.EnumerateDirectories(playwrightDirectory, ChromiumFolderPattern).Any();

            return new PlaywrightPaths(
                baseDir,
                playwrightDirectory,
                driverDirectory,
                driverExecutable,
                driverFound,
                browsersFound);
        }

        /// <summary>
        /// Aplica as variáveis de ambiente do Playwright quando os diretórios existem.
        /// Retorna os caminhos resolvidos para fins de logging/diagnóstico. Idempotente.
        /// </summary>
        public static PlaywrightPaths Configure(string? baseDirectory = null)
        {
            var paths = Resolve(baseDirectory);

            if (paths.DriverFound)
            {
                // O Playwright concatena "\.playwright\node\win32_x64\node.exe" a este valor,
                // logo o valor correto é o diretório base (que CONTÉM ".playwright").
                Environment.SetEnvironmentVariable(DriverPathVariable, paths.BaseDirectory);
            }

            if (paths.BrowsersFound)
            {
                Environment.SetEnvironmentVariable(BrowsersPathVariable, paths.PlaywrightDirectory);
            }

            return paths;
        }
    }

    /// <summary>
    /// Caminhos resolvidos do Playwright (driver + browsers) para diagnóstico e testes.
    /// </summary>
    /// <param name="BaseDirectory">Diretório base da aplicação.</param>
    /// <param name="PlaywrightDirectory">Diretório ".playwright" (raiz dos browsers e do driver).</param>
    /// <param name="DriverDirectory">Diretório que contém o "node.exe" (driver).</param>
    /// <param name="DriverExecutable">Caminho completo esperado do "node.exe".</param>
    /// <param name="DriverFound">True quando o "node.exe" existe no caminho esperado.</param>
    /// <param name="BrowsersFound">True quando existe ao menos um diretório "chromium-*".</param>
    public sealed record PlaywrightPaths(
        string BaseDirectory,
        string PlaywrightDirectory,
        string DriverDirectory,
        string DriverExecutable,
        bool DriverFound,
        bool BrowsersFound);
}
