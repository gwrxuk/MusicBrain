using ListeningBrain.Core.Models;

namespace ListeningBrain.Alignment;

/// <summary>
/// Interface for alignment strategies that match performance notes to score notes.
/// </summary>
public interface IAlignmentStrategy
{
    /// <summary>
    /// Aligns a performance against a score.
    /// </summary>
    /// <param name="score">The ground-truth score.</param>
    /// <param name="performance">The student's performance.</param>
    /// <param name="options">Optional alignment configuration.</param>
    /// <returns>The alignment result containing matched pairs and statistics.</returns>
    AlignmentResult Align(Score score, Performance performance, AlignmentOptions? options = null);
    
    /// <summary>
    /// Name of the alignment strategy.
    /// </summary>
    string Name { get; }
}

/// <summary>
/// Configuration options for alignment algorithms.
/// </summary>
public record AlignmentOptions
{
    /// <summary>
    /// Maximum allowed timing deviation (ms) for a note to be considered a match.
    /// </summary>
    public double MaxTimingDeviationMs { get; init; } = 500;
    
    /// <summary>
    /// Weight for pitch matching in cost function (0-1).
    /// </summary>
    public double PitchWeight { get; init; } = 0.6;
    
    /// <summary>
    /// Weight for timing in cost function (0-1).
    /// </summary>
    public double TimingWeight { get; init; } = 0.3;
    
    /// <summary>
    /// Weight for velocity/dynamics in cost function (0-1).
    /// </summary>
    public double VelocityWeight { get; init; } = 0.1;
    
    /// <summary>
    /// Penalty for inserting a gap (missed note) in Needleman-Wunsch.
    /// </summary>
    public double GapPenalty { get; init; } = 1.0;
    
    /// <summary>
    /// Penalty for wrong octave (vs completely wrong note).
    /// </summary>
    public double WrongOctavePenalty { get; init; } = 0.3;
    
    /// <summary>
    /// Allow tempo flexibility in alignment (for rubato).
    /// </summary>
    public bool AllowTempoFlexibility { get; init; } = true;
    
    /// <summary>
    /// Maximum tempo ratio deviation from expected (e.g., 0.3 = ±30%).
    /// </summary>
    public double MaxTempoDeviation { get; init; } = 0.3;
    
    /// <summary>
    /// Use global (whole piece) or local (window-based) alignment.
    /// </summary>
    public AlignmentMode Mode { get; init; } = AlignmentMode.Global;
    
    /// <summary>
    /// Window size in milliseconds for local alignment mode.
    /// </summary>
    public double LocalWindowMs { get; init; } = 5000;
    
    /// <summary>
    /// Treat grace notes with relaxed timing requirements.
    /// </summary>
    public bool RelaxGraceNoteTiming { get; init; } = true;
    
    /// <summary>
    /// Consider octave errors as partial matches.
    /// </summary>
    public bool AllowOctaveErrors { get; init; } = true;
    
    /// <summary>
    /// Default alignment options.
    /// </summary>
    public static AlignmentOptions Default => new();
    
    /// <summary>
    /// Strict alignment for advanced players.
    /// </summary>
    public static AlignmentOptions Strict => new()
    {
        MaxTimingDeviationMs = 100,
        MaxTempoDeviation = 0.1,
        GapPenalty = 1.5,
        AllowOctaveErrors = false
    };
    
    /// <summary>
    /// Lenient alignment for beginners.
    /// </summary>
    public static AlignmentOptions Beginner => new()
    {
        MaxTimingDeviationMs = 1000,
        MaxTempoDeviation = 0.5,
        GapPenalty = 0.5,
        WrongOctavePenalty = 0.1
    };
}

/// <summary>
/// Alignment mode.
/// </summary>
public enum AlignmentMode
{
    /// <summary>
    /// Align entire performance at once (best for post-analysis).
    /// </summary>
    Global,
    
    /// <summary>
    /// Align in sliding windows (best for real-time).
    /// </summary>
    Local,
    
    /// <summary>
    /// Semi-global: allow free gaps at start/end.
    /// </summary>
    SemiGlobal
}

