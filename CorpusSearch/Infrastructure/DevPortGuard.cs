using System;
using System.Net;
using System.Net.Sockets;

namespace CorpusSearch.Infrastructure;

/// <summary>
/// Dev only: fails startup if a port we must listen on is already taken.
/// Kestrel would fail anyway, but only after the slow corpus load - and a stale
/// server from another checkout answering on our URLs means testing the wrong branch.
/// </summary>
internal static class DevPortGuard
{
    /// <param name="urls">semicolon-separated, as in ASPNETCORE_URLS / launchSettings.json</param>
    public static void EnsureListenPortsFree(string urls)
    {
        foreach (var url in urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // Kestrel allows wildcard hosts ("http://*:5000") which Uri cannot parse
            var parseable = url.Replace("://*", "://localhost").Replace("://+", "://localhost");
            if (!Uri.TryCreate(parseable, UriKind.Absolute, out var uri))
            {
                continue;
            }

            if (IsInUse(uri.Port))
            {
                throw new InvalidOperationException(
                    $"Port {uri.Port} ({url}) is already in use - probably by a server from another branch/worktree. " +
                    "Kill that process, or you would be testing the wrong code.");
            }
        }
    }

    private static bool IsInUse(int port) =>
        IsInUse(IPAddress.Loopback, port) || IsInUse(IPAddress.IPv6Loopback, port);

    private static bool IsInUse(IPAddress address, int port)
    {
        try
        {
            using var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(new IPEndPoint(address, port));
            return false;
        }
        catch (SocketException e) when (e.SocketErrorCode == SocketError.AddressAlreadyInUse)
        {
            return true;
        }
        catch (SocketException)
        {
            // e.g. no IPv6 support - can't tell from here, leave it to Kestrel
            return false;
        }
    }
}
