using ListeningBrain.Alignment;
using ListeningBrain.Core.Models;
using ListeningBrain.Evaluation;

namespace ListeningBrain.Feedback;

/// <summary>
/// Generates human-readable feedback from evaluation results.
/// Produces practice suggestions, identifies patterns, and prioritizes issues.
/// </summary>
public class FeedbackGenerator
{
    /// <summary>
    /// Maximum number of issues to include in summary.
    /// </summary>
    public int MaxIssuesInSummary { get; init; } = 5;
    
    /// <summary>
    /// Generates comprehensive feedback from all evaluation results.
    /// </summary>
    public FeedbackReport Generate(
        AlignmentResult alignment,
        NoteAccuracyResult? accuracyResult,
        RhythmResult? rhythmResult,
        TempoResult? tempoResult,
        Score score)
    {
        // Calculate overall score
        var scores = new List<double>();
        if (accuracyResult != null) scores.Add(accuracyResult.Score);
        if (rhythmResult != null) scores.Add(rhythmResult.Score);
        if (tempoResult != null) scores.Add(tempoResult.Score);
        
        double overallScore = scores.Count > 0 ? scores.Average() : 0;
        
        // Gather all issues
        var allIssues = new List<EvaluationIssue>();
        if (accuracyResult != null) allIssues.AddRange(accuracyResult.Issues);
        if (rhythmResult != null) allIssues.AddRange(rhythmResult.Issues);
        if (tempoResult != null) allIssues.AddRange(tempoResult.Issues);
        
        // Prioritize and deduplicate issues
        var prioritizedIssues = PrioritizeIssues(allIssues);
        
        // Generate per-measure reports
        var measureReports = GenerateMeasureReports(
            alignment, accuracyResult, rhythmResult, score);
        
        // Generate practice suggestions
        var suggestions = GeneratePracticeSuggestions(
            prioritizedIssues, measureReports, accuracyResult, rhythmResult, tempoResult);
        
        // Generate encouraging summary
        var summary = GenerateSummary(overallScore, accuracyResult, rhythmResult, tempoResult);
        
        // Identify strengths and weaknesses
        var (strengths, weaknesses) = IdentifyStrengthsAndWeaknesses(
            accuracyResult, rhythmResult, tempoResult);
        
        return new FeedbackReport
        {
            OverallScore = overallScore,
            OverallGrade = GetGrade(overallScore),
            Summary = summary,
            PrioritizedIssues = prioritizedIssues.Take(MaxIssuesInSummary).ToList(),
            AllIssues = prioritizedIssues,
            MeasureReports = measureReports,
            PracticeSuggestions = suggestions,
            Strengths = strengths,
            AreasForImprovement = weaknesses,
            NoteAccuracyScore = accuracyResult?.Score,
            RhythmScore = rhythmResult?.Score,
            TempoScore = tempoResult?.Score,
            PieceTitle = score.Title,
            TotalMeasures = score.TotalMeasures,
            ProblemMeasures = measureReports.Where(m => m.OverallScore < 70).Select(m => m.Measure).ToList()
        };
    }
    
    /// <summary>
    /// Generates quick feedback for a single issue (real-time mode).
    /// </summary>
    public string GenerateQuickFeedback(EvaluationIssue issue)
    {
        return issue.Type switch
        {
            IssueType.WrongNote => $"♪ Wrong note in m.{issue.Measure}",
            IssueType.MissedNote => $"♪ Missed note in m.{issue.Measure}",
            IssueType.RushedNote => $"⏱ Too early!",
            IssueType.DraggedNote => $"⏱ Too late!",
            IssueType.TempoUnstable => $"⏱ Watch your tempo",
            _ => issue.Description
        };
    }
    
