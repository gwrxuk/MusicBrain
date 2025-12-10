using ListeningBrain.Alignment;
using ListeningBrain.Core.Models;

namespace ListeningBrain.Evaluation;

/// <summary>
/// Evaluates pedal usage: sustain pedal timing, clarity, and appropriateness.
/// </summary>
public class PedalEvaluator : IEvaluator<PedalResult>
{
    public string Name => "Pedal Evaluator";
    
    /// <summary>
    /// Maximum acceptable delay for pedal changes after harmony change (ms).
    /// </summary>
    public double MaxPedalChangeDelay { get; init; } = 100;
    
    /// <summary>
    /// Minimum time pedal should be held for it to be meaningful (ms).
    /// </summary>
    public double MinPedalDuration { get; init; } = 200;
    
    public PedalResult Evaluate(AlignmentResult alignment, Score score, Performance performance)
    {
        var issues = new List<EvaluationIssue>();
        
        var pedalEvents = performance.SustainPedalEvents.ToList();
        
        if (pedalEvents.Count == 0)
        {
            // Check if pedal was expected
            var expectedPedal = DetectExpectedPedalUsage(alignment.Pairs, score);
            if (expectedPedal.ShouldUsePedal)
            {
                issues.Add(new EvaluationIssue
                {
                    Severity = IssueSeverity.Moderate,
                    Type = IssueType.FlatDynamics,
                    Description = "No sustain pedal used - this piece would benefit from pedaling",
                    Suggestion = "Add sustain pedal to connect harmonies and create resonance"
                });
                
                return new PedalResult
                {
                    Score = 60,
                    Summary = "No pedal used. This piece would benefit from sustain pedal.",
                    Issues = issues,
                    PedalUsed = false,
                    TotalPedalPresses = 0,
                    PedalSegments = [],
                    HarmonyClarity = 1.0
                };
            }
            
            return CreateNoPedalResult();
        }
        
        // Analyze pedal segments
        var segments = AnalyzePedalSegments(pedalEvents);
        
        // Check pedal timing relative to harmony changes
        var timingAnalysis = AnalyzePedalTiming(segments, alignment.Pairs, score);
        issues.AddRange(timingAnalysis.Issues);
        
        // Check for muddiness (pedal held through dissonant harmony changes)
        var clarityAnalysis = AnalyzeHarmonyClarity(segments, alignment.Pairs);
        issues.AddRange(clarityAnalysis.Issues);
        
        // Check pedal duration patterns
        var durationAnalysis = AnalyzePedalDurations(segments);
        issues.AddRange(durationAnalysis.Issues);
        
        // Calculate score
        double pedalScore = CalculateScore(timingAnalysis, clarityAnalysis, durationAnalysis);
        
        return new PedalResult
        {
            Score = pedalScore,
            Summary = GenerateSummary(pedalScore, segments.Count, clarityAnalysis),
            Issues = issues.OrderByDescending(i => i.Severity).ToList(),
            PedalUsed = true,
            TotalPedalPresses = segments.Count,
            AveragePedalDuration = segments.Count > 0 ? segments.Average(s => s.DurationMs) : 0,
            PedalTimingAccuracy = timingAnalysis.Accuracy,
            HarmonyClarity = clarityAnalysis.Clarity,
            PedalSegments = segments,
            PedalDensity = CalculatePedalDensity(segments, alignment)
        };
    }
    
    private List<PedalSegment> AnalyzePedalSegments(List<PedalEvent> events)
    {
        var segments = new List<PedalSegment>();
        PedalEvent? currentPress = null;
        
        foreach (var evt in events.OrderBy(e => e.TimeMs))
        {
            if (evt.IsPressed && currentPress == null)
            {
                currentPress = evt;
            }
            else if (!evt.IsPressed && currentPress != null)
            {
                segments.Add(new PedalSegment
                {
                    StartTimeMs = currentPress.TimeMs,
                    EndTimeMs = evt.TimeMs,
                    DurationMs = evt.TimeMs - currentPress.TimeMs,
                    PedalValue = currentPress.Value,
                    IsHalfPedal = currentPress.IsHalfPedal
                });
                currentPress = null;
            }
        }
        
        // Handle pedal still pressed at end
        if (currentPress != null)
        {
            segments.Add(new PedalSegment
            {
                StartTimeMs = currentPress.TimeMs,
                EndTimeMs = currentPress.TimeMs + 1000, // Assume held to end
                DurationMs = 1000,
                PedalValue = currentPress.Value,
                IsHalfPedal = currentPress.IsHalfPedal
            });
        }
        
        return segments;
    }
    
