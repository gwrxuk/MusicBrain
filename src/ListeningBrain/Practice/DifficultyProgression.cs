using ListeningBrain.Intelligence;

namespace ListeningBrain.Practice;

/// <summary>
/// Manages difficulty progression and curriculum.
/// </summary>
public class DifficultyProgressionManager
{
    private readonly Dictionary<string, CurriculumPath> _curriculumPaths = new();
    
    /// <summary>
    /// Initializes with default curriculum paths.
    /// </summary>
    public DifficultyProgressionManager()
    {
        InitializeDefaultPaths();
    }
    
    /// <summary>
    /// Gets recommended difficulty level for a student.
    /// </summary>
    public DifficultyRecommendation GetRecommendedDifficulty(StudentProfile profile)
    {
        var skillAverage = profile.Skills.Average;
        var recentPerformance = profile.PerformanceHistory
            .OrderByDescending(p => p.Timestamp)
            .Take(10)
            .ToList();
        
        // Calculate comfort zone
        double averageScore = recentPerformance.Count > 0
            ? recentPerformance.Average(p => p.OverallScore)
            : 50;
        
        double currentDifficulty = profile.Repertoire
            .Where(r => r.Status == RepertoireStatus.Learning)
            .Select(r => r.Difficulty)
            .DefaultIfEmpty(3)
            .Average();
        
        // Determine progression
        ProgressionAdvice advice;
        double recommendedDifficulty;
        
        if (averageScore >= 90 && skillAverage >= 70)
        {
            advice = ProgressionAdvice.IncreaseChallenge;
            recommendedDifficulty = Math.Min(10, currentDifficulty + 1);
        }
        else if (averageScore >= 80)
        {
            advice = ProgressionAdvice.MaintainCurrent;
            recommendedDifficulty = currentDifficulty;
        }
        else if (averageScore >= 70)
        {
            advice = ProgressionAdvice.ConsolidateSkills;
            recommendedDifficulty = currentDifficulty;
        }
        else
        {
            advice = ProgressionAdvice.ReduceDifficulty;
            recommendedDifficulty = Math.Max(1, currentDifficulty - 1);
        }
        
        return new DifficultyRecommendation
        {
            CurrentLevel = currentDifficulty,
            RecommendedLevel = recommendedDifficulty,
            Advice = advice,
            Reasoning = GenerateProgressionReasoning(advice, averageScore, skillAverage),
            SuggestedFocusAreas = profile.GetPriorityPracticeAreas().Take(3).ToList(),
            EstimatedTimeToNextLevel = EstimateTimeToNextLevel(profile, recommendedDifficulty)
        };
    }
    
    /// <summary>
    /// Gets a curriculum path for a skill focus.
    /// </summary>
    public CurriculumPath? GetCurriculumPath(string pathName)
    {
        return _curriculumPaths.GetValueOrDefault(pathName);
    }
    
    /// <summary>
    /// Gets all available curriculum paths.
    /// </summary>
    public List<CurriculumPath> GetAllPaths() => _curriculumPaths.Values.ToList();
    
    /// <summary>
    /// Gets appropriate exercises for current level.
    /// </summary>
    public List<ExerciseSuggestion> GetExerciseSuggestions(
        StudentProfile profile,
        int count = 5)
    {
        var suggestions = new List<ExerciseSuggestion>();
        var weakAreas = profile.GetPriorityPracticeAreas();
        var difficulty = GetRecommendedDifficulty(profile);
        
        foreach (var area in weakAreas.Take(3))
        {
            var exercises = GetExercisesForSkill(area, (int)difficulty.RecommendedLevel);
            suggestions.AddRange(exercises.Take(2));
        }
        
        return suggestions.Take(count).ToList();
    }
    
