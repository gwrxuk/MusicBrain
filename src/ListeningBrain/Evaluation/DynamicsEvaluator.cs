using ListeningBrain.Alignment;
using ListeningBrain.Core.Models;

namespace ListeningBrain.Evaluation;

/// <summary>
/// Evaluates dynamics (velocity/volume) accuracy and expression.
/// Analyzes whether the student follows dynamic markings and creates musical expression.
/// </summary>
public class DynamicsEvaluator : IEvaluator<DynamicsResult>
{
    public string Name => "Dynamics Evaluator";
    
    /// <summary>
    /// Tolerance for velocity matching (0-127 scale).
    /// </summary>
    public int VelocityTolerance { get; init; } = 15;
    
    /// <summary>
    /// Minimum velocity difference to consider a dynamic change.
    /// </summary>
    public int MinDynamicChange { get; init; } = 10;
    
    public DynamicsResult Evaluate(AlignmentResult alignment, Score score, Performance performance)
    {
        var issues = new List<EvaluationIssue>();
        
        if (alignment.Pairs.Count == 0)
        {
            return CreateEmptyResult();
        }
        
        // Analyze velocity accuracy
        var velocityAnalysis = AnalyzeVelocityAccuracy(alignment.Pairs);
        
        // Analyze dynamic range
        var rangeAnalysis = AnalyzeDynamicRange(alignment.Pairs, performance);
        
        // Analyze dynamic contour (crescendo/diminuendo)
        var contourAnalysis = AnalyzeDynamicContour(alignment.Pairs, score);
        
        // Analyze dynamic levels by section
        var sectionAnalysis = AnalyzeSectionDynamics(alignment.Pairs, score);
        
        // Generate issues
        issues.AddRange(velocityAnalysis.Issues);
        issues.AddRange(rangeAnalysis.Issues);
        issues.AddRange(contourAnalysis.Issues);
        issues.AddRange(sectionAnalysis.Issues);
        
        // Calculate overall score
        double dynamicsScore = CalculateScore(velocityAnalysis, rangeAnalysis, contourAnalysis);
        
        return new DynamicsResult
        {
            Score = dynamicsScore,
            Summary = GenerateSummary(dynamicsScore, rangeAnalysis, contourAnalysis),
            Issues = issues.OrderByDescending(i => i.Severity).ToList(),
            MeanVelocityError = velocityAnalysis.MeanError,
            VelocityErrorStdDev = velocityAnalysis.StdDev,
            DynamicRange = rangeAnalysis.Range,
            ExpectedDynamicRange = rangeAnalysis.ExpectedRange,
            DynamicRangeRatio = rangeAnalysis.RangeRatio,
            VelocityDistribution = rangeAnalysis.Distribution,
            DynamicContourAccuracy = contourAnalysis.Accuracy,
            CrescendoAccuracy = contourAnalysis.CrescendoAccuracy,
            DiminuendoAccuracy = contourAnalysis.DiminuendoAccuracy,
            SectionDynamics = sectionAnalysis.Sections,
            VelocityCurve = ExtractVelocityCurve(alignment.Pairs)
        };
    }
    