    private PedalTimingAnalysis AnalyzePedalTiming(
        List<PedalSegment> segments,
        IReadOnlyList<AlignedNotePair> pairs,
        Score score)
    {
        var issues = new List<EvaluationIssue>();
        
        if (segments.Count == 0)
        {
            return new PedalTimingAnalysis(1.0, issues);
        }
        
        // Detect harmony changes (simplified: look for bass note changes)
        var harmonyChanges = DetectHarmonyChanges(pairs);
        
        int timingIssues = 0;
        int totalChanges = 0;
        
        foreach (var change in harmonyChanges)
        {
            totalChanges++;
            
            // Find pedal changes near this harmony change
            var nearbyPedalChange = segments
                .Where(s => Math.Abs(s.StartTimeMs - change.TimeMs) < MaxPedalChangeDelay * 2)
                .OrderBy(s => Math.Abs(s.StartTimeMs - change.TimeMs))
                .FirstOrDefault();
            
            if (nearbyPedalChange == null)
            {
                // No pedal change - might cause muddiness
                timingIssues++;
            }
            else if (nearbyPedalChange.StartTimeMs < change.TimeMs - MaxPedalChangeDelay)
            {
                // Pedal changed too early
                timingIssues++;
            }
        }
        
        double accuracy = totalChanges > 0 ? 1.0 - ((double)timingIssues / totalChanges) : 1.0;
        
        if (accuracy < 0.7 && totalChanges > 3)
        {
            issues.Add(new EvaluationIssue
            {
                Severity = IssueSeverity.Moderate,
                Type = IssueType.FlatDynamics,
                Description = "Pedal changes not aligned with harmony changes",
                Suggestion = "Practice 'syncopated pedaling' - change pedal just after the new chord sounds"
            });
        }
        
        return new PedalTimingAnalysis(accuracy, issues);
    }
    
    private HarmonyClarity AnalyzeHarmonyClarity(
        List<PedalSegment> segments,
        IReadOnlyList<AlignedNotePair> pairs)
    {
        var issues = new List<EvaluationIssue>();
        
        if (segments.Count == 0 || pairs.Count == 0)
        {
            return new HarmonyClarity(1.0, issues);
        }
        
        int muddySegments = 0;
        
        foreach (var segment in segments)
        {
            // Find notes played during this pedal segment
            var notesInSegment = pairs
                .Where(p => p.PerformanceNote.StartTimeMs >= segment.StartTimeMs &&
                            p.PerformanceNote.StartTimeMs <= segment.EndTimeMs)
                .Select(p => p.PerformanceNote.PitchClass)
                .Distinct()
                .ToList();
            
            // Check for dissonant combinations
            if (HasDissonantCluster(notesInSegment))
            {
                muddySegments++;
            }
        }
        
        double clarity = segments.Count > 0 
            ? 1.0 - ((double)muddySegments / segments.Count) 
            : 1.0;
        
        if (clarity < 0.7)
        {
            issues.Add(new EvaluationIssue
            {
                Severity = IssueSeverity.Moderate,
                Type = IssueType.FlatDynamics,
                Description = "Pedal creating muddy sound - holding through clashing harmonies",
                Suggestion = "Change pedal more frequently, especially between different chords"
            });
        }
        
        return new HarmonyClarity(clarity, issues);
    }
    
    private bool HasDissonantCluster(List<int> pitchClasses)
    {
        if (pitchClasses.Count < 3) return false;
        
        // Check for semitone clusters (e.g., C, C#, D all sounding)
        var sorted = pitchClasses.OrderBy(p => p).ToList();
        
        for (int i = 0; i < sorted.Count - 2; i++)
        {
            int dist1 = (sorted[i + 1] - sorted[i] + 12) % 12;
            int dist2 = (sorted[i + 2] - sorted[i + 1] + 12) % 12;
            
            // Two semitones in a row = likely dissonant cluster
            if (dist1 <= 2 && dist2 <= 2)
            {
                return true;
            }
        }
        
        return false;
    }
    
    private PedalDurationAnalysis AnalyzePedalDurations(List<PedalSegment> segments)
    {
        var issues = new List<EvaluationIssue>();
        
        if (segments.Count == 0)
        {
            return new PedalDurationAnalysis(1.0, issues);
        }
        
        // Check for very short pedal presses (nervous pedaling)
        int shortPedals = segments.Count(s => s.DurationMs < MinPedalDuration);
        
        // Check for very long pedal holds
        int longPedals = segments.Count(s => s.DurationMs > 5000);
        
        double consistency = 1.0 - ((double)(shortPedals + longPedals) / segments.Count);
        
        if (shortPedals > segments.Count * 0.3)
        {
            issues.Add(new EvaluationIssue
            {
                Severity = IssueSeverity.Minor,
                Type = IssueType.FlatDynamics,
                Description = "Many very short pedal presses - pedal needs to be held longer",
                Suggestion = "Hold the pedal for the full duration of the harmony"
            });
        }
        
        if (longPedals > segments.Count * 0.2)
        {
            issues.Add(new EvaluationIssue
            {
                Severity = IssueSeverity.Minor,
                Type = IssueType.FlatDynamics,
                Description = "Pedal held too long in places - may cause blurring",
                Suggestion = "Change pedal more frequently to keep harmonies clear"
            });
        }
        
        return new PedalDurationAnalysis(consistency, issues);
    }
    