    private List<EvaluationIssue> PrioritizeIssues(List<EvaluationIssue> issues)
    {
        // Group by measure to avoid repeating the same measure
        var byMeasure = issues
            .Where(i => i.Measure.HasValue)
            .GroupBy(i => i.Measure!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());
        
        var result = new List<EvaluationIssue>();
        
        // First, add critical/significant issues
        result.AddRange(issues
            .Where(i => i.Severity >= IssueSeverity.Significant)
            .OrderByDescending(i => i.Severity)
            .ThenBy(i => i.Measure));
        
        // Then add moderate issues, but limit per measure
        var addedMeasures = new HashSet<int>(result.Where(i => i.Measure.HasValue).Select(i => i.Measure!.Value));
        
        foreach (var issue in issues
            .Where(i => i.Severity == IssueSeverity.Moderate && 
                        (!i.Measure.HasValue || !addedMeasures.Contains(i.Measure.Value)))
            .OrderBy(i => i.Measure))
        {
            result.Add(issue);
            if (issue.Measure.HasValue)
                addedMeasures.Add(issue.Measure.Value);
        }
        
        // Add global issues (no measure)
        result.AddRange(issues
            .Where(i => !i.Measure.HasValue && !result.Contains(i))
            .OrderByDescending(i => i.Severity));
        
        return result.Distinct().ToList();
    }
    
    private List<MeasureReport> GenerateMeasureReports(
        AlignmentResult alignment,
        NoteAccuracyResult? accuracy,
        RhythmResult? rhythm,
        Score score)
    {
        var reports = new List<MeasureReport>();
        
        for (int m = 1; m <= score.TotalMeasures; m++)
        {
            var measurePairs = alignment.Pairs.Where(p => p.ScoreNote.Measure == m).ToList();
            var measureMissed = alignment.MissedNotes.Where(n => n.ExpectedNote.Measure == m).ToList();
            
            int expectedNotes = score.Notes.Count(n => n.Measure == m);
            int correctNotes = measurePairs.Count(p => p.IsExactPitchMatch);
            
            double accuracyScore = expectedNotes > 0 ? (double)correctNotes / expectedNotes * 100 : 100;
            
            double rhythmScore = 100;
            if (rhythm != null)
            {
                var measureTiming = rhythm.MeasureBreakdown.FirstOrDefault(mr => mr.Measure == m);
                if (measureTiming != null)
                {
                    rhythmScore = measureTiming.OnTimePercent;
                }
            }
            
            // Collect issues for this measure
            var issues = new List<string>();
            
            foreach (var missed in measureMissed)
            {
                issues.Add($"Missed {missed.ExpectedNote.NoteName}");
            }
            
            foreach (var pair in measurePairs.Where(p => !p.IsExactPitchMatch))
            {
                if (pair.IsOctaveError)
                    issues.Add($"Octave error on {pair.ScoreNote.NoteName}");
                else
                    issues.Add($"Wrong note: {pair.PerformanceNote.NoteName} instead of {pair.ScoreNote.NoteName}");
            }
            
            double overallScore = (accuracyScore + rhythmScore) / 2;
            
            reports.Add(new MeasureReport
            {
                Measure = m,
                OverallScore = overallScore,
                AccuracyScore = accuracyScore,
                RhythmScore = rhythmScore,
                ExpectedNotes = expectedNotes,
                CorrectNotes = correctNotes,
                MissedNotes = measureMissed.Count,
                WrongNotes = measurePairs.Count(p => !p.IsExactPitchMatch && !p.IsOctaveError),
                Issues = issues,
                NeedsPractice = overallScore < 80
            });
        }
        
        return reports;
    }
    
