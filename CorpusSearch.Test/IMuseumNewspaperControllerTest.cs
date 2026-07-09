using System.Linq;
using CorpusSearch.Controllers;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;

namespace CorpusSearch.Test;

[TestFixture]
public class IMuseumNewspaperControllerTest
{
    /// <remarks>
    /// /IMuseumNewspaper links are shared externally
    /// </remarks>
    [Test]
    public void EndpointsAreUnchanged()
    {
        var controllerRoutes = typeof(IMuseumNewspaperController)
            .GetCustomAttributes(typeof(RouteAttribute), inherit: true)
            .Cast<RouteAttribute>()
            .Select(x => x.Template!.Replace("[controller]", "IMuseumNewspaper"))
            .ToList();

        var actionRoutes = typeof(IMuseumNewspaperController).GetMethods()
            .SelectMany(x => x.GetCustomAttributes(typeof(HttpGetAttribute), inherit: true))
            .Cast<HttpGetAttribute>()
            .Select(x => x.Template)
            .ToList();

        var servedUrls = controllerRoutes.SelectMany(c => actionRoutes.Select(a => $"/{c}/{a}"));

        Assert.That(servedUrls, Is.EquivalentTo(new[]
        {
            "/api/IMuseumNewspaper/Image/V1",
            "/api/IMuseumNewspaper/Chunk/V1",
            "/api/IMuseumNewspaper/Component/V1",
            "/IMuseumNewspaper/Image/V1",
            "/IMuseumNewspaper/Chunk/V1",
            "/IMuseumNewspaper/Component/V1",
        }));
    }
}
