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
        public IActionResult GenerateResult(string input1, string input2, int resolution = 5, bool isSymmetric = true, int colorCount = 1, int saturation = 70, int lightness = 50, int minHueDistance = 45, int hueSpacing = 45)
        {
            if (string.IsNullOrWhiteSpace(input1) && string.IsNullOrWhiteSpace(input2))
            {
                return Content("<div class='alert alert-warning text-center'>At least one identifier cannot be empty</div>");
            }

            var parameters = new IdenticonParameters(resolution, isSymmetric, colorCount, saturation, lightness, minHueDistance, hueSpacing);
            var generator = new IdenticonGenerator(parameters);
            
            var results = new List<IdenticonResult>();
            if (!string.IsNullOrWhiteSpace(input1)) results.Add(generator.Generate(input1));
            if (!string.IsNullOrWhiteSpace(input2)) results.Add(generator.Generate(input2));

            ViewBag.Parameters = parameters;

            // Pass the list of results to the view
            return PartialView("_IdenticonResult", results);
        }
    }
}