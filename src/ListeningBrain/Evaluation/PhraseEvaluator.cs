using ListeningBrain.Alignment;
using ListeningBrain.Core.Models;

namespace ListeningBrain.Evaluation;

/// <summary>
/// Evaluates musical phrase shaping: dynamic arcs, breath points, and musical direction.
/// Analyzes whether performances have musical shape rather than mechanical evenness.
/// </summary>
public class PhraseEvaluator : IEvaluator<PhraseResult>
{
    public string Name => "Phrase Shaping Evaluator";
    
    /// <summary>
    /// Typical phrase length in beats.
    /// </summary>
    public int DefaultPhraseLength { get; init; } = 8;
    
    /// <summary>
    /// Minimum velocity variation expected within a musical phrase.
    /// </summary>
    public int MinPhraseVariation { get; init; } = 10;
    
    public PhraseResult Evaluate(AlignmentResult alignment, Score score, Performance performance)
    {
        var issues = new List<EvaluationIssue>();
        
        if (alignment.Pairs.Count < 4)
        {
            return CreateEmptyResult();
        }
        
        // Detect phrase boundaries
        var phrases = DetectPhrases(alignment.Pairs, score);
        
        // Analyze each phrase for musical shaping
        var phraseAnalyses = phrases.Select(p => AnalyzePhrase(p, alignment.Pairs)).ToList();
        
        // Check for musical breathing (slight gaps between phrases)
        var breathingAnalysis = AnalyzePhraseBreathing(phrases, alignment.Pairs);
        issues.AddRange(breathingAnalysis.Issues);
        
        // Check for overall dynamic arc
        var arcAnalysis = AnalyzeDynamicArcs(phraseAnalyses);
        issues.AddRange(arcAnalysis.Issues);
        
        // Check for mechanical vs musical playing
        var musicalityAnalysis = AnalyzeMusicality(phraseAnalyses);
        issues.AddRange(musicalityAnalysis.Issues);
        
        // Calculate score
        double phraseScore = CalculateScore(phraseAnalyses, breathingAnalysis, arcAnalysis, musicalityAnalysis);
        
        return new PhraseResult
        {
            Score = phraseScore,
            Summary = GenerateSummary(phraseScore, phraseAnalyses),
            Issues = issues.OrderByDescending(i => i.Severity).ToList(),
            TotalPhrases = phrases.Count,
            PhrasesWithGoodShape = phraseAnalyses.Count(p => p.HasGoodShape),
            AveragePhraseVariation = phraseAnalyses.Count > 0 
                ? phraseAnalyses.Average(p => p.VelocityVariation) 
                : 0,
            BreathingScore = breathingAnalysis.Score,
            DynamicArcScore = arcAnalysis.Score,
            MusicalityScore = musicalityAnalysis.Score,
            PhraseDetails = phraseAnalyses.Select(p => new PhraseDetail
            {
                StartMeasure = p.StartMeasure,
                EndMeasure = p.EndMeasure,
                NoteCount = p.NoteCount,
                VelocityVariation = p.VelocityVariation,
                HasDynamicArc = p.HasDynamicArc,
                PeakPosition = p.PeakPosition,
                ShapeType = p.ShapeType
            }).ToList()
        };
    }
    
    private List<PhraseInfo> DetectPhrases(IReadOnlyList<AlignedNotePair> pairs, Score score)
    {
        var phrases = new List<PhraseInfo>();
        
        // Group notes into phrases based on measure groupings
        var sortedPairs = pairs.OrderBy(p => p.ScoreNote.StartTick).ToList();
        
        int startMeasure = sortedPairs.First().ScoreNote.Measure;
        int currentPhraseStart = startMeasure;
        
        // Simple phrase detection: group by DefaultPhraseLength measures
        int totalMeasures = score.TotalMeasures;
        
        for (int m = 1; m <= totalMeasures; m += DefaultPhraseLength / 2)
        {
            int phraseEnd = Math.Min(m + DefaultPhraseLength / 2 - 1, totalMeasures);
            
            var phraseNotes = sortedPairs
                .Where(p => p.ScoreNote.Measure >= m && p.ScoreNote.Measure <= phraseEnd)
                .ToList();
            
            if (phraseNotes.Count > 0)
            {
                phrases.Add(new PhraseInfo
                {
                    StartMeasure = m,
                    EndMeasure = phraseEnd,
                    Notes = phraseNotes,
                    StartTimeMs = phraseNotes.First().PerformanceNote.StartTimeMs,
                    EndTimeMs = phraseNotes.Last().PerformanceNote.EndTimeMs
                });
            }
        }
        
        return phrases;
    }
    
