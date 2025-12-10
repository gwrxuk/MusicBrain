using ListeningBrain.Core.Models;

namespace ListeningBrain.Intelligence;

/// <summary>
/// Assesses and adapts difficulty levels based on student performance.
/// Provides intelligent difficulty recommendations for progressive learning.
/// </summary>
public class AdaptiveDifficultyAssessor
{
    /// <summary>
    /// Target success rate for optimal learning (not too easy, not too hard).
    /// </summary>
    public double TargetSuccessRate { get; init; } = 0.75;
    
    /// <summary>
    /// Minimum sessions before adjusting difficulty.
    /// </summary>
    public int MinSessionsForAdjustment { get; init; } = 3;
    
    /// <summary>
    /// Assesses the difficulty of a score.
    /// </summary>
    public DifficultyAssessment AssessScoreDifficulty(Score score)
    {
        var factors = new Dictionary<string, double>();
        
        // Note density (notes per second)
        double durationSeconds = score.TotalDurationMs / 1000.0;
        double noteDensity = durationSeconds > 0 ? score.Notes.Count / durationSeconds : 0;
        factors["Note Density"] = NormalizeDensity(noteDensity);
        
        // Polyphony (average simultaneous notes)
        double avgPolyphony = CalculateAveragePolyphony(score);
        factors["Polyphony"] = NormalizePolyphony(avgPolyphony);
        
        // Range (span of pitches used)
        int range = score.Notes.Max(n => n.Pitch) - score.Notes.Min(n => n.Pitch);
        factors["Pitch Range"] = NormalizeRange(range);
        
        // Rhythmic complexity
        double rhythmicComplexity = CalculateRhythmicComplexity(score);
        factors["Rhythmic Complexity"] = rhythmicComplexity;
        
        // Leap frequency (large intervals)
        double leapFrequency = CalculateLeapFrequency(score);
        factors["Leap Frequency"] = leapFrequency;
        
        // Accidental density (sharps/flats)
        double accidentalDensity = CalculateAccidentalDensity(score);
        factors["Accidentals"] = accidentalDensity;
        
        // Tempo
        double tempoDifficulty = NormalizeTempo(score.TempoMarkings.First().BPM);
        factors["Tempo"] = tempoDifficulty;
        
        // Calculate overall difficulty (1-10 scale)
        double overallDifficulty = CalculateOverallDifficulty(factors);
        
        return new DifficultyAssessment
        {
            OverallDifficulty = overallDifficulty,
            DifficultyLevel = GetDifficultyLevel(overallDifficulty),
            Factors = factors,
            TechnicalChallenges = IdentifyTechnicalChallenges(score, factors),
            RecommendedLevel = GetRecommendedStudentLevel(overallDifficulty),
            EstimatedPracticeHours = EstimatePracticeTime(overallDifficulty)
        };
    }
    
    /// <summary>
    /// Recommends difficulty adjustment based on student performance.
    /// </summary>
    public DifficultyRecommendation RecommendDifficulty(
        StudentProfile profile,
        double recentPerformanceScore)
    {
        var recentHistory = profile.PerformanceHistory
            .OrderByDescending(p => p.Timestamp)
            .Take(MinSessionsForAdjustment)
            .ToList();
        
        if (recentHistory.Count < MinSessionsForAdjustment)
        {
            return new DifficultyRecommendation
            {
                CurrentLevel = profile.OverallLevel,
                RecommendedLevel = profile.OverallLevel,
                Adjustment = DifficultyAdjustment.Maintain,
                Confidence = 0.5,
                Reasoning = "Not enough data yet - continue at current level"
            };
        }
        
        double avgScore = recentHistory.Average(p => p.OverallScore);
        double avgDifficulty = recentHistory.Average(p => p.Difficulty);
        
        // Calculate success rate relative to difficulty
        double successRate = avgScore / 100.0;
        
        DifficultyAdjustment adjustment;
        double newLevel;
        string reasoning;
        
        if (successRate > TargetSuccessRate + 0.1)
        {
            // Doing too well - increase challenge
            adjustment = DifficultyAdjustment.Increase;
            newLevel = Math.Min(10, profile.OverallLevel + 0.5);
            reasoning = $"Excellent performance ({avgScore:F0}%) - ready for more challenge!";
        }
        else if (successRate < TargetSuccessRate - 0.15)
        {
            // Struggling - decrease difficulty
            adjustment = DifficultyAdjustment.Decrease;
            newLevel = Math.Max(1, profile.OverallLevel - 0.3);
            reasoning = $"Finding current level challenging ({avgScore:F0}%) - building foundations";
        }
        else
        {
            // In the sweet spot
            adjustment = DifficultyAdjustment.Maintain;
            newLevel = profile.OverallLevel;
            reasoning = $"Good progress at current level ({avgScore:F0}%) - stay the course";
        }
        
        // Check for consistent improvement trend
        var trend = CalculatePerformanceTrend(recentHistory);
        if (trend > 5 && adjustment != DifficultyAdjustment.Increase)
        {
            adjustment = DifficultyAdjustment.Increase;
            newLevel = Math.Min(10, profile.OverallLevel + 0.3);
            reasoning = "Strong improvement trend - ready for next level";
        }
        
        return new DifficultyRecommendation
        {
            CurrentLevel = profile.OverallLevel,
            RecommendedLevel = newLevel,
            Adjustment = adjustment,
            Confidence = CalculateConfidence(recentHistory),
            Reasoning = reasoning,
            SuggestedPieceRange = GetSuggestedPieceRange(newLevel),
            SkillGaps = IdentifySkillGaps(profile)
        };
    }
    
