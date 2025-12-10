using ListeningBrain.Core.Models;
using ListeningBrain.Evaluation;

namespace ListeningBrain.Intelligence;

/// <summary>
/// Represents a student's skill profile, tracking abilities across multiple dimensions.
/// Used for personalized feedback and adaptive difficulty.
/// </summary>
public class StudentProfile
{
    /// <summary>
    /// Unique identifier for the student.
    /// </summary>
    public required string StudentId { get; init; }
    
    /// <summary>
    /// Display name.
    /// </summary>
    public string Name { get; set; } = "";
    
    /// <summary>
    /// When the profile was created.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// Last practice session.
    /// </summary>
    public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Overall skill level (1-10).
    /// </summary>
    public double OverallLevel { get; set; } = 1.0;
    
    /// <summary>
    /// Individual skill dimensions.
    /// </summary>
    public SkillDimensions Skills { get; set; } = new();
    
    /// <summary>
    /// Historical performance data.
    /// </summary>
    public List<PerformanceRecord> PerformanceHistory { get; set; } = [];
    
    /// <summary>
    /// Known strengths.
    /// </summary>
    public List<string> Strengths { get; set; } = [];
    
    /// <summary>
    /// Areas needing improvement.
    /// </summary>
    public List<string> Weaknesses { get; set; } = [];
    
    /// <summary>
    /// Recurring error patterns.
    /// </summary>
    public List<RecurringPattern> RecurringPatterns { get; set; } = [];
    
    /// <summary>
    /// Preferred feedback style.
    /// </summary>
    public FeedbackPreference FeedbackPreference { get; set; } = FeedbackPreference.Balanced;
    
    /// <summary>
    /// Current practice goals.
    /// </summary>
    public List<PracticeGoal> Goals { get; set; } = [];
    
    /// <summary>
    /// Pieces in current repertoire.
    /// </summary>
    public List<RepertoireItem> Repertoire { get; set; } = [];
    
    /// <summary>
    /// Total practice time in minutes.
    /// </summary>
    public int TotalPracticeMinutes { get; set; }
    
    /// <summary>
    /// Practice streak (consecutive days).
    /// </summary>
    public int PracticeStreak { get; set; }
    
    /// <summary>
    /// Calculates the recommended difficulty level for new pieces.
    /// </summary>
    public double GetRecommendedDifficulty()
    {
        // Base on overall level with adjustment for recent progress
        double base_difficulty = OverallLevel;
        
        // Check recent trend
        var recentPerformances = PerformanceHistory
            .OrderByDescending(p => p.Timestamp)
            .Take(5)
            .ToList();
        
        if (recentPerformances.Count >= 3)
        {
            double avgRecent = recentPerformances.Average(p => p.OverallScore);
            
            if (avgRecent > 85)
            {
                base_difficulty += 0.5; // Ready for harder pieces
            }
            else if (avgRecent < 65)
            {
                base_difficulty -= 0.5; // Need easier pieces
            }
        }
        
        return Math.Max(1, Math.Min(10, base_difficulty));
    }
    
    /// <summary>
    /// Gets areas that need the most practice.
    /// </summary>
    public List<string> GetPriorityPracticeAreas()
    {
        var areas = new List<(string area, double score)>
        {
            ("Note Accuracy", Skills.NoteAccuracy),
            ("Rhythm", Skills.Rhythm),
            ("Tempo Control", Skills.TempoControl),
            ("Dynamics", Skills.Dynamics),
            ("Articulation", Skills.Articulation),
            ("Sight Reading", Skills.SightReading),
            ("Memorization", Skills.Memorization)
        };
        
        return areas
            .OrderBy(a => a.score)
            .Take(3)
            .Select(a => a.area)
            .ToList();
    }
}

/// <summary>
/// Individual skill dimensions tracked for each student.
/// All scores are 0-100.
/// </summary>
public record SkillDimensions
{
    public double NoteAccuracy { get; set; } = 50;
    public double Rhythm { get; set; } = 50;
    public double TempoControl { get; set; } = 50;
    public double Dynamics { get; set; } = 50;
    public double Articulation { get; set; } = 50;
    public double Phrasing { get; set; } = 50;
    public double PedalTechnique { get; set; } = 50;
    public double SightReading { get; set; } = 50;
    public double Memorization { get; set; } = 50;
    public double TechnicalFacility { get; set; } = 50;
    
    /// <summary>
    /// Gets overall average skill level.
    /// </summary>
    public double Average => (NoteAccuracy + Rhythm + TempoControl + Dynamics + 
                              Articulation + Phrasing + PedalTechnique + 
                              SightReading + Memorization + TechnicalFacility) / 10.0;
}

/// <summary>
/// Record of a single performance session.
/// </summary>
public record PerformanceRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string PieceTitle { get; init; } = "";
    public string? PieceId { get; init; }
    public double Difficulty { get; init; }
    public double OverallScore { get; init; }
    public double NoteAccuracyScore { get; init; }
    public double RhythmScore { get; init; }
    public double TempoScore { get; init; }
    public double DynamicsScore { get; init; }
    public double ExpressionScore { get; init; }
    public int DurationMinutes { get; init; }
    public List<string> IssuesIdentified { get; init; } = [];
    public List<ErrorPatternType> PatternsDetected { get; init; } = [];
}

