using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace SecurityOfIdenticons
{
    public class IdenticonGenerator
    {
        private readonly IdenticonParameters parameters;

        public IdenticonGenerator(IdenticonParameters parameters)
        {
            if (parameters.Resolution < 3)
                throw new ArgumentException("Resolution must be at least 3");

            this.parameters = parameters;
        }

        public IdenticonResult Generate(string input)
        {
            byte[] hash = ComputeHash(input);

            int columnsToGenerate = parameters.IsSymmetric ? (int)Math.Ceiling(parameters.Resolution / 2.0) : parameters.Resolution;
            int totalBitsRequired = parameters.Resolution * columnsToGenerate;

            bool[] bits = ExtractBitsFromHash(hash, totalBitsRequired);
            int[] grid = new int[parameters.Resolution * parameters.Resolution];

            int colorBitOffset = totalBitsRequired;

            for (int row = 0; row < parameters.Resolution; row++)
            {
                for (int col = 0; col < columnsToGenerate; col++)
                {
                    int index = row * columnsToGenerate + col;
                    bool isActive = bits[index % bits.Length];

                    int colorIndex = 0;
                    if (isActive)
                    {
                        if (parameters.ColorCount == 0)
                        {
                            // No color mode: mark as active but use default color
                            colorIndex = 1;
                        }
                        else if (parameters.ColorCount == 1)
                        {
                            colorIndex = 1;
                        }
                        else
                        {
                            // Use hash to determine which color (1 to colorCount)
                            int hashIndex = colorBitOffset + index;
                            int hashByte = hash[hashIndex % hash.Length];
                            colorIndex = 1 + (hashByte % parameters.ColorCount);
                        }
                    }

                    grid[row * parameters.Resolution + col] = colorIndex;

                    if (parameters.IsSymmetric)
                    {
                        grid[row * parameters.Resolution + (parameters.Resolution - 1 - col)] = colorIndex;
                    }
                }
            }

            // Generate colors based on hash with guaranteed visual distinction
            List<string> colors = new List<string>();
            double paletteEntropyBits = 0;
            string warningMsg = null;
            int paletteEntropyBuckets = 1;

            if (parameters.ColorCount > 0)
            {
                int bucketCount = (int)Math.Floor(360.0 / parameters.MinHueDistance);
                if (bucketCount < 1)
                {
                    bucketCount = 1;
                }
                
                double md = parameters.MinHueDistance;
                double sp = parameters.HueSpacing;

                bool IsValidDist(double h1, double h2) {
                    double d = Math.Abs(h1 - h2);
                    if (d > 180.0) d = 360.0 - d;
                    return d >= sp;
                }

                uint hashVal = BitConverter.ToUInt32(hash, 0);

                if (parameters.ColorCount == 1)
                {
                    paletteEntropyBuckets = bucketCount;
                    double hue = (hashVal % bucketCount) * md;
                    string hueStr = hue.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
                    colors.Add($"hsl({hueStr}, {parameters.Saturation}%, {parameters.Lightness}%)");
                }
                else if (parameters.ColorCount == 2)
                {
                    int c1 = (int)(hashVal % bucketCount);
                    double h1 = c1 * md;

                    List<int> validC2 = new List<int>();
                    for (int i = 0; i < bucketCount; i++) if (IsValidDist(h1, i * md)) validC2.Add(i);

                    paletteEntropyBuckets = bucketCount * validC2.Count;

                    if (validC2.Count > 0)
                    {
                        int c2 = validC2[(int)((hashVal / bucketCount) % validC2.Count)];
                        double h2 = c2 * md;
                        colors.Add($"hsl({h1.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}, {parameters.Saturation}%, {parameters.Lightness}%)");
                        colors.Add($"hsl({h2.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}, {parameters.Saturation}%, {parameters.Lightness}%)");
                    }
                }
                else if (parameters.ColorCount >= 3)
                {
                    int c1 = (int)(hashVal % bucketCount);
                    double h1 = c1 * md;

                    List<int> validC2 = new List<int>();
                    for (int i = 0; i < bucketCount; i++) if (IsValidDist(h1, i * md)) validC2.Add(i);

                    if (validC2.Count > 0)
                    {
                        int c2 = validC2[(int)((hashVal / bucketCount) % validC2.Count)];
                        double h2 = c2 * md;

                        // Calculate the true count of possible permutations for accurately reporting entropy (O(N^2) max 129,600 simple iterations, very fast)
                        int validTripletsForC1 = 0;
                        for (int i = 0; i < validC2.Count; i++)
                        {
                            double tempH2 = validC2[i] * md;
                            for (int j = 0; j < bucketCount; j++)
                            {
                                if (IsValidDist(h1, j * md) && IsValidDist(tempH2, j * md)) validTripletsForC1++;
                            }
                        }
                        paletteEntropyBuckets = bucketCount * validTripletsForC1;

                        List<int> validC3 = new List<int>();
                        for (int i = 0; i < bucketCount; i++) if (IsValidDist(h1, i * md) && IsValidDist(h2, i * md)) validC3.Add(i);

                        if (validC3.Count > 0)
                        {
                            // Utilize the next bytes of the hash block so our pseudo-RNG remains distributed for the last color
                            int c3 = validC3[(int)(BitConverter.ToUInt32(hash, 4) % validC3.Count)];
                            double h3 = c3 * md;
                            colors.Add($"hsl({h1.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}, {parameters.Saturation}%, {parameters.Lightness}%)");
                            colors.Add($"hsl({h2.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}, {parameters.Saturation}%, {parameters.Lightness}%)");
                            colors.Add($"hsl({h3.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}, {parameters.Saturation}%, {parameters.Lightness}%)");
                        }
                        else
                        {
                            paletteEntropyBuckets = 0;
                        }
                    }
                    else
                    {
                        paletteEntropyBuckets = 0;
                    }
                }

                if (paletteEntropyBuckets <= 1)
                {
                    paletteEntropyBuckets = 1;
                    if (colors.Count == 0) // fallback if nothing was valid
                    {
                        for (int i=0; i<parameters.ColorCount; i++) colors.Add($"hsl(0, {parameters.Saturation}%, {parameters.Lightness}%)");
                    }
                    warningMsg = $"Warning: The combination of Minimum Hue Distance ({parameters.MinHueDistance}°) and Color Spacing ({parameters.HueSpacing}°) leaves no valid color combinations. 0 bits of visual entropy.";
                }
                else if (paletteEntropyBuckets < 5)
                {
                    warningMsg = $"Note: With the current distance/spacing, you only have {paletteEntropyBuckets} possible color palettes. This adds very little visual entropy.";
                }

                paletteEntropyBits = Math.Log2(paletteEntropyBuckets);
            }

            double entropyBits = totalBitsRequired;
            double colorEntropyBits = 0;
            int activeCount = 0;

            // Count active cells
            foreach (int cell in grid)
            {
                if (cell > 0) activeCount++;
            }

            // Add palette entropy (hue selection)
            if (parameters.ColorCount > 0)
            {
                entropyBits += paletteEntropyBits;
            }

            // Add color assignment entropy (for multiple colors)
            if (parameters.ColorCount > 1)
            {
                // To display the total mathematically expected search space of the identicon system, 
                // we treat the average generated active cells (50%) to be independent of the current hash output.
                double expectedActiveCells = totalBitsRequired / 2.0;
                colorEntropyBits = expectedActiveCells * Math.Log2(parameters.ColorCount);
                entropyBits += colorEntropyBits;
            }

            return new IdenticonResult
            {
                Grid = grid,
                Colors = colors,
                Resolution = parameters.Resolution,
                EntropyBits = entropyBits,
                PatternEntropyBits = totalBitsRequired,
                PaletteEntropyBits = paletteEntropyBits,
                ColorEntropyBits = colorEntropyBits,
                ActiveCellCount = activeCount,
                PaletteOptions = paletteEntropyBuckets,
                WarningMessage = warningMsg
            };
        }

        private byte[] ComputeHash(string input)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            }
        }

        private bool[] ExtractBitsFromHash(byte[] hash, int count)
        {
            bool[] bits = new bool[count];
            for (int i = 0; i < count; i++)
            {
                int byteIdx = (i / 8) % hash.Length;
                int bitIdx = i % 8;
                bits[i] = (hash[byteIdx] & (1 << bitIdx)) != 0;
            }
            return bits;
        }
    }

    public class IdenticonParameters
    {
        public int Resolution { get; set; } = 5;
        public bool IsSymmetric { get; set; } = true;
        public int ColorCount { get; set; } = 1;
        public int Saturation { get; set; } = 70;
        public int Lightness { get; set; } = 50;
        public int MinHueDistance { get; set; } = 45;
        public int HueSpacing { get; set; } = 0;

        public IdenticonParameters()
        {

        }

        public IdenticonParameters(int resolution, bool isSymmetric, int colorCount, int saturation, int lightness, int minHueDistance = 45, int hueSpacing = 0)
        {
            Resolution = resolution;
            IsSymmetric = isSymmetric;
            ColorCount = colorCount;
            Saturation = Math.Clamp(saturation, 0, 100);
            Lightness = Math.Clamp(lightness, 0, 100);
            MinHueDistance = Math.Clamp(minHueDistance, 1, 360);
            HueSpacing = Math.Clamp(hueSpacing, 0, 360);
        }
    }

    public class IdenticonResult
    {
        public int[] Grid { get; set; }
        public List<string> Colors { get; set; }
        public int Resolution { get; set; }
        public double EntropyBits { get; set; }
        public int PatternEntropyBits { get; set; }
        public double PaletteEntropyBits { get; set; }
        public double ColorEntropyBits { get; set; }
        public int ActiveCellCount { get; set; }
        public int PaletteOptions { get; set; }
        public string WarningMessage { get; set; }
    }
}