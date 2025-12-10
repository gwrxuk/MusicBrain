using ListeningBrain.Alignment;
using ListeningBrain.Core.Models;

namespace ListeningBrain.Intelligence;

/// <summary>
/// ML-enhanced alignment using a neural-network-inspired scoring system.
/// Learns from previous alignments to improve matching accuracy.
/// </summary>
public class NeuralAligner : IAlignmentStrategy
{
    public string Name => "Neural-Enhanced Alignment";
    
    private readonly HybridAligner _baseAligner = new();
    
    /// <summary>
    /// Feature weights learned from previous alignments.
    /// </summary>
    private readonly FeatureWeights _weights;
    
    /// <summary>
    /// Training history for online learning.
    /// </summary>
    private readonly List<AlignmentExample> _trainingHistory = [];
    
    /// <summary>
    /// Learning rate for weight updates.
    /// </summary>
    public double LearningRate { get; init; } = 0.1;
    
    /// <summary>
    /// Maximum training examples to retain.
    /// </summary>
    public int MaxTrainingExamples { get; init; } = 1000;
    
    public NeuralAligner()
    {
        _weights = FeatureWeights.Default;
    }
    
    public NeuralAligner(FeatureWeights weights)
    {
        _weights = weights;
    }
    
    public AlignmentResult Align(Score score, Performance performance, AlignmentOptions? options = null)
    {
        options ??= AlignmentOptions.Default;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // Get base alignment
        var baseResult = _baseAligner.Align(score, performance, options);
        
        // Enhance with neural scoring
        var enhancedPairs = EnhanceAlignment(baseResult.Pairs, score, performance);
        
        // Re-evaluate missed and extra notes with learned features
        var (refinedMissed, refinedExtra) = RefineUnmatched(
            baseResult.MissedNotes, baseResult.ExtraNotes, score, performance);
        
        // Calculate refined confidence scores
        var confidencePairs = enhancedPairs
            .Select(p => p with { Confidence = CalculateNeuralConfidence(p) })
            .ToList();
        
        stopwatch.Stop();
        
        return new AlignmentResult
        {
            Pairs = confidencePairs,
            MissedNotes = refinedMissed,
            ExtraNotes = refinedExtra,
            TotalCost = baseResult.TotalCost,
            NormalizedScore = CalculateNeuralScore(confidencePairs, refinedMissed.Count, refinedExtra.Count),
            WarpingPath = baseResult.WarpingPath,
            EstimatedTempoRatio = baseResult.EstimatedTempoRatio,
            TimeOffsetMs = baseResult.TimeOffsetMs,
            AlgorithmUsed = Name,
            ComputeTime = stopwatch.Elapsed
        };
    }
    
    /// <summary>
    /// Trains the aligner with a known-correct alignment.
    /// </summary>
    public void Train(Score score, Performance performance, AlignmentResult correctAlignment)
    {
        // Extract features from correct alignment
        foreach (var pair in correctAlignment.Pairs)
        {
            var features = ExtractFeatures(pair, score, performance);
            var example = new AlignmentExample
            {
                Features = features,
                WasCorrectMatch = true,
                Timestamp = DateTime.UtcNow
            };
            
            _trainingHistory.Add(example);
        }
        
        // Add negative examples from missed notes
        foreach (var missed in correctAlignment.MissedNotes)
        {
            var nearbyPerf = performance.Notes
                .Where(n => Math.Abs(n.StartTimeMs - missed.ExpectedNote.StartTimeMs) < 500)
                .Take(3);
            
            foreach (var perfNote in nearbyPerf)
            {
                var fakePair = new AlignedNotePair
                {
                    ScoreNote = missed.ExpectedNote,
                    PerformanceNote = perfNote,
                    Confidence = 0,
                    TimingDeviationMs = perfNote.StartTimeMs - missed.ExpectedNote.StartTimeMs,
                    TimingDeviationBeats = 0
                };
                
                var features = ExtractFeatures(fakePair, score, performance);
                _trainingHistory.Add(new AlignmentExample
                {
                    Features = features,
                    WasCorrectMatch = false,
                    Timestamp = DateTime.UtcNow
                });
            }
        }
        
        // Trim history if needed
        while (_trainingHistory.Count > MaxTrainingExamples)
        {
            _trainingHistory.RemoveAt(0);
        }
        
        // Update weights
        UpdateWeights();
    }
    
