using ListeningBrain.Alignment;
using ListeningBrain.Core.Models;
using ListeningBrain.Evaluation;

namespace ListeningBrain.Intelligence;

/// <summary>
/// Recognizes common error patterns in piano performance.
/// Identifies recurring mistakes and their likely causes.
/// </summary>
public class ErrorPatternRecognizer
{
    /// <summary>
    /// Minimum occurrences to consider a pattern.
    /// </summary>
    public int MinPatternOccurrences { get; init; } = 2;
    
    /// <summary>
    /// Analyzes a performance for common error patterns.
    /// </summary>
    public ErrorPatternAnalysis AnalyzePatterns(
        AlignmentResult alignment,
        Score score,
        NoteAccuracyResult? accuracyResult = null,
        RhythmResult? rhythmResult = null)
    {
        var patterns = new List<ErrorPattern>();
        
        // Detect different types of patterns
        patterns.AddRange(DetectIntervalPatterns(alignment));
        patterns.AddRange(DetectPositionPatterns(alignment, score));
        patterns.AddRange(DetectFingeringPatterns(alignment, score));
        patterns.AddRange(DetectRhythmPatterns(alignment, rhythmResult));
        patterns.AddRange(DetectChordPatterns(alignment, score));
        patterns.AddRange(DetectPassagePatterns(alignment, score));
        
        // Calculate pattern statistics
        var statistics = CalculateStatistics(patterns, alignment);
        
        // Generate insights
        var insights = GenerateInsights(patterns, statistics);
        
        return new ErrorPatternAnalysis
        {
            Patterns = patterns.OrderByDescending(p => p.Severity).ThenByDescending(p => p.Occurrences).ToList(),
            Statistics = statistics,
            Insights = insights,
            TotalErrorsAnalyzed = alignment.MissedNotes.Count + 
                                  alignment.Pairs.Count(p => !p.IsExactPitchMatch),
            PatternsIdentified = patterns.Count,
            TopPattern = patterns.OrderByDescending(p => p.Occurrences).FirstOrDefault()
        };
    }
    
    private List<ErrorPattern> DetectIntervalPatterns(AlignmentResult alignment)
    {
        var patterns = new List<ErrorPattern>();
        var intervalErrors = new Dictionary<int, List<AlignedNotePair>>();
        
        // Find wrong notes and categorize by interval error
        foreach (var pair in alignment.Pairs.Where(p => !p.IsExactPitchMatch && !p.IsOctaveError))
        {
            int interval = pair.PitchDifference;
            
            if (!intervalErrors.ContainsKey(interval))
                intervalErrors[interval] = new List<AlignedNotePair>();
            intervalErrors[interval].Add(pair);
        }
        
        // Identify recurring interval errors
        foreach (var (interval, errors) in intervalErrors)
        {
            if (errors.Count >= MinPatternOccurrences)
            {
                string direction = interval > 0 ? "higher" : "lower";
                int semitones = Math.Abs(interval);
                string intervalName = GetIntervalName(semitones);
                
                patterns.Add(new ErrorPattern
                {
                    Type = ErrorPatternType.IntervalError,
                    Description = $"Consistently playing {intervalName} ({semitones} semitones) {direction} than written",
                    Occurrences = errors.Count,
                    Severity = semitones <= 2 ? PatternSeverity.Minor : PatternSeverity.Moderate,
                    AffectedMeasures = errors.Select(e => e.ScoreNote.Measure).Distinct().ToList(),
                    LikelyCause = semitones == 1 
                        ? "Possible accidental confusion (sharps/flats)" 
                        : semitones == 12 
                            ? "Octave displacement - check hand position"
                            : "Reading error or hand position shift issue",
                    PracticeRecommendation = "Practice the specific intervals slowly, saying note names aloud"
                });
            }
        }
        
        return patterns;
    }
    