    /// <summary>
    /// Determines if a student is ready to advance to the next level.
    /// </summary>
    public LevelAssessment AssessLevelReadiness(StudentProfile profile)
    {
        var recentScores = profile.PerformanceHistory
            .OrderByDescending(p => p.Timestamp)
            .Take(10)
            .Select(p => p.OverallScore)
            .ToList();
        
        if (recentScores.Count < 5)
        {
            return new LevelAssessment
            {
                IsReady = false,
                Message = "Need more practice data to assess readiness",
                RequirementsStatus = new Dictionary<string, RequirementStatus>
                {
                    ["Minimum Sessions"] = new() { Met = false, Current = recentScores.Count, Required = 5 }
                }
            };
        }
        
        var requirements = new Dictionary<string, RequirementStatus>
        {
            ["Average Score ≥ 85%"] = new()
            {
                Met = recentScores.Average() >= 85,
                Current = recentScores.Average(),
                Required = 85
            },
            ["Consistency (5+ sessions at 80%+)"] = new()
            {
                Met = recentScores.Count(s => s >= 80) >= 5,
                Current = recentScores.Count(s => s >= 80),
                Required = 5
            },
            ["Peak Performance (1 session at 90%+)"] = new()
            {
                Met = recentScores.Any(s => s >= 90),
                Current = recentScores.Max(),
                Required = 90
            }
        };
        
        bool allMet = requirements.Values.All(r => r.Met);
        
        return new LevelAssessment
        {
            IsReady = allMet,
            CurrentLevel = EstimateCurrentLevel(profile),
            NextLevel = allMet ? EstimateCurrentLevel(profile) + 1 : EstimateCurrentLevel(profile),
            RequirementsStatus = requirements,
            Message = allMet
                ? "You're ready to advance to more challenging pieces!"
                : "Keep practicing to meet all requirements for advancement."
        };
    }
    
    /// <summary>
    /// Gets a structured practice plan based on current level.
    /// </summary>
    public PracticePlan GetStructuredPracticePlan(
        StudentProfile profile,
        int durationMinutes = 30)
    {
        var difficulty = GetRecommendedDifficulty(profile);
        var weakAreas = profile.GetPriorityPracticeAreas();
        
        var plan = new PracticePlan
        {
            TotalDuration = durationMinutes,
            FocusAreas = weakAreas.Take(3).ToList(),
            DifficultyLevel = difficulty.RecommendedLevel
        };
        
        // Warm-up (20% of time)
        int warmUpTime = (int)(durationMinutes * 0.2);
        plan.Segments.Add(new PracticeSegment
        {
            Name = "Warm-up",
            DurationMinutes = warmUpTime,
            Type = SegmentType.WarmUp,
            Description = "Scales and finger exercises",
            Exercises = GetWarmUpExercises((int)difficulty.RecommendedLevel)
        });
        
        // Technical focus (30% of time)
        int technicalTime = (int)(durationMinutes * 0.3);
        plan.Segments.Add(new PracticeSegment
        {
            Name = "Technical Work",
            DurationMinutes = technicalTime,
            Type = SegmentType.Technical,
            Description = $"Focus on {weakAreas.FirstOrDefault() ?? "technique"}",
            Exercises = GetExercisesForSkill(weakAreas.FirstOrDefault() ?? "General", (int)difficulty.RecommendedLevel)
        });
        
        // Repertoire (40% of time)
        int repertoireTime = (int)(durationMinutes * 0.4);
        var currentPiece = profile.Repertoire
            .FirstOrDefault(r => r.Status == RepertoireStatus.Learning);
        
        plan.Segments.Add(new PracticeSegment
        {
            Name = "Repertoire",
            DurationMinutes = repertoireTime,
            Type = SegmentType.Repertoire,
            Description = currentPiece != null
                ? $"Continue working on {currentPiece.Title}"
                : "Learn a new piece at your level",
            PieceSuggestion = currentPiece?.Title
        });
        
        // Cool-down/Review (10% of time)
        int coolDownTime = durationMinutes - warmUpTime - technicalTime - repertoireTime;
        plan.Segments.Add(new PracticeSegment
        {
            Name = "Review & Cool-down",
            DurationMinutes = coolDownTime,
            Type = SegmentType.CoolDown,
            Description = "Play through a comfortable piece"
        });
        
        return plan;
    }
    