/// <summary>
/// A recurring error pattern across sessions.
/// </summary>
public record RecurringPattern
{
    public ErrorPatternType Type { get; init; }
    public string Description { get; init; } = "";
    public int TimesDetected { get; init; }
    public DateTime FirstDetected { get; init; }
    public DateTime LastDetected { get; init; }
    public bool IsImproving { get; init; }
    public double ImprovementRate { get; init; }
}

/// <summary>
/// Student's feedback preference.
/// </summary>
public enum FeedbackPreference
{
    Encouraging,    // Focus on positives, gentle correction
    Balanced,       // Mix of praise and criticism
    Critical,       // Direct, focus on areas to improve
    Technical       // Detailed technical analysis
}

/// <summary>
/// A practice goal.
/// </summary>
public record PracticeGoal
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public GoalType Type { get; init; }
    public double TargetValue { get; init; }
    public double CurrentValue { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? TargetDate { get; init; }
    public bool IsComplete => CurrentValue >= TargetValue;
    public double Progress => TargetValue > 0 ? Math.Min(1, CurrentValue / TargetValue) : 0;
}

/// <summary>
/// Types of practice goals.
/// </summary>
public enum GoalType
{
    PracticeTime,       // Minutes practiced
    PieceCompletion,    // Finish learning a piece
    SkillImprovement,   // Improve specific skill
    AccuracyTarget,     // Hit accuracy percentage
    StreakTarget,       // Practice streak days
    RepertoireSize      // Number of pieces learned
}

/// <summary>
/// A piece in the student's repertoire.
/// </summary>
public record RepertoireItem
{
    public string PieceId { get; init; } = "";
    public string Title { get; init; } = "";
    public string? Composer { get; init; }
    public double Difficulty { get; init; }
    public RepertoireStatus Status { get; set; }
    public DateTime AddedAt { get; init; } = DateTime.UtcNow;
    public DateTime? MasteredAt { get; set; }
    public double BestScore { get; set; }
    public int PracticeCount { get; set; }
    public int TotalPracticeMinutes { get; set; }
}

/// <summary>
/// Status of a repertoire piece.
/// </summary>
public enum RepertoireStatus
{
    NotStarted,
    Learning,
    Practicing,
    PerformanceReady,
    Mastered,
    Archived
}

/// <summary>
/// Manages student profiles and learning analytics.
/// </summary>
public class StudentProfileManager
{
    private readonly Dictionary<string, StudentProfile> _profiles = new();
    
    /// <summary>
    /// Gets or creates a student profile.
    /// </summary>
    public StudentProfile GetOrCreateProfile(string studentId, string? name = null)
    {
        if (!_profiles.TryGetValue(studentId, out var profile))
        {
            profile = new StudentProfile
            {
                StudentId = studentId,
                Name = name ?? $"Student {studentId[..8]}"
            };
            _profiles[studentId] = profile;
        }
        
        return profile;
    }
    
    /// <summary>
    /// Updates a profile with new performance data.
    /// </summary>
    public void RecordPerformance(
        string studentId,
        string pieceTitle,
        double difficulty,
        NoteAccuracyResult? accuracy,
        RhythmResult? rhythm,
        TempoResult? tempo,
        DynamicsResult? dynamics,
        ExpressionResult? expression,
        ErrorPatternAnalysis? patterns,
        int durationMinutes)
    {
        var profile = GetOrCreateProfile(studentId);
        
        // Create performance record
        var record = new PerformanceRecord
        {
            PieceTitle = pieceTitle,
            Difficulty = difficulty,
            OverallScore = CalculateOverall(accuracy, rhythm, tempo, dynamics, expression),
            NoteAccuracyScore = accuracy?.Score ?? 0,
            RhythmScore = rhythm?.Score ?? 0,
            TempoScore = tempo?.Score ?? 0,
            DynamicsScore = dynamics?.Score ?? 0,
            ExpressionScore = expression?.Score ?? 0,
            DurationMinutes = durationMinutes,
            IssuesIdentified = GatherIssues(accuracy, rhythm, tempo),
            PatternsDetected = patterns?.Patterns.Select(p => p.Type).ToList() ?? []
        };
        
        profile.PerformanceHistory.Add(record);
        
        // Update skills
        UpdateSkills(profile, accuracy, rhythm, tempo, dynamics, expression);
        
        // Update patterns
        if (patterns != null)
        {
            UpdateRecurringPatterns(profile, patterns);
        }
        
        // Update activity
        profile.LastActiveAt = DateTime.UtcNow;
        profile.TotalPracticeMinutes += durationMinutes;
        
        // Update strengths/weaknesses
        UpdateStrengthsAndWeaknesses(profile);
        
        // Recalculate overall level
        profile.OverallLevel = CalculateOverallLevel(profile);
    }
    