    /// <summary>
    /// Gets current feature weights.
    /// </summary>
    public FeatureWeights GetWeights() => _weights;
    
    private List<AlignedNotePair> EnhanceAlignment(
        IReadOnlyList<AlignedNotePair> basePairs,
        Score score,
        Performance performance)
    {
        var enhanced = new List<AlignedNotePair>();
        
        foreach (var pair in basePairs)
        {
            // Calculate neural feature score
            var features = ExtractFeatures(pair, score, performance);
            double neuralScore = CalculateFeatureScore(features);
            
            // Potentially find better matches using learned features
            if (neuralScore < 0.5 && !pair.IsExactPitchMatch)
            {
                var betterMatch = FindBetterMatch(pair.ScoreNote, performance, pair);
                if (betterMatch != null)
                {
                    enhanced.Add(betterMatch);
                    continue;
                }
            }
            
            enhanced.Add(pair);
        }
        
        return enhanced;
    }
    
    private AlignedNotePair? FindBetterMatch(
        ScoreNote scoreNote,
        Performance performance,
        AlignedNotePair currentMatch)
    {
        var candidates = performance.Notes
            .Where(n => Math.Abs(n.StartTimeMs - scoreNote.StartTimeMs) < 300)
            .Where(n => n.Id != currentMatch.PerformanceNote.Id);
        
        AlignedNotePair? bestMatch = null;
        double bestScore = CalculateFeatureScore(ExtractFeatures(currentMatch, null!, performance));
        
        foreach (var candidate in candidates)
        {
            var testPair = new AlignedNotePair
            {
                ScoreNote = scoreNote,
                PerformanceNote = candidate,
                Confidence = 0,
                TimingDeviationMs = candidate.StartTimeMs - scoreNote.StartTimeMs,
                TimingDeviationBeats = 0
            };
            
            var features = ExtractFeatures(testPair, null!, performance);
            double score = CalculateFeatureScore(features);
            
            if (score > bestScore)
            {
                bestScore = score;
                bestMatch = testPair;
            }
        }
        
        return bestScore > 0.7 ? bestMatch : null;
    }
    
    private (List<MissedNote>, List<PerformanceNote>) RefineUnmatched(
        IReadOnlyList<MissedNote> missed,
        IReadOnlyList<PerformanceNote> extra,
        Score score,
        Performance performance)
    {
        var refinedMissed = new List<MissedNote>(missed);
        var refinedExtra = new List<PerformanceNote>(extra);
        
        // Try to match extras with missed using neural scoring
        var toRemoveMissed = new List<MissedNote>();
        var toRemoveExtra = new List<PerformanceNote>();
        
        foreach (var missedNote in missed)
        {
            var potentialMatch = extra
                .Where(e => !toRemoveExtra.Contains(e))
                .Where(e => Math.Abs(e.StartTimeMs - missedNote.ExpectedNote.StartTimeMs) < 200)
                .OrderBy(e => Math.Abs(e.Pitch - missedNote.ExpectedNote.Pitch))
                .FirstOrDefault();
            
            if (potentialMatch != null)
            {
                var testPair = new AlignedNotePair
                {
                    ScoreNote = missedNote.ExpectedNote,
                    PerformanceNote = potentialMatch,
                    Confidence = 0,
                    TimingDeviationMs = potentialMatch.StartTimeMs - missedNote.ExpectedNote.StartTimeMs,
                    TimingDeviationBeats = 0
                };
                
                var features = ExtractFeatures(testPair, score, performance);
                double score_val = CalculateFeatureScore(features);
                
                if (score_val > 0.6)
                {
                    toRemoveMissed.Add(missedNote);
                    toRemoveExtra.Add(potentialMatch);
                }
            }
        }
        
        refinedMissed.RemoveAll(m => toRemoveMissed.Contains(m));
        refinedExtra.RemoveAll(e => toRemoveExtra.Contains(e));
        
        return (refinedMissed, refinedExtra);
    }
    
