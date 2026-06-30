using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;

namespace SecurityOfIdenticons.Controllers
{
    public class IdenticonController : Controller
    {
        private static readonly ConcurrentDictionary<string, List<(string Input, IdenticonResult Result)>> _birthdayPool = new();

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public IActionResult GenerateResult(string input1, string input2, string input1Mode = "String", string input2Mode = "String", int resolution = 5, bool isSymmetric = true, int colorCount = 1, int saturation = 70, int lightness = 50, int minHueDistance = 45, int hueSpacing = 45, SecurityLevel safeMode = SecurityLevel.None)
        {
            if (string.IsNullOrEmpty(input1)) input1 = "";
            if (string.IsNullOrEmpty(input2)) input2 = "";

            if (string.IsNullOrWhiteSpace(input1) && string.IsNullOrWhiteSpace(input2))
            {
                return Content("<div class='alert alert-warning text-center'>At least one identifier cannot be empty</div>");
            }

            var parameters = new IdenticonParameters(resolution, isSymmetric, colorCount, saturation, lightness, minHueDistance, hueSpacing, safeMode);
            var generator = new IdenticonGenerator(parameters);

            var results = new List<IdenticonResult>();
            byte[] primaryHash = null;
            if (!string.IsNullOrWhiteSpace(input1)) 
            {
                if (input1Mode == "Hash")
                {
                    string binStr = new string(input1.Where(c => c == '0' || c == '1').ToArray());
                    primaryHash = new byte[32];
                    for (int i = 0; i < Math.Min(binStr.Length, 256); i++)
                    {
                        if (binStr[i] == '1')
                        {
                            primaryHash[i / 8] |= (byte)(1 << (7 - (i % 8)));
                        }
                    }
                    if (binStr.Length > 0)
                    {
                        results.Add(generator.GenerateFromHash(primaryHash, "Custom Hash 1"));
                    }
                }
                else
                {
                    primaryHash = generator.ComputeHash(input1);
                    results.Add(generator.GenerateFromHash(primaryHash, input1));
                }
            }
            if (!string.IsNullOrWhiteSpace(input2)) 
            {
                if (input2Mode == "Hash")
                {
                    // Parse binary string to byte array
                    string binStr = new string(input2.Where(c => c == '0' || c == '1').ToArray());
                    byte[] hashBytes = new byte[32];
                    for (int i = 0; i < Math.Min(binStr.Length, 256); i++)
                    {
                        if (binStr[i] == '1')
                        {
                            hashBytes[i / 8] |= (byte)(1 << (7 - (i % 8))); // pad bits
                        }
                    }
                    if (binStr.Length > 0)
                    {
                        results.Add(generator.GenerateFromHash(hashBytes, "Custom Hash 2"));
                    }
                }
                else if (input2Mode == "RandomBitFlips" && primaryHash != null)
                {
                    if (int.TryParse(input2, out int bitsToFlip))
                    {
                        byte[] modifiedHash = (byte[])primaryHash.Clone();
                        Random rand = new Random();
                        int totalBits = modifiedHash.Length * 8;
                        var bitsToSelect = new HashSet<int>();

                        // Sanity check preventing infinite loop
                        bitsToFlip = Math.Min(bitsToFlip, totalBits);

                        while (bitsToSelect.Count < bitsToFlip)
                        {
                            bitsToSelect.Add(rand.Next(totalBits));
                        }

                        foreach (int bitIndex in bitsToSelect)
                        {
                            int byteIndex = bitIndex / 8;
                            int bitInByte = 7 - (bitIndex % 8); // match display order
                            modifiedHash[byteIndex] ^= (byte)(1 << bitInByte);
                        }

                        results.Add(generator.GenerateFromHash(modifiedHash, $"{bitsToFlip} Bit Flips"));
                    }
                }
                else
                {
                    results.Add(generator.Generate(input2));
                }
            }

            ViewBag.Parameters = parameters;

            // Pass the list of results to the view
            return PartialView("_IdenticonResult", results);
        }

