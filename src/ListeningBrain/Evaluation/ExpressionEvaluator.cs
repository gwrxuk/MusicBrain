using ListeningBrain.Alignment;
using ListeningBrain.Core.Models;

namespace ListeningBrain.Evaluation;

/// <summary>
/// Evaluates overall expression and compliance with expression markings.
/// Combines insights from dynamics, articulation, pedaling, and phrasing
/// to assess overall musicality and expression marking compliance.
/// </summary>
public class ExpressionEvaluator : IEvaluator<ExpressionResult>
{
    public string Name => "Expression Evaluator";
    
    private readonly DynamicsEvaluator _dynamicsEvaluator = new();
    private readonly ArticulationEvaluator _articulationEvaluator = new();
    private readonly PedalEvaluator _pedalEvaluator = new();
    private readonly PhraseEvaluator _phraseEvaluator = new();
    
    public ExpressionResult Evaluate(AlignmentResult alignment, Score score, Performance performance)
    {
        var issues = new List<EvaluationIssue>();
        
        if (alignment.Pairs.Count == 0)
        {
            return CreateEmptyResult();
        }
        
        // Run all sub-evaluators
        var dynamicsResult = _dynamicsEvaluator.Evaluate(alignment, score, performance);
        var articulationResult = _articulationEvaluator.Evaluate(alignment, score, performance);
        var pedalResult = _pedalEvaluator.Evaluate(alignment, score, performance);
        var phraseResult = _phraseEvaluator.Evaluate(alignment, score, performance);
        
        // Analyze expression marking compliance
        var markingCompliance = AnalyzeExpressionMarkings(alignment, score);
        issues.AddRange(markingCompliance.Issues);
        
        // Analyze overall musicality
        var musicalityAnalysis = AnalyzeOverallMusicality(
            dynamicsResult, articulationResult, pedalResult, phraseResult);
        issues.AddRange(musicalityAnalysis.Issues);
        
        // Add top issues from each sub-evaluator
        issues.AddRange(dynamicsResult.Issues.Take(2));
        issues.AddRange(articulationResult.Issues.Take(2));
        issues.AddRange(pedalResult.Issues.Take(1));
        issues.AddRange(phraseResult.Issues.Take(2));
        
        // Calculate overall expression score
        double expressionScore = CalculateOverallScore(
            dynamicsResult, articulationResult, pedalResult, phraseResult, markingCompliance);
        
        // Determine expression character
        var character = DetermineExpressionCharacter(
            dynamicsResult, articulationResult, phraseResult);
        
        return new ExpressionResult
        {
            Score = expressionScore,
            Summary = GenerateSummary(expressionScore, character, markingCompliance),
            Issues = issues.Distinct().OrderByDescending(i => i.Severity).Take(10).ToList(),
            DynamicsScore = dynamicsResult.Score,
            ArticulationScore = articulationResult.Score,
            PedalScore = pedalResult.Score,
            PhraseScore = phraseResult.Score,
            MarkingComplianceScore = markingCompliance.Score,
            OverallMusicalityScore = musicalityAnalysis.Score,
            ExpressionCharacter = character,
            DynamicsResult = dynamicsResult,
            ArticulationResult = articulationResult,
            PedalResult = pedalResult,
            PhraseResult = phraseResult,
            ExpressionMarkings = markingCompliance.Markings
        };
    }
    
