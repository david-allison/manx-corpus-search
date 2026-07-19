using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace CorpusSearch.Infrastructure;

/// <summary>
/// The dictionary's own host (dictionary.gaelg.im), whose front door is the
/// dictionary landing at "/" (see App.tsx): /dictionary there names the same
/// page over. The sub-pages stay under /dictionary/, so new corpus routes can
/// be added without contesting the root. Mirrors the client's utils/Host.ts,
/// and like it counts any `dictionary.` host, so that dictionary.localhost
/// tries it on a dev machine without touching DNS.
/// </summary>
internal static class DictionaryHost
{
    public static bool Is(HttpRequest request) =>
        request.Host.Host.StartsWith("dictionary.", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Permanently moves the bare /dictionary to "/" on the dictionary host,
    /// query string and all: one address for the front door, not two.
    ///
    /// Sits after the MVC endpoints, so the server-rendered legacy pages
    /// (/Dictionary/Cregeen) are answered before it and stay where they are.
    /// Runs in Development too, unlike SpaRouteGuard: the redirect is the
    /// behaviour, not a production hardening.
    /// </summary>
    public static void UseDictionaryHostRootRedirect(this IApplicationBuilder app)
    {
        app.Use(async (context, next) =>
        {
            var target = RootRedirectTarget(context.Request);
            if (target != null)
            {
                context.Response.Redirect(target, permanent: true);
                return;
            }
            await next(context);
        });
    }

    /// <summary>"/" (with the query string riding along) for the dictionary
    /// host's bare /dictionary — or null where nothing moves: another host, or
    /// any sub-page, which lives under the prefix on both hosts</summary>
    internal static string? RootRedirectTarget(HttpRequest request)
    {
        var path = request.Path.Value?.TrimEnd('/');
        if (!Is(request)
            || !string.Equals(path, "/dictionary", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        return "/" + request.QueryString.ToUriComponent();
    }
}
