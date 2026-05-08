using Microsoft.AspNetCore.Mvc;
using SecurityOfIdenticons.Models;
using SecurityOfIdenticons.Services;
using System.Text.Json;

namespace SecurityOfIdenticons.Controllers
{
    public class ExperimentController : Controller
    {
        private readonly ExperimentService _experimentService;

        public ExperimentController(ExperimentService experimentService)
        {
            _experimentService = experimentService;
        }

        public IActionResult Index()
        {
            // Initial threshold starting point
            double startingThreshold = 0.80; 
            var parameters = new IdenticonParameters(5, true, 1, 70, 50, 45, 45); // Default params

            var trial = _experimentService.GenerateTrial(startingThreshold, parameters);

            ViewBag.Parameters = parameters;
            return View(trial);
        }

        [HttpPost]
        public IActionResult SubmitGuess(int guessedIndex, Guid trialId, int correctIndex, string threshold)
        {
            double parsedThreshold = 0.80; // default fallback
            if (!string.IsNullOrEmpty(threshold))
            {
                double.TryParse(threshold, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out parsedThreshold);
            }

            bool isCorrect = guessedIndex == correctIndex;

            var result = new ExperimentResult
            {
                TrialId = trialId,
                Threshold = parsedThreshold,
                GuessedCorrectly = isCorrect,
                GuessedIndex = guessedIndex,
                CorrectIndex = correctIndex
            };

            _experimentService.RecordResult(result);

            // Adaptive Step: adjust threshold for next trial
            double nextThreshold = isCorrect ? parsedThreshold + 0.05 : parsedThreshold - 0.05;

            // Keep threshold in bounds
            nextThreshold = Math.Max(0.5, Math.Min(0.99, nextThreshold));

            var parameters = new IdenticonParameters(5, true, 1, 70, 50, 45, 45); // Default params
            var nextTrial = _experimentService.GenerateTrial(nextThreshold, parameters);

            ViewBag.Parameters = parameters;
            return View("Index", nextTrial);
        }
        
        public IActionResult Results()
        {
            var results = _experimentService.GetResults().OrderBy(x => x.Timestamp).ToList();

            // Calculate Global Visual Similarity Threshold (Convergence point)
            // In a staircase method, the threshold converges around the 50% mark over time.
            // A standard way to estimate this is to average the thresholds of the latter half of the trials (ignoring the "burn-in" period).
            double globalThreshold = 0;
            if (results.Count > 4)
            {
                int dropCount = results.Count / 2;
                globalThreshold = results.Skip(dropCount).Average(x => x.Threshold);
            }
            else if (results.Count > 0)
            {
                globalThreshold = results.Average(x => x.Threshold);
            }

            ViewBag.GlobalThreshold = globalThreshold;
            return View(results.OrderByDescending(x => x.Timestamp));
        }

        [HttpPost]
        public IActionResult ClearResults()
        {
            _experimentService.ClearResults();
            return RedirectToAction("Results");
        }
    }
}
