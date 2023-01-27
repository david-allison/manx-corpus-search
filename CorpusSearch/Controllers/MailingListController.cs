using Microsoft.AspNetCore.Mvc;

namespace CorpusSearch.Controllers;

[Route("[controller]")]
public class MailingListController : Controller
{
    [HttpGet]
    public IActionResult Get()
    {
        return View("~/Views/MailingList.cshtml");
    }
}