using ListeningBrain.Alignment;
using ListeningBrain.Core.Models;
using ListeningBrain.Evaluation;
using ListeningBrain.Feedback;

namespace ListeningBrain.Intelligence;

/// <summary>
/// Generates personalized feedback based on student profile and learning history.
/// </summary>
public class PersonalizedFeedbackGenerator
{
    private readonly FeedbackGenerator _baseFeedbackGenerator = new();
    private readonly ErrorPatternRecognizer _patternRecognizer = new();
    
    /// <summary>
    /// Generates personalized feedback considering student history.
    /// </summary>
    public PersonalizedFeedbackReport Generate(
        AlignmentResult alignment,
        NoteAccuracyResult accuracyResult,
        RhythmResult rhythmResult,
        TempoResult tempoResult,
        ExpressionResult? expressionResult,
        Score score,
        StudentProfile profile)
    {
        // Get base feedback
        var baseFeedback = _baseFeedbackGenerator.Generate(
            alignment, accuracyResult, rhythmResult, tempoResult, score);
        
        // Analyze patterns
        var patterns = _patternRecognizer.AnalyzePatterns(
            alignment, score, accuracyResult, rhythmResult);
        
        // Generate personalized summary
        var personalizedSummary = GeneratePersonalizedSummary(
            baseFeedback, profile, patterns);
        
        // Generate targeted suggestions
        var targetedSuggestions = GenerateTargetedSuggestions(
            baseFeedback, profile, patterns);
        
        // Track progress comparison
        var progressComparison = CompareWithHistory(
            accuracyResult, rhythmResult, tempoResult, profile);
        
        // Generate encouragement based on preference
        var encouragement = GenerateEncouragement(
            baseFeedback.OverallScore, profile, progressComparison);
        
        // Create skill-specific insights
        var skillInsights = GenerateSkillInsights(
            accuracyResult, rhythmResult, tempoResult, expressionResult, profile);
        
        return new PersonalizedFeedbackReport
        {
            BaseFeedback = baseFeedback,
            PersonalizedSummary = personalizedSummary,
            TargetedSuggestions = targetedSuggestions,
            ProgressComparison = progressComparison,
            Encouragement = encouragement,
            PatternAnalysis = patterns,
            SkillInsights = skillInsights,
            RecommendedNextSteps = GenerateNextSteps(profile, baseFeedback, patterns),
            EstimatedTimeToImprove = EstimateImprovementTime(profile, baseFeedback)
        };
    }
    
    private string GeneratePersonalizedSummary(
        FeedbackReport base_feedback,
        StudentProfile profile,
        ErrorPatternAnalysis patterns)
    {
        var parts = new List<string>();
        
        // Greeting based on history
        if (profile.PerformanceHistory.Count <= 1)
        {
            parts.Add($"Welcome, {profile.Name}! Let's see how you did.");
        }
        else if (profile.PracticeStreak > 5)
        {
            parts.Add($"Great to see you again, {profile.Name}! {profile.PracticeStreak} days in a row!");
        }
        else
        {
            parts.Add($"Good work, {profile.Name}.");
        }
        
        // Score context based on level
        double expectedForLevel = 60 + (profile.OverallLevel * 3);
        
        if (base_feedback.OverallScore >= expectedForLevel + 10)
        {
            parts.Add($"Outstanding! Your {base_feedback.OverallScore:F0}% score is above your usual level.");
        }
        else if (base_feedback.OverallScore >= expectedForLevel)
        {
            parts.Add($"Solid performance at {base_feedback.OverallScore:F0}%.");
        }
        else
        {
            parts.Add($"This piece is challenging you - {base_feedback.OverallScore:F0}% is a starting point to build from.");
        }
        
        // Reference known strengths/weaknesses
        if (profile.Strengths.Any())
        {
            string strength = profile.Strengths.First();
            if (base_feedback.NoteAccuracyScore >= 85 && strength == "Note Accuracy")
            {
                parts.Add("Your strong note accuracy continues to shine!");
            }
        }
        
        if (profile.Weaknesses.Any())
        {
            string weakness = profile.Weaknesses.First();
            bool improved = weakness switch
            {
                "Rhythm" => base_feedback.RhythmScore > profile.Skills.Rhythm,
                "Dynamics" => (base_feedback.NoteAccuracyScore ?? 0) > profile.Skills.Dynamics,
                _ => false
            };
            
            if (improved)
            {
                parts.Add($"I see improvement in {weakness} - keep it up!");
            }
        }
        
        // Pattern insight
        if (patterns.TopPattern != null)
        {
            var isRecurring = profile.RecurringPatterns
                .Any(r => r.Type == patterns.TopPattern.Type);
            
            if (isRecurring)
            {
                parts.Add($"Remember to watch for your tendency to {GetPatternDescription(patterns.TopPattern.Type)}.");
            }
        }
        
        return string.Join(" ", parts);
    }
    