    private AlignmentFeatures ExtractFeatures(
        AlignedNotePair pair,
        Score? score,
        Performance performance)
    {
        // Extract features for neural scoring
        return new AlignmentFeatures
        {
            PitchMatch = pair.IsExactPitchMatch ? 1.0 : 0.0,
            PitchClassMatch = pair.ScoreNote.PitchClass == pair.PerformanceNote.PitchClass ? 1.0 : 0.0,
            OctaveError = pair.IsOctaveError ? 1.0 : 0.0,
            SemitoneDistance = Math.Min(1, Math.Abs(pair.PitchDifference) / 12.0),
            TimingError = Math.Min(1, Math.Abs(pair.TimingDeviationMs) / 500.0),
            VelocityDifference = Math.Min(1, Math.Abs(pair.VelocityDifference) / 64.0),
            DurationRatio = pair.ScoreNote.DurationMs > 0 
                ? Math.Min(2, pair.PerformanceNote.DurationMs / pair.ScoreNote.DurationMs) 
                : 1.0,
            IsGraceNote = pair.ScoreNote.IsGraceNote ? 1.0 : 0.0,
            IsTuplet = pair.ScoreNote.IsTuplet ? 1.0 : 0.0,
            LocalDensity = CalculateLocalDensity(pair, performance)
        };
    }
    
    private double CalculateLocalDensity(AlignedNotePair pair, Performance performance)
    {
        // Count nearby notes (measure of polyphonic complexity)
        int nearbyNotes = performance.Notes.Count(n => 
            Math.Abs(n.StartTimeMs - pair.PerformanceNote.StartTimeMs) < 100);
        return Math.Min(1, nearbyNotes / 5.0);
    }
    
    private double CalculateFeatureScore(AlignmentFeatures features)
    {
        double score = 0;
        
        score += features.PitchMatch * _weights.PitchMatch;
        score += features.PitchClassMatch * _weights.PitchClassMatch;
        score += features.OctaveError * _weights.OctaveError;
        score += (1 - features.SemitoneDistance) * _weights.SemitoneDistance;
        score += (1 - features.TimingError) * _weights.TimingError;
        score += (1 - features.VelocityDifference) * _weights.VelocityDifference;
        score += (1 - Math.Abs(features.DurationRatio - 1)) * _weights.DurationRatio;
        score += features.IsGraceNote * _weights.GraceNoteBonus;
        score += features.LocalDensity * _weights.DensityAdjustment;
        
        return Math.Max(0, Math.Min(1, score));
    }
    
    private double CalculateNeuralConfidence(AlignedNotePair pair)
    {
        // Base confidence from pitch match
        double confidence = pair.IsExactPitchMatch ? 0.8 : pair.IsOctaveError ? 0.5 : 0.2;
        
        // Adjust for timing
        double timingFactor = Math.Max(0, 1 - Math.Abs(pair.TimingDeviationMs) / 200);
        confidence *= (0.5 + timingFactor * 0.5);
        
        return confidence;
    }
    
    private double CalculateNeuralScore(
        List<AlignedNotePair> pairs,
        int missedCount,
        int extraCount)
    {
        if (pairs.Count + missedCount == 0) return 0;
        
        double matchScore = pairs.Sum(p => p.Confidence);
        double total = pairs.Count + missedCount;
        
        return matchScore / total;
    }
    