    private List<ErrorPattern> DetectPositionPatterns(AlignmentResult alignment, Score score)
    {
        var patterns = new List<ErrorPattern>();
        
        // Detect errors at specific beat positions
        var errorsByBeat = alignment.Pairs
            .Where(p => !p.IsExactPitchMatch)
            .GroupBy(p => (int)p.ScoreNote.Beat)
            .ToDictionary(g => g.Key, g => g.ToList());
        
        foreach (var (beat, errors) in errorsByBeat)
        {
            if (errors.Count >= MinPatternOccurrences)
            {
                double errorRate = (double)errors.Count / 
                    alignment.Pairs.Count(p => (int)p.ScoreNote.Beat == beat);
                
                if (errorRate > 0.3)
                {
                    patterns.Add(new ErrorPattern
                    {
                        Type = ErrorPatternType.BeatPositionError,
                        Description = $"Frequent errors on beat {beat} ({errorRate:P0} error rate)",
                        Occurrences = errors.Count,
                        Severity = errorRate > 0.5 ? PatternSeverity.Significant : PatternSeverity.Moderate,
                        AffectedMeasures = errors.Select(e => e.ScoreNote.Measure).Distinct().ToList(),
                        LikelyCause = beat == 1 
                            ? "Downbeat accuracy issue - may be rushing into measures"
                            : "Weak beat attention - notes between main beats need focus",
                        PracticeRecommendation = $"Practice with emphasis on beat {beat}, counting aloud"
                    });
                }
            }
        }
        
        // Detect errors in specific registers
        var errorsByRegister = alignment.Pairs
            .Where(p => !p.IsExactPitchMatch)
            .GroupBy(p => GetRegister(p.ScoreNote.Pitch))
            .ToDictionary(g => g.Key, g => g.ToList());
        
        foreach (var (register, errors) in errorsByRegister)
        {
            int totalInRegister = alignment.Pairs.Count(p => GetRegister(p.ScoreNote.Pitch) == register);
            double errorRate = totalInRegister > 0 ? (double)errors.Count / totalInRegister : 0;
            
            if (errors.Count >= MinPatternOccurrences && errorRate > 0.25)
            {
                patterns.Add(new ErrorPattern
                {
                    Type = ErrorPatternType.RegisterError,
                    Description = $"Higher error rate in {register} register ({errorRate:P0})",
                    Occurrences = errors.Count,
                    Severity = PatternSeverity.Moderate,
                    AffectedMeasures = errors.Select(e => e.ScoreNote.Measure).Distinct().ToList(),
                    LikelyCause = register == "high" 
                        ? "Difficulty reading ledger lines or right hand position"
                        : register == "low"
                            ? "Bass clef reading or left hand position issue"
                            : "Middle register coordination",
                    PracticeRecommendation = $"Practice {register} register passages hands separately"
                });
            }
        }
        
        return patterns;
    }
    
    private List<ErrorPattern> DetectFingeringPatterns(AlignmentResult alignment, Score score)
    {
        var patterns = new List<ErrorPattern>();
        
        // Detect thumb-under/finger-over passage errors (scales, arpeggios)
        var sortedPairs = alignment.Pairs.OrderBy(p => p.ScoreNote.StartTick).ToList();
        var scalePassageErrors = new List<int>();
        
        for (int i = 3; i < sortedPairs.Count - 1; i++)
        {
            // Detect scale-like passages (consecutive steps)
            var recent = sortedPairs.Skip(i - 3).Take(4).ToList();
            bool isScalePassage = true;
            
            for (int j = 1; j < recent.Count; j++)
            {
                int interval = Math.Abs(recent[j].ScoreNote.Pitch - recent[j - 1].ScoreNote.Pitch);
                if (interval > 2)
                {
                    isScalePassage = false;
                    break;
                }
            }
            
            if (isScalePassage && !recent.Last().IsExactPitchMatch)
            {
                scalePassageErrors.Add(recent.Last().ScoreNote.Measure);
            }
        }
        
        if (scalePassageErrors.Count >= MinPatternOccurrences)
        {
            patterns.Add(new ErrorPattern
            {
                Type = ErrorPatternType.FingeringPattern,
                Description = $"Errors during scale/stepwise passages ({scalePassageErrors.Count} occurrences)",
                Occurrences = scalePassageErrors.Count,
                Severity = PatternSeverity.Moderate,
                AffectedMeasures = scalePassageErrors.Distinct().ToList(),
                LikelyCause = "Thumb-under or finger-crossing technique issue",
                PracticeRecommendation = "Practice scales with correct fingering, slowly increasing speed"
            });
        }
        
        // Detect large leap errors
        var leapErrors = new List<(AlignedNotePair pair, int leap)>();
        
        for (int i = 1; i < sortedPairs.Count; i++)
        {
            int leap = Math.Abs(sortedPairs[i].ScoreNote.Pitch - sortedPairs[i - 1].ScoreNote.Pitch);
            if (leap > 7 && !sortedPairs[i].IsExactPitchMatch) // More than a fifth
            {
                leapErrors.Add((sortedPairs[i], leap));
            }
        }
        
        if (leapErrors.Count >= MinPatternOccurrences)
        {
            patterns.Add(new ErrorPattern
            {
                Type = ErrorPatternType.LeapError,
                Description = $"Difficulty with large leaps ({leapErrors.Count} errors on jumps > 5th)",
                Occurrences = leapErrors.Count,
                Severity = PatternSeverity.Significant,
                AffectedMeasures = leapErrors.Select(e => e.pair.ScoreNote.Measure).Distinct().ToList(),
                LikelyCause = "Hand/arm movement coordination for position shifts",
                PracticeRecommendation = "Practice leaps in isolation - look at target, then play"
            });
        }
        
        return patterns;
    }
    
