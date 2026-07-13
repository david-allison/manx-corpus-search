using System;
using CorpusSearch.Service;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SpaServices.StaticFiles;
using Microsoft.Extensions.DependencyInjection;

namespace CorpusSearch.Infrastructure;

/// <summary>
/// Returns a 404 for URLs that are neither a server route nor a page of the SPA.
/// Production only: in Development the SPA fallback also serves Vite's own requests
/// (/src/*, HMR), which cannot be enumerated here.
/// </summary>
internal static class SpaRouteGuard
{
    public static void UseSpaRouteGuard(this IApplicationBuilder app, WorkService workService)
    {
        app.Use(async (context, next) =>
        {
            // /Error is UseExceptionHandler's re-execute path let it keep
            // falling through to the shell rather than turn a 500 into a 404.
            if (context.Request.Path == "/Error" || IsSpaPage(context.Request.Path, workService))
            {
                await next(context);
                return;
            }

            // The shell must be sent from here with the status already set: falling
            // through to the SPA fallback would overwrite the status with 200.
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            var shell = context.RequestServices.GetService<ISpaStaticFileProvider>()
                ?.FileProvider?.GetFileInfo("index.html");
            if (shell is { Exists: true } && HttpMethods.IsGet(context.Request.Method))
            {
                context.Response.ContentType = "text/html; charset=utf-8";
                // as for the 200 shell: a stale shell references dead /assets/ files
                context.Response.Headers.CacheControl = "no-cache";
                await context.Response.SendFileAsync(shell);
            }
        });
    }

    /// <summary>
    /// The routes in ClientApp/src/App.tsx which render a page. Its catch-all
    /// (* -> NotFound) doesn't count: that's the 404 page itself.
    /// </summary>
    internal static bool IsSpaPage(PathString path, WorkService workService)
    {
        var value = (path.Value ?? "/").TrimEnd('/');
        if (value.Length == 0
            || value.Equals("/tools/youtube", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/contributions", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // the dictionary page: /dictionary and /dictionary/<any word>
        const string dictionary = "/dictionary";
        if (value.Equals(dictionary, StringComparison.OrdinalIgnoreCase)
            || (value.StartsWith(dictionary + "/", StringComparison.OrdinalIgnoreCase)
                && !value[(dictionary.Length + 1)..].Contains('/')))
        {
            return true;
        }

        const string docs = "/docs/";
        if (!value.StartsWith(docs, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        var docId = value[docs.Length..];
        // the ident check is case-sensitive on purpose: a wrong-case ident would render
        // the page but its api/Metadata lookup fails the same way
        return !docId.Contains('/') && workService.HasIdent(docId);
    }
}