    private VelocityAnalysis AnalyzeVelocityAccuracy(IReadOnlyList<AlignedNotePair> pairs)
    {
        var errors = pairs.Select(p => p.PerformanceNote.Velocity - p.ScoreNote.Velocity).ToList();
        var absErrors = errors.Select(Math.Abs).ToList();
        
        double meanError = errors.Average();
        double meanAbsError = absErrors.Average();
        double stdDev = CalculateStdDev(errors.Select(e => (double)e).ToList());
        
        var issues = new List<EvaluationIssue>();
        
        // Check for systematic issues
        if (meanError > VelocityTolerance)
        {
            issues.Add(new EvaluationIssue
            {
                Severity = IssueSeverity.Moderate,
                Type = IssueType.ToeLoud,
                Description = $"Playing consistently too loud (average +{meanError:F0} velocity)",
                Suggestion = "Focus on playing with lighter touch - imagine the keys are fragile"
            });
        }
        else if (meanError < -VelocityTolerance)
        {
            issues.Add(new EvaluationIssue
            {
                Severity = IssueSeverity.Moderate,
                Type = IssueType.TooSoft,
                Description = $"Playing consistently too soft (average {meanError:F0} velocity)",
                Suggestion = "Use more arm weight to project the sound - don't be afraid to play out"
            });
        }
        
        // Check for inconsistency
        if (stdDev > 20)
        {
            issues.Add(new EvaluationIssue
            {
                Severity = IssueSeverity.Minor,
                Type = IssueType.FlatDynamics,
                Description = "Inconsistent dynamic control - velocity varies unpredictably",
                Suggestion = "Practice scales at steady dynamics to develop even touch"
            });
        }
        
        return new VelocityAnalysis(meanError, meanAbsError, stdDev, issues);
    }
    
    private DynamicRangeAnalysis AnalyzeDynamicRange(
        IReadOnlyList<AlignedNotePair> pairs, 
        Performance performance)
    {
        var issues = new List<EvaluationIssue>();
        
        // Get performed velocities
        var perfVelocities = pairs.Select(p => p.PerformanceNote.Velocity).ToList();
        var scoreVelocities = pairs.Select(p => p.ScoreNote.Velocity).ToList();
        
        int perfMin = perfVelocities.Min();
        int perfMax = perfVelocities.Max();
        int perfRange = perfMax - perfMin;
        
        int scoreMin = scoreVelocities.Min();
        int scoreMax = scoreVelocities.Max();
        int scoreRange = scoreMax - scoreMin;
        
        double rangeRatio = scoreRange > 0 ? (double)perfRange / scoreRange : 1.0;
        
        // Check if dynamic range is compressed
        if (rangeRatio < 0.5 && scoreRange > 30)
        {
            issues.Add(new EvaluationIssue
            {
                Severity = IssueSeverity.Moderate,
                Type = IssueType.FlatDynamics,
                Description = $"Dynamic range compressed - using only {perfRange} velocity range vs expected {scoreRange}",
                Suggestion = "Exaggerate the dynamics more - make soft sections softer and loud sections louder"
            });
        }
        else if (rangeRatio > 1.5)
        {
            issues.Add(new EvaluationIssue
            {
                Severity = IssueSeverity.Minor,
                Type = IssueType.FlatDynamics,
                Description = "Dynamics may be exaggerated beyond what the piece calls for",
                Suggestion = "While expression is good, ensure dynamics serve the musical context"
            });
        }
        
        // Calculate distribution across dynamic levels
        var distribution = new Dictionary<DynamicLevel, int>();
        foreach (DynamicLevel level in Enum.GetValues<DynamicLevel>())
        {
            distribution[level] = 0;
        }
        
        foreach (var v in perfVelocities)
        {
            var level = VelocityToDynamicLevel(v);
            distribution[level]++;
        }
        
        return new DynamicRangeAnalysis(perfRange, scoreRange, rangeRatio, distribution, issues);
    }
    
