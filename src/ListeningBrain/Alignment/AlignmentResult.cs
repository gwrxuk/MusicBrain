using ListeningBrain.Core.Models;

namespace ListeningBrain.Alignment;

/// <summary>
/// Result of aligning a performance to a score.
/// Contains all matched note pairs and statistics.
/// </summary>
public record AlignmentResult
{
    /// <summary>
    /// Aligned note pairs (score note ↔ performance note).
    /// </summary>
    public required IReadOnlyList<AlignedNotePair> Pairs { get; init; }
    
    /// <summary>
    /// Notes in the score that were not matched to any played note.
    /// </summary>
    public required IReadOnlyList<MissedNote> MissedNotes { get; init; }
    
    /// <summary>
    /// Notes played that don't correspond to any score note.
    /// </summary>
    public required IReadOnlyList<PerformanceNote> ExtraNotes { get; init; }
    
    /// <summary>
    /// Total alignment cost (lower = better alignment).
    /// </summary>
    public double TotalCost { get; init; }
    
    /// <summary>
    /// Normalized alignment score (0-1, higher = better).
    /// </summary>
    public double NormalizedScore { get; init; }
    
    /// <summary>
    /// The warping path from DTW (if applicable).
    /// Maps performance time → score time.
    /// </summary>
    public IReadOnlyList<WarpingPoint>? WarpingPath { get; init; }
    
    /// <summary>
    /// Estimated tempo ratio (performance tempo / score tempo).
    /// </summary>
    public double EstimatedTempoRatio { get; init; } = 1.0;
    
    /// <summary>
    /// Time offset applied to align performance start to score start.
    /// </summary>
    public double TimeOffsetMs { get; init; }
    
    /// <summary>
    /// Algorithm used for this alignment.
    /// </summary>
    public required string AlgorithmUsed { get; init; }
    
    /// <summary>
    /// Time taken to compute alignment.
    /// </summary>
    public TimeSpan ComputeTime { get; init; }
    
    /// <summary>
    /// Quick statistics about the alignment.
    /// </summary>
    public AlignmentStatistics Statistics => new()
    {
        TotalScoreNotes = Pairs.Count + MissedNotes.Count,
        TotalPlayedNotes = Pairs.Count + ExtraNotes.Count,
        MatchedNotes = Pairs.Count,
        MissedNotes = MissedNotes.Count,
        ExtraNotes = ExtraNotes.Count,
        CorrectPitches = Pairs.Count(p => p.IsExactPitchMatch),
        WrongPitches = Pairs.Count(p => !p.IsExactPitchMatch && !p.IsOctaveError),
        OctaveErrors = Pairs.Count(p => p.IsOctaveError),
        MeanTimingErrorMs = Pairs.Count > 0 
            ? Pairs.Average(p => Math.Abs(p.TimingDeviationMs)) 
            : 0,
        TimingErrorStdDev = CalculateTimingStdDev()
    };
    
    private double CalculateTimingStdDev()
    {
        if (Pairs.Count <= 1) return 0;
        var errors = Pairs.Select(p => p.TimingDeviationMs).ToList();
        var mean = errors.Average();
        return Math.Sqrt(errors.Sum(e => Math.Pow(e - mean, 2)) / (errors.Count - 1));
    }
    
    /// <summary>
    /// Gets alignment results for a specific measure.
    /// </summary>
    public MeasureAlignment GetMeasureAlignment(int measure)
    {
        var measurePairs = Pairs.Where(p => p.ScoreNote.Measure == measure).ToList();
        var measureMissed = MissedNotes.Where(m => m.ExpectedNote.Measure == measure).ToList();
        
        return new MeasureAlignment
        {
            Measure = measure,
            Pairs = measurePairs,
            MissedNotes = measureMissed,
            TotalExpected = measurePairs.Count + measureMissed.Count,
            CorrectCount = measurePairs.Count(p => p.IsExactPitchMatch),
            MeanTimingError = measurePairs.Count > 0 
                ? measurePairs.Average(p => Math.Abs(p.TimingDeviationMs)) 
                : 0
        };
    }
}

/// <summary>
/// A single aligned pair: one score note matched to one performance note.
/// </summary>
public record AlignedNotePair
{
    /// <summary>
    /// The expected note from the score.
    /// </summary>
    public required ScoreNote ScoreNote { get; init; }
    
