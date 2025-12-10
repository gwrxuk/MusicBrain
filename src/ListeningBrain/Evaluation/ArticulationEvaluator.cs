using ListeningBrain.Alignment;
using ListeningBrain.Core.Models;

namespace ListeningBrain.Evaluation;

/// <summary>
/// Evaluates articulation: staccato, legato, accents, and other touch-related aspects.
/// Analyzes note durations and velocity patterns to detect articulation compliance.
/// </summary>
public class ArticulationEvaluator : IEvaluator<ArticulationResult>
{
    public string Name => "Articulation Evaluator";
    
    /// <summary>
    /// Staccato notes should be this fraction of their written duration or less.
    /// </summary>
    public double StaccatoThreshold { get; init; } = 0.5;
    
    /// <summary>
    /// Legato notes should connect with this much overlap (negative = gap).
    /// </summary>
    public double LegatoOverlapMs { get; init; } = 20;
    
    /// <summary>
    /// Accent notes should be this much louder than surrounding notes.
    /// </summary>
    public int AccentVelocityBoost { get; init; } = 15;
    
    public ArticulationResult Evaluate(AlignmentResult alignment, Score score, Performance performance)
    {
        var issues = new List<EvaluationIssue>();
        
        if (alignment.Pairs.Count == 0)
        {
            return CreateEmptyResult();
        }
        
        // Analyze staccato execution
        var staccatoAnalysis = AnalyzeStaccato(alignment.Pairs);
        
        // Analyze legato execution
        var legatoAnalysis = AnalyzeLegato(alignment.Pairs, performance);
        
        // Analyze accents
        var accentAnalysis = AnalyzeAccents(alignment.Pairs);
        
        // Analyze overall note duration consistency
        var durationAnalysis = AnalyzeDurationConsistency(alignment.Pairs);
        
        // Compile issues
        issues.AddRange(staccatoAnalysis.Issues);
        issues.AddRange(legatoAnalysis.Issues);
        issues.AddRange(accentAnalysis.Issues);
        issues.AddRange(durationAnalysis.Issues);
        
        // Calculate score
        double articulationScore = CalculateScore(
            staccatoAnalysis, legatoAnalysis, accentAnalysis, durationAnalysis);
        
        return new ArticulationResult
        {
            Score = articulationScore,
            Summary = GenerateSummary(articulationScore, staccatoAnalysis, legatoAnalysis),
            Issues = issues.OrderByDescending(i => i.Severity).ToList(),
            StaccatoAccuracy = staccatoAnalysis.Accuracy,
            StaccatoNotes = staccatoAnalysis.TotalNotes,
            StaccatoCorrect = staccatoAnalysis.CorrectNotes,
            LegatoAccuracy = legatoAnalysis.Accuracy,
            LegatoConnections = legatoAnalysis.TotalConnections,
            LegatoCorrect = legatoAnalysis.CorrectConnections,
            AccentAccuracy = accentAnalysis.Accuracy,
            AccentNotes = accentAnalysis.TotalNotes,
            AccentsCorrect = accentAnalysis.CorrectNotes,
            AverageDurationRatio = durationAnalysis.AverageRatio,
            DurationConsistency = durationAnalysis.Consistency,
            ArticulationDetails = ExtractArticulationDetails(alignment.Pairs)
        };
    }
    
    private StaccatoAnalysis AnalyzeStaccato(IReadOnlyList<AlignedNotePair> pairs)
    {
        var issues = new List<EvaluationIssue>();
        
        var staccatoPairs = pairs
            .Where(p => p.ScoreNote.Articulation == Articulation.Staccato || 
                        p.ScoreNote.Articulation == Articulation.Staccatissimo)
            .ToList();
        
        if (staccatoPairs.Count == 0)
        {
            return new StaccatoAnalysis(1.0, 0, 0, issues);
        }
        
        int correctCount = 0;
        var problemMeasures = new List<int>();
        
        foreach (var pair in staccatoPairs)
        {
            double expectedDuration = pair.ScoreNote.DurationMs;
            double actualDuration = pair.PerformanceNote.DurationMs;
            double ratio = actualDuration / expectedDuration;
            
            double threshold = pair.ScoreNote.Articulation == Articulation.Staccatissimo 
                ? StaccatoThreshold * 0.5 
                : StaccatoThreshold;
            
            if (ratio <= threshold + 0.1) // Allow some tolerance
            {
                correctCount++;
            }
            else
            {
                problemMeasures.Add(pair.ScoreNote.Measure);
            }
        }
        
        double accuracy = (double)correctCount / staccatoPairs.Count;
        
        if (accuracy < 0.7)
        {
            var measures = problemMeasures.Distinct().Take(3);
            issues.Add(new EvaluationIssue
            {
                Severity = IssueSeverity.Moderate,
                Type = IssueType.WrongDuration,
                Description = $"Staccato notes not short enough - holding too long",
                Measure = problemMeasures.FirstOrDefault(),
                Suggestion = "Release staccato notes quickly - think of touching a hot surface"
            });
        }
        
        return new StaccatoAnalysis(accuracy, staccatoPairs.Count, correctCount, issues);
    }
    