    private void InitializeDefaultPaths()
    {
        _curriculumPaths["Classical Foundation"] = new CurriculumPath
        {
            Name = "Classical Foundation",
            Description = "Traditional classical piano training",
            Levels = new List<CurriculumLevel>
            {
                new() { Level = 1, Name = "Beginner", Repertoire = ["Simple melodies", "Five-finger patterns"], Skills = ["Basic note reading", "Steady pulse"] },
                new() { Level = 2, Name = "Elementary", Repertoire = ["Burgmüller Op. 100", "Easy Sonatinas"], Skills = ["Both hands coordination", "Basic dynamics"] },
                new() { Level = 3, Name = "Late Elementary", Repertoire = ["Bach Minuets", "Easy Beethoven"], Skills = ["Independence of hands", "Phrasing"] },
                new() { Level = 4, Name = "Early Intermediate", Repertoire = ["Bach 2-Part Inventions", "Mozart Easy Sonatas"], Skills = ["Counterpoint", "Articulation"] },
                new() { Level = 5, Name = "Intermediate", Repertoire = ["Bach WTC Preludes", "Haydn Sonatas"], Skills = ["Ornamentation", "Structural analysis"] },
                new() { Level = 6, Name = "Late Intermediate", Repertoire = ["Chopin Waltzes", "Beethoven Op. 49"], Skills = ["Romantic expression", "Pedaling"] },
                new() { Level = 7, Name = "Early Advanced", Repertoire = ["Chopin Nocturnes", "Bach WTC Fugues"], Skills = ["Voicing", "Complex textures"] },
                new() { Level = 8, Name = "Advanced", Repertoire = ["Chopin Ballades", "Beethoven Sonatas"], Skills = ["Concert performance", "Artistic interpretation"] },
                new() { Level = 9, Name = "Virtuoso", Repertoire = ["Liszt Études", "Rachmaninoff Concertos"], Skills = ["Technical mastery", "Stage presence"] },
                new() { Level = 10, Name = "Professional", Repertoire = ["Full concert repertoire"], Skills = ["Complete artistic freedom"] }
            }
        };
        
        _curriculumPaths["Jazz & Improvisation"] = new CurriculumPath
        {
            Name = "Jazz & Improvisation",
            Description = "Jazz piano and improvisation skills",
            Levels = new List<CurriculumLevel>
            {
                new() { Level = 1, Name = "Beginner", Repertoire = ["Simple blues", "Basic chord voicings"], Skills = ["12-bar blues", "Swing feel"] },
                new() { Level = 2, Name = "Elementary", Repertoire = ["Jazz standards (lead sheets)"], Skills = ["7th chords", "Basic comping"] },
                new() { Level = 3, Name = "Developing", Repertoire = ["Autumn Leaves", "Blue Bossa"], Skills = ["ii-V-I progressions", "Simple improvisation"] },
                new() { Level = 4, Name = "Intermediate", Repertoire = ["All The Things You Are", "Take The A Train"], Skills = ["Chord extensions", "Walking bass"] },
                new() { Level = 5, Name = "Advanced", Repertoire = ["Giant Steps", "Donna Lee"], Skills = ["Bebop vocabulary", "Complex harmony"] }
            }
        };
        
        _curriculumPaths["Technical Development"] = new CurriculumPath
        {
            Name = "Technical Development",
            Description = "Focused technical and scale work",
            Levels = new List<CurriculumLevel>
            {
                new() { Level = 1, Name = "Fundamentals", Repertoire = ["Major scales (1 octave)"], Skills = ["Correct hand position", "Even tone"] },
                new() { Level = 2, Name = "Basic", Repertoire = ["All major scales (2 octaves)", "Arpeggios"], Skills = ["Thumb crossing", "Consistent tempo"] },
                new() { Level = 3, Name = "Elementary", Repertoire = ["Minor scales", "Hanon exercises"], Skills = ["Finger independence", "Speed building"] },
                new() { Level = 4, Name = "Intermediate", Repertoire = ["Czerny Op. 299", "All scales (4 octaves)"], Skills = ["Velocity", "Endurance"] },
                new() { Level = 5, Name = "Advanced", Repertoire = ["Chopin Études", "Liszt Technical Studies"], Skills = ["Virtuosic technique", "Complex passages"] }
            }
        };
    }
    