    private List<PracticeSuggestion> GeneratePracticeSuggestions(
        List<EvaluationIssue> issues,
        List<MeasureReport> measureReports,
        NoteAccuracyResult? accuracy,
        RhythmResult? rhythm,
        TempoResult? tempo)
    {
        var suggestions = new List<PracticeSuggestion>();
        
        // Identify problem measures
        var problemMeasures = measureReports
            .Where(m => m.OverallScore < 80)
            .OrderBy(m => m.OverallScore)
            .Take(5)
            .ToList();
        
        if (problemMeasures.Any())
        {
            var measureList = string.Join(", ", problemMeasures.Select(m => m.Measure));
            suggestions.Add(new PracticeSuggestion
            {
                Priority = 1,
                Title = "Focus on Problem Measures",
                Description = $"Measures {measureList} need extra attention.",
                DetailedSteps = [
                    $"Isolate measures {measureList}",
                    "Practice hands separately at half tempo",
                    "Gradually increase tempo while maintaining accuracy",
                    "Connect with surrounding measures once stable"
                ],
                ExpectedTimeMinutes = problemMeasures.Count * 5
            });
        }
        
        // Note accuracy suggestions
        if (accuracy != null && accuracy.Score < 90)
        {
            if (accuracy.MissedNotes > accuracy.TotalExpectedNotes * 0.1)
            {
                suggestions.Add(new PracticeSuggestion
                {
                    Priority = 2,
                    Title = "Address Missed Notes",
                    Description = $"You missed {accuracy.MissedNotes} notes. Focus on reading all notes carefully.",
                    DetailedSteps = [
                        "Review the score away from the piano",
                        "Mark difficult passages",
                        "Practice slowly, watching your hands",
                        "Use a pencil to trace the notes as you play"
                    ],
                    ExpectedTimeMinutes = 15
                });
            }
            
            if (accuracy.OctaveErrors > 2)
            {
                suggestions.Add(new PracticeSuggestion
                {
                    Priority = 3,
                    Title = "Check Hand Position",
                    Description = $"{accuracy.OctaveErrors} octave errors suggest hand position issues.",
                    DetailedSteps = [
                        "Review the clef and ledger lines in the score",
                        "Practice finding your starting position with eyes closed",
                        "Look at the keyboard less, trust your hand position"
                    ],
                    ExpectedTimeMinutes = 10
                });
            }
        }
        
        // Rhythm suggestions
        if (rhythm != null && rhythm.Score < 85)
        {
            suggestions.Add(new PracticeSuggestion
            {
                Priority = 2,
                Title = "Improve Rhythmic Precision",
                Description = $"Average timing error is {rhythm.AbsoluteTimingError:F0}ms.",
                DetailedSteps = [
                    "Practice with a metronome at a slower tempo",
                    "Tap the rhythm on a table before playing",
                    "Focus on subdividing the beat mentally",
                    "Record yourself and listen back"
                ],
                ExpectedTimeMinutes = 20
            });
        }
        
        // Tempo suggestions
        if (tempo != null && tempo.Score < 85)
        {
            if (tempo.TempoStability < 0.8)
            {
                suggestions.Add(new PracticeSuggestion
                {
                    Priority = 2,
                    Title = "Stabilize Your Tempo",
                    Description = "Your tempo varies throughout the piece.",
                    DetailedSteps = [
                        "Always practice with a metronome",
                        "Start at a comfortable tempo and maintain it",
                        "Avoid speeding up in easier sections",
                        "Practice difficult sections at the same tempo as easy ones"
                    ],
                    ExpectedTimeMinutes = 15
                });
            }
            
            if (Math.Abs(tempo.TempoDeviation) > 0.15)
            {
                string issue = tempo.TempoDeviation > 0 ? "too fast" : "too slow";
                suggestions.Add(new PracticeSuggestion
                {
                    Priority = 3,
                    Title = $"Correct Overall Tempo",
                    Description = $"You're playing {issue} ({tempo.DetectedBPM:F0} vs {tempo.ExpectedBPM:F0} BPM).",
                    DetailedSteps = [
                        $"Set metronome to {tempo.ExpectedBPM:F0} BPM",
                        "Internalize the tempo before starting",
                        "Practice maintaining this tempo throughout"
                    ],
                    ExpectedTimeMinutes = 10
                });
            }
        }
        
        return suggestions.OrderBy(s => s.Priority).ToList();
    }
    
    private string GenerateSummary(
        double overallScore,
        NoteAccuracyResult? accuracy,
        RhythmResult? rhythm,
        TempoResult? tempo)
    {
        var parts = new List<string>();
        
        // Opening based on overall score
        parts.Add(overallScore switch
        {
            >= 95 => "Outstanding performance! 🌟",
            >= 90 => "Excellent work! You're playing this piece very well.",
            >= 85 => "Great job! Just a few areas to polish.",
            >= 80 => "Good progress! Keep working on the details.",
            >= 70 => "Nice effort! Some sections need more practice.",
            >= 60 => "You're getting there! Focus on the problem areas.",
            _ => "Keep practicing! Every pianist has challenging pieces."
        });
        
        // Best aspect
        var scores = new Dictionary<string, double>();
        if (accuracy != null) scores["note accuracy"] = accuracy.Score;
        if (rhythm != null) scores["rhythmic precision"] = rhythm.Score;
        if (tempo != null) scores["tempo control"] = tempo.Score;
        
        if (scores.Any())
        {
            var best = scores.OrderByDescending(kv => kv.Value).First();
            if (best.Value >= 85)
            {
                parts.Add($"Your {best.Key} is particularly strong.");
            }
            
            var weakest = scores.OrderBy(kv => kv.Value).First();
            if (weakest.Value < 75)
            {
                parts.Add($"Focus on improving your {weakest.Key}.");
            }
        }
        
        return string.Join(" ", parts);
    }
    
