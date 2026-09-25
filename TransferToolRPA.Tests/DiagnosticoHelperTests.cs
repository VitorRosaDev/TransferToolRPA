using System;
using System.IO;
using TransferToolRPA.Models;
using Xunit;

namespace TransferToolRPA.Tests
{
    public class DiagnosticoHelperTests
    {
        [Fact]
        public void LimparAntigos_DeveRemoverApenasArquivosAntigos()
        {
            string dir = Path.Combine(Path.GetTempPath(), "TransferToolRPA.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string antigo = Path.Combine(dir, "diag_20260101_000000.png");
                string recente = Path.Combine(dir, "diag_20261231_000000.html");
                string frame = Path.Combine(dir, "diag_frame_antigo_20260101_000000.html");
                File.WriteAllText(antigo, "x");
                File.WriteAllText(recente, "x");
                File.WriteAllText(frame, "x");

                File.SetLastWriteTime(antigo, DateTime.Now.AddDays(-30));
                File.SetLastWriteTime(frame, DateTime.Now.AddDays(-30));
                File.SetLastWriteTime(recente, DateTime.Now);

                DiagnosticoHelper.LimparAntigos(dir, diasRetencao: 7);

                Assert.False(File.Exists(antigo));
                Assert.False(File.Exists(frame));
                Assert.True(File.Exists(recente));
            }
            finally
            {
                try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
            }
        }
    }
}
