using Microsoft.AspNetCore.Mvc;

namespace CorpusSearch.Controllers
{
    [Route("Dictionary/[controller]")]
    public class CregeenController : Controller
    {
        public IActionResult Index()
        {
            ViewData["query"] = "A";
            return View();
        }

        [HttpGet("{s}")]
        public IActionResult Get(string s)
        {
            ViewData["query"] = s;
            return View("~/Views/Cregeen/Index.cshtml");
        }
    }
}