using ListeningBrain.Alignment;
using ListeningBrain.Core.Models;

namespace ListeningBrain.Evaluation;

/// <summary>
/// Evaluates note accuracy: correct pitches, missed notes, extra notes.
/// </summary>
public class NoteAccuracyEvaluator : IEvaluator<NoteAccuracyResult>
{
    public string Name => "Note Accuracy Evaluator";
    
    /// <summary>
    /// Weights for different error types.
    /// </summary>
    public NoteAccuracyWeights Weights { get; init; } = NoteAccuracyWeights.Default;
    
    public NoteAccuracyResult Evaluate(AlignmentResult alignment, Score score, Performance performance)
    {
        var issues = new List<EvaluationIssue>();
        var noteErrors = new List<NoteError>();
        
        int correctNotes = 0;
        int wrongNotes = 0;
        int octaveErrors = 0;
        int missedNotes = alignment.MissedNotes.Count;
        int extraNotes = alignment.ExtraNotes.Count;
        
        // Analyze each aligned pair
        foreach (var pair in alignment.Pairs)
        {
            var error = AnalyzeNotePair(pair);
            noteErrors.Add(error);
            
            switch (error.Type)
            {
                case NoteErrorType.Correct:
                case NoteErrorType.EnharmonicMatch:
                    correctNotes++;
                    break;
                case NoteErrorType.WrongOctave:
                    octaveErrors++;
                    issues.Add(CreateOctaveErrorIssue(pair, error));
                    break;
                case NoteErrorType.WrongPitch:
                    wrongNotes++;
                    issues.Add(CreateWrongNoteIssue(pair, error));
                    break;
            }
        }
        
        // Add issues for missed notes
        foreach (var missed in alignment.MissedNotes)
        {
            issues.Add(CreateMissedNoteIssue(missed));
        }
        
        // Add issues for extra notes (only if they're disruptive)
        foreach (var extra in alignment.ExtraNotes.Where(IsDisruptiveExtraNote))
        {
            issues.Add(CreateExtraNoteIssue(extra));
        }
        
        // Calculate overall score
        int totalExpected = score.Notes.Count;
        double rawScore = CalculateScore(
            correctNotes, wrongNotes, octaveErrors, missedNotes, extraNotes, totalExpected);
        
        // Generate per-measure breakdown
        var measureBreakdown = GenerateMeasureBreakdown(alignment, score);
        
        // Find problem areas (measures with low accuracy)
        var problemMeasures = measureBreakdown
            .Where(m => m.Accuracy < 0.7)
            .OrderBy(m => m.Accuracy)
            .Take(5)
            .ToList();
        
        return new NoteAccuracyResult
        {
            Score = rawScore,
            Summary = GenerateSummary(rawScore, correctNotes, totalExpected, problemMeasures),
            Issues = issues.OrderByDescending(i => i.Severity).ToList(),
            CorrectNotes = correctNotes,
            WrongNotes = wrongNotes,
            OctaveErrors = octaveErrors,
            MissedNotes = missedNotes,
            ExtraNotes = extraNotes,
            TotalExpectedNotes = totalExpected,
            NoteErrors = noteErrors,
            MeasureBreakdown = measureBreakdown,
            ProblemMeasures = problemMeasures
        };
    }
    
    private NoteError AnalyzeNotePair(AlignedNotePair pair)
    {
        var scoreNote = pair.ScoreNote;
        var perfNote = pair.PerformanceNote;
        
        // Exact match
        if (pair.IsExactPitchMatch)
        {
            return new NoteError
            {
                ScoreNoteId = scoreNote.Id,
                PerformanceNoteId = perfNote.Id,
                Type = NoteErrorType.Correct,
                PitchDifference = 0,
                Measure = scoreNote.Measure,
                Beat = scoreNote.Beat
            };
        }
        
        // Octave error
        if (pair.IsOctaveError)
        {
            int octaveDiff = (perfNote.Pitch - scoreNote.Pitch) / 12;
            return new NoteError
            {
                ScoreNoteId = scoreNote.Id,
                PerformanceNoteId = perfNote.Id,
                Type = NoteErrorType.WrongOctave,
                PitchDifference = pair.PitchDifference,
                OctavesDifferent = octaveDiff,
                Measure = scoreNote.Measure,
                Beat = scoreNote.Beat,
                ExpectedNote = scoreNote.NoteName,
                PlayedNote = perfNote.NoteName
            };
        }
        
        // Wrong pitch
        return new NoteError
        {
            ScoreNoteId = scoreNote.Id,
            PerformanceNoteId = perfNote.Id,
            Type = NoteErrorType.WrongPitch,
            PitchDifference = pair.PitchDifference,
            Measure = scoreNote.Measure,
            Beat = scoreNote.Beat,
            ExpectedNote = scoreNote.NoteName,
            PlayedNote = perfNote.NoteName
        };
    }
    
