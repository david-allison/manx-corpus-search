using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CorpusSearch.Controllers;

/// <summary>
/// Relays a reader's report on a dictionary entry to a Google Apps Script,
/// which appends it as a row to a sheet: the feedback store lives entirely off
/// this server. Entries have no stable ids (identity is positional in the
/// books), so a report names its entry by dictionary and headword only.
/// Nothing from the body is ever logged — production runs with logging off
/// for user privacy, and this endpoint carries free-typed user text.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("feedback")]
public class FeedbackController(IHttpClientFactory httpClientFactory, FeedbackConfig config) : ControllerBase
{
    /// <summary>Covers an Apps Script cold start (a few seconds) without
    /// letting an outage hold request threads for long</summary>
    private static readonly TimeSpan RelayTimeout = TimeSpan.FromSeconds(10);

    [HttpPost]
    [RequestSizeLimit(16_384)]
    public async Task<IActionResult> Post([FromBody] FeedbackRequest request)
    {
        if (string.IsNullOrEmpty(config.AppsScriptUrl))
        {
            // the relay target is not configured: the feature is dark
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Comments))
        {
            return BadRequest();
        }

        if (!string.IsNullOrEmpty(request.Website))
        {
            // honeypot: the form never renders this field, so a value marks a
            // bot filling every box. Claim success so it does not try harder.
            return NoContent();
        }

        // truncate rather than reject: a heartfelt over-long report is still a
        // report, and the sheet should not be spillable by one either
        var payload = new
        {
            name = Truncate(request.Name, 200),
            comments = Truncate(request.Comments, 2000),
            dictionary = Truncate(request.Dictionary, 100),
            headword = Truncate(request.Headword, 200),
        };

        var client = httpClientFactory.CreateClient();
        client.Timeout = RelayTimeout;
        try
        {
            // Apps Script answers the POST with a redirect to
            // script.googleusercontent.com, which the handler follows as a GET —
            // the append has already run by then. A script that crashed can
            // still answer 200 with an HTML error page, so success is the
            // script's own {"ok":true}, not the status line.
            using var response = await client.PostAsJsonAsync(config.AppsScriptUrl, payload, HttpContext.RequestAborted);
            var body = await response.Content.ReadAsStringAsync(HttpContext.RequestAborted);
            if (!response.IsSuccessStatusCode || !body.Contains("\"ok\":true"))
            {
                return StatusCode(502);
            }
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            // network failure or the timeout: the report was (probably) not
            // recorded, and the form says so and keeps the reader's text
            return StatusCode(502);
        }

        return NoContent();
    }

    private static string? Truncate(string? s, int max) => s != null && s.Length > max ? s[..max] : s;

    public class FeedbackRequest
    {
        public string? Name { get; set; }
        public required string Comments { get; set; }
        public required string Dictionary { get; set; }
        public required string Headword { get; set; }
        /// <summary>Honeypot — the form never shows it; a value marks a bot</summary>
        public string? Website { get; set; }
    }
}