    /// <summary>
    /// Suggests pieces appropriate for the student's level.
    /// </summary>
    public List<PieceSuggestion> SuggestPieces(
        StudentProfile profile,
        IEnumerable<Score> availablePieces)
    {
        var targetDifficulty = profile.GetRecommendedDifficulty();
        var suggestions = new List<PieceSuggestion>();
        
        foreach (var piece in availablePieces)
        {
            var assessment = AssessScoreDifficulty(piece);
            double difficultyMatch = 1.0 - Math.Abs(assessment.OverallDifficulty - targetDifficulty) / 10.0;
            
            // Check if piece addresses weak areas
            double weaknessMatchScore = CalculateWeaknessMatch(profile, assessment);
            
            // Check if already in repertoire
            bool inRepertoire = profile.Repertoire.Any(r => r.Title == piece.Title);
            
            if (!inRepertoire && difficultyMatch > 0.5)
            {
                suggestions.Add(new PieceSuggestion
                {
                    Title = piece.Title,
                    Composer = piece.Composer,
                    Difficulty = assessment.OverallDifficulty,
                    DifficultyLevel = assessment.DifficultyLevel,
                    MatchScore = difficultyMatch * 0.6 + weaknessMatchScore * 0.4,
                    Reasoning = GenerateSuggestionReasoning(profile, assessment),
                    TechnicalFocus = assessment.TechnicalChallenges.Take(3).ToList(),
                    EstimatedWeeksToLearn = EstimateLearningTime(profile, assessment)
                });
            }
        }
        
        return suggestions
            .OrderByDescending(s => s.MatchScore)
            .Take(5)
            .ToList();
    }
    
    // Calculation helpers
    private double CalculateAveragePolyphony(Score score)
    {
        if (score.Notes.Count == 0) return 1;
        
        var notesByTime = score.Notes.GroupBy(n => n.StartTick / 10); // Approximate grouping
        return notesByTime.Average(g => g.Count());
    }
    
    private double CalculateRhythmicComplexity(Score score)
    {
        if (score.Notes.Count == 0) return 0;
        
        // Count different rhythmic values
        var rhythmValues = score.Notes.Select(n => n.RhythmicValue).Distinct().Count();
        
        // Check for syncopation (notes starting off the beat)
        int offBeatNotes = score.Notes.Count(n => n.Beat % 1.0 > 0.1);
        double syncopationRatio = (double)offBeatNotes / score.Notes.Count;
        
        // Check for tuplets
        int tupletNotes = score.Notes.Count(n => n.IsTuplet);
        double tupletRatio = (double)tupletNotes / score.Notes.Count;
        
        return (rhythmValues / 10.0 + syncopationRatio + tupletRatio * 2) / 3.0;
    }
    
    private double CalculateLeapFrequency(Score score)
    {
        var sorted = score.Notes.OrderBy(n => n.StartTick).ToList();
        int largeLeaps = 0;
        
        for (int i = 1; i < sorted.Count; i++)
        {
            int interval = Math.Abs(sorted[i].Pitch - sorted[i - 1].Pitch);
            if (interval > 7) largeLeaps++; // More than a 5th
        }
        
        return sorted.Count > 1 ? (double)largeLeaps / (sorted.Count - 1) : 0;
    }
    
