using System.Diagnostics;

namespace AnnoDesigner.Core.Helper
{
    public static class ProcessHelper
    {
        // net core needs UseShellExecute set to open a url, net48 had it on by default. plain Process.Start(url) used to work and now throws.
        public static void OpenUrl(string url)
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }
}