    private LegatoAnalysis AnalyzeLegato(IReadOnlyList<AlignedNotePair> pairs, Performance performance)
    {
        var issues = new List<EvaluationIssue>();
        
        // Find legato passages
        var legatoPairs = pairs
            .Where(p => p.ScoreNote.Articulation == Articulation.Legato)
            .OrderBy(p => p.ScoreNote.StartTick)
            .ToList();
        
        if (legatoPairs.Count < 2)
        {
            // Check for general smoothness even without explicit legato markings
            return AnalyzeGeneralSmoothness(pairs, issues);
        }
        
        int totalConnections = 0;
        int correctConnections = 0;
        var gapLocations = new List<int>();
        
        for (int i = 0; i < legatoPairs.Count - 1; i++)
        {
            var current = legatoPairs[i].PerformanceNote;
            var next = legatoPairs[i + 1].PerformanceNote;
            
            // Check if notes overlap or have minimal gap
            double gap = next.StartTimeMs - (current.StartTimeMs + current.DurationMs);
            
            totalConnections++;
            
            if (gap <= LegatoOverlapMs)
            {
                correctConnections++;
            }
            else
            {
                gapLocations.Add(legatoPairs[i].ScoreNote.Measure);
            }
        }
        
        double accuracy = totalConnections > 0 ? (double)correctConnections / totalConnections : 1.0;
        
        if (accuracy < 0.7 && totalConnections > 0)
        {
            issues.Add(new EvaluationIssue
            {
                Severity = IssueSeverity.Moderate,
                Type = IssueType.WrongDuration,
                Description = "Legato passages have gaps - notes not connecting smoothly",
                Measure = gapLocations.FirstOrDefault(),
                Suggestion = "Hold each note until the next one sounds - slight overlap is good for legato"
            });
        }
        
        return new LegatoAnalysis(accuracy, totalConnections, correctConnections, issues);
    }
    
    private LegatoAnalysis AnalyzeGeneralSmoothness(
        IReadOnlyList<AlignedNotePair> pairs, 
        List<EvaluationIssue> existingIssues)
    {
        // Analyze general note connectivity
        var sortedPairs = pairs.OrderBy(p => p.ScoreNote.StartTick).ToList();
        
        int totalTransitions = 0;
        int smoothTransitions = 0;
        
        for (int i = 0; i < sortedPairs.Count - 1; i++)
        {
            var current = sortedPairs[i].PerformanceNote;
            var next = sortedPairs[i + 1].PerformanceNote;
            
            // Skip if notes are at the same time (chord)
            double expectedGap = sortedPairs[i + 1].ScoreNote.StartTimeMs - 
                                 (sortedPairs[i].ScoreNote.StartTimeMs + sortedPairs[i].ScoreNote.DurationMs);
            
            if (expectedGap > 50) continue; // Notes shouldn't connect anyway
            
            double actualGap = next.StartTimeMs - (current.StartTimeMs + current.DurationMs);
            
            totalTransitions++;
            if (actualGap < 100) // Reasonable connection
            {
                smoothTransitions++;
            }
        }
        
        double smoothness = totalTransitions > 0 
            ? (double)smoothTransitions / totalTransitions 
            : 1.0;
        
        return new LegatoAnalysis(smoothness, totalTransitions, smoothTransitions, existingIssues);
    }
    
