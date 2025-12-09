using ListeningBrain.Alignment;
using ListeningBrain.Core.Models;

namespace ListeningBrain.Evaluation;

/// <summary>
/// Evaluates rhythmic precision: timing accuracy relative to the beat.
/// </summary>
public class RhythmEvaluator : IEvaluator<RhythmResult>
{
    public string Name => "Rhythm Evaluator";
    
    /// <summary>
    /// Timing thresholds for classification.
    /// </summary>
    public RhythmThresholds Thresholds { get; init; } = RhythmThresholds.Default;
    
    public RhythmResult Evaluate(AlignmentResult alignment, Score score, Performance performance)
    {
        var issues = new List<EvaluationIssue>();
        var timingErrors = new List<TimingError>();
        
        if (alignment.Pairs.Count == 0)
        {
            return CreateEmptyResult();
        }
        
        // Analyze timing of each aligned note
        foreach (var pair in alignment.Pairs)
        {
            var timingError = AnalyzeTiming(pair, score);
            timingErrors.Add(timingError);
            
            if (timingError.Severity != TimingSeverity.OnTime)
            {
                issues.Add(CreateTimingIssue(timingError));
            }
        }
        
        // Calculate statistics
        var deviations = timingErrors.Select(t => t.DeviationMs).ToList();
        double meanError = deviations.Average();
        double absError = deviations.Select(Math.Abs).Average();
        double stdDev = CalculateStdDev(deviations);
        
        // Check for systematic issues
        var rhythmPatterns = AnalyzeRhythmPatterns(timingErrors);
        issues.AddRange(rhythmPatterns);
        
        // Calculate score
        double rhythmScore = CalculateScore(absError, stdDev, timingErrors);
        
        // Generate measure breakdown
        var measureBreakdown = GenerateMeasureBreakdown(timingErrors);
        
        return new RhythmResult
        {
            Score = rhythmScore,
            Summary = GenerateSummary(rhythmScore, absError, rhythmPatterns),
            Issues = issues.OrderByDescending(i => i.Severity).ToList(),
            MeanTimingError = meanError,
            AbsoluteTimingError = absError,
            TimingStdDev = stdDev,
            TimingErrors = timingErrors,
            MeasureBreakdown = measureBreakdown,
            OnTimeNotes = timingErrors.Count(t => t.Severity == TimingSeverity.OnTime),
            SlightlyEarlyNotes = timingErrors.Count(t => t.Severity == TimingSeverity.SlightlyEarly),
            SlightlyLateNotes = timingErrors.Count(t => t.Severity == TimingSeverity.SlightlyLate),
            VeryEarlyNotes = timingErrors.Count(t => t.Severity == TimingSeverity.VeryEarly),
            VeryLateNotes = timingErrors.Count(t => t.Severity == TimingSeverity.VeryLate)
        };
    }
    
    private TimingError AnalyzeTiming(AlignedNotePair pair, Score score)
    {
        var scoreNote = pair.ScoreNote;
        double deviationMs = pair.TimingDeviationMs;
        double deviationBeats = pair.TimingDeviationBeats;
        
        // Get note-specific tolerance
        var tolerance = scoreNote.GetTimingTolerance();
        
        // Classify severity
        TimingSeverity severity;
        if (Math.Abs(deviationMs) <= Thresholds.OnTimeMs)
        {
            severity = TimingSeverity.OnTime;
        }
        else if (deviationMs < -Thresholds.VeryEarlyMs)
        {
            severity = TimingSeverity.VeryEarly;
        }
        else if (deviationMs < -Thresholds.SlightlyEarlyMs)
        {
            severity = TimingSeverity.SlightlyEarly;
        }
        else if (deviationMs > Thresholds.VeryLateMs)
        {
            severity = TimingSeverity.VeryLate;
        }
        else if (deviationMs > Thresholds.SlightlyLateMs)
        {
            severity = TimingSeverity.SlightlyLate;
        }
        else if (deviationMs < 0)
        {
            severity = TimingSeverity.SlightlyEarly;
        }
        else
        {
            severity = TimingSeverity.SlightlyLate;
        }
        
        // Relax for grace notes
        if (scoreNote.IsGraceNote && severity != TimingSeverity.OnTime)
        {
            severity = TimingSeverity.OnTime; // Grace notes get pass on timing
        }
        
        return new TimingError
        {
            ScoreNoteId = scoreNote.Id,
            PerformanceNoteId = pair.PerformanceNote.Id,
            DeviationMs = deviationMs,
            DeviationBeats = deviationBeats,
            Measure = scoreNote.Measure,
            Beat = scoreNote.Beat,
            Severity = severity,
            NoteName = scoreNote.NoteName,
            IsGraceNote = scoreNote.IsGraceNote,
            IsTuplet = scoreNote.IsTuplet
        };
    }
    