    private void UpdateWeights()
    {
        if (_trainingHistory.Count < 10) return;
        
        // Simple gradient descent on feature weights
        var positive = _trainingHistory.Where(e => e.WasCorrectMatch).ToList();
        var negative = _trainingHistory.Where(e => !e.WasCorrectMatch).ToList();
        
        if (positive.Count == 0 || negative.Count == 0) return;
        
        // Calculate average features for positive and negative examples
        var avgPositive = AverageFeatures(positive.Select(e => e.Features));
        var avgNegative = AverageFeatures(negative.Select(e => e.Features));
        
        // Update weights to maximize separation
        // (Simplified - real implementation would use proper gradient descent)
        _weights.PitchMatch += LearningRate * (avgPositive.PitchMatch - avgNegative.PitchMatch);
        _weights.TimingError += LearningRate * (avgNegative.TimingError - avgPositive.TimingError);
        
        // Normalize weights
        NormalizeWeights();
    }
    
    private AlignmentFeatures AverageFeatures(IEnumerable<AlignmentFeatures> features)
    {
        var list = features.ToList();
        if (list.Count == 0) return new AlignmentFeatures();
        
        return new AlignmentFeatures
        {
            PitchMatch = list.Average(f => f.PitchMatch),
            PitchClassMatch = list.Average(f => f.PitchClassMatch),
            OctaveError = list.Average(f => f.OctaveError),
            SemitoneDistance = list.Average(f => f.SemitoneDistance),
            TimingError = list.Average(f => f.TimingError),
            VelocityDifference = list.Average(f => f.VelocityDifference),
            DurationRatio = list.Average(f => f.DurationRatio),
            IsGraceNote = list.Average(f => f.IsGraceNote),
            IsTuplet = list.Average(f => f.IsTuplet),
            LocalDensity = list.Average(f => f.LocalDensity)
        };
    }
    
    private void NormalizeWeights()
    {
        double sum = Math.Abs(_weights.PitchMatch) + Math.Abs(_weights.PitchClassMatch) +
                     Math.Abs(_weights.TimingError) + Math.Abs(_weights.VelocityDifference);
        
        if (sum > 0)
        {
            double scale = 1.0 / sum;
            _weights.PitchMatch *= scale;
            _weights.PitchClassMatch *= scale;
            _weights.TimingError *= scale;
            _weights.VelocityDifference *= scale;
        }
    }
}

/// <summary>
/// Features extracted from an alignment pair for neural scoring.
/// </summary>
public record AlignmentFeatures
{
    public double PitchMatch { get; init; }
    public double PitchClassMatch { get; init; }
    public double OctaveError { get; init; }
    public double SemitoneDistance { get; init; }
    public double TimingError { get; init; }
    public double VelocityDifference { get; init; }
    public double DurationRatio { get; init; }
    public double IsGraceNote { get; init; }
    public double IsTuplet { get; init; }
    public double LocalDensity { get; init; }
}

/// <summary>
/// Learned feature weights for neural alignment.
/// </summary>
public class FeatureWeights
{
    public double PitchMatch { get; set; } = 0.35;
    public double PitchClassMatch { get; set; } = 0.15;
    public double OctaveError { get; set; } = 0.10;
    public double SemitoneDistance { get; set; } = 0.10;
    public double TimingError { get; set; } = 0.15;
    public double VelocityDifference { get; set; } = 0.05;
    public double DurationRatio { get; set; } = 0.05;
    public double GraceNoteBonus { get; set; } = 0.03;
    public double DensityAdjustment { get; set; } = 0.02;
    
    public static FeatureWeights Default => new();
}

/// <summary>
/// A training example for the neural aligner.
/// </summary>
internal record AlignmentExample
{
    public AlignmentFeatures Features { get; init; } = new();
    public bool WasCorrectMatch { get; init; }
    public DateTime Timestamp { get; init; }
}