    private string GenerateProgressionReasoning(ProgressionAdvice advice, double averageScore, double skillAverage)
    {
        return advice switch
        {
            ProgressionAdvice.IncreaseChallenge => 
                $"With an average score of {averageScore:F0}% and skill level of {skillAverage:F0}%, you're ready for more challenging material!",
            ProgressionAdvice.MaintainCurrent =>
                $"Your scores ({averageScore:F0}% average) show solid progress. Continue at this level to build mastery.",
            ProgressionAdvice.ConsolidateSkills =>
                $"Good progress ({averageScore:F0}%). Focus on solidifying your current skills before advancing.",
            ProgressionAdvice.ReduceDifficulty =>
                $"Current pieces may be too challenging ({averageScore:F0}% average). Try easier material to build confidence.",
            _ => "Continue your current practice routine."
        };
    }
    
    private string EstimateTimeToNextLevel(StudentProfile profile, double targetDifficulty)
    {
        var recentImprovement = CalculateRecentImprovement(profile);
        
        if (recentImprovement <= 0)
        {
            return "Focus on consistent practice";
        }
        
        var gapToNextLevel = 10; // Arbitrary points needed
        var weeksNeeded = gapToNextLevel / recentImprovement;
        
        if (weeksNeeded <= 4)
            return "~1 month";
        else if (weeksNeeded <= 12)
            return "~3 months";
        else
            return "~6 months";
    }
    
    private double CalculateRecentImprovement(StudentProfile profile)
    {
        var history = profile.PerformanceHistory
            .OrderBy(p => p.Timestamp)
            .ToList();
        
        if (history.Count < 4) return 0;
        
        var firstHalf = history.Take(history.Count / 2).Average(p => p.OverallScore);
        var secondHalf = history.Skip(history.Count / 2).Average(p => p.OverallScore);
        
        return secondHalf - firstHalf;
    }
    
    private int EstimateCurrentLevel(StudentProfile profile)
    {
        var skillAverage = profile.Skills.Average;
        
        if (skillAverage >= 90) return 9;
        if (skillAverage >= 80) return 7;
        if (skillAverage >= 70) return 5;
        if (skillAverage >= 60) return 4;
        if (skillAverage >= 50) return 3;
        if (skillAverage >= 40) return 2;
        return 1;
    }
    
    private List<ExerciseSuggestion> GetExercisesForSkill(string skill, int level)
    {
        var exercises = new List<ExerciseSuggestion>();
        
        switch (skill.ToLower())
        {
            case "note accuracy":
            case "accuracy":
                exercises.Add(new ExerciseSuggestion { Name = "Slow Practice", Description = "Play at 50% tempo focusing on correct notes", Level = level, SkillFocus = skill });
                exercises.Add(new ExerciseSuggestion { Name = "Hands Separate", Description = "Practice each hand independently", Level = level, SkillFocus = skill });
                break;
                
            case "rhythm":
                exercises.Add(new ExerciseSuggestion { Name = "Metronome Drills", Description = "Practice with metronome at various tempos", Level = level, SkillFocus = skill });
                exercises.Add(new ExerciseSuggestion { Name = "Rhythm Clapping", Description = "Clap rhythm before playing", Level = level, SkillFocus = skill });
                break;
                
            case "tempo control":
                exercises.Add(new ExerciseSuggestion { Name = "Gradual Speed Building", Description = "Start slow and increase by 10 BPM", Level = level, SkillFocus = skill });
                exercises.Add(new ExerciseSuggestion { Name = "Consistent Tempo Challenge", Description = "Play entire piece at one steady tempo", Level = level, SkillFocus = skill });
                break;
                
            case "dynamics":
                exercises.Add(new ExerciseSuggestion { Name = "Dynamic Range Exercise", Description = "Practice scales from pp to ff", Level = level, SkillFocus = skill });
                exercises.Add(new ExerciseSuggestion { Name = "Crescendo/Decrescendo", Description = "Practice smooth dynamic changes", Level = level, SkillFocus = skill });
                break;
                
            default:
                exercises.Add(new ExerciseSuggestion { Name = "Technical Exercise", Description = $"Practice exercises for {skill}", Level = level, SkillFocus = skill });
                break;
        }
        
        return exercises;
    }
    