    private DynamicContourAnalysis AnalyzeDynamicContour(
        IReadOnlyList<AlignedNotePair> pairs,
        Score score)
    {
        var issues = new List<EvaluationIssue>();
        
        if (pairs.Count < 4)
        {
            return new DynamicContourAnalysis(1.0, 1.0, 1.0, issues);
        }
        
        // Detect expected crescendos and diminuendos from score
        var expectedContours = DetectExpectedContours(pairs.Select(p => p.ScoreNote).ToList());
        var actualContours = DetectActualContours(pairs);
        
        int crescendoMatches = 0, crescendoTotal = 0;
        int diminuendoMatches = 0, diminuendoTotal = 0;
        
        foreach (var expected in expectedContours)
        {
            var matching = actualContours.FirstOrDefault(a => 
                Math.Abs(a.StartIndex - expected.StartIndex) <= 2 &&
                Math.Abs(a.EndIndex - expected.EndIndex) <= 2);
            
            if (expected.IsCrescendo)
            {
                crescendoTotal++;
                if (matching != null && matching.IsCrescendo)
                    crescendoMatches++;
            }
            else
            {
                diminuendoTotal++;
                if (matching != null && !matching.IsCrescendo)
                    diminuendoMatches++;
            }
        }
        
        double crescendoAcc = crescendoTotal > 0 ? (double)crescendoMatches / crescendoTotal : 1.0;
        double diminuendoAcc = diminuendoTotal > 0 ? (double)diminuendoMatches / diminuendoTotal : 1.0;
        double overallAcc = (crescendoAcc + diminuendoAcc) / 2;
        
        if (crescendoAcc < 0.5 && crescendoTotal > 0)
        {
            issues.Add(new EvaluationIssue
            {
                Severity = IssueSeverity.Moderate,
                Type = IssueType.FlatDynamics,
                Description = "Crescendos not clearly executed - volume doesn't build as expected",
                Suggestion = "Mark crescendos in your score and consciously increase volume through them"
            });
        }
        
        if (diminuendoAcc < 0.5 && diminuendoTotal > 0)
        {
            issues.Add(new EvaluationIssue
            {
                Severity = IssueSeverity.Moderate,
                Type = IssueType.FlatDynamics,
                Description = "Diminuendos not clearly executed - volume doesn't decrease as expected",
                Suggestion = "Practice gradual volume reduction - think of 'fading away'"
            });
        }
        
        return new DynamicContourAnalysis(overallAcc, crescendoAcc, diminuendoAcc, issues);
    }
    
    private SectionDynamicsAnalysis AnalyzeSectionDynamics(
        IReadOnlyList<AlignedNotePair> pairs,
        Score score)
    {
        var issues = new List<EvaluationIssue>();
        var sections = new List<SectionDynamicInfo>();
        
        // Group by measures (4-measure sections)
        var byMeasure = pairs.GroupBy(p => (p.ScoreNote.Measure - 1) / 4);
        
        foreach (var group in byMeasure.OrderBy(g => g.Key))
        {
            int startMeasure = group.Key * 4 + 1;
            int endMeasure = Math.Min(startMeasure + 3, score.TotalMeasures);
            
            var sectionPairs = group.ToList();
            if (sectionPairs.Count == 0) continue;
            
            double avgExpected = sectionPairs.Average(p => p.ScoreNote.Velocity);
            double avgActual = sectionPairs.Average(p => p.PerformanceNote.Velocity);
            double deviation = avgActual - avgExpected;
            
            var expectedLevel = VelocityToDynamicLevel((int)avgExpected);
            var actualLevel = VelocityToDynamicLevel((int)avgActual);
            
            sections.Add(new SectionDynamicInfo
            {
                StartMeasure = startMeasure,
                EndMeasure = endMeasure,
                ExpectedDynamic = expectedLevel,
                ActualDynamic = actualLevel,
                ExpectedVelocity = avgExpected,
                ActualVelocity = avgActual,
                Deviation = deviation,
                IsAccurate = Math.Abs(deviation) <= VelocityTolerance
            });
            
            if (Math.Abs(deviation) > VelocityTolerance * 2)
            {
                string direction = deviation > 0 ? "louder" : "softer";
                issues.Add(new EvaluationIssue
                {
                    Severity = IssueSeverity.Minor,
                    Type = deviation > 0 ? IssueType.ToeLoud : IssueType.TooSoft,
                    Description = $"Measures {startMeasure}-{endMeasure}: playing {direction} than marked ({actualLevel} vs {expectedLevel})",
                    Measure = startMeasure,
                    Suggestion = $"This section should be {expectedLevel} - adjust your touch"
                });
            }
        }
        
        return new SectionDynamicsAnalysis(sections, issues);
    }
    
