using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SecurityOfIdenticons.Models
{
    public class ExperimentTrial
    {
        public Guid TrialId { get; set; } = Guid.NewGuid();
        public double CurrentThreshold { get; set; }
        public IdenticonResult Target { get; set; }
        public List<IdenticonResult> Lineup { get; set; }
        public int CorrectLineupIndex { get; set; } // -1 if "None"
    }

    public class ExperimentResult
    {
        [BsonId]
        [BsonRepresentation(BsonType.String)]
        public Guid ResultId { get; set; } = Guid.NewGuid();

        [BsonRepresentation(BsonType.String)]
        public Guid TrialId { get; set; }

        public double Threshold { get; set; }
        public bool GuessedCorrectly { get; set; }
        public int GuessedIndex { get; set; }
        public int CorrectIndex { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
