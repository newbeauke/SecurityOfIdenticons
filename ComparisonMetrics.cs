using System;
using System.Collections.Generic;
using System.Linq;

namespace SecurityOfIdenticons
{
    public class MetricResult
    {
        public double SimilarityPercentage { get; set; }
        public string PrimaryText { get; set; }
        public string SecondaryText { get; set; }
    }

    public abstract class ComparisonMetric
    {
        public abstract string Id { get; }
        public abstract string Name { get; }

        public abstract MetricResult Compare(IdenticonResult a, IdenticonResult b);

        protected double Combinations(int n, int k)
        {
            if (k < 0 || k > n) return 0;
            if (k == 0 || k == n) return 1;
            k = Math.Min(k, n - k);
            double c = 1;
            for (int i = 1; i <= k; i++)
                c = c * (n - i + 1) / i;
            return c;
        }

        protected string GetCellColor(IdenticonResult res, int idx)
        {
            int cIdx = res.Grid[idx];
            if (cIdx == 0) return "bg";
            if (res.Colors != null && res.Colors.Count > 0 && cIdx <= res.Colors.Count) return res.Colors[cIdx - 1];
            return "black";
        }
    }

    // Hamming Distance
    public class HammingDistanceMetric : ComparisonMetric
    {
        public override string Id => "Hamming";
        public override string Name => "Hamming Distance";

        public override MetricResult Compare(IdenticonResult a, IdenticonResult b)
        {
            int hammingDistance = 0;
            string bin1 = a.HashBinary;
            string bin2 = b.HashBinary;
            int maxBits = 0;
            if (!string.IsNullOrEmpty(bin1) && !string.IsNullOrEmpty(bin2))
            {
                int len = Math.Min(bin1.Length, bin2.Length);
                maxBits = Math.Max(bin1.Length, bin2.Length);
                for (int i = 0; i < len; i++)
                {
                    if (bin1[i] != bin2[i]) hammingDistance++;
                }
                hammingDistance += Math.Abs(bin1.Length - bin2.Length);
            }
            
            double sim = maxBits > 0 ? 1.0 - (hammingDistance / (double)maxBits) : 0;
            return new MetricResult
            {
                SimilarityPercentage = sim,
                PrimaryText = $"{hammingDistance} / {maxBits} bits",
                SecondaryText = $"{((1.0 - sim) * 100).ToString("F1")}% difference"
            };
        }
    }

    // Shape
    public class ShapeMetric : ComparisonMetric
    {
        public override string Id => "Shape";
        public override string Name => "Shape";

        public override MetricResult Compare(IdenticonResult a, IdenticonResult b)
        {
            int totalCells = a.Grid?.Length ?? 0;
            int shapeDiff = 0;
            if (a.Grid != null && b.Grid != null && a.Grid.Length == b.Grid.Length)
            {
                for (int i = 0; i < totalCells; i++)
                {
                    if ((a.Grid[i] > 0) != (b.Grid[i] > 0)) shapeDiff++;
                }
            }
            double sim = totalCells > 0 ? 1.0 - (shapeDiff / (double)totalCells) : 0;
            return new MetricResult
            {
                SimilarityPercentage = sim,
                PrimaryText = $"{shapeDiff} / {totalCells} cells",
                SecondaryText = $"{((1.0 - sim) * 100).ToString("F1")}% difference"
            };
        }
    }

    // ShapeAndColor
    public class ShapeAndColorMetric : ComparisonMetric
    {
        public override string Id => "ShapeAndColor";
        public override string Name => "Shape & Color";

        public override MetricResult Compare(IdenticonResult a, IdenticonResult b)
        {
            int totalCells = a.Grid?.Length ?? 0;
            int exactDiff = 0;
            if (a.Grid != null && b.Grid != null && a.Grid.Length == b.Grid.Length)
            {
                for (int i = 0; i < totalCells; i++)
                {
                    if (a.Grid[i] != b.Grid[i] || GetCellColor(a, i) != GetCellColor(b, i)) exactDiff++;
                }
            }
            double sim = totalCells > 0 ? 1.0 - (exactDiff / (double)totalCells) : 0;
            return new MetricResult
            {
                SimilarityPercentage = sim,
                PrimaryText = $"{exactDiff} / {totalCells} cells",
                SecondaryText = $"{((1.0 - sim) * 100).ToString("F1")}% difference"
            };
        }
    }

