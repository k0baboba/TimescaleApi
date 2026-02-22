using Microsoft.AspNetCore.Mvc;

namespace TimescaleApi.Controllers
{
    public class ValuesController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
