using Microsoft.AspNetCore.Mvc;

namespace SecurityOfIdenticons.Controllers
{
    public class IdenticonController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public IActionResult GenerateResult(string input, int resolution = 5, bool isSymmetric = true, int colorCount = 1, int saturation = 70, int lightness = 50, int minHueDistance = 45, int hueSpacing = 45)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return Content("<div class='alert alert-warning text-center'>Identifier cannot be empty</div>");
            }

            var parameters = new IdenticonParameters(resolution, isSymmetric, colorCount, saturation, lightness, minHueDistance, hueSpacing);
            var generator = new IdenticonGenerator(parameters);
            var result = generator.Generate(input);

            ViewBag.Parameters = parameters;

            return PartialView("_IdenticonResult", result);
        }
    }
}