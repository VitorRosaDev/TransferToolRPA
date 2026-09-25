using System;
using System.IO;
using TransferToolRPA.Models;
using TransferToolRPA.Services;
using Xunit;

namespace TransferToolRPA.Tests
{
    public class ObservableLoggerServiceTests
    {
        [Fact]
        public void Log_DeveAcumularEmMemoriaEPersistirNoArquivo()
        {
            string dir = CriarDiretorioTemporario();
            try
            {
                var logger = new ObservableLoggerService(dir);

                LogEntry? ultimo = null;
                logger.OnLogAdded += e => ultimo = e;

                logger.Log("mensagem info");
                logger.LogWarning("mensagem aviso");
                logger.LogError("mensagem erro");
                logger.LogSuccess("mensagem sucesso");

                Assert.NotNull(ultimo);
                Assert.Equal(NivelLog.Sucesso, ultimo!.Nivel);
                Assert.Contains("mensagem sucesso", ultimo.Texto);

                Assert.Contains("[INFO] mensagem info", logger.FullLog);
                Assert.Contains("[ERRO] mensagem erro", logger.FullLog);

                Assert.NotNull(logger.CaminhoArquivoLog);
                Assert.True(File.Exists(logger.CaminhoArquivoLog));

                string conteudo = File.ReadAllText(logger.CaminhoArquivoLog!);
                Assert.Contains("Sessão iniciada", conteudo);
                Assert.Contains("[INFO] mensagem info", conteudo);
                Assert.Contains("[AVISO] mensagem aviso", conteudo);
                Assert.Contains("[ERRO] mensagem erro", conteudo);
                Assert.Contains("[SUCESSO] mensagem sucesso", conteudo);
            }
            finally
            {
                RemoverDiretorio(dir);
            }
        }

        [Fact]
        public void Clear_DeveLimparMemoriaENotificarEntradaVazia()
        {
            string dir = CriarDiretorioTemporario();
            try
            {
                var logger = new ObservableLoggerService(dir);
                logger.Log("algo");

                string? ultimoTexto = "x";
                logger.OnLogAdded += e => ultimoTexto = e.Texto;

                logger.Clear();

                Assert.Equal(string.Empty, ultimoTexto);
                Assert.Equal(string.Empty, logger.FullLog);
            }
            finally
            {
                RemoverDiretorio(dir);
            }
        }

        [Fact]
        public void Log_MensagemComQuebraDeLinha_DeveSanitizarParaUmaUnicaLinha()
        {
            string dir = CriarDiretorioTemporario();
            try
            {
                var logger = new ObservableLoggerService(dir);
                logger.Log("linha1\r\n[02:00:00] [SUCESSO] forjado");

                Assert.Contains("linha1\\r\\n", logger.FullLog);
                Assert.DoesNotContain("\n[02:00:00] [SUCESSO] forjado", logger.FullLog);

                string conteudo = File.ReadAllText(logger.CaminhoArquivoLog!);
                string[] linhas = conteudo.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
                Assert.DoesNotContain("[02:00:00] [SUCESSO] forjado", linhas);
            }
            finally
            {
                RemoverDiretorio(dir);
            }
        }

        private static string CriarDiretorioTemporario()
            => Path.Combine(Path.GetTempPath(), "TransferToolRPA.Tests", Guid.NewGuid().ToString("N"));

        private static void RemoverDiretorio(string dir)
        {
            try
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
            catch
            {
            }
        }
    }
}