    private List<EvaluationIssue> AnalyzeRhythmPatterns(List<TimingError> errors)
    {
        var issues = new List<EvaluationIssue>();
        
        // Check for consistent rushing
        double avgDeviation = errors.Average(e => e.DeviationMs);
        if (avgDeviation < -Thresholds.SlightlyEarlyMs)
        {
            issues.Add(new EvaluationIssue
            {
                Severity = IssueSeverity.Moderate,
                Type = IssueType.RushedNote,
                Description = $"Tendency to rush - average {Math.Abs(avgDeviation):F0}ms early",
                Suggestion = "Use a metronome and focus on playing slightly behind the beat"
            });
        }
        else if (avgDeviation > Thresholds.SlightlyLateMs)
        {
            issues.Add(new EvaluationIssue
            {
                Severity = IssueSeverity.Moderate,
                Type = IssueType.DraggedNote,
                Description = $"Tendency to drag - average {avgDeviation:F0}ms late",
                Suggestion = "Practice with a metronome at a slower tempo and gradually increase"
            });
        }
        
        // Check for uneven rhythm (high std dev)
        double stdDev = CalculateStdDev(errors.Select(e => e.DeviationMs).ToList());
        if (stdDev > Thresholds.UnevenThresholdMs)
        {
            issues.Add(new EvaluationIssue
            {
                Severity = IssueSeverity.Moderate,
                Type = IssueType.UnevenRhythm,
                Description = "Uneven rhythm - timing varies significantly",
                Suggestion = "Practice slowly with a metronome, focusing on consistent note spacing"
            });
        }
        
        // Check for problematic passages (clusters of timing errors)
        var measureGroups = errors.GroupBy(e => e.Measure).ToList();
        foreach (var group in measureGroups)
        {
            double measureAvg = Math.Abs(group.Average(e => e.DeviationMs));
            if (measureAvg > Thresholds.SlightlyLateMs)
            {
                issues.Add(new EvaluationIssue
                {
                    Severity = IssueSeverity.Minor,
                    Type = IssueType.UnevenRhythm,
                    Description = $"Measure {group.Key} has timing issues (avg {measureAvg:F0}ms off)",
                    Measure = group.Key,
                    Suggestion = $"Isolate and practice measure {group.Key} with a metronome"
                });
            }
        }
        
        return issues;
    }
    
    private double CalculateScore(double absError, double stdDev, List<TimingError> errors)
    {
        // Base score starts at 100
        double score = 100;
        
        // Penalty for average timing error
        // Every 10ms of average error costs 1 point
        score -= absError / 10.0;
        
        // Penalty for inconsistency (std dev)
        // Every 15ms of std dev costs 1 point
        score -= stdDev / 15.0;
        
        // Penalty for severe errors
        int veryEarly = errors.Count(e => e.Severity == TimingSeverity.VeryEarly);
        int veryLate = errors.Count(e => e.Severity == TimingSeverity.VeryLate);
        score -= (veryEarly + veryLate) * 0.5;
        
        return Math.Max(0, Math.Min(100, score));
    }
    
    private EvaluationIssue CreateTimingIssue(TimingError error)
    {
        var (type, description, suggestion) = error.Severity switch
        {
            TimingSeverity.VeryEarly => (
                IssueType.RushedNote,
                $"Rushed note {error.NoteName} in measure {error.Measure} ({Math.Abs(error.DeviationMs):F0}ms early)",
                "Slow down and wait for the beat"),
            TimingSeverity.SlightlyEarly => (
                IssueType.RushedNote,
                $"Slightly early {error.NoteName} in measure {error.Measure}",
                "Focus on placing this note precisely on the beat"),
            TimingSeverity.SlightlyLate => (
                IssueType.DraggedNote,
                $"Slightly late {error.NoteName} in measure {error.Measure}",
                "Prepare this note earlier"),
            TimingSeverity.VeryLate => (
                IssueType.DraggedNote,
                $"Late note {error.NoteName} in measure {error.Measure} ({error.DeviationMs:F0}ms late)",
                "This note is significantly behind - check fingering"),
            _ => (IssueType.UnevenRhythm, "", "")
        };
        
        return new EvaluationIssue
        {
            Severity = error.Severity is TimingSeverity.VeryEarly or TimingSeverity.VeryLate 
                ? IssueSeverity.Moderate 
                : IssueSeverity.Minor,
            Type = type,
            Description = description,
            Measure = error.Measure,
            Beat = error.Beat,
            ScoreNoteId = error.ScoreNoteId,
            PerformanceNoteId = error.PerformanceNoteId,
            Suggestion = suggestion
        };
    }
    
    private List<MeasureRhythm> GenerateMeasureBreakdown(List<TimingError> errors)
    {
        return errors
            .GroupBy(e => e.Measure)
            .Select(g => new MeasureRhythm
            {
                Measure = g.Key,
                MeanDeviation = g.Average(e => e.DeviationMs),
                AbsDeviation = g.Average(e => Math.Abs(e.DeviationMs)),
                OnTimeCount = g.Count(e => e.Severity == TimingSeverity.OnTime),
                TotalNotes = g.Count()
            })
            .OrderBy(m => m.Measure)
            .ToList();
    }
    