    private double CalculateScore(
        int correct, int wrong, int octaveErrors, int missed, int extra, int total)
    {
        if (total == 0) return 100;
        
        // Base score from correct notes
        double baseScore = (double)correct / total * 100;
        
        // Penalties
        double wrongPenalty = wrong * Weights.WrongNotePenalty;
        double octavePenalty = octaveErrors * Weights.OctaveErrorPenalty;
        double missedPenalty = missed * Weights.MissedNotePenalty;
        double extraPenalty = extra * Weights.ExtraNotePenalty;
        
        // Partial credit for octave errors
        double octaveCredit = octaveErrors * Weights.OctavePartialCredit * (100.0 / total);
        
        double finalScore = baseScore + octaveCredit - wrongPenalty - octavePenalty - missedPenalty - extraPenalty;
        
        return Math.Max(0, Math.Min(100, finalScore));
    }
    
    private EvaluationIssue CreateWrongNoteIssue(AlignedNotePair pair, NoteError error)
    {
        return new EvaluationIssue
        {
            Severity = IssueSeverity.Significant,
            Type = IssueType.WrongNote,
            Description = $"Wrong note in measure {error.Measure}: played {error.PlayedNote} instead of {error.ExpectedNote}",
            Measure = error.Measure,
            Beat = error.Beat,
            ScoreNoteId = pair.ScoreNote.Id,
            PerformanceNoteId = pair.PerformanceNote.Id,
            Suggestion = $"Practice measure {error.Measure} slowly, paying attention to the {error.ExpectedNote}"
        };
    }
    
    private EvaluationIssue CreateOctaveErrorIssue(AlignedNotePair pair, NoteError error)
    {
        string direction = error.OctavesDifferent > 0 ? "higher" : "lower";
        return new EvaluationIssue
        {
            Severity = IssueSeverity.Moderate,
            Type = IssueType.OctaveError,
            Description = $"Octave error in measure {error.Measure}: played {error.PlayedNote} ({Math.Abs(error.OctavesDifferent ?? 0)} octave(s) {direction})",
            Measure = error.Measure,
            Beat = error.Beat,
            ScoreNoteId = pair.ScoreNote.Id,
            PerformanceNoteId = pair.PerformanceNote.Id,
            Suggestion = "Check hand position - you may be in the wrong octave"
        };
    }
    
    private EvaluationIssue CreateMissedNoteIssue(MissedNote missed)
    {
        var note = missed.ExpectedNote;
        
        IssueSeverity severity = note.IsGraceNote 
            ? IssueSeverity.Minor 
            : IssueSeverity.Significant;
        
        string description = note.IsGraceNote
            ? $"Grace note {note.NoteName} omitted in measure {note.Measure}"
            : $"Missed note {note.NoteName} in measure {note.Measure}, beat {note.Beat:F1}";
        
        return new EvaluationIssue
        {
            Severity = severity,
            Type = IssueType.MissedNote,
            Description = description,
            Measure = note.Measure,
            Beat = note.Beat,
            ScoreNoteId = note.Id,
            Suggestion = note.IsGraceNote 
                ? "Grace notes add expression - try to include them"
                : $"Practice measure {note.Measure} with focus on all notes"
        };
    }
    
    private EvaluationIssue CreateExtraNoteIssue(PerformanceNote extra)
    {
        return new EvaluationIssue
        {
            Severity = IssueSeverity.Minor,
            Type = IssueType.ExtraNote,
            Description = $"Extra note {extra.NoteName} played",
            PerformanceNoteId = extra.Id,
            Suggestion = "Review the score to ensure you're not adding unintended notes"
        };
    }
    
    private bool IsDisruptiveExtraNote(PerformanceNote note)
    {
        // Extra notes are less disruptive if they're quiet or very short
        return note.Velocity > 40 && note.DurationMs > 50;
    }
    
    private List<MeasureAccuracy> GenerateMeasureBreakdown(AlignmentResult alignment, Score score)
    {
        var measures = new Dictionary<int, (int correct, int total)>();
        
        // Count expected notes per measure
        foreach (var note in score.Notes)
        {
            if (!measures.ContainsKey(note.Measure))
                measures[note.Measure] = (0, 0);
            var (c, t) = measures[note.Measure];
            measures[note.Measure] = (c, t + 1);
        }
        
        // Count correct notes per measure
        foreach (var pair in alignment.Pairs.Where(p => p.IsExactPitchMatch))
        {
            int measure = pair.ScoreNote.Measure;
            if (measures.ContainsKey(measure))
            {
                var (c, t) = measures[measure];
                measures[measure] = (c + 1, t);
            }
        }
        
        return measures
            .OrderBy(kv => kv.Key)
            .Select(kv => new MeasureAccuracy
            {
                Measure = kv.Key,
                CorrectNotes = kv.Value.correct,
                TotalNotes = kv.Value.total,
                Accuracy = kv.Value.total > 0 ? (double)kv.Value.correct / kv.Value.total : 1.0
            })
            .ToList();
    }
    