    private PhraseAnalysis AnalyzePhrase(PhraseInfo phrase, IReadOnlyList<AlignedNotePair> allPairs)
    {
        var velocities = phrase.Notes.Select(n => n.PerformanceNote.Velocity).ToList();
        
        if (velocities.Count == 0)
        {
            return new PhraseAnalysis
            {
                StartMeasure = phrase.StartMeasure,
                EndMeasure = phrase.EndMeasure,
                NoteCount = 0,
                VelocityVariation = 0,
                HasDynamicArc = false,
                HasGoodShape = false,
                PeakPosition = 0.5,
                ShapeType = PhraseShape.Flat
            };
        }
        
        int minVel = velocities.Min();
        int maxVel = velocities.Max();
        int variation = maxVel - minVel;
        
        // Find where the peak is in the phrase (0 = start, 1 = end)
        int peakIndex = velocities.IndexOf(maxVel);
        double peakPosition = (double)peakIndex / velocities.Count;
        
        // Determine phrase shape
        var shapeType = DetermineShape(velocities);
        
        // Check if it has a musical arc (build and release)
        bool hasDynamicArc = variation >= MinPhraseVariation && 
                            shapeType != PhraseShape.Flat;
        
        bool hasGoodShape = hasDynamicArc && 
                           peakPosition > 0.2 && peakPosition < 0.8;
        
        return new PhraseAnalysis
        {
            StartMeasure = phrase.StartMeasure,
            EndMeasure = phrase.EndMeasure,
            NoteCount = phrase.Notes.Count,
            VelocityVariation = variation,
            HasDynamicArc = hasDynamicArc,
            HasGoodShape = hasGoodShape,
            PeakPosition = peakPosition,
            ShapeType = shapeType,
            AverageVelocity = velocities.Average()
        };
    }
    
    private PhraseShape DetermineShape(List<int> velocities)
    {
        if (velocities.Count < 3)
            return PhraseShape.Flat;
        
        int third = velocities.Count / 3;
        
        double firstThird = velocities.Take(third).Average();
        double middleThird = velocities.Skip(third).Take(third).Average();
        double lastThird = velocities.Skip(third * 2).Average();
        
        double variation = velocities.Max() - velocities.Min();
        
        if (variation < MinPhraseVariation)
            return PhraseShape.Flat;
        
        if (middleThird > firstThird && middleThird > lastThird)
            return PhraseShape.Arc; // Classic phrase shape
        
        if (firstThird < middleThird && middleThird < lastThird)
            return PhraseShape.Crescendo;
        
        if (firstThird > middleThird && middleThird > lastThird)
            return PhraseShape.Diminuendo;
        
        if (lastThird > firstThird + 5)
            return PhraseShape.Building;
        
        if (firstThird > lastThird + 5)
            return PhraseShape.Fading;
        
        return PhraseShape.Irregular;
    }
    
    private BreathingAnalysis AnalyzePhraseBreathing(
        List<PhraseInfo> phrases, 
        IReadOnlyList<AlignedNotePair> pairs)
    {
        var issues = new List<EvaluationIssue>();
        
        if (phrases.Count < 2)
        {
            return new BreathingAnalysis(1.0, issues);
        }
        
        int breathingGaps = 0;
        int totalTransitions = 0;
        
        for (int i = 0; i < phrases.Count - 1; i++)
        {
            var currentPhrase = phrases[i];
            var nextPhrase = phrases[i + 1];
            
            // Check for slight gap or timing adjustment between phrases
            double gap = nextPhrase.StartTimeMs - currentPhrase.EndTimeMs;
            
            totalTransitions++;
            
            // A small breath (20-100ms gap or slight ritardando) is musical
            if (gap > 10 && gap < 200)
            {
                breathingGaps++;
            }
        }
        
        double score = totalTransitions > 0 
            ? (double)breathingGaps / totalTransitions 
            : 1.0;
        
        if (score < 0.3 && totalTransitions > 2)
        {
            issues.Add(new EvaluationIssue
            {
                Severity = IssueSeverity.Minor,
                Type = IssueType.FlatDynamics,
                Description = "Phrases run together without breathing points",
                Suggestion = "Add tiny pauses between phrases - imagine taking a breath before each new phrase"
            });
        }
        
        return new BreathingAnalysis(score, issues);
    }
    
    private DynamicArcAnalysis AnalyzeDynamicArcs(List<PhraseAnalysis> analyses)
    {
        var issues = new List<EvaluationIssue>();
        
        if (analyses.Count == 0)
        {
            return new DynamicArcAnalysis(1.0, issues);
        }
        
        int goodArcs = analyses.Count(a => a.HasDynamicArc);
        double score = (double)goodArcs / analyses.Count;
        
        if (score < 0.5)
        {
            issues.Add(new EvaluationIssue
            {
                Severity = IssueSeverity.Moderate,
                Type = IssueType.FlatDynamics,
                Description = "Phrases lack dynamic shape - playing sounds mechanical",
                Suggestion = "Shape each phrase with a natural rise and fall in volume"
            });
        }
        
        // Check for variety in phrase shapes
        var shapes = analyses.Select(a => a.ShapeType).Distinct().Count();
        if (shapes == 1 && analyses.Count > 3)
        {
            issues.Add(new EvaluationIssue
            {
                Severity = IssueSeverity.Minor,
                Type = IssueType.FlatDynamics,
                Description = "All phrases have the same shape - consider more variety",
                Suggestion = "Vary your phrase shaping to match the musical content"
            });
        }
        
        return new DynamicArcAnalysis(score, issues);
    }
    
