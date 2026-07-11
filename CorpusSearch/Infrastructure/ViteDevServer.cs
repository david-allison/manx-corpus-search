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
    /// An already-running server is only reused if it serves this checkout - a stale
    /// one from another branch/worktree would mean silently testing the wrong code.
    /// </summary>
    public static async Task<Uri> EnsureRunningAsync(Uri devServer)
    {
        if (await IsRespondingAsync(devServer))
        {
            await EnsureServesThisCheckoutAsync(devServer);
            return devServer;
        }

        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = isWindows ? "cmd" : "npm",
            Arguments = isWindows ? "/c npm run dev" : "run dev",
            WorkingDirectory = ClientAppDir,
            UseShellExecute = false,
        });

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        while (!await IsRespondingAsync(devServer))
        {
            // strictPort makes Vite exit rather than drift to another port
            if (process is { HasExited: true })
            {
                throw new InvalidOperationException(
                    $"'npm run dev' exited with code {process.ExitCode} before {devServer} was reachable. " +
                    $"Is port {devServer.Port} taken by another process?");
            }

            try
            {
                await Task.Delay(500, timeout.Token);
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException($"Timed out waiting for the Vite dev server at {devServer}");
            }
        }
        return devServer;
    }

    /// <summary>
    /// Dev only, at startup: fails if the dev-server port is held by a server which
    /// does not serve this checkout (e.g. left running by another branch/worktree).
    /// </summary>
    public static void EnsureFreeOrOurs(Uri devServer)
    {
        if (IsRespondingAsync(devServer).GetAwaiter().GetResult())
        {
            EnsureServesThisCheckoutAsync(devServer).GetAwaiter().GetResult();
        }
    }

    private static string ClientAppDir => Path.Combine(Directory.GetCurrentDirectory(), "ClientApp");

    private static async Task EnsureServesThisCheckoutAsync(Uri devServer)
    {
        string? served = null;
        try
        {
            // ReSharper disable once ShortLivedHttpClient
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(2);
            var response = await client.GetAsync(new Uri(devServer, "/__root"));
            // Vite without our plugin answers /__root with the index.html fallback;
            // only the plugin (vite.config.ts) responds with text/plain
            if (response.IsSuccessStatusCode && response.Content.Headers.ContentType?.MediaType == "text/plain")
            {
                served = (await response.Content.ReadAsStringAsync()).Trim();
            }
        }
        catch
        {
            // unreachable/unidentifiable: treated as "not ours" below
        }

        if (served != null && NormalizePath(served).Equals(NormalizePath(ClientAppDir), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var identity = served == null
            ? "it did not identify itself via /__root"
            : $"it serves '{Truncate(served)}' rather than '{ClientAppDir}'";
        throw new InvalidOperationException(
            $"A server is already running on {devServer}, but {identity}. " +
            "It is probably a stale Vite dev server from another branch/worktree - kill it, then restart.");
    }

    // Vite reports its root with forward slashes, even on Windows
    private static string NormalizePath(string path) =>
        Path.TrimEndingDirectorySeparator(path.Replace('\\', '/'));

    private static string Truncate(string s) => s.Length <= 120 ? s : s[..120] + "…";

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
