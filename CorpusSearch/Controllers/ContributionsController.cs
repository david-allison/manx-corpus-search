using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CorpusSearch.Service;
using Microsoft.AspNetCore.Mvc;

namespace CorpusSearch.Controllers;

/// <summary>
/// Unstable API
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ContributionsController(ContributionsService contributionsService)
{
    /// <summary>
    /// The all-time contributors leaderboard: most documents first. Roles and
    /// per-document credits are tracked in the service but deliberately not
    /// published: the page shows a rank, not an audit.
    /// </summary>
    [HttpGet]
    // ReSharper disable once UnusedMember.Global
    public async Task<List<ContributorDto>> Get() =>
        [.. (await contributionsService.GetContributors()).Select(x => new ContributorDto(x.Name, x.DocumentCount))];

    public record ContributorDto(string Name, int DocumentCount);
}