    private ExpressionMarkingAnalysis AnalyzeExpressionMarkings(
        AlignmentResult alignment, 
        Score score)
    {
        var issues = new List<EvaluationIssue>();
        var markings = new List<ExpressionMarkingResult>();
        
        // Group notes by expected dynamic level
        var dynamicGroups = alignment.Pairs
            .GroupBy(p => p.ScoreNote.ExpectedDynamic)
            .ToList();
        
        int correctMarkings = 0;
        int totalMarkings = 0;
        
        foreach (var group in dynamicGroups)
        {
            var expectedLevel = group.Key;
            var notes = group.ToList();
            
            if (notes.Count == 0) continue;
            
            double avgVelocity = notes.Average(n => n.PerformanceNote.Velocity);
            var actualLevel = VelocityToDynamicLevel((int)avgVelocity);
            
            totalMarkings++;
            
            int levelDiff = Math.Abs((int)actualLevel - (int)expectedLevel);
            bool isCorrect = levelDiff <= 1; // Allow one level off
            
            if (isCorrect) correctMarkings++;
            
            markings.Add(new ExpressionMarkingResult
            {
                ExpectedDynamic = expectedLevel,
                ActualDynamic = actualLevel,
                NoteCount = notes.Count,
                AverageVelocity = avgVelocity,
                IsCorrect = isCorrect,
                Deviation = levelDiff
            });
            
            if (levelDiff >= 2)
            {
                issues.Add(new EvaluationIssue
                {
                    Severity = IssueSeverity.Moderate,
                    Type = levelDiff > 0 && (int)actualLevel > (int)expectedLevel 
                        ? IssueType.ToeLoud 
                        : IssueType.TooSoft,
                    Description = $"Dynamic marking {expectedLevel} played as {actualLevel}",
                    Suggestion = $"This section is marked {expectedLevel} - adjust your touch accordingly"
                });
            }
        }
        
        double complianceScore = totalMarkings > 0 
            ? (double)correctMarkings / totalMarkings * 100 
            : 100;
        
        return new ExpressionMarkingAnalysis(complianceScore, markings, issues);
    }
    
    private MusicalityOverallAnalysis AnalyzeOverallMusicality(
        DynamicsResult dynamics,
        ArticulationResult articulation,
        PedalResult pedal,
        PhraseResult phrase)
    {
        var issues = new List<EvaluationIssue>();
        
        // Check for balanced expression across all dimensions
        var scores = new[] { dynamics.Score, articulation.Score, phrase.Score };
        double avgScore = scores.Average();
        double variance = scores.Select(s => Math.Pow(s - avgScore, 2)).Average();
        
        // High variance means unbalanced development
        if (variance > 400 && avgScore < 80)
        {
            var weakest = scores.Select((s, i) => (s, i)).OrderBy(x => x.s).First();
            string area = weakest.i switch
            {
                0 => "dynamics",
                1 => "articulation",
                _ => "phrasing"
            };
            
            issues.Add(new EvaluationIssue
            {
                Severity = IssueSeverity.Minor,
                Type = IssueType.FlatDynamics,
                Description = $"Expression is unbalanced - {area} needs more attention",
                Suggestion = $"While other aspects are developing well, focus on improving {area}"
            });
        }
        
        // Check for mechanical playing (high accuracy but low expression)
        if (dynamics.DynamicRange < 30 && articulation.DurationConsistency > 0.9)
        {
            issues.Add(new EvaluationIssue
            {
                Severity = IssueSeverity.Moderate,
                Type = IssueType.FlatDynamics,
                Description = "Playing is technically accurate but sounds mechanical",
                Suggestion = "Don't be afraid to exaggerate expression - music needs contrast!"
            });
        }
        
        double musicalityScore = avgScore;
        
        // Bonus for variety and contrast
        if (dynamics.DynamicRange > 50)
            musicalityScore = Math.Min(100, musicalityScore + 5);
        
        if (phrase.PhrasesWithGoodShape > phrase.TotalPhrases * 0.7)
            musicalityScore = Math.Min(100, musicalityScore + 5);
        
        return new MusicalityOverallAnalysis(musicalityScore, issues);
    }
    
    private ExpressionCharacter DetermineExpressionCharacter(
        DynamicsResult dynamics,
        ArticulationResult articulation,
        PhraseResult phrase)
    {
        // Determine the overall character of the performance
        
        if (dynamics.DynamicRange > 60 && phrase.PhrasesWithGoodShape > phrase.TotalPhrases * 0.6)
        {
            return ExpressionCharacter.Dramatic;
        }
        
        if (dynamics.DynamicRange < 25)
        {
            return ExpressionCharacter.Restrained;
        }
        
        if (articulation.StaccatoAccuracy > 0.8 && articulation.LegatoAccuracy < 0.5)
        {
            return ExpressionCharacter.Crisp;
        }
        
        if (articulation.LegatoAccuracy > 0.8)
        {
            return ExpressionCharacter.Lyrical;
        }
        
        if (phrase.MusicalityScore > 0.8)
        {
            return ExpressionCharacter.Musical;
        }
        
        if (dynamics.DynamicContourAccuracy > 0.7)
        {
            return ExpressionCharacter.Expressive;
        }
        
        return ExpressionCharacter.Developing;
    }
    