    private AccentAnalysis AnalyzeAccents(IReadOnlyList<AlignedNotePair> pairs)
    {
        var issues = new List<EvaluationIssue>();
        
        var accentPairs = pairs
            .Where(p => p.ScoreNote.Articulation == Articulation.Accent || 
                        p.ScoreNote.Articulation == Articulation.Marcato)
            .ToList();
        
        if (accentPairs.Count == 0)
        {
            return new AccentAnalysis(1.0, 0, 0, issues);
        }
        
        int correctCount = 0;
        var missedAccents = new List<int>();
        
        foreach (var accentPair in accentPairs)
        {
            // Find surrounding notes to compare
            var surroundingNotes = pairs
                .Where(p => p.ScoreNote.Measure == accentPair.ScoreNote.Measure &&
                            p.ScoreNote.Articulation != Articulation.Accent &&
                            p.ScoreNote.Articulation != Articulation.Marcato)
                .Select(p => p.PerformanceNote.Velocity)
                .ToList();
            
            if (surroundingNotes.Count == 0)
            {
                correctCount++; // Can't compare, give benefit of doubt
                continue;
            }
            
            double avgSurrounding = surroundingNotes.Average();
            int accentVelocity = accentPair.PerformanceNote.Velocity;
            
            int requiredBoost = accentPair.ScoreNote.Articulation == Articulation.Marcato 
                ? AccentVelocityBoost + 5 
                : AccentVelocityBoost;
            
            if (accentVelocity >= avgSurrounding + requiredBoost * 0.7)
            {
                correctCount++;
            }
            else
            {
                missedAccents.Add(accentPair.ScoreNote.Measure);
            }
        }
        
        double accuracy = (double)correctCount / accentPairs.Count;
        
        if (accuracy < 0.7)
        {
            issues.Add(new EvaluationIssue
            {
                Severity = IssueSeverity.Moderate,
                Type = IssueType.MissedAccent,
                Description = "Accented notes not emphasized enough",
                Measure = missedAccents.FirstOrDefault(),
                Suggestion = "Give accented notes a stronger attack - they should stand out"
            });
        }
        
        return new AccentAnalysis(accuracy, accentPairs.Count, correctCount, issues);
    }
    
    private DurationAnalysis AnalyzeDurationConsistency(IReadOnlyList<AlignedNotePair> pairs)
    {
        var issues = new List<EvaluationIssue>();
        
        // Calculate duration ratios (actual / expected)
        var ratios = pairs
            .Where(p => p.ScoreNote.DurationMs > 0)
            .Select(p => p.PerformanceNote.DurationMs / p.ScoreNote.DurationMs)
            .ToList();
        
        if (ratios.Count == 0)
        {
            return new DurationAnalysis(1.0, 1.0, issues);
        }
        
        double avgRatio = ratios.Average();
        double stdDev = CalculateStdDev(ratios);
        double consistency = Math.Max(0, 1 - stdDev);
        
        if (avgRatio < 0.7)
        {
            issues.Add(new EvaluationIssue
            {
                Severity = IssueSeverity.Moderate,
                Type = IssueType.WrongDuration,
                Description = "Notes consistently too short - not holding full value",
                Suggestion = "Count out the full duration of each note - don't rush releases"
            });
        }
        else if (avgRatio > 1.3)
        {
            issues.Add(new EvaluationIssue
            {
                Severity = IssueSeverity.Minor,
                Type = IssueType.WrongDuration,
                Description = "Notes held longer than written",
                Suggestion = "Release notes on time - unless pedaling, notes shouldn't overlap"
            });
        }
        
        if (consistency < 0.7)
        {
            issues.Add(new EvaluationIssue
            {
                Severity = IssueSeverity.Minor,
                Type = IssueType.UnevenRhythm,
                Description = "Inconsistent note durations - some short, some long",
                Suggestion = "Practice with focus on consistent note lengths"
            });
        }
        
        return new DurationAnalysis(avgRatio, consistency, issues);
    }
    
    private List<ArticulationDetail> ExtractArticulationDetails(IReadOnlyList<AlignedNotePair> pairs)
    {
        return pairs
            .Where(p => p.ScoreNote.Articulation != Articulation.Normal)
            .Select(p => new ArticulationDetail
            {
                Measure = p.ScoreNote.Measure,
                Beat = p.ScoreNote.Beat,
                ExpectedArticulation = p.ScoreNote.Articulation,
                NoteName = p.ScoreNote.NoteName,
                ExpectedDurationMs = p.ScoreNote.DurationMs,
                ActualDurationMs = p.PerformanceNote.DurationMs,
                DurationRatio = p.ScoreNote.DurationMs > 0 
                    ? p.PerformanceNote.DurationMs / p.ScoreNote.DurationMs 
                    : 1.0,
                VelocityDifference = p.PerformanceNote.Velocity - p.ScoreNote.Velocity
            })
            .ToList();
    }
    
