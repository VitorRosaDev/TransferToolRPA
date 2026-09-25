using System;
using System.IO;
using TransferToolRPA.Models;
using Xunit;

namespace TransferToolRPA.Tests
{
    /// <summary>
    /// Coleção sem paralelismo: os testes abaixo manipulam variáveis de ambiente do processo
    /// (PLAYWRIGHT_*), o que não pode concorrer com outras classes de teste.
    /// </summary>
    [CollectionDefinition("PlaywrightEnv", DisableParallelization = true)]
    public class PlaywrightEnvCollection
    {
    }

    [Collection("PlaywrightEnv")]
    public class PlaywrightPathResolverTests
    {
        [Fact]
        public void Resolve_ComDriverEBrowsers_DeveDetectarAmbos()
        {
            // Arrange
            string baseDir = CriarEstruturaFake(withDriver: true, withBrowser: true);

            try
            {
                // Act
                var paths = PlaywrightPathResolver.Resolve(baseDir);

                // Assert
                Assert.Equal(baseDir, paths.BaseDirectory);
                Assert.Equal(Path.Combine(baseDir, ".playwright"), paths.PlaywrightDirectory);
                Assert.Equal(
                    Path.Combine(baseDir, ".playwright", "node", "win32_x64", "node.exe"),
                    paths.DriverExecutable);
                Assert.True(paths.DriverFound);
                Assert.True(paths.BrowsersFound);
            }
            finally
            {
                LimparDiretorio(baseDir);
            }
        }

        [Fact]
        public void Resolve_SemDriver_DeveIndicarDriverNaoEncontrado()
        {
            // Arrange
            string baseDir = CriarEstruturaFake(withDriver: false, withBrowser: true);

            try
            {
                // Act
                var paths = PlaywrightPathResolver.Resolve(baseDir);

                // Assert
                Assert.False(paths.DriverFound);
                Assert.True(paths.BrowsersFound);
            }
            finally
            {
                LimparDiretorio(baseDir);
            }
        }

        [Fact]
        public void Resolve_DiretorioInexistente_NaoDeveLancarEAmbosFalsos()
        {
            // Arrange
            string inexistente = Path.Combine(Path.GetTempPath(), "tt2_inexistente_" + Guid.NewGuid().ToString("N"));

            // Act
            var paths = PlaywrightPathResolver.Resolve(inexistente);

            // Assert
            Assert.False(paths.DriverFound);
            Assert.False(paths.BrowsersFound);
        }

        [Fact]
        public void Configure_ComDriver_DeveApontarDriverSearchPathParaBaseDirectory()
        {
            // Arrange
            string baseDir = CriarEstruturaFake(withDriver: true, withBrowser: true);
            string? driverAnterior = Environment.GetEnvironmentVariable(PlaywrightPathResolver.DriverPathVariable);
            string? browsersAnterior = Environment.GetEnvironmentVariable(PlaywrightPathResolver.BrowsersPathVariable);

            try
            {
                // Act
                var paths = PlaywrightPathResolver.Configure(baseDir);

                // Assert
                // A variável do driver DEVE apontar para o diretório base (que CONTÉM ".playwright"),
                // e não para a própria pasta ".playwright" (isso faria o Playwright lançar
                // "Couldn't find driver in PLAYWRIGHT_DRIVER_SEARCH_PATH").
                Assert.Equal(baseDir, Environment.GetEnvironmentVariable(PlaywrightPathResolver.DriverPathVariable));
                Assert.Equal(paths.PlaywrightDirectory, Environment.GetEnvironmentVariable(PlaywrightPathResolver.BrowsersPathVariable));
            }
            finally
            {
                Environment.SetEnvironmentVariable(PlaywrightPathResolver.DriverPathVariable, driverAnterior);
                Environment.SetEnvironmentVariable(PlaywrightPathResolver.BrowsersPathVariable, browsersAnterior);
                LimparDiretorio(baseDir);
            }
        }

        [Fact]
        public void Configure_SemDriverNemBrowsers_NaoDeveDefinirVariaveis()
        {
            // Arrange
            string baseDir = CriarEstruturaFake(withDriver: false, withBrowser: false);
            string? driverAnterior = Environment.GetEnvironmentVariable(PlaywrightPathResolver.DriverPathVariable);
            string? browsersAnterior = Environment.GetEnvironmentVariable(PlaywrightPathResolver.BrowsersPathVariable);

            try
            {
                Environment.SetEnvironmentVariable(PlaywrightPathResolver.DriverPathVariable, null);
                Environment.SetEnvironmentVariable(PlaywrightPathResolver.BrowsersPathVariable, null);

                // Act
                PlaywrightPathResolver.Configure(baseDir);

                // Assert
                Assert.Null(Environment.GetEnvironmentVariable(PlaywrightPathResolver.DriverPathVariable));
                Assert.Null(Environment.GetEnvironmentVariable(PlaywrightPathResolver.BrowsersPathVariable));
            }
            finally
            {
                Environment.SetEnvironmentVariable(PlaywrightPathResolver.DriverPathVariable, driverAnterior);
                Environment.SetEnvironmentVariable(PlaywrightPathResolver.BrowsersPathVariable, browsersAnterior);
                LimparDiretorio(baseDir);
            }
        }


        private static string CriarEstruturaFake(bool withDriver, bool withBrowser)
        {
            string baseDir = Path.Combine(Path.GetTempPath(), "tt2_test_" + Guid.NewGuid().ToString("N"));
            string playwrightDir = Path.Combine(baseDir, ".playwright");
            Directory.CreateDirectory(playwrightDir);

            if (withDriver)
            {
                string driverDir = Path.Combine(playwrightDir, "node", "win32_x64");
                Directory.CreateDirectory(driverDir);
                File.WriteAllText(Path.Combine(driverDir, "node.exe"), "fake");
            }

            if (withBrowser)
            {
                Directory.CreateDirectory(Path.Combine(playwrightDir, "chromium-1117"));
            }

            return baseDir;
        }

        private static void LimparDiretorio(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
            catch
            {
                // Limpeza best-effort: arquivos em uso não devem quebrar o teste.
            }
        }
    }
}
