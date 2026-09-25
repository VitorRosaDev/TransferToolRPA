using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace TransferToolRPA.Models
{
    public static class NavegadorHelper
    {
        public static string? LocalizarChromeOuEdge()
        {
            string[] caminhos = new[]
            {
                @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
                @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
                @"C:\Program Files\Microsoft\Edge\Application\msedge.exe"
            };

            return caminhos.FirstOrDefault(File.Exists);
        }

        public static bool IniciarNavegadorComDepuracao()
        {
            string? navegadorPath = LocalizarChromeOuEdge();
            if (navegadorPath == null) return false;

            try
            {
                string profilePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                    "TransferToolRPA", "ChromeProfile");

                Directory.CreateDirectory(profilePath);

                var startInfo = new ProcessStartInfo
                {
                    FileName = navegadorPath,
                    Arguments = $"--remote-debugging-port={CdpHelper.PortaDepuracao} --user-data-dir=\"{profilePath}\" --no-first-run --no-default-browser-check \"https://alvorada.atende.net/atende.php?rot=1&aca=1#!/sistema/28\"",
                    UseShellExecute = true
                };

                Process.Start(startInfo);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