    private List<DynamicContour> DetectExpectedContours(List<ScoreNote> notes)
    {
        var contours = new List<DynamicContour>();
        
        // Simple contour detection: look for consistent velocity changes
        int windowSize = 4;
        for (int i = 0; i < notes.Count - windowSize; i++)
        {
            var window = notes.Skip(i).Take(windowSize).ToList();
            int velocityChange = window.Last().Velocity - window.First().Velocity;
            
            if (velocityChange > MinDynamicChange)
            {
                contours.Add(new DynamicContour(i, i + windowSize - 1, true, velocityChange));
                i += windowSize - 1; // Skip ahead
            }
            else if (velocityChange < -MinDynamicChange)
            {
                contours.Add(new DynamicContour(i, i + windowSize - 1, false, velocityChange));
                i += windowSize - 1;
            }
        }
        
        return contours;
    }
    
    private List<DynamicContour> DetectActualContours(IReadOnlyList<AlignedNotePair> pairs)
    {
        var contours = new List<DynamicContour>();
        
        int windowSize = 4;
        for (int i = 0; i < pairs.Count - windowSize; i++)
        {
            var window = pairs.Skip(i).Take(windowSize).ToList();
            int velocityChange = window.Last().PerformanceNote.Velocity - window.First().PerformanceNote.Velocity;
            
            if (Math.Abs(velocityChange) > MinDynamicChange)
            {
                contours.Add(new DynamicContour(i, i + windowSize - 1, velocityChange > 0, velocityChange));
            }
        }
        
        return contours;
    }
    
    private List<VelocityPoint> ExtractVelocityCurve(IReadOnlyList<AlignedNotePair> pairs)
    {
        return pairs.Select(p => new VelocityPoint
        {
            TimeMs = p.PerformanceNote.StartTimeMs,
            ExpectedVelocity = p.ScoreNote.Velocity,
            ActualVelocity = p.PerformanceNote.Velocity,
            Measure = p.ScoreNote.Measure,
            Beat = p.ScoreNote.Beat
        }).ToList();
    }
    
    private double CalculateScore(
        VelocityAnalysis velocity,
        DynamicRangeAnalysis range,
        DynamicContourAnalysis contour)
    {
        double score = 100;
        
        // Penalty for velocity accuracy (40% weight)
        double velocityPenalty = Math.Min(velocity.MeanAbsError / 2, 20);
        score -= velocityPenalty * 0.4;
        
        // Penalty for range compression (30% weight)
        if (range.RangeRatio < 1.0)
        {
            double rangePenalty = (1.0 - range.RangeRatio) * 30;
            score -= rangePenalty * 0.3;
        }
        
        // Bonus/penalty for contour accuracy (30% weight)
        double contourScore = contour.Accuracy * 30;
        score = score - 15 + (contourScore * 0.5);
        
        return Math.Max(0, Math.Min(100, score));
    }
    
    private string GenerateSummary(double score, DynamicRangeAnalysis range, DynamicContourAnalysis contour)
    {
        if (score >= 90)
            return "Excellent dynamic expression! Your playing has beautiful musical shaping.";
        if (score >= 80)
            return $"Good dynamics with a range of {range.Range} velocity units. Minor refinements needed.";
        if (score >= 70)
            return "Fair dynamic control. Focus on making dynamic contrasts more pronounced.";
        return "Dynamics need significant work. Practice exaggerating loud and soft passages.";
    }
    
    private DynamicLevel VelocityToDynamicLevel(int velocity) => velocity switch
    {
        <= 0 => DynamicLevel.Silent,
        <= 31 => DynamicLevel.Pianissimo,
        <= 47 => DynamicLevel.Piano,
        <= 63 => DynamicLevel.MezzoPiano,
        <= 79 => DynamicLevel.MezzoForte,
        <= 95 => DynamicLevel.Forte,
        _ => DynamicLevel.Fortissimo
    };
    
