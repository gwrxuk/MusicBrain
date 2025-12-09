using ListeningBrain.Alignment;
using ListeningBrain.Core.Models;

namespace ListeningBrain.Evaluation;

/// <summary>
/// Base interface for all evaluators.
/// </summary>
public interface IEvaluator<TResult> where TResult : EvaluationResult
{
    /// <summary>
    /// Evaluates an alignment result and produces detailed metrics.
    /// </summary>
    TResult Evaluate(AlignmentResult alignment, Score score, Performance performance);
    
    /// <summary>
    /// Name of this evaluator.
    /// </summary>
    string Name { get; }
}

/// <summary>
/// Base class for all evaluation results.
/// </summary>
public abstract record EvaluationResult
{
    /// <summary>
    /// Overall score (0-100).
    /// </summary>
    public required double Score { get; init; }
    
    /// <summary>
    /// Letter grade (A+, A, B+, B, C+, C, D, F).
    /// </summary>
    public string Grade => Score switch
    {
        >= 97 => "A+",
        >= 93 => "A",
        >= 90 => "A-",
        >= 87 => "B+",
        >= 83 => "B",
        >= 80 => "B-",
        >= 77 => "C+",
        >= 73 => "C",
        >= 70 => "C-",
        >= 67 => "D+",
        >= 63 => "D",
        >= 60 => "D-",
        _ => "F"
    };
    
    /// <summary>
    /// Short summary of the evaluation.
    /// </summary>
    public required string Summary { get; init; }
    
    /// <summary>
    /// Detailed issues found, ordered by severity.
    /// </summary>
    public IReadOnlyList<EvaluationIssue> Issues { get; init; } = [];
}

/// <summary>
/// An issue found during evaluation.
/// </summary>
public record EvaluationIssue
{
    /// <summary>
    /// Severity of the issue.
    /// </summary>
    public required IssueSeverity Severity { get; init; }
    
    /// <summary>
    /// Type of issue.
    /// </summary>
    public required IssueType Type { get; init; }
    
    /// <summary>
    /// Human-readable description.
    /// </summary>
    public required string Description { get; init; }
    
    /// <summary>
    /// Measure where the issue occurred (if applicable).
    /// </summary>
    public int? Measure { get; init; }
    
    /// <summary>
    /// Beat within the measure (if applicable).
    /// </summary>
    public double? Beat { get; init; }
    
    /// <summary>
    /// The score note involved (if applicable).
    /// </summary>
    public Guid? ScoreNoteId { get; init; }
    
    /// <summary>
    /// The performance note involved (if applicable).
    /// </summary>
    public Guid? PerformanceNoteId { get; init; }
    
    /// <summary>
    /// Suggestion for improvement.
    /// </summary>
    public string? Suggestion { get; init; }
}

/// <summary>
/// Severity levels for issues.
/// </summary>
public enum IssueSeverity
{
    Info = 0,       // Informational only
    Minor = 1,      // Small issue, doesn't significantly affect performance
    Moderate = 2,   // Noticeable issue that should be addressed
    Significant = 3, // Major issue affecting the music
    Critical = 4    // Fundamental problem (wrong notes, lost place)
}

/// <summary>
/// Types of issues that can be identified.
/// </summary>
public enum IssueType
{
    // Note Accuracy
    WrongNote,
    MissedNote,
    ExtraNote,
    OctaveError,
    
    // Rhythm
    RushedNote,        // Played too early
    DraggedNote,       // Played too late
    UnevenRhythm,      // Inconsistent timing
    WrongDuration,     // Note held too long/short
    
    // Tempo
    TempoTooFast,
    TempoTooSlow,
    TempoUnstable,
    Accelerating,
    Decelerating,
    
    // Dynamics (future)
    ToeLoud,
    TooSoft,
    FlatDynamics,
    MissedAccent,
    
    // General
    PassageSkipped,
    RepeatMissed,
    LostPlace
}

