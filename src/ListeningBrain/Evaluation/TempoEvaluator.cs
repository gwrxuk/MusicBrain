using ListeningBrain.Alignment;
using ListeningBrain.Core.Models;

namespace ListeningBrain.Evaluation;

/// <summary>
/// Evaluates tempo stability: consistent tempo throughout the piece.
/// Detects rushing, dragging, and tempo inconsistency.
/// </summary>
public class TempoEvaluator : IEvaluator<TempoResult>
{
    public string Name => "Tempo Evaluator";
    
    /// <summary>
    /// Minimum notes needed to estimate tempo for a segment.
    /// </summary>
    public int MinNotesPerSegment { get; init; } = 4;
    
    /// <summary>
    /// Measures per tempo segment.
    /// </summary>
    public int MeasuresPerSegment { get; init; } = 4;
    
    public TempoResult Evaluate(AlignmentResult alignment, Score score, Performance performance)
    {
        var issues = new List<EvaluationIssue>();
        
        if (alignment.Pairs.Count < 2)
        {
            return CreateEmptyResult(score);
        }
        
        // Get expected tempo from score
        double expectedBPM = score.TempoMarkings.First().BPM;
        
        // Calculate inter-onset intervals (IOI) for tempo estimation
        var ioiData = CalculateIOI(alignment.Pairs, score);
        
        // Estimate average performed tempo
        double detectedBPM = EstimateAverageTempo(ioiData, expectedBPM);
        
        // Calculate tempo deviation
        double tempoDeviation = (detectedBPM - expectedBPM) / expectedBPM;
        
        // Analyze tempo segments (measure by measure or grouped)
        var segments = AnalyzeTempoSegments(alignment.Pairs, score, expectedBPM);
        
        // Calculate stability (how consistent is the tempo)
        double stability = CalculateTempoStability(segments);
        
        // Detect tempo drift (accelerando/ritardando)
        var driftAnalysis = DetectTempoDrift(segments);
        
        // Generate issues
        if (Math.Abs(tempoDeviation) > 0.15) // More than 15% off
        {
            issues.Add(CreateTempoDeviationIssue(tempoDeviation, expectedBPM, detectedBPM));
        }
        
        if (stability < 0.85)
        {
            issues.Add(CreateStabilityIssue(stability));
        }
        
        issues.AddRange(driftAnalysis.Issues);
        
        // Calculate overall score
        double tempoScore = CalculateScore(tempoDeviation, stability, driftAnalysis);
        
        return new TempoResult
        {
            Score = tempoScore,
            Summary = GenerateSummary(tempoScore, expectedBPM, detectedBPM, stability),
            Issues = issues.OrderByDescending(i => i.Severity).ToList(),
            ExpectedBPM = expectedBPM,
            DetectedBPM = detectedBPM,
            TempoDeviation = tempoDeviation,
            TempoStability = stability,
            Segments = segments,
            DriftTrend = driftAnalysis.OverallTrend,
            IOIData = ioiData
        };
    }
    
    /// <summary>
    /// Calculates Inter-Onset Intervals between consecutive notes.
    /// </summary>
    private List<IOIDataPoint> CalculateIOI(IReadOnlyList<AlignedNotePair> pairs, Score score)
    {
        var data = new List<IOIDataPoint>();
        
        var sortedPairs = pairs.OrderBy(p => p.ScoreNote.StartTick).ToList();
        
        for (int i = 1; i < sortedPairs.Count; i++)
        {
            var prev = sortedPairs[i - 1];
            var curr = sortedPairs[i];
            
            // Expected IOI from score
            double expectedIOI = curr.ScoreNote.StartTimeMs - prev.ScoreNote.StartTimeMs;
            
            // Actual IOI from performance
            double actualIOI = curr.PerformanceNote.StartTimeMs - prev.PerformanceNote.StartTimeMs;
            
            if (expectedIOI > 10) // Avoid division by tiny intervals
            {
                double ratio = actualIOI / expectedIOI;
                
                data.Add(new IOIDataPoint
                {
                    Measure = curr.ScoreNote.Measure,
                    Beat = curr.ScoreNote.Beat,
                    ExpectedIOI = expectedIOI,
                    ActualIOI = actualIOI,
                    Ratio = ratio,
                    LocalBPM = ratio > 0 
                        ? score.TempoMarkings.First().BPM / ratio 
                        : score.TempoMarkings.First().BPM
                });
            }
        }
        
        return data;
    }
    
