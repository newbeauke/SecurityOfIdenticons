using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace SecurityOfIdenticons
{
    public enum SecurityLevel
    {
        None,
        OneDParity,
        TwoDParity,
        ExtendedHamming
    }

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
            return GenerateFromHash(hash, input);
        }

        public IdenticonResult GenerateFromHash(byte[] hash, string identifier)
        {
            var bitRoles = new List<HashBitRole>();

            int columnsToGenerate = parameters.IsSymmetric ? (int)Math.Ceiling(parameters.Resolution / 2.0) : parameters.Resolution;
            int totalBitsRequired = parameters.Resolution * columnsToGenerate;

            // Bytes 0-15 (128 bits): Reserved for Shape
            bitRoles.Add(new HashBitRole { StartBit = 0, BitLength = Math.Min(totalBitsRequired, 128), RoleName = "Shape", ColorHex = "#3b82f6" });

            bool[] bits = ExtractBitsFromHash(hash, totalBitsRequired);

            int shapeConstraintBits = 0;
            if (parameters.SafeMode == SecurityLevel.OneDParity) shapeConstraintBits = 1;
            else if (parameters.SafeMode == SecurityLevel.TwoDParity) shapeConstraintBits = parameters.Resolution + columnsToGenerate - 1;
            else if (parameters.SafeMode == SecurityLevel.ExtendedHamming) shapeConstraintBits = 5;
            
            ApplyShapeConstraints(bits, parameters.Resolution, columnsToGenerate, parameters.SafeMode);

            int[] grid = new int[parameters.Resolution * parameters.Resolution];

            // Bytes 24-31 (64 bits): Reserved for Color Mapping
            if (parameters.ColorCount > 1)
            {
                bitRoles.Add(new HashBitRole { StartBit = 192, BitLength = 64, RoleName = "Color Mapping", ColorHex = "#f59e0b" });
            }

            List<int> activeIndependentIndices = new List<int>();
            for (int row = 0; row < parameters.Resolution; row++)
            {
                for (int col = 0; col < columnsToGenerate; col++)
                {
                    int index = row * columnsToGenerate + col;
                    if (bits[index % bits.Length]) {
                        activeIndependentIndices.Add(index);
                    }
                }
            }

            Dictionary<int, int> independentColorMapping = new Dictionary<int, int>();

            if (parameters.ColorCount > 1 && activeIndependentIndices.Count > 0)
            {
                int sumColorIndices = 0;
                for (int i = 0; i < activeIndependentIndices.Count; i++)
                {
                    int index = activeIndependentIndices[i];
                    int hashByte = hash[24 + (index % 8)];
                    int colorVal = hashByte % parameters.ColorCount;
                    
                    sumColorIndices += colorVal;
                    independentColorMapping[index] = colorVal + 1; // 1-based index
                }
            }

            for (int row = 0; row < parameters.Resolution; row++)
            {
                for (int col = 0; col < columnsToGenerate; col++)
                {
                    int index = row * columnsToGenerate + col;
                    bool isActive = bits[index % bits.Length];

                    int colorIndex = 0;
                    if (isActive)
                    {
                        if (parameters.ColorCount <= 1)
                        {
                            colorIndex = 1;
                        }
                        else
                        {
                            colorIndex = independentColorMapping[index];
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
                // Bytes 16-23 (64 bits): Reserved for Palette
                bitRoles.Add(new HashBitRole { StartBit = 128, BitLength = parameters.ColorCount >= 3 ? 64 : 32, RoleName = "Palette", ColorHex = "#10b981" });

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

                uint hashVal = BitConverter.ToUInt32(hash, 16); // Extract 4 bytes from byte 16

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

                        // Calculate the true count of possible permutations for accurately reporting entropy (O(N^2) max 129.600 simple iterations, very fast)
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
                            // Utilize the next bytes (bytes 20-23) of the isolated palette block
                            int c3 = validC3[(int)(BitConverter.ToUInt32(hash, 20) % validC3.Count)];
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
                    if (colors.Count == 0) // Fallback if nothing was valid
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

            int patternDataBits = totalBitsRequired - shapeConstraintBits;
            double entropyBits = patternDataBits;
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
                double expectedActiveCells = totalBitsRequired / 2.0;
                double rawExpectedColorEntropy = expectedActiveCells * Math.Log2(parameters.ColorCount);
                double expectedColorConstraint = 0;
                
                colorEntropyBits = rawExpectedColorEntropy - expectedColorConstraint;
                entropyBits += colorEntropyBits;
            }

            return new IdenticonResult
            {
                Identifier = identifier,
                HashHex = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant(), 
                HashBinary = string.Join("", hash.Select(b => Convert.ToString(b, 2).PadLeft(8, '0'))),
                Grid = grid,
                Colors = colors,
                Resolution = parameters.Resolution,
                EntropyBits = entropyBits,
                PatternEntropyBits = patternDataBits,
                PaletteEntropyBits = paletteEntropyBits,
                ColorEntropyBits = colorEntropyBits,
                ActiveCellCount = activeCount,
                PaletteOptions = paletteEntropyBuckets,
                WarningMessage = warningMsg,
                BitRoles = bitRoles,
                ShapeConstraintBits = shapeConstraintBits,
                RawShapeEntropyBits = totalBitsRequired,
                ColorConstraintBits = 0,
                RawColorEntropyBits = (parameters.ColorCount > 1) ? (totalBitsRequired / 2.0) * Math.Log2(parameters.ColorCount) : 0
            };
        }

        public byte[] ComputeHash(string input)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            }
        }

        private void ApplyShapeConstraints(bool[] bits, int rows, int cols, SecurityLevel level)
        {
            if (level == SecurityLevel.OneDParity)
            {
                if (bits.Length < 2) return;
                bool parity = false;
                for (int i = 0; i < bits.Length - 1; i++) parity ^= bits[i];
                bits[bits.Length - 1] = parity;
            }
            else if (level == SecurityLevel.TwoDParity)
            {
                if (rows < 2 || cols < 2 || bits.Length < rows * cols) return;
                
                bool[,] matrix = new bool[rows, cols];
                for(int r = 0; r < rows; r++) {
                    for(int c = 0; c < cols; c++) {
                        matrix[r, c] = bits[r * cols + c];
                    }
                }

                for (int r = 0; r < rows - 1; r++) {
                    bool parity = false;
                    for (int c = 0; c < cols - 1; c++) parity ^= matrix[r, c];
                    matrix[r, cols - 1] = parity;
                }

                for (int c = 0; c < cols; c++) {
                    bool parity = false;
                    for (int r = 0; r < rows - 1; r++) parity ^= matrix[r, c];
                    matrix[rows - 1, c] = parity;
                }

                for(int r = 0; r < rows; r++) {
                    for(int c = 0; c < cols; c++) {
                        bits[r * cols + c] = matrix[r, c];
                    }
                }
            }
            else if (level == SecurityLevel.ExtendedHamming)
            {
                if (bits.Length < 15) return;
                
                // We use cells 1-15 (0-indexed as 0-14 in code).
                // Parity bits: 1 (idx 0), 2 (idx 1), 4 (idx 3), 8 (idx 7), and 15 (idx 14).
                // Data bits: 3, 5, 6, 7, 9, 10, 11, 12, 13, 14.
                
                // Read the first 10 bits of the hash into our data cells
                bool c3 = bits[0];
                bool c5 = bits[1];
                bool c6 = bits[2];
                bool c7 = bits[3];
                bool c9 = bits[4];
                bool c10 = bits[5];
                bool c11 = bits[6];
                bool c12 = bits[7];
                bool c13 = bits[8];
                bool c14 = bits[9];
                
                // Calculate standard Hamming Parity cells (positions 1, 2, 4, 8)
                bool c1 = c3 ^ c5 ^ c7 ^ c9 ^ c11 ^ c13;
                bool c2 = c3 ^ c6 ^ c7 ^ c10 ^ c11 ^ c14;
                bool c4 = c5 ^ c6 ^ c7 ^ c12 ^ c13 ^ c14;
                bool c8 = c9 ^ c10 ^ c11 ^ c12 ^ c13 ^ c14;

                // Calculate Overall Parity cell (Cell 15 / index 14) to enforce even parity across all 15 cells
                bool c15 = c1 ^ c2 ^ c3 ^ c4 ^ c5 ^ c6 ^ c7 ^ c8 ^ c9 ^ c10 ^ c11 ^ c12 ^ c13 ^ c14;

                // Write everything back into the 15-bit array
                bits[0] = c1;
                bits[1] = c2;
                bits[2] = c3;
                bits[3] = c4;
                bits[4] = c5;
                bits[5] = c6;
                bits[6] = c7;
                bits[7] = c8;
                bits[8] = c9;
                bits[9] = c10;
                bits[10] = c11;
                bits[11] = c12;
                bits[12] = c13;
                bits[13] = c14;
                bits[14] = c15;
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
        public SecurityLevel SafeMode { get; set; } = SecurityLevel.None;

        public IdenticonParameters()
        {

        }

        public IdenticonParameters(int resolution, bool isSymmetric, int colorCount, int saturation, int lightness, int minHueDistance = 45, int hueSpacing = 0, SecurityLevel safeMode = SecurityLevel.None)
        {
            Resolution = resolution;
            IsSymmetric = isSymmetric;
            ColorCount = colorCount;
            Saturation = Math.Clamp(saturation, 0, 100);
            Lightness = Math.Clamp(lightness, 0, 100);
            MinHueDistance = Math.Clamp(minHueDistance, 1, 360);
            HueSpacing = Math.Clamp(hueSpacing, 0, 360);
            SafeMode = safeMode;
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
        public string HashHex { get; set; }
        public string HashBinary { get; set; }
        public string Identifier { get; set; }
        public List<HashBitRole> BitRoles { get; set; } = new List<HashBitRole>();
        public int ShapeConstraintBits { get; set; }
        public int RawShapeEntropyBits { get; set; }
        public double ColorConstraintBits { get; set; }
        public double RawColorEntropyBits { get; set; }
    }

    public class HashBitRole
    {
        public int StartBit { get; set; }
        public int BitLength { get; set; }
        public string RoleName { get; set; }
        public string ColorHex { get; set; }
    }
}