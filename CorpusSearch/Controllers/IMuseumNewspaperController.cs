using Microsoft.AspNetCore.Mvc;
using System;
using static CorpusSearch.Service.IMuseumNewspaperService;

namespace CorpusSearch.Controllers;

[Route("[controller]")]
public class IMuseumNewspaperController : Controller
{
    [HttpGet("Image/V1")]
    [HttpGet("Chunk/V1")]
    public IActionResult Image([FromQuery] string newspaper, [FromQuery] string date, [FromQuery] string id)
    {
        try
        {
            NewspaperClippingReference reference = NewspaperClippingReference.FromOrThrow(newspaper, date, id);
            string href = reference.Href;
            string clipId = reference.NewspaperClippingReferenceId;
            return base.Redirect($"https://www.imuseum.im/Olive/APA/IsleofMan/get/image.ashx?kind=block&href={href}&id={clipId}&ext=.png");
        } 
        catch (Exception e)
        {
            return base.BadRequest(e.Message);
        }
    }

    /// <summary>Redirects to a page where a user can view a full component (one or more pieces of Manx)</summary>
    /// <remarks>A Component and a chunk id are typically the same, but some chunks aren't components</remarks>
    /// <remarks>sample call: /IMuseumNewspaper/Component/V1?newspaper=MNH&date=1845-01-08&id=Ar00318</remarks>
    [HttpGet("Component/V1")]
    public IActionResult Component([FromQuery] string newspaper, [FromQuery] string date, [FromQuery] string id)
    {
        try
        {
            NewspaperComponent reference = NewspaperComponent.FromOrThrow(newspaper, date, id);
            string href = reference.Href;
            string componentId = reference.ComponentId;

            // A ts parameter was provided (the timestamp that the item was modified. YYYYMMDDHHMMSS)
            // this does not seem to be necessary in some cases, but not others.
            // Sometimes a bad TS value (or no TS value) causes the images to fail to load with a 403 forbidden
            string ts = "20210606011511"; 

            return base.Redirect($"https://www.imuseum.im/Olive/APA/IsleofMan/get/article.ashx?href={href}&id={componentId}&mode=image&ts={ts}");
        }
        catch (Exception e)
        {
            return base.BadRequest(e.Message);
        }
    }
}