    /// <summary>
    /// Estimates the average performed tempo from IOI data.
    /// </summary>
    private double EstimateAverageTempo(List<IOIDataPoint> ioiData, double expectedBPM)
    {
        if (ioiData.Count == 0) return expectedBPM;
        
        // Use median to be robust to outliers
        var sortedRatios = ioiData.Select(d => d.Ratio).OrderBy(r => r).ToList();
        double medianRatio = sortedRatios[sortedRatios.Count / 2];
        
        return expectedBPM / medianRatio;
    }
    
    /// <summary>
    /// Analyzes tempo in segments (groups of measures).
    /// </summary>
    private List<TempoSegment> AnalyzeTempoSegments(
        IReadOnlyList<AlignedNotePair> pairs, 
        Score score,
        double expectedBPM)
    {
        var segments = new List<TempoSegment>();
        
        // Group pairs by measure
        var byMeasure = pairs
            .GroupBy(p => p.ScoreNote.Measure)
            .OrderBy(g => g.Key)
            .ToList();
        
        if (byMeasure.Count == 0) return segments;
        
        // Create segments of MeasuresPerSegment measures each
        int segmentStart = byMeasure.First().Key;
        var currentSegmentPairs = new List<AlignedNotePair>();
        
        foreach (var measureGroup in byMeasure)
        {
            currentSegmentPairs.AddRange(measureGroup);
            
            if (measureGroup.Key >= segmentStart + MeasuresPerSegment - 1 || 
                measureGroup == byMeasure.Last())
            {
                if (currentSegmentPairs.Count >= MinNotesPerSegment)
                {
                    var segment = CreateSegment(currentSegmentPairs, segmentStart, measureGroup.Key, score, expectedBPM);
                    segments.Add(segment);
                }
                
                segmentStart = measureGroup.Key + 1;
                currentSegmentPairs = [];
            }
        }
        
        return segments;
    }
    
    private TempoSegment CreateSegment(
        List<AlignedNotePair> pairs,
        int startMeasure,
        int endMeasure,
        Score score,
        double expectedBPM)
    {
        // Calculate local tempo from this segment
        var sortedPairs = pairs.OrderBy(p => p.ScoreNote.StartTick).ToList();
        var ratios = new List<double>();
        
        for (int i = 1; i < sortedPairs.Count; i++)
        {
            double expectedIOI = sortedPairs[i].ScoreNote.StartTimeMs - sortedPairs[i - 1].ScoreNote.StartTimeMs;
            double actualIOI = sortedPairs[i].PerformanceNote.StartTimeMs - sortedPairs[i - 1].PerformanceNote.StartTimeMs;
            
            if (expectedIOI > 10)
            {
                ratios.Add(actualIOI / expectedIOI);
            }
        }
        
        double medianRatio = ratios.Count > 0 ? GetMedian(ratios) : 1.0;
        double localBPM = expectedBPM / medianRatio;
        double deviation = (localBPM - expectedBPM) / expectedBPM;
        
        // Determine trend
        TempoTrend trend = TempoTrend.Steady;
        if (ratios.Count >= 3)
        {
            double firstHalf = ratios.Take(ratios.Count / 2).Average();
            double secondHalf = ratios.Skip(ratios.Count / 2).Average();
            double change = secondHalf - firstHalf;
            
            if (change < -0.05)
                trend = TempoTrend.Accelerating;
            else if (change > 0.05)
                trend = TempoTrend.Decelerating;
        }
        
        return new TempoSegment
        {
            MeasureStart = startMeasure,
            MeasureEnd = endMeasure,
            BPM = localBPM,
            ExpectedBPM = expectedBPM,
            Deviation = deviation,
            Trend = trend,
            NoteCount = pairs.Count,
            Stability = ratios.Count > 1 ? 1 - CalculateStdDev(ratios) : 1.0
        };
    }
    
    /// <summary>
    /// Calculates overall tempo stability (0-1, higher = more stable).
    /// </summary>
    private double CalculateTempoStability(List<TempoSegment> segments)
    {
        if (segments.Count == 0) return 1.0;
        
        var deviations = segments.Select(s => s.Deviation).ToList();
        double stdDev = CalculateStdDev(deviations);
        
        // Convert std dev to 0-1 stability score
        // 0% std dev = perfect stability (1.0)
        // 20% std dev = poor stability (0.0)
        return Math.Max(0, 1 - stdDev / 0.2);
    }
    