    private List<TargetedSuggestion> GenerateTargetedSuggestions(
        FeedbackReport baseFeedback,
        StudentProfile profile,
        ErrorPatternAnalysis patterns)
    {
        var suggestions = new List<TargetedSuggestion>();
        
        // Prioritize based on student's weakest areas
        var priorityAreas = profile.GetPriorityPracticeAreas();
        
        foreach (var area in priorityAreas)
        {
            var suggestion = area switch
            {
                "Note Accuracy" when baseFeedback.NoteAccuracyScore < 80 => new TargetedSuggestion
                {
                    Area = "Note Accuracy",
                    Priority = 1,
                    Title = "Build Your Note Accuracy Foundation",
                    Description = "Note accuracy is foundational - let's strengthen it.",
                    ActionItems = new[]
                    {
                        "Practice hands separately at 50% tempo",
                        "Name notes aloud as you play",
                        "Use a metronome to prevent rushing through difficult spots",
                        $"Focus on measures {string.Join(", ", baseFeedback.ProblemMeasures.Take(3))}"
                    }.ToList(),
                    EstimatedPracticeMinutes = 15,
                    SkillImpact = new Dictionary<string, double>
                    {
                        ["NoteAccuracy"] = 5.0,
                        ["SightReading"] = 2.0
                    }
                },
                
                "Rhythm" when baseFeedback.RhythmScore < 80 => new TargetedSuggestion
                {
                    Area = "Rhythm",
                    Priority = 1,
                    Title = "Develop Your Internal Clock",
                    Description = "Consistent rhythm is essential for musical playing.",
                    ActionItems = new[]
                    {
                        "Practice with a metronome every session",
                        "Tap rhythms on a table before playing",
                        "Record yourself and listen for timing issues",
                        "Practice difficult passages at different tempos"
                    }.ToList(),
                    EstimatedPracticeMinutes = 10,
                    SkillImpact = new Dictionary<string, double>
                    {
                        ["Rhythm"] = 5.0,
                        ["TempoControl"] = 3.0
                    }
                },
                
                "Dynamics" when (baseFeedback.NoteAccuracyScore ?? 0) < 75 => new TargetedSuggestion
                {
                    Area = "Dynamics",
                    Priority = 2,
                    Title = "Bring Music to Life with Dynamics",
                    Description = "Dynamics create expression and interest.",
                    ActionItems = new[]
                    {
                        "Exaggerate dynamics - make loud LOUD and soft SOFT",
                        "Practice crescendos and diminuendos on scales",
                        "Mark dynamic levels in your score",
                        "Listen to professional recordings for inspiration"
                    }.ToList(),
                    EstimatedPracticeMinutes = 10,
                    SkillImpact = new Dictionary<string, double>
                    {
                        ["Dynamics"] = 5.0,
                        ["Phrasing"] = 2.0
                    }
                },
                
                _ => null
            };
            
            if (suggestion != null)
            {
                suggestions.Add(suggestion);
            }
        }
        
        // Add pattern-based suggestions
        foreach (var pattern in patterns.Patterns.Take(2))
        {
            suggestions.Add(new TargetedSuggestion
            {
                Area = "Pattern Correction",
                Priority = pattern.Severity == PatternSeverity.Significant ? 1 : 2,
                Title = $"Address: {GetPatternTitle(pattern.Type)}",
                Description = pattern.Description,
                ActionItems = new[] { pattern.PracticeRecommendation }.ToList(),
                EstimatedPracticeMinutes = 10,
                AffectedMeasures = pattern.AffectedMeasures
            });
        }
        
        return suggestions.OrderBy(s => s.Priority).ToList();
    }
    