    private MusicalityAnalysis AnalyzeMusicality(List<PhraseAnalysis> analyses)
    {
        var issues = new List<EvaluationIssue>();
        
        if (analyses.Count == 0)
        {
            return new MusicalityAnalysis(1.0, issues);
        }
        
        // Check for good phrase shaping
        int wellShaped = analyses.Count(a => a.HasGoodShape);
        double shapeScore = (double)wellShaped / analyses.Count;
        
        // Check for appropriate peak positions (not at very start or end)
        int goodPeaks = analyses.Count(a => a.PeakPosition > 0.25 && a.PeakPosition < 0.75);
        double peakScore = (double)goodPeaks / analyses.Count;
        
        double overallScore = (shapeScore + peakScore) / 2;
        
        if (overallScore < 0.5)
        {
            issues.Add(new EvaluationIssue
            {
                Severity = IssueSeverity.Moderate,
                Type = IssueType.FlatDynamics,
                Description = "Playing lacks musical direction - sounds flat",
                Suggestion = "Think of phrases as sentences - build to the important notes and relax after"
            });
        }
        
        return new MusicalityAnalysis(overallScore, issues);
    }
    
    private double CalculateScore(
        List<PhraseAnalysis> phrases,
        BreathingAnalysis breathing,
        DynamicArcAnalysis arcs,
        MusicalityAnalysis musicality)
    {
        if (phrases.Count == 0) return 100;
        
        double phraseShapeScore = phrases.Count(p => p.HasGoodShape) / (double)phrases.Count * 100;
        
        return (
            phraseShapeScore * 0.4 +
            breathing.Score * 100 * 0.2 +
            arcs.Score * 100 * 0.2 +
            musicality.Score * 100 * 0.2
        );
    }
    
    private string GenerateSummary(double score, List<PhraseAnalysis> analyses)
    {
        int goodPhrases = analyses.Count(a => a.HasGoodShape);
        int total = analyses.Count;
        
        if (score >= 90)
            return $"Beautiful phrase shaping! {goodPhrases}/{total} phrases have excellent musical shape.";
        if (score >= 80)
            return $"Good musical phrasing with {goodPhrases}/{total} well-shaped phrases.";
        if (score >= 70)
            return $"Developing phrase sense. Work on making more phrases sing.";
        return "Phrasing needs attention - focus on building and releasing tension in each phrase.";
    }
    
    private PhraseResult CreateEmptyResult() => new()
    {
        Score = 0,
        Summary = "Not enough notes to evaluate phrase shaping.",
        Issues = [],
        PhraseDetails = []
    };
}

// Helper records
internal record PhraseInfo
{
    public int StartMeasure { get; init; }
    public int EndMeasure { get; init; }
    public List<AlignedNotePair> Notes { get; init; } = [];
    public double StartTimeMs { get; init; }
    public double EndTimeMs { get; init; }
}

internal record PhraseAnalysis
{
    public int StartMeasure { get; init; }
    public int EndMeasure { get; init; }
    public int NoteCount { get; init; }
    public int VelocityVariation { get; init; }
    public bool HasDynamicArc { get; init; }
    public bool HasGoodShape { get; init; }
    public double PeakPosition { get; init; }
    public PhraseShape ShapeType { get; init; }
    public double AverageVelocity { get; init; }
}

internal record BreathingAnalysis(double Score, List<EvaluationIssue> Issues);
internal record DynamicArcAnalysis(double Score, List<EvaluationIssue> Issues);
internal record MusicalityAnalysis(double Score, List<EvaluationIssue> Issues);

/// <summary>
/// Types of phrase shapes.
/// </summary>
public enum PhraseShape
{
    Flat,        // No significant variation
    Arc,         // Classic phrase shape: build to middle, release
    Crescendo,   // Continuous build
    Diminuendo,  // Continuous decrease
    Building,    // Ends stronger than starts
    Fading,      // Ends weaker than starts
    Irregular    // No clear pattern
}

/// <summary>
/// Result of phrase shaping evaluation.
/// </summary>
public record PhraseResult : EvaluationResult
{
    public int TotalPhrases { get; init; }
    public int PhrasesWithGoodShape { get; init; }
    public double AveragePhraseVariation { get; init; }
    public double BreathingScore { get; init; }
    public double DynamicArcScore { get; init; }
    public double MusicalityScore { get; init; }
    public IReadOnlyList<PhraseDetail> PhraseDetails { get; init; } = [];
}

/// <summary>
/// Details about a single phrase.
/// </summary>
public record PhraseDetail
{
    public int StartMeasure { get; init; }
    public int EndMeasure { get; init; }
    public int NoteCount { get; init; }
    public int VelocityVariation { get; init; }
    public bool HasDynamicArc { get; init; }
    public double PeakPosition { get; init; }
    public PhraseShape ShapeType { get; init; }
}