    private List<ErrorPattern> DetectRhythmPatterns(AlignmentResult alignment, RhythmResult? rhythmResult)
    {
        var patterns = new List<ErrorPattern>();
        
        if (rhythmResult == null) return patterns;
        
        // Detect rushing pattern
        var earlyNotes = rhythmResult.TimingErrors
            .Where(t => t.Severity == TimingSeverity.VeryEarly || t.Severity == TimingSeverity.SlightlyEarly)
            .ToList();
        
        if (earlyNotes.Count >= MinPatternOccurrences)
        {
            double avgEarly = earlyNotes.Average(n => n.DeviationMs);
            
            patterns.Add(new ErrorPattern
            {
                Type = ErrorPatternType.RushingPattern,
                Description = $"Tendency to rush - {earlyNotes.Count} notes played early (avg {Math.Abs(avgEarly):F0}ms)",
                Occurrences = earlyNotes.Count,
                Severity = Math.Abs(avgEarly) > 80 ? PatternSeverity.Significant : PatternSeverity.Moderate,
                AffectedMeasures = earlyNotes.Select(n => n.Measure).Distinct().ToList(),
                LikelyCause = "Anxiety or excitement causing anticipation of beats",
                PracticeRecommendation = "Practice with metronome, consciously waiting for the beat"
            });
        }
        
        // Detect dragging pattern
        var lateNotes = rhythmResult.TimingErrors
            .Where(t => t.Severity == TimingSeverity.VeryLate || t.Severity == TimingSeverity.SlightlyLate)
            .ToList();
        
        if (lateNotes.Count >= MinPatternOccurrences)
        {
            double avgLate = lateNotes.Average(n => n.DeviationMs);
            
            patterns.Add(new ErrorPattern
            {
                Type = ErrorPatternType.DraggingPattern,
                Description = $"Tendency to drag - {lateNotes.Count} notes played late (avg {avgLate:F0}ms)",
                Occurrences = lateNotes.Count,
                Severity = avgLate > 80 ? PatternSeverity.Significant : PatternSeverity.Moderate,
                AffectedMeasures = lateNotes.Select(n => n.Measure).Distinct().ToList(),
                LikelyCause = "Hesitation or uncertainty about notes",
                PracticeRecommendation = "Build confidence through slower practice, then maintain tempo"
            });
        }
        
        return patterns;
    }
    