    private double CalculateScore(
        StaccatoAnalysis staccato,
        LegatoAnalysis legato,
        AccentAnalysis accent,
        DurationAnalysis duration)
    {
        double score = 100;
        
        // Weighted average based on how many of each type exist
        double totalWeight = 0;
        double weightedSum = 0;
        
        if (staccato.TotalNotes > 0)
        {
            weightedSum += staccato.Accuracy * 25;
            totalWeight += 25;
        }
        
        if (legato.TotalConnections > 0)
        {
            weightedSum += legato.Accuracy * 25;
            totalWeight += 25;
        }
        
        if (accent.TotalNotes > 0)
        {
            weightedSum += accent.Accuracy * 25;
            totalWeight += 25;
        }
        
        // Duration consistency always counts
        weightedSum += duration.Consistency * 25;
        totalWeight += 25;
        
        if (totalWeight > 0)
        {
            score = (weightedSum / totalWeight) * 100;
        }
        
        return Math.Max(0, Math.Min(100, score));
    }
    
    private string GenerateSummary(double score, StaccatoAnalysis staccato, LegatoAnalysis legato)
    {
        if (score >= 90)
            return "Excellent articulation! Your touch and note shaping are very musical.";
        if (score >= 80)
            return "Good articulation control with room for refinement.";
        if (score >= 70)
        {
            if (staccato.Accuracy < 0.7)
                return "Work on making staccato notes shorter and more detached.";
            if (legato.Accuracy < 0.7)
                return "Focus on connecting notes smoothly in legato passages.";
            return "Fair articulation - continue developing your touch variety.";
        }
        return "Articulation needs significant attention - practice different touch types.";
    }
    
    private double CalculateStdDev(List<double> values)
    {
        if (values.Count <= 1) return 0;
        double avg = values.Average();
        return Math.Sqrt(values.Sum(v => Math.Pow(v - avg, 2)) / (values.Count - 1));
    }
    
    private ArticulationResult CreateEmptyResult() => new()
    {
        Score = 0,
        Summary = "No notes to evaluate.",
        Issues = [],
        ArticulationDetails = []
    };
}

// Analysis helper records
internal record StaccatoAnalysis(
    double Accuracy, 
    int TotalNotes, 
    int CorrectNotes, 
    List<EvaluationIssue> Issues);

internal record LegatoAnalysis(
    double Accuracy, 
    int TotalConnections, 
    int CorrectConnections, 
    List<EvaluationIssue> Issues);

internal record AccentAnalysis(
    double Accuracy, 
    int TotalNotes, 
    int CorrectNotes, 
    List<EvaluationIssue> Issues);

internal record DurationAnalysis(
    double AverageRatio, 
    double Consistency, 
    List<EvaluationIssue> Issues);

/// <summary>
/// Result of articulation evaluation.
/// </summary>
public record ArticulationResult : EvaluationResult
{
    public double StaccatoAccuracy { get; init; }
    public int StaccatoNotes { get; init; }
    public int StaccatoCorrect { get; init; }
    
    public double LegatoAccuracy { get; init; }
    public int LegatoConnections { get; init; }
    public int LegatoCorrect { get; init; }
    
    public double AccentAccuracy { get; init; }
    public int AccentNotes { get; init; }
    public int AccentsCorrect { get; init; }
    
    /// <summary>
    /// Average ratio of actual to expected note duration.
    /// </summary>
    public double AverageDurationRatio { get; init; }
    
    /// <summary>
    /// Consistency of note durations (0-1, higher = more consistent).
    /// </summary>
    public double DurationConsistency { get; init; }
    
    /// <summary>
    /// Detailed articulation analysis for each marked note.
    /// </summary>
    public IReadOnlyList<ArticulationDetail> ArticulationDetails { get; init; } = [];
}

/// <summary>
/// Detailed info about a single articulation marking.
/// </summary>
public record ArticulationDetail
{
    public int Measure { get; init; }
    public double Beat { get; init; }
    public Articulation ExpectedArticulation { get; init; }
    public string NoteName { get; init; } = "";
    public double ExpectedDurationMs { get; init; }
    public double ActualDurationMs { get; init; }
    public double DurationRatio { get; init; }
    public int VelocityDifference { get; init; }
}

