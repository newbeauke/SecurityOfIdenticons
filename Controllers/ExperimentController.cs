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

        public IActionResult Index([FromQuery] IdenticonParameters parameters)
        {
            if (parameters.Resolution == 0) // default value if not provided
            {
                parameters = new IdenticonParameters(5, true, 1, 70, 50, 45, 45);
            }

            // Find last threshold for these settings to continue where we left off, otherwise 0.80
            double startingThreshold = 0.80;
            var allResults = _experimentService.GetResults().OrderBy(x => x.Timestamp).ToList();
            var serializedParams = JsonSerializer.Serialize(parameters);
            
            var resultsForParams = allResults.Where(x => x.Parameters != null && JsonSerializer.Serialize(x.Parameters) == serializedParams).ToList();
            
            if (resultsForParams.Any())
            {
                startingThreshold = resultsForParams.Last().Threshold;
            }

            var trial = _experimentService.GenerateTrial(startingThreshold, parameters);
            trial.Parameters = parameters;

            ViewBag.Parameters = parameters;
            return View(trial);
        }

        [HttpPost]
        public IActionResult SubmitGuess(int guessedIndex, Guid trialId, int correctIndex, string threshold, IdenticonParameters parameters, int streak = 0)
        {
            double parsedThreshold = 0.80; // default fallback
            if (!string.IsNullOrEmpty(threshold))
            {
                double.TryParse(threshold, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out parsedThreshold);
            }

            bool isCorrect = guessedIndex == correctIndex;
            int currentStreak = isCorrect ? streak + 1 : 0;
            ViewBag.Streak = currentStreak;

            var result = new ExperimentResult
            {
                TrialId = trialId,
                Threshold = parsedThreshold,
                GuessedCorrectly = isCorrect,
                GuessedIndex = guessedIndex,
                CorrectIndex = correctIndex,
                Parameters = parameters
            };

            _experimentService.RecordResult(result);

            // Adaptive Step: adjust threshold for next trial
            double nextThreshold = isCorrect ? parsedThreshold + 0.05 : parsedThreshold - 0.05;

            // 30% of the time, scatter the next threshold within +/- 10% of the calculated next threshold 
            // to build up the psychometric curve faster and avoid getting stuck on a single value.
            var rnd = new Random();
            if (rnd.NextDouble() < 0.3)
            {
                double scatter = (rnd.NextDouble() * 0.20) - 0.10; // -0.10 to +0.10
                nextThreshold += scatter;
            }

            // Keep threshold in bounds
            nextThreshold = Math.Max(0.5, Math.Min(0.99, nextThreshold));

            var nextTrial = _experimentService.GenerateTrial(nextThreshold, parameters);
            nextTrial.Parameters = parameters;

            ViewBag.Parameters = parameters;
            return View("Index", nextTrial);
        }
        
        public IActionResult Results(string configFilter = null)
        {
            var allResults = _experimentService.GetResults().OrderBy(x => x.Timestamp).ToList();

            // Normalize parameters so color-irrelevant settings don't create duplicate configs in the dropdown
            foreach(var r in allResults)
            {
                if (r.Parameters != null && r.Parameters.ColorCount <= 1)
                {
                    r.Parameters.MinHueDistance = 45;
                    r.Parameters.HueSpacing = 0;
                }
            }

            var configs = allResults.Where(r => r.Parameters != null)
                                    .Select(r => r.Parameters)
                                    .GroupBy(p => JsonSerializer.Serialize(p))
                                    .Select(g => g.First())
                                    .ToList();

            ViewBag.Configs = configs;

            var filteredResults = allResults;
            if (configFilter == "all")
            {
                ViewBag.CurrentConfigFilter = "all";
            }
            else if (!string.IsNullOrEmpty(configFilter))
            {
                filteredResults = allResults.Where(r => r.Parameters != null && JsonSerializer.Serialize(r.Parameters) == configFilter).ToList();
                ViewBag.CurrentConfigFilter = configFilter;
            }
            else if (configs.Any())
            {
                var latestConfig = allResults.Last(r => r.Parameters != null).Parameters;
                configFilter = JsonSerializer.Serialize(latestConfig);
                filteredResults = allResults.Where(r => r.Parameters != null && JsonSerializer.Serialize(r.Parameters) == configFilter).ToList();
                ViewBag.CurrentConfigFilter = configFilter;
            }
            else
            {
                ViewBag.CurrentConfigFilter = "";
            }

            double globalThreshold = 0;
            if (filteredResults.Count > 4)
            {
                int dropCount = filteredResults.Count / 2;
                globalThreshold = filteredResults.Skip(dropCount).Average(x => x.Threshold);
            }
            else if (filteredResults.Count > 0)
            {
                globalThreshold = filteredResults.Average(x => x.Threshold);
            }

            ViewBag.GlobalThreshold = globalThreshold;

            // Calculate entropy and visual entropy
            double rawEntropy = 0;
            double visualEntropy = 0;
            if (filteredResults.Any() && filteredResults.First().Parameters != null)
            {
                var cfg = filteredResults.First().Parameters;
                var gen = new IdenticonGenerator(cfg);
                var dummyRes = gen.Generate("dummy");
                rawEntropy = dummyRes.EntropyBits;

                int N = dummyRes.PatternEntropyBits;
                int k = (int)Math.Round(N * (1.0 - globalThreshold));
                
                double V = 0;
                for (int i = 0; i <= k; i++)
                {
                    V += Combinations(N, i);
                }

                double totalSearchSpace = Math.Pow(2, rawEntropy);
                double visualSearchSpace = Math.Max(1, totalSearchSpace / Math.Max(1, V));

                visualEntropy = Math.Max(0, rawEntropy - Math.Log2(Math.Max(1, V)));

                ViewBag.SearchSpaceCount = totalSearchSpace;
                ViewBag.VisualSpaceCount = visualSearchSpace;
            }
            ViewBag.RawEntropy = rawEntropy;
            ViewBag.VisualEntropy = visualEntropy;

            // Prepare Chart Data
            // Bin by 0.02 (2%) intervals for the graph to smooth out the curve
            var chartDataPoints = filteredResults
                .GroupBy(x => Math.Round(x.Threshold * 50) / 50.0)
                .Select(g => new { 
                    x = g.Key * 100, // Threshold percentage
                    y = g.Average(r => r.GuessedCorrectly ? 100.0 : 0.0), // Accuracy percentage
                    count = g.Count()
                })
                .OrderBy(d => d.x)
                .ToList();

            ViewBag.ChartData = JsonSerializer.Serialize(chartDataPoints);

            return View(filteredResults.OrderByDescending(x => x.Timestamp));
        }

        [HttpPost]
        public IActionResult ClearResults()
        {
            _experimentService.ClearResults();
            return RedirectToAction("Results");
        }

        private double Combinations(int n, int k)
        {
            if (k < 0 || k > n) return 0;
            if (k == 0 || k == n) return 1;
            k = Math.Min(k, n - k);
            double c = 1;
            for (int i = 1; i <= k; i++)
                c = c * (n - i + 1) / i;
            return c;
        }
    }
}