    private double CalculateAccidentalDensity(Score score)
    {
        // Estimate accidentals from key signature and note pitches
        var keySignature = score.KeySignatures.FirstOrDefault() ?? KeySignature.CMajor;
        
        // Notes outside the key signature scale
        var scaleNotes = GetScaleNotes(keySignature);
        int accidentals = score.Notes.Count(n => !scaleNotes.Contains(n.PitchClass));
        
        return score.Notes.Count > 0 ? (double)accidentals / score.Notes.Count : 0;
    }
    
    private HashSet<int> GetScaleNotes(KeySignature key)
    {
        // Simplified scale detection
        int[] majorScale = { 0, 2, 4, 5, 7, 9, 11 };
        int[] minorScale = { 0, 2, 3, 5, 7, 8, 10 };
        
        var scale = key.IsMinor ? minorScale : majorScale;
        return scale.Select(n => (n + key.RootPitchClass) % 12).ToHashSet();
    }
    
    private double NormalizeDensity(double notesPerSecond)
    {
        // 0 = very sparse (< 1 note/sec), 1 = very dense (> 10 notes/sec)
        return Math.Min(1, notesPerSecond / 10.0);
    }
    
    private double NormalizePolyphony(double avgSimultaneous)
    {
        // 0 = monophonic, 1 = 4+ voices
        return Math.Min(1, (avgSimultaneous - 1) / 3.0);
    }
    
    private double NormalizeRange(int semitones)
    {
        // 0 = 1 octave or less, 1 = 4+ octaves
        return Math.Min(1, (semitones - 12) / 36.0);
    }
    
    private double NormalizeTempo(double bpm)
    {
        // Difficulty increases with tempo extremes
        if (bpm < 60) return (60 - bpm) / 60.0; // Slow is difficult too
        if (bpm > 120) return (bpm - 120) / 120.0;
        return 0.2; // Moderate tempo
    }
    
    private double CalculateOverallDifficulty(Dictionary<string, double> factors)
    {
        // Weighted average of factors
        var weights = new Dictionary<string, double>
        {
            ["Note Density"] = 0.20,
            ["Polyphony"] = 0.15,
            ["Pitch Range"] = 0.10,
            ["Rhythmic Complexity"] = 0.20,
            ["Leap Frequency"] = 0.15,
            ["Accidentals"] = 0.10,
            ["Tempo"] = 0.10
        };
        
        double weighted = factors.Sum(f => f.Value * weights.GetValueOrDefault(f.Key, 0.1));
        
        // Scale to 1-10
        return 1 + weighted * 9;
    }
    
    private DifficultyLevel GetDifficultyLevel(double difficulty) => difficulty switch
    {
        < 2 => DifficultyLevel.Beginner,
        < 4 => DifficultyLevel.Elementary,
        < 5.5 => DifficultyLevel.Intermediate,
        < 7 => DifficultyLevel.Advanced,
        < 8.5 => DifficultyLevel.Professional,
        _ => DifficultyLevel.Virtuoso
    };
    
    private List<string> IdentifyTechnicalChallenges(Score score, Dictionary<string, double> factors)
    {
        var challenges = new List<string>();
        
        if (factors.GetValueOrDefault("Note Density") > 0.6)
            challenges.Add("Fast passages requiring finger agility");
        
        if (factors.GetValueOrDefault("Polyphony") > 0.5)
            challenges.Add("Multi-voice coordination");
        
        if (factors.GetValueOrDefault("Leap Frequency") > 0.3)
            challenges.Add("Large hand position shifts");
        
        if (factors.GetValueOrDefault("Rhythmic Complexity") > 0.5)
            challenges.Add("Complex rhythms and syncopation");
        
        if (factors.GetValueOrDefault("Pitch Range") > 0.6)
            challenges.Add("Wide keyboard range coverage");
        
        if (factors.GetValueOrDefault("Accidentals") > 0.3)
            challenges.Add("Chromatic passages and accidentals");
        
        return challenges;
    }
    
    private double GetRecommendedStudentLevel(double difficulty)
    {
        return difficulty; // 1-10 scale matches
    }
    
    private int EstimatePracticeTime(double difficulty)
    {
        // Rough estimate in hours
        return (int)Math.Ceiling(difficulty * 3);
    }
    