    private double CalculateOverall(
        NoteAccuracyResult? accuracy,
        RhythmResult? rhythm,
        TempoResult? tempo,
        DynamicsResult? dynamics,
        ExpressionResult? expression)
    {
        var scores = new List<double>();
        if (accuracy != null) scores.Add(accuracy.Score);
        if (rhythm != null) scores.Add(rhythm.Score);
        if (tempo != null) scores.Add(tempo.Score);
        if (dynamics != null) scores.Add(dynamics.Score);
        if (expression != null) scores.Add(expression.Score);
        
        return scores.Count > 0 ? scores.Average() : 0;
    }
    
    private List<string> GatherIssues(
        NoteAccuracyResult? accuracy,
        RhythmResult? rhythm,
        TempoResult? tempo)
    {
        var issues = new List<string>();
        
        if (accuracy != null)
            issues.AddRange(accuracy.Issues.Take(2).Select(i => i.Description));
        if (rhythm != null)
            issues.AddRange(rhythm.Issues.Take(2).Select(i => i.Description));
        if (tempo != null)
            issues.AddRange(tempo.Issues.Take(2).Select(i => i.Description));
        
        return issues;
    }
    
    private void UpdateSkills(
        StudentProfile profile,
        NoteAccuracyResult? accuracy,
        RhythmResult? rhythm,
        TempoResult? tempo,
        DynamicsResult? dynamics,
        ExpressionResult? expression)
    {
        // Exponential moving average for skill updates
        const double alpha = 0.3; // Learning rate
        
        if (accuracy != null)
            profile.Skills.NoteAccuracy = Lerp(profile.Skills.NoteAccuracy, accuracy.Score, alpha);
        
        if (rhythm != null)
            profile.Skills.Rhythm = Lerp(profile.Skills.Rhythm, rhythm.Score, alpha);
        
        if (tempo != null)
            profile.Skills.TempoControl = Lerp(profile.Skills.TempoControl, tempo.Score, alpha);
        
        if (dynamics != null)
            profile.Skills.Dynamics = Lerp(profile.Skills.Dynamics, dynamics.Score, alpha);
        
        if (expression != null)
        {
            profile.Skills.Articulation = Lerp(profile.Skills.Articulation, 
                expression.ArticulationScore, alpha);
            profile.Skills.Phrasing = Lerp(profile.Skills.Phrasing, 
                expression.PhraseScore, alpha);
            profile.Skills.PedalTechnique = Lerp(profile.Skills.PedalTechnique, 
                expression.PedalScore, alpha);
        }
    }
    
    private void UpdateRecurringPatterns(StudentProfile profile, ErrorPatternAnalysis patterns)
    {
        foreach (var pattern in patterns.Patterns)
        {
            var existing = profile.RecurringPatterns
                .FirstOrDefault(p => p.Type == pattern.Type);
            
            if (existing != null)
            {
                // Update existing pattern
                var index = profile.RecurringPatterns.IndexOf(existing);
                profile.RecurringPatterns[index] = existing with
                {
                    TimesDetected = existing.TimesDetected + 1,
                    LastDetected = DateTime.UtcNow,
                    IsImproving = pattern.Occurrences < existing.TimesDetected / 2
                };
            }
            else
            {
                // Add new pattern
                profile.RecurringPatterns.Add(new RecurringPattern
                {
                    Type = pattern.Type,
                    Description = pattern.Description,
                    TimesDetected = 1,
                    FirstDetected = DateTime.UtcNow,
                    LastDetected = DateTime.UtcNow
                });
            }
        }
    }
    
    private void UpdateStrengthsAndWeaknesses(StudentProfile profile)
    {
        var skills = new Dictionary<string, double>
        {
            ["Note Accuracy"] = profile.Skills.NoteAccuracy,
            ["Rhythm"] = profile.Skills.Rhythm,
            ["Tempo Control"] = profile.Skills.TempoControl,
            ["Dynamics"] = profile.Skills.Dynamics,
            ["Articulation"] = profile.Skills.Articulation,
            ["Phrasing"] = profile.Skills.Phrasing,
            ["Pedal Technique"] = profile.Skills.PedalTechnique
        };
        
        var sorted = skills.OrderByDescending(kv => kv.Value).ToList();
        
        profile.Strengths = sorted.Where(kv => kv.Value >= 75)
            .Take(3)
            .Select(kv => kv.Key)
            .ToList();
        
        profile.Weaknesses = sorted.Where(kv => kv.Value < 60)
            .TakeLast(3)
            .Select(kv => kv.Key)
            .ToList();
    }
    
    private double CalculateOverallLevel(StudentProfile profile)
    {
        // 1-10 scale based on average skills
        double avgSkill = profile.Skills.Average;
        return 1 + (avgSkill / 100.0) * 9.0;
    }
    
    private double Lerp(double current, double target, double alpha)
    {
        return current + (target - current) * alpha;
    }
}