    /// <summary>
    /// Detects overall tempo drift (accelerando/ritardando) across the piece.
    /// </summary>
    private (TempoTrend OverallTrend, List<EvaluationIssue> Issues) DetectTempoDrift(List<TempoSegment> segments)
    {
        var issues = new List<EvaluationIssue>();
        
        if (segments.Count < 2)
        {
            return (TempoTrend.Steady, issues);
        }
        
        // Linear regression on tempo over time
        var xValues = Enumerable.Range(0, segments.Count).Select(i => (double)i).ToList();
        var yValues = segments.Select(s => s.BPM).ToList();
        
        double slope = CalculateSlope(xValues, yValues);
        double avgBPM = yValues.Average();
        double slopePercent = slope / avgBPM;
        
        TempoTrend trend = TempoTrend.Steady;
        
        if (slopePercent > 0.02) // More than 2% increase per segment
        {
            trend = TempoTrend.Accelerating;
            issues.Add(new EvaluationIssue
            {
                Severity = IssueSeverity.Moderate,
                Type = IssueType.Accelerating,
                Description = $"Gradually speeding up throughout the piece",
                Suggestion = "Use a metronome and consciously maintain the starting tempo"
            });
        }
        else if (slopePercent < -0.02)
        {
            trend = TempoTrend.Decelerating;
            issues.Add(new EvaluationIssue
            {
                Severity = IssueSeverity.Moderate,
                Type = IssueType.Decelerating,
                Description = $"Gradually slowing down throughout the piece",
                Suggestion = "Maintain energy and momentum - the tempo shouldn't drift"
            });
        }
        
        // Check for segments that are significantly different
        foreach (var segment in segments)
        {
            if (Math.Abs(segment.Deviation) > 0.2) // More than 20% off
            {
                issues.Add(new EvaluationIssue
                {
                    Severity = IssueSeverity.Moderate,
                    Type = segment.Deviation > 0 ? IssueType.TempoTooFast : IssueType.TempoTooSlow,
                    Description = $"Measures {segment.MeasureStart}-{segment.MeasureEnd}: tempo significantly {(segment.Deviation > 0 ? "fast" : "slow")} ({segment.BPM:F0} BPM vs {segment.ExpectedBPM:F0})",
                    Measure = segment.MeasureStart,
                    Suggestion = "Practice this section with a metronome"
                });
            }
        }
        
        return (trend, issues);
    }
    
    private double CalculateScore(double tempoDeviation, double stability, (TempoTrend, List<EvaluationIssue>) driftAnalysis)
    {
        double score = 100;
        
        // Penalty for tempo deviation
        // 10% deviation = 10 points off, 20% = 25 points, 30%+ = 40 points
        double absDeviation = Math.Abs(tempoDeviation);
        if (absDeviation > 0.3)
            score -= 40;
        else if (absDeviation > 0.2)
            score -= 25;
        else if (absDeviation > 0.1)
            score -= absDeviation * 100;
        
        // Penalty for instability
        // Stability score is 0-1, so (1-stability) * 30 gives penalty
        score -= (1 - stability) * 30;
        
        // Penalty for drift
        if (driftAnalysis.Item1 != TempoTrend.Steady)
        {
            score -= 10;
        }
        
        return Math.Max(0, Math.Min(100, score));
    }
    
    private EvaluationIssue CreateTempoDeviationIssue(double deviation, double expected, double detected)
    {
        bool tooFast = deviation > 0;
        return new EvaluationIssue
        {
            Severity = Math.Abs(deviation) > 0.25 ? IssueSeverity.Significant : IssueSeverity.Moderate,
            Type = tooFast ? IssueType.TempoTooFast : IssueType.TempoTooSlow,
            Description = $"Tempo is {Math.Abs(deviation * 100):F0}% {(tooFast ? "faster" : "slower")} than marked ({detected:F0} BPM vs {expected:F0})",
            Suggestion = tooFast 
                ? "Practice with a metronome at the marked tempo - you're rushing"
                : "The tempo is dragging - check that you can play this piece at full speed"
        };
    }
    