    private ProgressComparison CompareWithHistory(
        NoteAccuracyResult accuracy,
        RhythmResult rhythm,
        TempoResult tempo,
        StudentProfile profile)
    {
        var recent = profile.PerformanceHistory
            .OrderByDescending(p => p.Timestamp)
            .Take(5)
            .ToList();
        
        if (recent.Count == 0)
        {
            return new ProgressComparison
            {
                HasPreviousData = false,
                Message = "This is your first session - let's establish your baseline!"
            };
        }
        
        double prevAccuracy = recent.Average(p => p.NoteAccuracyScore);
        double prevRhythm = recent.Average(p => p.RhythmScore);
        double prevTempo = recent.Average(p => p.TempoScore);
        double prevOverall = recent.Average(p => p.OverallScore);
        
        double currentOverall = (accuracy.Score + rhythm.Score + tempo.Score) / 3;
        
        var trends = new Dictionary<string, TrendDirection>();
        trends["Note Accuracy"] = GetTrend(accuracy.Score, prevAccuracy);
        trends["Rhythm"] = GetTrend(rhythm.Score, prevRhythm);
        trends["Tempo"] = GetTrend(tempo.Score, prevTempo);
        trends["Overall"] = GetTrend(currentOverall, prevOverall);
        
        string message;
        if (trends["Overall"] == TrendDirection.Improving)
        {
            message = $"Great progress! Your overall score improved by {currentOverall - prevOverall:F0} points.";
        }
        else if (trends["Overall"] == TrendDirection.Declining)
        {
            message = "Today was challenging. That's part of learning - consistency matters more than any single session.";
        }
        else
        {
            message = "You're maintaining your level consistently.";
        }
        
        return new ProgressComparison
        {
            HasPreviousData = true,
            PreviousAverageScore = prevOverall,
            CurrentScore = currentOverall,
            ScoreChange = currentOverall - prevOverall,
            Trends = trends,
            Message = message,
            DaysSinceLastSession = recent.Count > 0 
                ? (int)(DateTime.UtcNow - recent.First().Timestamp).TotalDays 
                : 0
        };
    }
    
    private string GenerateEncouragement(
        double score,
        StudentProfile profile,
        ProgressComparison progress)
    {
        var messages = new List<string>();
        
        switch (profile.FeedbackPreference)
        {
            case FeedbackPreference.Encouraging:
                messages.Add(score >= 70
                    ? "You're doing wonderfully! Every practice session builds your skills."
                    : "Remember, challenging pieces help us grow. You're making progress!");
                
                if (profile.PracticeStreak > 0)
                {
                    messages.Add($"Keep your {profile.PracticeStreak}-day streak going!");
                }
                break;
                
            case FeedbackPreference.Balanced:
                if (progress.Trends.GetValueOrDefault("Overall") == TrendDirection.Improving)
                {
                    messages.Add("Your dedication is showing in your progress.");
                }
                messages.Add("Focus on the suggestions above and you'll see improvement.");
                break;
                
            case FeedbackPreference.Critical:
                messages.Add($"Current level: {profile.OverallLevel:F1}/10. Target the weakest areas for efficient improvement.");
                break;
                
            case FeedbackPreference.Technical:
                messages.Add($"Statistical improvement rate: {CalculateImprovementRate(profile):F1}%/week based on recent sessions.");
                break;
        }
        
        return string.Join(" ", messages);
    }
    