    private (List<string> Strengths, List<string> Weaknesses) IdentifyStrengthsAndWeaknesses(
        NoteAccuracyResult? accuracy,
        RhythmResult? rhythm,
        TempoResult? tempo)
    {
        var strengths = new List<string>();
        var weaknesses = new List<string>();
        
        if (accuracy != null)
        {
            if (accuracy.Score >= 90)
                strengths.Add("Excellent note accuracy");
            else if (accuracy.Score < 70)
                weaknesses.Add("Note accuracy needs improvement");
            
            if (accuracy.OctaveErrors == 0 && accuracy.Score >= 80)
                strengths.Add("Good hand position awareness");
        }
        
        if (rhythm != null)
        {
            if (rhythm.Score >= 90)
                strengths.Add("Strong rhythmic precision");
            else if (rhythm.Score < 70)
                weaknesses.Add("Rhythmic precision needs work");
            
            if (rhythm.OnTimePercent >= 80)
                strengths.Add("Good sense of the beat");
        }
        
        if (tempo != null)
        {
            if (tempo.TempoStability >= 0.9)
                strengths.Add("Consistent tempo throughout");
            else if (tempo.TempoStability < 0.7)
                weaknesses.Add("Tempo stability needs improvement");
            
            if (Math.Abs(tempo.TempoDeviation) < 0.1)
                strengths.Add("Playing at the correct tempo");
        }
        
        return (strengths, weaknesses);
    }
    
    private string GetGrade(double score) => score switch
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
}

/// <summary>
/// Complete feedback report from evaluation.
/// </summary>
public record FeedbackReport
{
    public double OverallScore { get; init; }
    public string OverallGrade { get; init; } = "";
    public string Summary { get; init; } = "";
    
    /// <summary>
    /// Top issues to address (limited for clarity).
    /// </summary>
    public IReadOnlyList<EvaluationIssue> PrioritizedIssues { get; init; } = [];
    
    /// <summary>
    /// All issues found.
    /// </summary>
    public IReadOnlyList<EvaluationIssue> AllIssues { get; init; } = [];
    
    /// <summary>
    /// Per-measure breakdown.
    /// </summary>
    public IReadOnlyList<MeasureReport> MeasureReports { get; init; } = [];
    
    /// <summary>
    /// Practice suggestions.
    /// </summary>
    public IReadOnlyList<PracticeSuggestion> PracticeSuggestions { get; init; } = [];
    
    /// <summary>
    /// Identified strengths.
    /// </summary>
    public IReadOnlyList<string> Strengths { get; init; } = [];
    
    /// <summary>
    /// Areas needing improvement.
    /// </summary>
    public IReadOnlyList<string> AreasForImprovement { get; init; } = [];
    
    /// <summary>
    /// Individual scores.
    /// </summary>
    public double? NoteAccuracyScore { get; init; }
    public double? RhythmScore { get; init; }
    public double? TempoScore { get; init; }
    
    public string PieceTitle { get; init; } = "";
    public int TotalMeasures { get; init; }
    public IReadOnlyList<int> ProblemMeasures { get; init; } = [];
}

/// <summary>
/// Detailed report for a single measure.
/// </summary>
public record MeasureReport
{
    public int Measure { get; init; }
    public double OverallScore { get; init; }
    public double AccuracyScore { get; init; }
    public double RhythmScore { get; init; }
    public int ExpectedNotes { get; init; }
    public int CorrectNotes { get; init; }
    public int MissedNotes { get; init; }
    public int WrongNotes { get; init; }
    public IReadOnlyList<string> Issues { get; init; } = [];
    public bool NeedsPractice { get; init; }
}

/// <summary>
/// A practice suggestion with actionable steps.
/// </summary>
public record PracticeSuggestion
{
    public int Priority { get; init; }
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public IReadOnlyList<string> DetailedSteps { get; init; } = [];
    public int ExpectedTimeMinutes { get; init; }
}