    private List<ErrorPattern> DetectChordPatterns(AlignmentResult alignment, Score score)
    {
        var patterns = new List<ErrorPattern>();
        
        // Find chords (notes at same time) and their error rates
        var notesByTime = alignment.Pairs
            .GroupBy(p => p.ScoreNote.StartTick)
            .Where(g => g.Count() >= 3) // At least 3 notes = chord
            .ToList();
        
        var chordErrors = notesByTime
            .Where(g => g.Any(p => !p.IsExactPitchMatch))
            .ToList();
        
        if (chordErrors.Count >= MinPatternOccurrences)
        {
            // Analyze which chord tones are missed most
            var missedPositions = new Dictionary<string, int>
            {
                ["top"] = 0,
                ["middle"] = 0,
                ["bottom"] = 0
            };
            
            foreach (var chord in chordErrors)
            {
                var sorted = chord.OrderBy(n => n.ScoreNote.Pitch).ToList();
                for (int i = 0; i < sorted.Count; i++)
                {
                    if (!sorted[i].IsExactPitchMatch)
                    {
                        if (i == 0) missedPositions["bottom"]++;
                        else if (i == sorted.Count - 1) missedPositions["top"]++;
                        else missedPositions["middle"]++;
                    }
                }
            }
            
            var mostMissed = missedPositions.OrderByDescending(kv => kv.Value).First();
            
            patterns.Add(new ErrorPattern
            {
                Type = ErrorPatternType.ChordError,
                Description = $"Chord accuracy issues - {chordErrors.Count} chords with errors, especially {mostMissed.Key} notes",
                Occurrences = chordErrors.Count,
                Severity = PatternSeverity.Moderate,
                AffectedMeasures = chordErrors.Select(c => c.First().ScoreNote.Measure).Distinct().ToList(),
                LikelyCause = mostMissed.Key == "top" 
                    ? "Difficulty voicing melody in chords"
                    : mostMissed.Key == "bottom"
                        ? "Bass note accuracy in chord voicing"
                        : "Inner voice accuracy",
                PracticeRecommendation = "Practice chords as broken chords first, then blocked"
            });
        }
        
        return patterns;
    }
    
    private List<ErrorPattern> DetectPassagePatterns(AlignmentResult alignment, Score score)
    {
        var patterns = new List<ErrorPattern>();
        
        // Find measures with high error density
        var errorsByMeasure = alignment.Pairs
            .GroupBy(p => p.ScoreNote.Measure)
            .Select(g => new
            {
                Measure = g.Key,
                Total = g.Count(),
                Errors = g.Count(p => !p.IsExactPitchMatch),
                ErrorRate = g.Count(p => !p.IsExactPitchMatch) / (double)g.Count()
            })
            .Where(m => m.ErrorRate > 0.4 && m.Errors >= 2)
            .OrderByDescending(m => m.ErrorRate)
            .ToList();
        
        // Find consecutive difficult measures
        var difficultPassages = new List<(int start, int end, double avgErrorRate)>();
        
        for (int i = 0; i < errorsByMeasure.Count; i++)
        {
            int start = errorsByMeasure[i].Measure;
            int end = start;
            double totalRate = errorsByMeasure[i].ErrorRate;
            int count = 1;
            
            // Extend passage
            for (int j = i + 1; j < errorsByMeasure.Count; j++)
            {
                if (errorsByMeasure[j].Measure == end + 1 || errorsByMeasure[j].Measure == end + 2)
                {
                    end = errorsByMeasure[j].Measure;
                    totalRate += errorsByMeasure[j].ErrorRate;
                    count++;
                }
                else break;
            }
            
            if (end > start)
            {
                difficultPassages.Add((start, end, totalRate / count));
            }
        }
        
        foreach (var passage in difficultPassages.Take(3))
        {
            patterns.Add(new ErrorPattern
            {
                Type = ErrorPatternType.DifficultPassage,
                Description = $"Consistently difficult passage: measures {passage.start}-{passage.end} ({passage.avgErrorRate:P0} error rate)",
                Occurrences = passage.end - passage.start + 1,
                Severity = passage.avgErrorRate > 0.6 ? PatternSeverity.Significant : PatternSeverity.Moderate,
                AffectedMeasures = Enumerable.Range(passage.start, passage.end - passage.start + 1).ToList(),
                LikelyCause = "Technical difficulty or unfamiliar pattern",
                PracticeRecommendation = $"Isolate measures {passage.start}-{passage.end} and practice hands separately at slow tempo"
            });
        }
        
        return patterns;
    }
    