    private List<SkillInsight> GenerateSkillInsights(
        NoteAccuracyResult accuracy,
        RhythmResult rhythm,
        TempoResult tempo,
        ExpressionResult? expression,
        StudentProfile profile)
    {
        var insights = new List<SkillInsight>();
        
        // Note accuracy insight
        insights.Add(new SkillInsight
        {
            SkillName = "Note Accuracy",
            CurrentScore = accuracy.Score,
            ProfileScore = profile.Skills.NoteAccuracy,
            Trend = GetTrend(accuracy.Score, profile.Skills.NoteAccuracy),
            Insight = accuracy.Score > profile.Skills.NoteAccuracy + 5
                ? "Above your average - great focus today!"
                : accuracy.Score < profile.Skills.NoteAccuracy - 5
                    ? "Below your average - this piece may need more practice"
                    : "Consistent with your skill level"
        });
        
        // Rhythm insight
        insights.Add(new SkillInsight
        {
            SkillName = "Rhythm",
            CurrentScore = rhythm.Score,
            ProfileScore = profile.Skills.Rhythm,
            Trend = GetTrend(rhythm.Score, profile.Skills.Rhythm),
            Insight = rhythm.OnTimePercent > 80
                ? "Excellent timing consistency!"
                : $"Work on timing - {rhythm.OnTimePercent:F0}% of notes were on time"
        });
        
        // Expression insight
        if (expression != null)
        {
            insights.Add(new SkillInsight
            {
                SkillName = "Expression",
                CurrentScore = expression.Score,
                ProfileScore = (profile.Skills.Dynamics + profile.Skills.Phrasing) / 2,
                Trend = GetTrend(expression.Score, (profile.Skills.Dynamics + profile.Skills.Phrasing) / 2),
                Insight = $"Expression character: {expression.ExpressionCharacter}"
            });
        }
        
        return insights;
    }
    
    private List<string> GenerateNextSteps(
        StudentProfile profile,
        FeedbackReport feedback,
        ErrorPatternAnalysis patterns)
    {
        var steps = new List<string>();
        
        // Based on current performance
        if (feedback.OverallScore >= 90)
        {
            steps.Add("Consider learning a more challenging piece");
            steps.Add("Focus on musical expression and interpretation");
        }
        else if (feedback.OverallScore >= 75)
        {
            steps.Add("Continue practicing this piece for refinement");
            steps.Add("Work on the specific issues identified");
        }
        else
        {
            steps.Add("Slow down and practice hands separately");
            steps.Add("Focus on problem measures before playing through");
        }
        
        // Based on patterns
        if (patterns.Patterns.Any(p => p.Type == ErrorPatternType.RushingPattern))
        {
            steps.Add("Practice with a metronome at a slower tempo");
        }
        
        // Based on goals
        var incompleteGoals = profile.Goals.Where(g => !g.IsComplete).Take(2);
        foreach (var goal in incompleteGoals)
        {
            steps.Add($"Work toward goal: {goal.Title} ({goal.Progress:P0} complete)");
        }
        
        return steps.Take(5).ToList();
    }
    
    private string EstimateImprovementTime(StudentProfile profile, FeedbackReport feedback)
    {
        double targetScore = 85;
        double currentScore = feedback.OverallScore;
        
        if (currentScore >= targetScore)
            return "You've reached proficiency! Focus on musical refinement.";
        
        double gap = targetScore - currentScore;
        double improvementRate = CalculateImprovementRate(profile);
        
        if (improvementRate <= 0)
            improvementRate = 2.0; // Default estimate
        
        int weeksNeeded = (int)Math.Ceiling(gap / improvementRate);
        
        if (weeksNeeded <= 1)
            return "With focused practice, you could master this piece within a week!";
        if (weeksNeeded <= 4)
            return $"Estimated {weeksNeeded} weeks to proficiency with regular practice.";
        
        return $"This is a challenging piece - expect {weeksNeeded} weeks of practice for mastery.";
    }
    
