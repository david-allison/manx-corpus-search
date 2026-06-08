using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace CorpusSearch.Infrastructure;

internal static class ViteDevServer
{
    /// <summary>
    /// Dev only: starts the Vite dev server (npm run dev) and waits until it answers.
    /// </summary>
    public static async Task<Uri> EnsureRunningAsync(Uri devServer)
    {
        if (await IsRespondingAsync(devServer))
        {
            return devServer;
        }

        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        Process.Start(new ProcessStartInfo
        {
            FileName = isWindows ? "cmd" : "npm",
            Arguments = isWindows ? "/c npm run dev" : "run dev",
            WorkingDirectory = Path.Combine(Directory.GetCurrentDirectory(), "ClientApp"),
            UseShellExecute = false,
        });

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        while (!await IsRespondingAsync(devServer))
        {
            await Task.Delay(500, timeout.Token);
        }
        return devServer;
    }

    private static async Task<bool> IsRespondingAsync(Uri uri)
    {
        try
        {
            // ReSharper disable once ShortLivedHttpClient
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(2);
            await client.GetAsync(uri);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