    /// <summary>
    /// The actual note played by the student.
    /// </summary>
    public required PerformanceNote PerformanceNote { get; init; }
    
    /// <summary>
    /// Alignment confidence score (0-1).
    /// </summary>
    public required double Confidence { get; init; }
    
    /// <summary>
    /// Timing deviation in milliseconds (positive = late).
    /// </summary>
    public required double TimingDeviationMs { get; init; }
    
    /// <summary>
    /// Timing deviation in musical beats.
    /// </summary>
    public required double TimingDeviationBeats { get; init; }
    
    /// <summary>
    /// Pitch difference in semitones (0 = correct).
    /// </summary>
    public int PitchDifference => PerformanceNote.Pitch - ScoreNote.Pitch;
    
    /// <summary>
    /// Was the exact correct pitch played?
    /// </summary>
    public bool IsExactPitchMatch => PitchDifference == 0;
    
    /// <summary>
    /// Was the pitch correct but in wrong octave?
    /// </summary>
    public bool IsOctaveError => !IsExactPitchMatch && 
        ScoreNote.PitchClass == PerformanceNote.PitchClass;
    
    /// <summary>
    /// Was the timing within acceptable tolerance?
    /// </summary>
    public bool IsTimingAcceptable(double toleranceMs = 50) 
        => Math.Abs(TimingDeviationMs) <= toleranceMs;
    
    /// <summary>
    /// Velocity difference (positive = played louder than expected).
    /// </summary>
    public int VelocityDifference => PerformanceNote.Velocity - ScoreNote.Velocity;
    
    /// <summary>
    /// Duration difference in milliseconds (positive = held longer).
    /// </summary>
    public double DurationDifferenceMs => PerformanceNote.DurationMs - ScoreNote.DurationMs;
    
    /// <summary>
    /// Classifies the overall quality of this note match.
    /// </summary>
    public NoteMatchQuality Quality
    {
        get
        {
            if (!IsExactPitchMatch && !IsOctaveError)
                return NoteMatchQuality.WrongNote;
            if (IsOctaveError)
                return NoteMatchQuality.OctaveError;
            if (Math.Abs(TimingDeviationMs) > 100)
                return NoteMatchQuality.SignificantTimingError;
            if (Math.Abs(TimingDeviationMs) > 50)
                return NoteMatchQuality.MinorTimingError;
            return NoteMatchQuality.Excellent;
        }
    }
}

/// <summary>
/// Quality classification for a matched note.
/// </summary>
public enum NoteMatchQuality
{
    Excellent,              // Pitch correct, timing ≤ 50ms
    MinorTimingError,       // Pitch correct, timing 50-100ms
    SignificantTimingError, // Pitch correct, timing > 100ms
    OctaveError,            // Right pitch class, wrong octave
    WrongNote               // Wrong pitch entirely
}

/// <summary>
/// A point in the DTW warping path.
/// </summary>
public record WarpingPoint(
    int ScoreIndex,
    int PerformanceIndex,
    double Cost
);

/// <summary>
/// Quick statistics about an alignment.
/// </summary>
public record AlignmentStatistics
{
    public int TotalScoreNotes { get; init; }
    public int TotalPlayedNotes { get; init; }
    public int MatchedNotes { get; init; }
    public int MissedNotes { get; init; }
    public int ExtraNotes { get; init; }
    public int CorrectPitches { get; init; }
    public int WrongPitches { get; init; }
    public int OctaveErrors { get; init; }
    public double MeanTimingErrorMs { get; init; }
    public double TimingErrorStdDev { get; init; }
    
    /// <summary>
    /// Percentage of score notes matched correctly.
    /// </summary>
    public double AccuracyPercent => TotalScoreNotes > 0 
        ? (double)CorrectPitches / TotalScoreNotes * 100 
        : 0;
}

/// <summary>
/// Alignment results for a single measure.
/// </summary>
public record MeasureAlignment
{
    public int Measure { get; init; }
    public IReadOnlyList<AlignedNotePair> Pairs { get; init; } = [];
    public IReadOnlyList<MissedNote> MissedNotes { get; init; } = [];
    public int TotalExpected { get; init; }
    public int CorrectCount { get; init; }
    public double MeanTimingError { get; init; }
    
    public double AccuracyPercent => TotalExpected > 0 
        ? (double)CorrectCount / TotalExpected * 100 
        : 100;
}