    private double CalculateImprovementRate(StudentProfile profile)
    {
        var history = profile.PerformanceHistory
            .OrderBy(p => p.Timestamp)
            .ToList();
        
        if (history.Count < 3)
            return 2.0; // Default estimate
        
        // Calculate weekly improvement rate
        var firstWeek = history.Take(history.Count / 2).Average(p => p.OverallScore);
        var secondWeek = history.Skip(history.Count / 2).Average(p => p.OverallScore);
        
        return secondWeek - firstWeek;
    }
    
    private TrendDirection GetTrend(double current, double previous)
    {
        double diff = current - previous;
        if (diff > 3) return TrendDirection.Improving;
        if (diff < -3) return TrendDirection.Declining;
        return TrendDirection.Stable;
    }
    
    private string GetPatternDescription(ErrorPatternType type) => type switch
    {
        ErrorPatternType.RushingPattern => "rush through difficult passages",
        ErrorPatternType.DraggingPattern => "slow down when uncertain",
        ErrorPatternType.IntervalError => "miss interval distances",
        ErrorPatternType.LeapError => "miss large jumps",
        ErrorPatternType.ChordError => "miss notes in chords",
        _ => "make this type of error"
    };
    
    private string GetPatternTitle(ErrorPatternType type) => type switch
    {
        ErrorPatternType.RushingPattern => "Rushing Tendency",
        ErrorPatternType.DraggingPattern => "Dragging Tendency",
        ErrorPatternType.IntervalError => "Interval Accuracy",
        ErrorPatternType.LeapError => "Large Leap Accuracy",
        ErrorPatternType.ChordError => "Chord Voicing",
        ErrorPatternType.DifficultPassage => "Difficult Passage",
        _ => "Error Pattern"
    };
}

/// <summary>
/// Trend direction for skill comparison.
/// </summary>
public enum TrendDirection
{
    Declining,
    Stable,
    Improving
}

/// <summary>
/// Personalized feedback report.
/// </summary>
public record PersonalizedFeedbackReport
{
    public required FeedbackReport BaseFeedback { get; init; }
    public required string PersonalizedSummary { get; init; }
    public required List<TargetedSuggestion> TargetedSuggestions { get; init; }
    public required ProgressComparison ProgressComparison { get; init; }
    public required string Encouragement { get; init; }
    public required ErrorPatternAnalysis PatternAnalysis { get; init; }
    public required List<SkillInsight> SkillInsights { get; init; }
    public required List<string> RecommendedNextSteps { get; init; }
    public required string EstimatedTimeToImprove { get; init; }
}

/// <summary>
/// A targeted practice suggestion.
/// </summary>
public record TargetedSuggestion
{
    public string Area { get; init; } = "";
    public int Priority { get; init; }
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public List<string> ActionItems { get; init; } = [];
    public int EstimatedPracticeMinutes { get; init; }
    public Dictionary<string, double>? SkillImpact { get; init; }
    public List<int>? AffectedMeasures { get; init; }
}

/// <summary>
/// Comparison with historical performance.
/// </summary>
public record ProgressComparison
{
    public bool HasPreviousData { get; init; }
    public double PreviousAverageScore { get; init; }
    public double CurrentScore { get; init; }
    public double ScoreChange { get; init; }
    public Dictionary<string, TrendDirection> Trends { get; init; } = new();
    public string Message { get; init; } = "";
    public int DaysSinceLastSession { get; init; }
}

/// <summary>
/// Insight about a specific skill.
/// </summary>
public record SkillInsight
{
    public string SkillName { get; init; } = "";
    public double CurrentScore { get; init; }
    public double ProfileScore { get; init; }
    public TrendDirection Trend { get; init; }
    public string Insight { get; init; } = "";
}

