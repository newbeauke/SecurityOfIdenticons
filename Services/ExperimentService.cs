using System;
using MongoDB.Driver;
using SecurityOfIdenticons.Models;
using System.Collections.Generic;
using System.Linq;

namespace SecurityOfIdenticons.Services
{
    public class ExperimentService
    {
        private readonly IMongoCollection<ExperimentResult> _resultsCollection;

        public ExperimentService()
        {
            var mongoClient = new MongoClient("mongodb://localhost:27017"); // Assuming default local connection
            var database = mongoClient.GetDatabase("SecurityOfIdenticons");
            _resultsCollection = database.GetCollection<ExperimentResult>("ExperimentResults");
        }

        public ExperimentTrial GenerateTrial(double targetThreshold, IdenticonParameters parameters)
        {
            var generator = new IdenticonGenerator(parameters);
            byte[] targetHash = generator.ComputeHash(Guid.NewGuid().ToString());
            var target = generator.GenerateFromHash(targetHash, "Target");

            var random = new Random();
            var cogniconV1 = new CogniconV1Metric();

            // Initialize lineup list
            var lineup = new List<IdenticonResult>();

            // Generate required extra distractors using bit mutation
            // We want 4 distractors (or 5 if target isn't included)
            bool targetInLineup = random.NextDouble() > 0.5;
            int numDistractors = targetInLineup ? 4 : 5;

            double maxDistractorSimilarity = 0;

            for (int i = 0; i < numDistractors; i++)
            {
                IdenticonResult bestCandidateForThisSlot = null;
                double closestDiffToThreshold = double.MaxValue;
                double actualSimForThisCandidate = 0;

                // Cascading thresholds: Primary fake is exact target. The rest trail behind tightly.
                // e.g. T, T-0.03, T-0.06, T-0.09
                double currentSlotTargetThreshold = targetThreshold - (i * 0.03);
                if (currentSlotTargetThreshold < 0) currentSlotTargetThreshold = 0;

                // Mutate the target hash to find a nearby candidate
                for (int attempt = 0; attempt < 500; attempt++)
                {
                    byte[] mutatedHash = (byte[])targetHash.Clone();

                    // The more we want to mutate, the more bits we flip. 
                    int bitsToFlip = random.Next(1, 8); 

                    for (int f = 0; f < bitsToFlip; f++)
                    {
                        // Target the bits that actually matter for shape (0-127) and color mapping (192-255)
                        // to avoid the unused bit loophole.
                        int bitIndex = (random.Next(2) == 0) ? random.Next(0, 128) : random.Next(192, 256);
                        int byteIndex = bitIndex / 8;
                        int bitInByte = 7 - (bitIndex % 8);
                        mutatedHash[byteIndex] ^= (byte)(1 << bitInByte);
                    }

                    var candidate = generator.GenerateFromHash(mutatedHash, $"Candidate {i}-{attempt}");
                    double sim = cogniconV1.Compare(target, candidate).SimilarityPercentage;

                    // BUG FIX: Reject 100% copies. A distractor must not be visually identical to the target.
                    if (sim >= 0.999) continue;

                    double diff = Math.Abs(sim - currentSlotTargetThreshold);

                    if (diff < closestDiffToThreshold)
                    {
                        closestDiffToThreshold = diff;
                        bestCandidateForThisSlot = candidate;
                        actualSimForThisCandidate = sim;
                    }

                    // Early breakout if we nail the threshold
                    if (diff < 0.01) break;
                }

                if (bestCandidateForThisSlot == null)
                {
                    // Fallback in case of extreme bad luck generating valid mutations
                    bestCandidateForThisSlot = generator.Generate(Guid.NewGuid().ToString());
                    actualSimForThisCandidate = cogniconV1.Compare(target, bestCandidateForThisSlot).SimilarityPercentage;
                }

                lineup.Add(bestCandidateForThisSlot);

                // Track the maximum similarity among all distractors in the lineup
                if (actualSimForThisCandidate > maxDistractorSimilarity)
                {
                    maxDistractorSimilarity = actualSimForThisCandidate;
                }
            }

            if (targetInLineup)
            {
                lineup.Add(target);
            }

            // Shuffle lineup
            var shuffledLineup = lineup.OrderBy(x => random.Next()).ToList();
            int correctIndex = targetInLineup ? shuffledLineup.IndexOf(target) : -1;

            return new ExperimentTrial
            {
                CurrentThreshold = maxDistractorSimilarity, // Use true maximum similarity among distractors (closest distractor)
                Target = target,
                Lineup = shuffledLineup,
                CorrectLineupIndex = correctIndex
            };
        }

        public void RecordResult(ExperimentResult result)
        {
            _resultsCollection.InsertOne(result);
        }

        public IEnumerable<ExperimentResult> GetResults()
        {
            return _resultsCollection.Find(_ => true).ToList();
        }

        public void ClearResults()
        {
            _resultsCollection.DeleteMany(_ => true);
        }
    }
}