    private double CalculateStdDev(List<double> values)
    {
        if (values.Count <= 1) return 0;
        double avg = values.Average();
        double sumSquares = values.Sum(v => Math.Pow(v - avg, 2));
        return Math.Sqrt(sumSquares / (values.Count - 1));
    }
    
    private string GenerateSummary(double score, double absError, List<EvaluationIssue> patterns)
    {
        if (score >= 95)
            return $"Excellent rhythm! Average timing within {absError:F0}ms.";
        if (score >= 85)
            return $"Good rhythmic control. Average {absError:F0}ms deviation.";
        if (score >= 70)
        {
            var mainIssue = patterns.FirstOrDefault();
            return mainIssue != null 
                ? $"Rhythm needs work. {mainIssue.Description}"
                : $"Fair rhythm with {absError:F0}ms average deviation.";
        }
        return $"Significant rhythm issues. Practice slowly with a metronome.";
    }
    
    private RhythmResult CreateEmptyResult()
    {
        return new RhythmResult
        {
            Score = 0,
            Summary = "No notes to evaluate.",
            Issues = [],
            MeanTimingError = 0,
            AbsoluteTimingError = 0,
            TimingStdDev = 0,
            TimingErrors = [],
            MeasureBreakdown = []
        };
    }
}

/// <summary>
/// Result of rhythm evaluation.
/// </summary>
public record RhythmResult : EvaluationResult
{
    /// <summary>
    /// Mean timing error (positive = late, negative = early).
    /// </summary>
    public double MeanTimingError { get; init; }
    
    /// <summary>
    /// Mean absolute timing error.
    /// </summary>
    public double AbsoluteTimingError { get; init; }
    
    /// <summary>
    /// Standard deviation of timing errors.
    /// </summary>
    public double TimingStdDev { get; init; }
    
    /// <summary>
    /// Timing analysis for each note.
    /// </summary>
    public IReadOnlyList<TimingError> TimingErrors { get; init; } = [];
    
    /// <summary>
    /// Rhythm breakdown by measure.
    /// </summary>
    public IReadOnlyList<MeasureRhythm> MeasureBreakdown { get; init; } = [];
    
    public int OnTimeNotes { get; init; }
    public int SlightlyEarlyNotes { get; init; }
    public int SlightlyLateNotes { get; init; }
    public int VeryEarlyNotes { get; init; }
    public int VeryLateNotes { get; init; }
    
    public double OnTimePercent => TimingErrors.Count > 0 
        ? (double)OnTimeNotes / TimingErrors.Count * 100 
        : 100;
}

/// <summary>
/// Timing error for a single note.
/// </summary>
public record TimingError
{
    public Guid ScoreNoteId { get; init; }
    public Guid? PerformanceNoteId { get; init; }
    public double DeviationMs { get; init; }
    public double DeviationBeats { get; init; }
    public int Measure { get; init; }
    public double Beat { get; init; }
    public TimingSeverity Severity { get; init; }
    public string NoteName { get; init; } = "";
    public bool IsGraceNote { get; init; }
    public bool IsTuplet { get; init; }
}

/// <summary>
/// Timing severity classification.
/// </summary>
public enum TimingSeverity
{
    OnTime,
    SlightlyEarly,
    SlightlyLate,
    VeryEarly,
    VeryLate
}

/// <summary>
/// Rhythm statistics for a measure.
/// </summary>
public record MeasureRhythm
{
    public int Measure { get; init; }
    public double MeanDeviation { get; init; }
    public double AbsDeviation { get; init; }
    public int OnTimeCount { get; init; }
    public int TotalNotes { get; init; }
    public double OnTimePercent => TotalNotes > 0 ? (double)OnTimeCount / TotalNotes * 100 : 100;
}

/// <summary>
/// Configurable timing thresholds.
/// </summary>
public record RhythmThresholds
{
    public double OnTimeMs { get; init; } = 30;
    public double SlightlyEarlyMs { get; init; } = 50;
    public double SlightlyLateMs { get; init; } = 50;
    public double VeryEarlyMs { get; init; } = 100;
    public double VeryLateMs { get; init; } = 100;
    public double UnevenThresholdMs { get; init; } = 40;
    
    public static RhythmThresholds Default => new();
    
    public static RhythmThresholds Strict => new()
    {
        OnTimeMs = 20,
        SlightlyEarlyMs = 35,
        SlightlyLateMs = 35,
        VeryEarlyMs = 70,
        VeryLateMs = 70,
        UnevenThresholdMs = 25
    };
    
    public static RhythmThresholds Lenient => new()
    {
        OnTimeMs = 50,
        SlightlyEarlyMs = 80,
        SlightlyLateMs = 80,
        VeryEarlyMs = 150,
        VeryLateMs = 150,
        UnevenThresholdMs = 60
    };
}