    private PatternStatistics CalculateStatistics(List<ErrorPattern> patterns, AlignmentResult alignment)
    {
        return new PatternStatistics
        {
            TotalPatterns = patterns.Count,
            CriticalPatterns = patterns.Count(p => p.Severity == PatternSeverity.Critical),
            SignificantPatterns = patterns.Count(p => p.Severity == PatternSeverity.Significant),
            ModeratePatterns = patterns.Count(p => p.Severity == PatternSeverity.Moderate),
            MinorPatterns = patterns.Count(p => p.Severity == PatternSeverity.Minor),
            MostCommonType = patterns.GroupBy(p => p.Type)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault()?.Key ?? ErrorPatternType.Unknown,
            AffectedMeasureCount = patterns.SelectMany(p => p.AffectedMeasures).Distinct().Count()
        };
    }
    
    private List<string> GenerateInsights(List<ErrorPattern> patterns, PatternStatistics stats)
    {
        var insights = new List<string>();
        
        if (patterns.Count == 0)
        {
            insights.Add("No recurring error patterns detected - errors appear random or isolated.");
            return insights;
        }
        
        if (stats.CriticalPatterns > 0 || stats.SignificantPatterns > 2)
        {
            insights.Add("Several significant patterns found - focused practice on these areas will yield quick improvement.");
        }
        
        var topPattern = patterns.OrderByDescending(p => p.Occurrences).First();
        insights.Add($"Most frequent issue: {topPattern.Description}");
        
        if (patterns.Any(p => p.Type == ErrorPatternType.RushingPattern))
        {
            insights.Add("You tend to rush - slow, metronome practice will help build a steady internal pulse.");
        }
        
        if (patterns.Any(p => p.Type == ErrorPatternType.IntervalError))
        {
            insights.Add("Interval accuracy issues suggest sight-reading practice would be beneficial.");
        }
        
        if (patterns.Any(p => p.Type == ErrorPatternType.LeapError))
        {
            insights.Add("Large leaps are challenging - practice looking ahead to the target note.");
        }
        
        return insights;
    }
    
    private string GetIntervalName(int semitones) => semitones switch
    {
        1 => "a half step",
        2 => "a whole step",
        3 => "a minor 3rd",
        4 => "a major 3rd",
        5 => "a 4th",
        7 => "a 5th",
        12 => "an octave",
        _ => $"{semitones} semitones"
    };
    
    private string GetRegister(int pitch) => pitch switch
    {
        < 48 => "low",
        < 72 => "middle",
        _ => "high"
    };
}

/// <summary>
/// Types of error patterns.
/// </summary>
public enum ErrorPatternType
{
    Unknown,
    IntervalError,        // Consistent wrong intervals
    BeatPositionError,    // Errors on specific beats
    RegisterError,        // Errors in high/low register
    FingeringPattern,     // Scale/arpeggio fingering issues
    LeapError,            // Errors on large jumps
    RushingPattern,       // Consistent early playing
    DraggingPattern,      // Consistent late playing
    ChordError,           // Chord voicing issues
    DifficultPassage      // Consistently problematic measures
}

/// <summary>
/// Severity of an error pattern.
/// </summary>
public enum PatternSeverity
{
    Minor,
    Moderate,
    Significant,
    Critical
}

/// <summary>
/// A detected error pattern.
/// </summary>
public record ErrorPattern
{
    public ErrorPatternType Type { get; init; }
    public string Description { get; init; } = "";
    public int Occurrences { get; init; }
    public PatternSeverity Severity { get; init; }
    public List<int> AffectedMeasures { get; init; } = [];
    public string LikelyCause { get; init; } = "";
    public string PracticeRecommendation { get; init; } = "";
}

/// <summary>
/// Statistics about detected patterns.
/// </summary>
public record PatternStatistics
{
    public int TotalPatterns { get; init; }
    public int CriticalPatterns { get; init; }
    public int SignificantPatterns { get; init; }
    public int ModeratePatterns { get; init; }
    public int MinorPatterns { get; init; }
    public ErrorPatternType MostCommonType { get; init; }
    public int AffectedMeasureCount { get; init; }
}

/// <summary>
/// Complete error pattern analysis result.
/// </summary>
public record ErrorPatternAnalysis
{
    public IReadOnlyList<ErrorPattern> Patterns { get; init; } = [];
    public PatternStatistics Statistics { get; init; } = new();
    public IReadOnlyList<string> Insights { get; init; } = [];
    public int TotalErrorsAnalyzed { get; init; }
    public int PatternsIdentified { get; init; }
    public ErrorPattern? TopPattern { get; init; }
}

