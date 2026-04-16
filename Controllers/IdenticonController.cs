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
        public IActionResult GenerateResult(string input1, string input2, string input1Mode = "String", string input2Mode = "String", int resolution = 5, bool isSymmetric = true, int colorCount = 1, int saturation = 70, int lightness = 50, int minHueDistance = 45, int hueSpacing = 45)
        {
            if (string.IsNullOrWhiteSpace(input1) && string.IsNullOrWhiteSpace(input2))
            {
                return Content("<div class='alert alert-warning text-center'>At least one identifier cannot be empty</div>");
            }

            var parameters = new IdenticonParameters(resolution, isSymmetric, colorCount, saturation, lightness, minHueDistance, hueSpacing);
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
        public IActionResult MineCollisionChunk(string input1, string matchType = "Shape", int startAttempt = 1, int batchSize = 25000, int resolution = 5, bool isSymmetric = true, int colorCount = 1, int saturation = 70, int lightness = 50, int minHueDistance = 45, int hueSpacing = 45)
        {
            if (string.IsNullOrWhiteSpace(input1)) return Content("<div class='text-danger fw-bold text-center'>Provide Identifier 1</div>", "text/html");

            var parameters = new IdenticonParameters(resolution, isSymmetric, colorCount, saturation, lightness, minHueDistance, hueSpacing);
            var generator = new IdenticonGenerator(parameters);

            var baseResult = generator.Generate(input1);

            int maxAttempts = 2000000;
            int endAttempt = Math.Min(startAttempt + batchSize - 1, maxAttempts);

            string[] baseColors = new string[baseResult.Grid.Length];
            for (int g = 0; g < baseResult.Grid.Length; g++)
            {
                int cIdx = baseResult.Grid[g];
                if (cIdx == 0) baseColors[g] = "bg";
                else if (baseResult.Colors != null && baseResult.Colors.Count > 0 && cIdx <= baseResult.Colors.Count) baseColors[g] = baseResult.Colors[cIdx - 1];
                else baseColors[g] = "black";
            }

            for (int i = startAttempt; i <= endAttempt; i++)
            {
                string testString = $"{input1}_{i}";
                var testResult = generator.Generate(testString);

                bool isMatch = false;

                if (matchType == "Shape")
                {
                    isMatch = true;
                    for (int g = 0; g < baseResult.Grid.Length; g++)
                    {
                        if ((baseResult.Grid[g] > 0) != (testResult.Grid[g] > 0))
                        {
                            isMatch = false;
                            break;
                        }
                    }
                }
                else if (matchType == "ColorAndShape")
                {
                    isMatch = true;
                    for (int g = 0; g < baseResult.Grid.Length; g++)
                    {
                        if (baseResult.Grid[g] != testResult.Grid[g])
                        {
                            isMatch = false;
                            break;
                        }
                    }
                    if (isMatch && baseResult.Colors != null && testResult.Colors != null && baseResult.Colors.Count == testResult.Colors.Count)
                    {
                        for (int c=0; c < baseResult.Colors.Count; c++)
                        {
                            if (baseResult.Colors[c] != testResult.Colors[c])
                            {
                                isMatch = false;
                                break;
                            }
                        }
                    }
                }
                else if (matchType == "NearClone")
                {
                    int totalCells = baseResult.Grid.Length;
                    int exactMatches = 0;

                    for (int g = 0; g < totalCells; g++)
                    {
                        string c1 = baseColors[g];
                        string c2 = "black";
                        int cIdx = testResult.Grid[g];

                        if (cIdx == 0) c2 = "bg";
                        else if (testResult.Colors != null && testResult.Colors.Count > 0 && cIdx <= testResult.Colors.Count) c2 = testResult.Colors[cIdx - 1];

                        if (c1 == c2) exactMatches++;
                    }

                    // Treat >= 90% cell match as a "Near Clone"
                    double matchPercentage = (exactMatches / (double)totalCells);
                    if (matchPercentage >= 0.90)
                    {
                        isMatch = true;
                    }
                }

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
            string matchLabel = matchType == "NearClone" ? "Near Clone (90%+)" : (matchType == "Shape" ? "Shape" : "Exact Clone");

            return Content($@"
                <div hx-get=""/Identicon/MineCollisionChunk?matchType={matchType}&startAttempt={endAttempt + 1}&batchSize={batchSize}"" hx-trigger=""load"" hx-include=""#identiconForm"" hx-target=""#minerModalBody"">
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