    private EvaluationIssue CreateStabilityIssue(double stability)
    {
        return new EvaluationIssue
        {
            Severity = stability < 0.7 ? IssueSeverity.Significant : IssueSeverity.Moderate,
            Type = IssueType.TempoUnstable,
            Description = $"Tempo is unstable - varies significantly throughout",
            Suggestion = "Practice with a metronome to develop a consistent internal pulse"
        };
    }
    
    private string GenerateSummary(double score, double expected, double detected, double stability)
    {
        if (score >= 95)
            return $"Excellent tempo control at {detected:F0} BPM.";
        if (score >= 85)
            return $"Good tempo ({detected:F0} BPM, expected {expected:F0}).";
        if (score >= 70)
            return $"Tempo needs work ({detected:F0} vs {expected:F0} BPM, {stability * 100:F0}% stable).";
        return $"Significant tempo issues. Practice slowly with a metronome.";
    }
    
    private TempoResult CreateEmptyResult(Score score)
    {
        var expectedBPM = score.TempoMarkings.FirstOrDefault()?.BPM ?? 120;
        return new TempoResult
        {
            Score = 0,
            Summary = "Insufficient notes to evaluate tempo.",
            Issues = [],
            ExpectedBPM = expectedBPM,
            DetectedBPM = expectedBPM,
            TempoDeviation = 0,
            TempoStability = 0,
            Segments = [],
            IOIData = []
        };
    }
    
    private double CalculateStdDev(List<double> values)
    {
        if (values.Count <= 1) return 0;
        double avg = values.Average();
        double sumSquares = values.Sum(v => Math.Pow(v - avg, 2));
        return Math.Sqrt(sumSquares / (values.Count - 1));
    }
    
    private double GetMedian(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        return sorted[sorted.Count / 2];
    }
    
    private double CalculateSlope(List<double> x, List<double> y)
    {
        if (x.Count != y.Count || x.Count < 2) return 0;
        
        double avgX = x.Average();
        double avgY = y.Average();
        
        double numerator = x.Zip(y, (xi, yi) => (xi - avgX) * (yi - avgY)).Sum();
        double denominator = x.Sum(xi => Math.Pow(xi - avgX, 2));
        
        return denominator != 0 ? numerator / denominator : 0;
    }
}

/// <summary>
/// Result of tempo evaluation.
/// </summary>
public record TempoResult : EvaluationResult
{
    public double ExpectedBPM { get; init; }
    public double DetectedBPM { get; init; }
    
    /// <summary>
    /// Relative deviation (positive = faster than expected).
    /// </summary>
    public double TempoDeviation { get; init; }
    
    /// <summary>
    /// Percentage deviation.
    /// </summary>
    public double TempoDeviationPercent => TempoDeviation * 100;
    
    /// <summary>
    /// Tempo stability (0-1, higher = more stable).
    /// </summary>
    public double TempoStability { get; init; }
    
    /// <summary>
    /// Tempo analysis by segment.
    /// </summary>
    public IReadOnlyList<TempoSegment> Segments { get; init; } = [];
    
    /// <summary>
    /// Overall drift trend.
    /// </summary>
    public TempoTrend DriftTrend { get; init; } = TempoTrend.Steady;
    
    /// <summary>
    /// Inter-onset interval data.
    /// </summary>
    public IReadOnlyList<IOIDataPoint> IOIData { get; init; } = [];
}

/// <summary>
/// Tempo analysis for a segment of measures.
/// </summary>
public record TempoSegment
{
    public int MeasureStart { get; init; }
    public int MeasureEnd { get; init; }
    public double BPM { get; init; }
    public double ExpectedBPM { get; init; }
    public double Deviation { get; init; }
    public TempoTrend Trend { get; init; }
    public int NoteCount { get; init; }
    public double Stability { get; init; }
}

/// <summary>
/// Tempo trend direction.
/// </summary>
public enum TempoTrend
{
    Steady,
    Accelerating,
    Decelerating
}

/// <summary>
/// Inter-onset interval data point.
/// </summary>
public record IOIDataPoint
{
    public int Measure { get; init; }
    public double Beat { get; init; }
    public double ExpectedIOI { get; init; }
    public double ActualIOI { get; init; }
    public double Ratio { get; init; }
    public double LocalBPM { get; init; }
}

