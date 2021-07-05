using Microsoft.AspNetCore.Mvc;
using System;
using static CorpusSearch.Service.IMuseumNewspaperService;

namespace CorpusSearch.Controllers
{
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
    }
}