    private List<HarmonyChange> DetectHarmonyChanges(IReadOnlyList<AlignedNotePair> pairs)
    {
        var changes = new List<HarmonyChange>();
        
        // Simple bass note change detection
        var bassNotes = pairs
            .Where(p => p.PerformanceNote.Pitch < 60) // Bass register
            .OrderBy(p => p.PerformanceNote.StartTimeMs)
            .ToList();
        
        int? lastBassPitch = null;
        
        foreach (var note in bassNotes)
        {
            if (lastBassPitch == null || note.PerformanceNote.PitchClass != lastBassPitch % 12)
            {
                changes.Add(new HarmonyChange(
                    note.PerformanceNote.StartTimeMs,
                    note.PerformanceNote.Pitch,
                    note.ScoreNote.Measure
                ));
                lastBassPitch = note.PerformanceNote.Pitch;
            }
        }
        
        return changes;
    }
    
    private ExpectedPedalUsage DetectExpectedPedalUsage(
        IReadOnlyList<AlignedNotePair> pairs,
        Score score)
    {
        // Simple heuristic: pieces with arpeggios or long notes likely need pedal
        if (pairs.Count == 0)
        {
            return new ExpectedPedalUsage(false, 0);
        }
        
        // Check for arpeggiated patterns
        var sortedNotes = pairs.OrderBy(p => p.ScoreNote.StartTick).ToList();
        int arpeggiatedMeasures = 0;
        
        var byMeasure = sortedNotes.GroupBy(p => p.ScoreNote.Measure);
        foreach (var measure in byMeasure)
        {
            var notes = measure.OrderBy(n => n.ScoreNote.StartTick).ToList();
            if (notes.Count >= 4)
            {
                // Check if notes are spread out (arpeggio pattern)
                bool isArpeggiated = true;
                for (int i = 1; i < Math.Min(4, notes.Count); i++)
                {
                    if (notes[i].ScoreNote.StartTick == notes[i - 1].ScoreNote.StartTick)
                    {
                        isArpeggiated = false;
                        break;
                    }
                }
                if (isArpeggiated) arpeggiatedMeasures++;
            }
        }
        
        double arpeggiatedRatio = byMeasure.Count() > 0 
            ? (double)arpeggiatedMeasures / byMeasure.Count() 
            : 0;
        
        return new ExpectedPedalUsage(arpeggiatedRatio > 0.3, arpeggiatedRatio);
    }
    
    private double CalculatePedalDensity(List<PedalSegment> segments, AlignmentResult alignment)
    {
        if (alignment.Pairs.Count == 0) return 0;
        
        double totalDuration = alignment.Pairs.Max(p => p.PerformanceNote.EndTimeMs);
        double pedaledDuration = segments.Sum(s => s.DurationMs);
        
        return totalDuration > 0 ? pedaledDuration / totalDuration : 0;
    }
    
    private double CalculateScore(
        PedalTimingAnalysis timing,
        HarmonyClarity clarity,
        PedalDurationAnalysis duration)
    {
        return (timing.Accuracy * 40 + clarity.Clarity * 40 + duration.Consistency * 20);
    }
    
    private string GenerateSummary(double score, int pedalCount, HarmonyClarity clarity)
    {
        if (score >= 90)
            return $"Excellent pedaling! Clear harmonies with {pedalCount} well-timed pedal changes.";
        if (score >= 80)
            return "Good pedal technique with minor timing refinements needed.";
        if (score >= 70)
        {
            if (clarity.Clarity < 0.7)
                return "Pedal causing some muddiness - change more frequently.";
            return "Fair pedaling - work on timing pedal changes with harmony.";
        }
        return "Pedal technique needs work - focus on clean pedal changes.";
    }
    
    private PedalResult CreateNoPedalResult() => new()
    {
        Score = 100,
        Summary = "No pedal required or used for this piece.",
        Issues = [],
        PedalUsed = false,
        PedalSegments = []
    };
}

// Analysis helper records
internal record PedalTimingAnalysis(double Accuracy, List<EvaluationIssue> Issues);
internal record HarmonyClarity(double Clarity, List<EvaluationIssue> Issues);
internal record PedalDurationAnalysis(double Consistency, List<EvaluationIssue> Issues);
internal record HarmonyChange(double TimeMs, int BassPitch, int Measure);
internal record ExpectedPedalUsage(bool ShouldUsePedal, double ArpeggioRatio);

/// <summary>
/// Result of pedal evaluation.
/// </summary>
public record PedalResult : EvaluationResult
{
    public bool PedalUsed { get; init; }
    public int TotalPedalPresses { get; init; }
    public double AveragePedalDuration { get; init; }
    public double PedalTimingAccuracy { get; init; }
    public double HarmonyClarity { get; init; }
    public double PedalDensity { get; init; }
    public IReadOnlyList<PedalSegment> PedalSegments { get; init; } = [];
}

/// <summary>
/// A segment of sustained pedal.
/// </summary>
public record PedalSegment
{
    public double StartTimeMs { get; init; }
    public double EndTimeMs { get; init; }
    public double DurationMs { get; init; }
    public int PedalValue { get; init; }
    public bool IsHalfPedal { get; init; }
}

