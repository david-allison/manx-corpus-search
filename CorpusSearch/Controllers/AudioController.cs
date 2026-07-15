using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CorpusSearch.Service.Dictionaries;
using Microsoft.AspNetCore.Mvc;

namespace CorpusSearch.Controllers;

/// <summary>
/// Same-origin relay for the spoken dictionary's pronunciation recordings.
/// Browsers fetching learnmanx.com audio cross-site are at the mercy of its
/// CDN's per-session rules (hotlink/bot heuristics can 403 what a plain
/// request serves fine), so the popup plays through this endpoint instead:
/// the fetch becomes server-to-server and the browser stays on our origin.
/// Strictly limited to the Culture Vannin media the artifact references —
/// this must never become an open proxy.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AudioController(IHttpClientFactory httpClientFactory) : ControllerBase
{
    /// <summary>Only the spoken dictionary's own media: https, one known host,
    /// its media directory, an mp3, no query/fragment tricks</summary>
    internal static readonly Regex AllowedUrl =
        new(@"^https://www\.learnmanx\.com/media/[^?#]+\.mp3$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>A recording is ~200KB: a small bounded cache keeps repeat plays
    /// (and Safari's range retries) off the source's servers</summary>
    private static readonly ConcurrentDictionary<string, byte[]> Cache = new();
    private static readonly ConcurrentQueue<string> CacheOrder = new();
    private const int CacheEntries = 64;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string url)
    {
        if (!CultureVanninSpokenDictionaryService.Enabled)
        {
            return NotFound();
        }

        if (url == null || !AllowedUrl.IsMatch(url) || !Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            return NotFound();
        }

        if (!Cache.TryGetValue(url, out var bytes))
        {
            var client = httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
                "manx-corpus-search/1.0 (+https://github.com/david-allison/manx-corpus-search)");
            try
            {
                bytes = await client.GetByteArrayAsync(url, HttpContext.RequestAborted);
            }
            catch (HttpRequestException)
            {
                return StatusCode(502);
            }
            if (Cache.TryAdd(url, bytes))
            {
                CacheOrder.Enqueue(url);
                while (Cache.Count > CacheEntries && CacheOrder.TryDequeue(out var oldest))
                {
                    Cache.TryRemove(oldest, out _);
                }
            }
        }

        // recordings never change: let the browser keep them for a week
        Response.Headers.CacheControl = "public, max-age=604800, immutable";
        // range processing keeps Safari's media pipeline happy (it expects 206)
        return File(bytes, "audio/mpeg", enableRangeProcessing: true);
    }
}