    // CogniconV1
    public class CogniconV1Metric : ComparisonMetric
    {
        public override string Id => "CogniconV1";
        public override string Name => "Cognicon V1";

        public override MetricResult Compare(IdenticonResult a, IdenticonResult b)
        {
            int totalCells = a.Grid?.Length ?? 0;
            double penalty = 0;

            if (a.Grid != null && b.Grid != null && a.Grid.Length == b.Grid.Length)
            {
                for (int i = 0; i < totalCells; i++)
                {
                    bool active1 = a.Grid[i] > 0;
                    bool active2 = b.Grid[i] > 0;

                    if (active1 != active2)
                    {
                        penalty += 1.0; // Shape mismatch: max penalty
                    }
                    else if (active1 && active2)
                    {
                        string c1 = GetCellColor(a, i);
                        string c2 = GetCellColor(b, i);
                        if (c1 != c2)
                        {
                            var hsl1 = ParseHsl(c1);
                            var hsl2 = ParseHsl(c2);
                            double dist = HslEuclideanDistance(hsl1, hsl2);
                            penalty += Math.Min(0.5, dist * 0.5); // Cap color difference penalty at 0.5
                        }
                    }
                }
            }

            double sim = totalCells > 0 ? Math.Max(0, 1.0 - (penalty / totalCells)) : 0;
            
            return new MetricResult
            {
                SimilarityPercentage = sim,
                PrimaryText = $"{penalty.ToString("F1")} / {totalCells} penalty score",
                SecondaryText = $"{((1.0 - sim) * 100).ToString("F1")}% difference"
            };
        }

        private (double h, double s, double l) ParseHsl(string hsl)
        {
            if (string.IsNullOrEmpty(hsl) || !hsl.StartsWith("hsl(") || !hsl.EndsWith(")")) return (0, 0, 0);
            var inner = hsl.Substring(4, hsl.Length - 5);
            var parts = inner.Split(',');
            if (parts.Length != 3) return (0, 0, 0);

            double.TryParse(parts[0], System.Globalization.CultureInfo.InvariantCulture, out double h);
            double.TryParse(parts[1].Replace("%", "").Trim(), System.Globalization.CultureInfo.InvariantCulture, out double s);
            double.TryParse(parts[2].Replace("%", "").Trim(), System.Globalization.CultureInfo.InvariantCulture, out double l);
            return (h, s, l);
        }

        private double HslEuclideanDistance((double h, double s, double l) c1, (double h, double s, double l) c2)
        {
            double dh = Math.Abs(c1.h - c2.h);
            if (dh > 180) dh = 360 - dh;

            // Scale hue difference logically (0-100 instead of 0-180 for weight balance)
            dh = (dh / 180.0) * 100.0;
            double ds = Math.Abs(c1.s - c2.s);
            double dl = Math.Abs(c1.l - c2.l);

            double dist = Math.Sqrt(dh * dh + ds * ds + dl * dl);
            double maxDist = 173.2; // Math.Sqrt(100^2 + 100^2 + 100^2)
            return dist / maxDist;
        }
    }

    public static class MetricRegistry
    {
        public static readonly List<ComparisonMetric> AllMetrics = new List<ComparisonMetric>
        {
            // new HammingDistanceMetric(),
            new ShapeMetric(),
            new ShapeAndColorMetric(),
            new CogniconV1Metric()
        };

        public static ComparisonMetric Get(string id)
        {
            if (id == "Hamming") return new HammingDistanceMetric();
            return AllMetrics.FirstOrDefault(m => m.Id == id) ?? AllMetrics.First();
        }
    }
}