    private double CalculateOverallScore(
        DynamicsResult dynamics,
        ArticulationResult articulation,
        PedalResult pedal,
        PhraseResult phrase,
        ExpressionMarkingAnalysis markings)
    {
        // Weighted average
        return (
            dynamics.Score * 0.25 +
            articulation.Score * 0.20 +
            pedal.Score * 0.15 +
            phrase.Score * 0.25 +
            markings.Score * 0.15
        );
    }
    
    private string GenerateSummary(
        double score, 
        ExpressionCharacter character,
        ExpressionMarkingAnalysis markings)
    {
        string characterDesc = character switch
        {
            ExpressionCharacter.Dramatic => "dramatic and bold",
            ExpressionCharacter.Lyrical => "lyrical and singing",
            ExpressionCharacter.Crisp => "crisp and articulate",
            ExpressionCharacter.Restrained => "restrained (consider more dynamic contrast)",
            ExpressionCharacter.Expressive => "expressive and musical",
            ExpressionCharacter.Musical => "naturally musical",
            _ => "developing"
        };
        
        if (score >= 90)
            return $"Outstanding expression! Your playing is {characterDesc} with excellent musical shaping.";
        if (score >= 80)
            return $"Good expression with a {characterDesc} character. Minor refinements will enhance musicality.";
        if (score >= 70)
            return $"Fair expression. Your style is {characterDesc}. Focus on dynamic contrast and phrase shaping.";
        return $"Expression needs development. Work on creating more contrast and musical interest.";
    }
    
    private DynamicLevel VelocityToDynamicLevel(int velocity) => velocity switch
    {
        <= 31 => DynamicLevel.Pianissimo,
        <= 47 => DynamicLevel.Piano,
        <= 63 => DynamicLevel.MezzoPiano,
        <= 79 => DynamicLevel.MezzoForte,
        <= 95 => DynamicLevel.Forte,
        _ => DynamicLevel.Fortissimo
    };
    
    private ExpressionResult CreateEmptyResult() => new()
    {
        Score = 0,
        Summary = "No notes to evaluate.",
        Issues = [],
        ExpressionMarkings = []
    };
}

// Helper records
internal record ExpressionMarkingAnalysis(
    double Score, 
    List<ExpressionMarkingResult> Markings, 
    List<EvaluationIssue> Issues);

internal record MusicalityOverallAnalysis(double Score, List<EvaluationIssue> Issues);

/// <summary>
/// Overall expression character of the performance.
/// </summary>
public enum ExpressionCharacter
{
    Developing,   // Still learning expression
    Restrained,   // Limited dynamic range
    Crisp,        // Good articulation, detached style
    Lyrical,      // Singing, connected style
    Dramatic,     // Bold dynamic contrasts
    Expressive,   // Good dynamic contours
    Musical       // Well-rounded musicality
}

/// <summary>
/// Result of expression evaluation.
/// </summary>
public record ExpressionResult : EvaluationResult
{
    public double DynamicsScore { get; init; }
    public double ArticulationScore { get; init; }
    public double PedalScore { get; init; }
    public double PhraseScore { get; init; }
    public double MarkingComplianceScore { get; init; }
    public double OverallMusicalityScore { get; init; }
    
    /// <summary>
    /// The overall character of expression in this performance.
    /// </summary>
    public ExpressionCharacter ExpressionCharacter { get; init; }
    
    /// <summary>
    /// Detailed dynamics result.
    /// </summary>
    public DynamicsResult? DynamicsResult { get; init; }
    
    /// <summary>
    /// Detailed articulation result.
    /// </summary>
    public ArticulationResult? ArticulationResult { get; init; }
    
    /// <summary>
    /// Detailed pedal result.
    /// </summary>
    public PedalResult? PedalResult { get; init; }
    
    /// <summary>
    /// Detailed phrase result.
    /// </summary>
    public PhraseResult? PhraseResult { get; init; }
    
    /// <summary>
    /// Expression marking compliance details.
    /// </summary>
    public IReadOnlyList<ExpressionMarkingResult> ExpressionMarkings { get; init; } = [];
}

/// <summary>
/// Result for a specific expression marking.
/// </summary>
public record ExpressionMarkingResult
{
    public DynamicLevel ExpectedDynamic { get; init; }
    public DynamicLevel ActualDynamic { get; init; }
    public int NoteCount { get; init; }
    public double AverageVelocity { get; init; }
    public bool IsCorrect { get; init; }
    public int Deviation { get; init; }
}