    private string GenerateSummary(double score, int correct, int total, List<MeasureAccuracy> problemMeasures)
    {
        if (score >= 95)
            return $"Excellent accuracy! {correct}/{total} notes correct.";
        if (score >= 85)
            return $"Good accuracy with {correct}/{total} notes correct. Minor issues to address.";
        if (score >= 70)
            return $"Fair accuracy ({correct}/{total} notes). Focus on measures {string.Join(", ", problemMeasures.Take(3).Select(m => m.Measure))}.";
        
        return $"Needs practice. {correct}/{total} notes correct. Review the problem measures carefully.";
    }
}

/// <summary>
/// Result of note accuracy evaluation.
/// </summary>
public record NoteAccuracyResult : EvaluationResult
{
    public int CorrectNotes { get; init; }
    public int WrongNotes { get; init; }
    public int OctaveErrors { get; init; }
    public int MissedNotes { get; init; }
    public int ExtraNotes { get; init; }
    public int TotalExpectedNotes { get; init; }
    
    /// <summary>
    /// Percentage of notes played correctly.
    /// </summary>
    public double AccuracyPercent => TotalExpectedNotes > 0 
        ? (double)CorrectNotes / TotalExpectedNotes * 100 
        : 100;
    
    /// <summary>
    /// Detailed errors for each note.
    /// </summary>
    public IReadOnlyList<NoteError> NoteErrors { get; init; } = [];
    
    /// <summary>
    /// Accuracy breakdown by measure.
    /// </summary>
    public IReadOnlyList<MeasureAccuracy> MeasureBreakdown { get; init; } = [];
    
    /// <summary>
    /// Measures with the lowest accuracy (top 5).
    /// </summary>
    public IReadOnlyList<MeasureAccuracy> ProblemMeasures { get; init; } = [];
}

/// <summary>
/// Error details for a single note.
/// </summary>
public record NoteError
{
    public Guid ScoreNoteId { get; init; }
    public Guid? PerformanceNoteId { get; init; }
    public NoteErrorType Type { get; init; }
    public int PitchDifference { get; init; }
    public int? OctavesDifferent { get; init; }
    public int Measure { get; init; }
    public double Beat { get; init; }
    public string? ExpectedNote { get; init; }
    public string? PlayedNote { get; init; }
}

/// <summary>
/// Note error classification.
/// </summary>
public enum NoteErrorType
{
    Correct,
    WrongPitch,
    WrongOctave,
    Missed,
    Extra,
    EnharmonicMatch
}

/// <summary>
/// Accuracy for a single measure.
/// </summary>
public record MeasureAccuracy
{
    public int Measure { get; init; }
    public int CorrectNotes { get; init; }
    public int TotalNotes { get; init; }
    public double Accuracy { get; init; }
    public double AccuracyPercent => Accuracy * 100;
}

/// <summary>
/// Configurable weights for accuracy scoring.
/// </summary>
public record NoteAccuracyWeights
{
    /// <summary>
    /// Penalty per wrong note (subtracted from total score).
    /// </summary>
    public double WrongNotePenalty { get; init; } = 3.0;
    
    /// <summary>
    /// Penalty per octave error.
    /// </summary>
    public double OctaveErrorPenalty { get; init; } = 1.0;
    
    /// <summary>
    /// Partial credit for octave errors (0-1).
    /// </summary>
    public double OctavePartialCredit { get; init; } = 0.5;
    
    /// <summary>
    /// Penalty per missed note.
    /// </summary>
    public double MissedNotePenalty { get; init; } = 2.5;
    
    /// <summary>
    /// Penalty per extra note.
    /// </summary>
    public double ExtraNotePenalty { get; init; } = 0.5;
    
    public static NoteAccuracyWeights Default => new();
    
    public static NoteAccuracyWeights Strict => new()
    {
        WrongNotePenalty = 5.0,
        OctaveErrorPenalty = 2.0,
        OctavePartialCredit = 0.25,
        MissedNotePenalty = 4.0,
        ExtraNotePenalty = 1.0
    };
    
    public static NoteAccuracyWeights Lenient => new()
    {
        WrongNotePenalty = 2.0,
        OctaveErrorPenalty = 0.5,
        OctavePartialCredit = 0.75,
        MissedNotePenalty = 1.5,
        ExtraNotePenalty = 0.25
    };
}