    private double CalculatePerformanceTrend(List<PerformanceRecord> history)
    {
        if (history.Count < 2) return 0;
        
        var sorted = history.OrderBy(p => p.Timestamp).ToList();
        double firstHalf = sorted.Take(sorted.Count / 2).Average(p => p.OverallScore);
        double secondHalf = sorted.Skip(sorted.Count / 2).Average(p => p.OverallScore);
        
        return secondHalf - firstHalf;
    }
    
    private double CalculateConfidence(List<PerformanceRecord> history)
    {
        if (history.Count < MinSessionsForAdjustment) return 0.5;
        
        // Higher confidence with more consistent scores
        double stdDev = CalculateStdDev(history.Select(p => p.OverallScore).ToList());
        return Math.Max(0.5, 1.0 - stdDev / 50);
    }
    
    private (double min, double max) GetSuggestedPieceRange(double level)
    {
        return (Math.Max(1, level - 0.5), Math.Min(10, level + 1.0));
    }
    
    private List<string> IdentifySkillGaps(StudentProfile profile)
    {
        return profile.GetPriorityPracticeAreas();
    }
    
    private double CalculateWeaknessMatch(StudentProfile profile, DifficultyAssessment assessment)
    {
        // Score higher if piece addresses student's weak areas
        double match = 0;
        
        foreach (var weakness in profile.Weaknesses)
        {
            if (weakness == "Rhythm" && assessment.Factors.GetValueOrDefault("Rhythmic Complexity") > 0.5)
                match += 0.3;
            if (weakness == "Dynamics" && assessment.TechnicalChallenges.Any(c => c.Contains("expression")))
                match += 0.3;
        }
        
        return Math.Min(1, match);
    }
    
    private string GenerateSuggestionReasoning(StudentProfile profile, DifficultyAssessment assessment)
    {
        var reasons = new List<string>();
        
        reasons.Add($"Difficulty {assessment.OverallDifficulty:F1}/10 matches your level");
        
        if (assessment.TechnicalChallenges.Any())
        {
            reasons.Add($"Develops: {assessment.TechnicalChallenges.First()}");
        }
        
        return string.Join(". ", reasons);
    }
    
    private int EstimateLearningTime(StudentProfile profile, DifficultyAssessment assessment)
    {
        double diffGap = assessment.OverallDifficulty - profile.OverallLevel;
        int baseWeeks = (int)assessment.OverallDifficulty;
        
        if (diffGap > 1) baseWeeks += (int)diffGap;
        
        return Math.Max(1, baseWeeks);
    }
    
    private double CalculateStdDev(List<double> values)
    {
        if (values.Count <= 1) return 0;
        double avg = values.Average();
        return Math.Sqrt(values.Sum(v => Math.Pow(v - avg, 2)) / (values.Count - 1));
    }
}

/// <summary>
/// Difficulty levels for pieces.
/// </summary>
public enum DifficultyLevel
{
    Beginner,
    Elementary,
    Intermediate,
    Advanced,
    Professional,
    Virtuoso
}

/// <summary>
/// Difficulty adjustment direction.
/// </summary>
public enum DifficultyAdjustment
{
    Decrease,
    Maintain,
    Increase
}

/// <summary>
/// Assessment of a piece's difficulty.
/// </summary>
public record DifficultyAssessment
{
    public double OverallDifficulty { get; init; }
    public DifficultyLevel DifficultyLevel { get; init; }
    public Dictionary<string, double> Factors { get; init; } = new();
    public List<string> TechnicalChallenges { get; init; } = [];
    public double RecommendedLevel { get; init; }
    public int EstimatedPracticeHours { get; init; }
}

/// <summary>
/// Recommendation for difficulty adjustment.
/// </summary>
public record DifficultyRecommendation
{
    public double CurrentLevel { get; init; }
    public double RecommendedLevel { get; init; }
    public DifficultyAdjustment Adjustment { get; init; }
    public double Confidence { get; init; }
    public string Reasoning { get; init; } = "";
    public (double min, double max) SuggestedPieceRange { get; init; }
    public List<string> SkillGaps { get; init; } = [];
}

/// <summary>
/// A piece suggestion for a student.
/// </summary>
public record PieceSuggestion
{
    public string Title { get; init; } = "";
    public string? Composer { get; init; }
    public double Difficulty { get; init; }
    public DifficultyLevel DifficultyLevel { get; init; }
    public double MatchScore { get; init; }
    public string Reasoning { get; init; } = "";
    public List<string> TechnicalFocus { get; init; } = [];
    public int EstimatedWeeksToLearn { get; init; }
}