        [HttpGet]
        public IActionResult MineCollisionChunk(string input1, string matchType = "Shape", double targetMatch = 90.0, int startAttempt = 1, int batchSize = 25000, int resolution = 5, bool isSymmetric = true, int colorCount = 1, int saturation = 70, int lightness = 50, int minHueDistance = 45, int hueSpacing = 45, string mineMode = "Target", SecurityLevel safeMode = SecurityLevel.None)
        {
            if (string.IsNullOrEmpty(input1)) input1 = "";

            var parameters = new IdenticonParameters(resolution, isSymmetric, colorCount, saturation, lightness, minHueDistance, hueSpacing, safeMode);
            var generator = new IdenticonGenerator(parameters);

            var baseResult = generator.Generate(input1);

            int maxAttempts = 2000000;
            int endAttempt = Math.Min(startAttempt + batchSize - 1, maxAttempts);

            string targetMetricId = matchType == "ExactClone" ? "ShapeAndColor" : matchType;
            double actualTargetMatch = matchType == "ExactClone" ? 1.0 : (targetMatch / 100.0);
            var metric = MetricRegistry.Get(targetMetricId);

            if (mineMode == "Any")
            {
                string poolKey = $"{input1}_{matchType}_{targetMatch}_{resolution}_{isSymmetric}_{colorCount}_{saturation}_{lightness}_{minHueDistance}_{hueSpacing}";
                if (startAttempt == 1) _birthdayPool[poolKey] = new List<(string Input, IdenticonResult Result)>();

                var pool = _birthdayPool.ContainsKey(poolKey) ? _birthdayPool[poolKey] : new List<(string Input, IdenticonResult Result)>();

                // Cap chunk size for Any Collision to prevent extreme HTMX timeouts due to exponential comparisons
                int currentBatchSize = Math.Min(batchSize, 1500); 
                int endAttemptAny = Math.Min(startAttempt + currentBatchSize - 1, maxAttempts);

                for (int i = startAttempt; i <= endAttemptAny; i++)
                {
                    string testString = $"{input1}_{i}";
                    var testResult = generator.Generate(testString);

                    foreach (var past in pool)
                    {
                        var metricResult = metric.Compare(past.Result, testResult);
                        if (metricResult.SimilarityPercentage >= actualTargetMatch)
                        {
                            _birthdayPool.TryRemove(poolKey, out _); // clean up

                            string oobInput1 = $"<input name=\"input1\" type=\"text\" id=\"userInput1\" placeholder=\"Enter identifier 1\" value=\"{past.Input}\" hx-get=\"/Identicon/GenerateResult\" hx-trigger=\"keyup changed delay:200ms\" hx-target=\"#resultContainer\" hx-include=\"#identiconForm\" hx-vals='js:{{\"isSymmetric\": document.getElementById(\"symToggle\").checked}}' class=\"form-control\" hx-swap-oob=\"true\">";
                            string oobInput2 = $"<input name=\"input2\" type=\"text\" id=\"userInput2\" placeholder=\"Enter identifier 2\" value=\"{testString}\" hx-get=\"/Identicon/GenerateResult\" hx-trigger=\"keyup changed delay:200ms, load\" hx-target=\"#resultContainer\" hx-include=\"#identiconForm\" hx-vals='js:{{\"isSymmetric\": document.getElementById(\"symToggle\").checked}}' class=\"form-control\" hx-swap-oob=\"true\">";

                            return Content($@"
                                <div class='text-center mb-3 mt-2'>
                                    <div class='badge bg-success mb-2 p-2'>Birthday Collision Found!</div><br>
                                    <div class='mb-2 small font-mono'><strong>{past.Input}</strong><br>==<br><strong>{testString}</strong></div>
                                    <span class='font-mono small text-muted'>Attempts: {pool.Count:N0}</span><br>
                                    Closing...
                                </div>
                                <script>
                                    setTimeout(function() {{
                                        var btn = document.querySelector('#minerModal .btn-outline-danger');
                                        if (btn) btn.click();
                                        else {{ var modalEl = document.getElementById('minerModal'); if(modalEl) bootstrap.Modal.getInstance(modalEl).hide(); }}
                                    }}, 2500);
                                </script>
                                {oobInput1}
                                {oobInput2}
                            ", "text/html");
                        }
                    }
                    pool.Add((testString, testResult));

                    // Memory cap to prevent explosion 
                    if (pool.Count > 50000) {
                         _birthdayPool.TryRemove(poolKey, out _);
                         return Content("<div class='text-danger text-center font-mono small'>Pool reached 50,000 limit. Terminating.</div>", "text/html");
                    }
                }

                _birthdayPool[poolKey] = pool;

                double pctAny = (pool.Count / 50000.0) * 100.0;
                string tmQueryAny = targetMatch.ToString(System.Globalization.CultureInfo.InvariantCulture);

                return Content($@"
                    <div hx-get=""/Identicon/MineCollisionChunk?matchType={matchType}&targetMatch={tmQueryAny}&startAttempt={endAttemptAny + 1}&batchSize={batchSize}&mineMode=Any"" hx-trigger=""load"" hx-include=""#identiconForm"" hx-target=""#minerModalBody"">
                        <div class='text-center mb-3'>
                            <strong>Mining Any Collision (Birthday Attack)...</strong><br>
                            Pool Size: {pool.Count:N0}<br>
                            <span class='text-muted small'>{(pool.Count * currentBatchSize):N0} comparisons this chunk</span>
                        </div>
                        <div class='progress' style='height: 5px;'>
                            <div class='progress-bar bg-warning' role='progressbar' style='width: {pctAny:F1}%'></div>
                        </div>
                    </div>
                ", "text/html");
            }

            for (int i = startAttempt; i <= endAttempt; i++)
            {
                string testString = $"{input1}_{i}";
                var testResult = generator.Generate(testString);

                string targetMetricId2 = matchType == "ExactClone" ? "ShapeAndColor" : matchType;
                double actualTargetMatch2 = matchType == "ExactClone" ? 1.0 : (targetMatch / 100.0);
                var metric2 = MetricRegistry.Get(targetMetricId2);

                var metricResult = metric2.Compare(baseResult, testResult);
                bool isMatch = metricResult.SimilarityPercentage >= actualTargetMatch2;

                if (isMatch)
                {
                    string oobInput = $"<input name=\"input2\" id=\"userInput2\" type=\"text\" placeholder=\"Enter identifier 2\" value=\"{testString}\" hx-get=\"/Identicon/GenerateResult\" hx-trigger=\"keyup changed delay:200ms, load\" hx-target=\"#resultContainer\" hx-include=\"#identiconForm\" hx-vals='js:{{\"isSymmetric\": document.getElementById(\"symToggle\").checked}}' class=\"form-control\" hx-swap-oob=\"true\">";

                    return Content($@"
                        <div class='text-center mb-3'>
                            <strong>Match found!</strong><br>
                            Closing...
                        </div>
                        <script>
                            setTimeout(function() {{
                                var btn = document.querySelector('#minerModal .btn-outline-danger');
                                if (btn) btn.click();
                                else {{
                                    var modalEl = document.getElementById('minerModal');
                                    if (modalEl) {{
                                        var modal = bootstrap.Modal.getInstance(modalEl);
                                        if (modal) modal.hide();
                                    }}
                                }}
                            }}, 500);
                        </script>
                        {oobInput}
                    ", "text/html");
                }
            }

            if (endAttempt >= maxAttempts)
            {
                string oobInput = $"<input name=\"input2\" id=\"userInput2\" type=\"text\" placeholder=\"Enter identifier 2\" value=\"Not Found in {maxAttempts:N0} tries\" hx-get=\"/Identicon/GenerateResult\" hx-trigger=\"keyup changed delay:200ms\" hx-target=\"#resultContainer\" hx-include=\"#identiconForm\" hx-vals='js:{{\"isSymmetric\": document.getElementById(\"symToggle\").checked}}' class=\"form-control is-invalid border-danger\" hx-swap-oob=\"true\">";

                return Content($@"
                    <div class='text-center text-danger mb-3'>
                        <strong>Not found!</strong><br>
                        Closing...
                    </div>
                    <script>
                        setTimeout(function() {{
                            var btn = document.querySelector('#minerModal .btn-outline-danger');
                            if (btn) btn.click();
                            else {{
                                var modalEl = document.getElementById('minerModal');
                                if (modalEl) {{
                                    var modal = bootstrap.Modal.getInstance(modalEl);
                                    if (modal) modal.hide();
                                }}
                            }}
                        }}, 500);
                    </script>
                    {oobInput}
                ", "text/html");
            }

            // Continue loop via HTMX load
            double pct = (endAttempt / (double)maxAttempts) * 100;
            string matchLabel = matchType == "Shape" ? "Shape" : (matchType == "ExactClone" ? "Exact Clone" : matchType);
            string tmQuery = targetMatch.ToString(System.Globalization.CultureInfo.InvariantCulture);

            return Content($@"
                <div hx-get=""/Identicon/MineCollisionChunk?matchType={matchType}&targetMatch={tmQuery}&startAttempt={endAttempt + 1}&batchSize={batchSize}"" hx-trigger=""load"" hx-include=""#identiconForm"" hx-target=""#minerModalBody"">
                    <div class='text-center mb-3'>
                        <strong>Mining {matchLabel}...</strong><br>
                        Attempt {endAttempt:N0} / {maxAttempts:N0}
                    </div>
                    <div class='progress' style='height: 5px;'>
                        <div class='progress-bar bg-primary' role='progressbar' style='width: {pct:F1}%'></div>
                    </div>
                </div>
            ", "text/html");
        }
    }
}