    private double CalculateStdDev(List<double> values)
    {
        if (values.Count <= 1) return 0;
        double avg = values.Average();
        return Math.Sqrt(values.Sum(v => Math.Pow(v - avg, 2)) / (values.Count - 1));
    }
    
    private DynamicsResult CreateEmptyResult() => new()
    {
        Score = 0,
        Summary = "No notes to evaluate.",
        Issues = [],
        VelocityDistribution = new Dictionary<DynamicLevel, int>(),
        SectionDynamics = [],
        VelocityCurve = []
    };
}

// Analysis helper records
internal record VelocityAnalysis(
    double MeanError, 
    double MeanAbsError, 
    double StdDev, 
    List<EvaluationIssue> Issues);

internal record DynamicRangeAnalysis(
    int Range, 
    int ExpectedRange, 
    double RangeRatio, 
    Dictionary<DynamicLevel, int> Distribution,
    List<EvaluationIssue> Issues);

internal record DynamicContourAnalysis(
    double Accuracy, 
    double CrescendoAccuracy, 
    double DiminuendoAccuracy,
    List<EvaluationIssue> Issues);

internal record SectionDynamicsAnalysis(
    List<SectionDynamicInfo> Sections,
    List<EvaluationIssue> Issues);

internal record DynamicContour(
    int StartIndex, 
    int EndIndex, 
    bool IsCrescendo, 
    int VelocityChange);

/// <summary>
/// Result of dynamics evaluation.
/// </summary>
public record DynamicsResult : EvaluationResult
{
    /// <summary>
    /// Mean velocity error (positive = too loud).
    /// </summary>
    public double MeanVelocityError { get; init; }
    
    /// <summary>
    /// Standard deviation of velocity errors.
    /// </summary>
    public double VelocityErrorStdDev { get; init; }
    
    /// <summary>
    /// Actual dynamic range used (max - min velocity).
    /// </summary>
    public int DynamicRange { get; init; }
    
    /// <summary>
    /// Expected dynamic range from score.
    /// </summary>
    public int ExpectedDynamicRange { get; init; }
    
    /// <summary>
    /// Ratio of actual to expected dynamic range.
    /// </summary>
    public double DynamicRangeRatio { get; init; }
    
    /// <summary>
    /// Distribution of notes across dynamic levels.
    /// </summary>
    public Dictionary<DynamicLevel, int> VelocityDistribution { get; init; } = new();
    
    /// <summary>
    /// Accuracy of following dynamic contours (crescendo/diminuendo).
    /// </summary>
    public double DynamicContourAccuracy { get; init; }
    
    /// <summary>
    /// Accuracy of crescendo execution.
    /// </summary>
    public double CrescendoAccuracy { get; init; }
    
    /// <summary>
    /// Accuracy of diminuendo execution.
    /// </summary>
    public double DiminuendoAccuracy { get; init; }
    
    /// <summary>
    /// Dynamic analysis by section.
    /// </summary>
    public IReadOnlyList<SectionDynamicInfo> SectionDynamics { get; init; } = [];
    
    /// <summary>
    /// Velocity curve data for visualization.
    /// </summary>
    public IReadOnlyList<VelocityPoint> VelocityCurve { get; init; } = [];
}

/// <summary>
/// Dynamic information for a section of the piece.
/// </summary>
public record SectionDynamicInfo
{
    public int StartMeasure { get; init; }
    public int EndMeasure { get; init; }
    public DynamicLevel ExpectedDynamic { get; init; }
    public DynamicLevel ActualDynamic { get; init; }
    public double ExpectedVelocity { get; init; }
    public double ActualVelocity { get; init; }
    public double Deviation { get; init; }
    public bool IsAccurate { get; init; }
}

/// <summary>
/// A point on the velocity curve.
/// </summary>
public record VelocityPoint
{
    public double TimeMs { get; init; }
    public int ExpectedVelocity { get; init; }
    public int ActualVelocity { get; init; }
    public int Measure { get; init; }
    public double Beat { get; init; }
}