    private List<ExerciseSuggestion> GetWarmUpExercises(int level)
    {
        var warmUps = new List<ExerciseSuggestion>
        {
            new() { Name = "Major Scales", Description = "Play all major scales at comfortable tempo", Level = level, SkillFocus = "Warm-up" },
            new() { Name = "Arpeggios", Description = "Play major and minor arpeggios", Level = level, SkillFocus = "Warm-up" }
        };
        
        if (level >= 3)
        {
            warmUps.Add(new ExerciseSuggestion { Name = "Hanon Exercise", Description = "Hanon #1-3 for finger independence", Level = level, SkillFocus = "Technique" });
        }
        
        return warmUps;
    }
}

/// <summary>
/// Difficulty recommendation for a student.
/// </summary>
public record DifficultyRecommendation
{
    public double CurrentLevel { get; init; }
    public double RecommendedLevel { get; init; }
    public ProgressionAdvice Advice { get; init; }
    public string Reasoning { get; init; } = "";
    public List<string> SuggestedFocusAreas { get; init; } = [];
    public string EstimatedTimeToNextLevel { get; init; } = "";
}

/// <summary>
/// Progression advice type.
/// </summary>
public enum ProgressionAdvice
{
    IncreaseChallenge,
    MaintainCurrent,
    ConsolidateSkills,
    ReduceDifficulty
}

/// <summary>
/// Assessment of readiness to advance.
/// </summary>
public record LevelAssessment
{
    public bool IsReady { get; init; }
    public int CurrentLevel { get; init; }
    public int NextLevel { get; init; }
    public Dictionary<string, RequirementStatus> RequirementsStatus { get; init; } = new();
    public string Message { get; init; } = "";
}

/// <summary>
/// Status of a level requirement.
/// </summary>
public record RequirementStatus
{
    public bool Met { get; init; }
    public double Current { get; init; }
    public double Required { get; init; }
}

/// <summary>
/// A curriculum path for structured learning.
/// </summary>
public record CurriculumPath
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public List<CurriculumLevel> Levels { get; init; } = [];
}

/// <summary>
/// A level within a curriculum.
/// </summary>
public record CurriculumLevel
{
    public int Level { get; init; }
    public string Name { get; init; } = "";
    public List<string> Repertoire { get; init; } = [];
    public List<string> Skills { get; init; } = [];
}

/// <summary>
/// An exercise suggestion.
/// </summary>
public record ExerciseSuggestion
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public int Level { get; init; }
    public string SkillFocus { get; init; } = "";
}

/// <summary>
/// A structured practice plan.
/// </summary>
public record PracticePlan
{
    public int TotalDuration { get; init; }
    public List<string> FocusAreas { get; init; } = [];
    public double DifficultyLevel { get; init; }
    public List<PracticeSegment> Segments { get; init; } = [];
}

/// <summary>
/// A segment of a practice session.
/// </summary>
public record PracticeSegment
{
    public string Name { get; init; } = "";
    public int DurationMinutes { get; init; }
    public SegmentType Type { get; init; }
    public string Description { get; init; } = "";
    public List<ExerciseSuggestion> Exercises { get; init; } = [];
    public string? PieceSuggestion { get; init; }
}

/// <summary>
/// Type of practice segment.
/// </summary>
public enum SegmentType
{
    WarmUp,
    Technical,
    Repertoire,
    SightReading,
    CoolDown
}

