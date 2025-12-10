using ListeningBrain.Intelligence;

namespace ListeningBrain.Practice;

/// <summary>
/// Manages practice goals and achievement tracking.
/// </summary>
public class GoalManager
{
    private readonly Dictionary<string, List<PracticeGoal>> _goalsByStudent = new();
    
    /// <summary>
    /// Creates a new goal for a student.
    /// </summary>
    public PracticeGoal CreateGoal(
        string studentId,
        string title,
        string description,
        GoalType type,
        double targetValue,
        DateTime? targetDate = null)
    {
        var goal = new PracticeGoal
        {
            Title = title,
            Description = description,
            Type = type,
            TargetValue = targetValue,
            TargetDate = targetDate
        };
        
        if (!_goalsByStudent.ContainsKey(studentId))
        {
            _goalsByStudent[studentId] = [];
        }
        
        _goalsByStudent[studentId].Add(goal);
        return goal;
    }
    
    /// <summary>
    /// Creates suggested goals based on student profile.
    /// </summary>
    public List<GoalSuggestion> SuggestGoals(StudentProfile profile)
    {
        var suggestions = new List<GoalSuggestion>();
        
        // Practice time goal
        int currentWeeklyMinutes = profile.PerformanceHistory
            .Where(p => p.Timestamp > DateTime.UtcNow.AddDays(-7))
            .Sum(p => p.DurationMinutes);
        
        suggestions.Add(new GoalSuggestion
        {
            Title = "Weekly Practice Goal",
            Description = $"Practice at least {Math.Max(60, currentWeeklyMinutes + 30)} minutes this week",
            Type = GoalType.PracticeTime,
            SuggestedTarget = Math.Max(60, currentWeeklyMinutes + 30),
            Reasoning = "Consistent practice is key to improvement"
        });
        
        // Skill improvement goal
        var weakestSkill = profile.GetPriorityPracticeAreas().FirstOrDefault();
        if (weakestSkill != null)
        {
            var currentLevel = GetSkillLevel(profile.Skills, weakestSkill);
            suggestions.Add(new GoalSuggestion
            {
                Title = $"Improve {weakestSkill}",
                Description = $"Raise {weakestSkill} score from {currentLevel:F0}% to {currentLevel + 10:F0}%",
                Type = GoalType.SkillImprovement,
                SuggestedTarget = currentLevel + 10,
                Reasoning = $"{weakestSkill} is your current area for growth"
            });
        }
        
        // Streak goal
        if (profile.PracticeStreak < 7)
        {
            suggestions.Add(new GoalSuggestion
            {
                Title = "Build a Practice Habit",
                Description = "Practice every day for 7 days",
                Type = GoalType.StreakTarget,
                SuggestedTarget = 7,
                Reasoning = "Daily practice builds muscle memory faster"
            });
        }
        else
        {
            suggestions.Add(new GoalSuggestion
            {
                Title = "Extend Your Streak",
                Description = $"Reach a {profile.PracticeStreak + 7}-day streak",
                Type = GoalType.StreakTarget,
                SuggestedTarget = profile.PracticeStreak + 7,
                Reasoning = "Keep your excellent habit going!"
            });
        }
        
        // Piece mastery goal
        var learningPieces = profile.Repertoire
            .Where(r => r.Status == RepertoireStatus.Learning || r.Status == RepertoireStatus.Practicing)
            .OrderBy(r => r.BestScore)
            .FirstOrDefault();
        
        if (learningPieces != null)
        {
            suggestions.Add(new GoalSuggestion
            {
                Title = $"Master {learningPieces.Title}",
                Description = "Achieve 90% score consistently",
                Type = GoalType.PieceCompletion,
                SuggestedTarget = 90,
                Reasoning = "Completing pieces builds confidence"
            });
        }
        
        // Accuracy goal
        if (profile.Skills.NoteAccuracy < 85)
        {
            suggestions.Add(new GoalSuggestion
            {
                Title = "Accuracy Challenge",
                Description = "Play a piece with 95%+ note accuracy",
                Type = GoalType.AccuracyTarget,
                SuggestedTarget = 95,
                Reasoning = "Precision practice improves all playing"
            });
        }
        
        return suggestions;
    }
    
    /// <summary>
    /// Updates goal progress.
    /// </summary>
    public void UpdateProgress(string studentId, Guid goalId, double newValue)
    {
        if (!_goalsByStudent.TryGetValue(studentId, out var goals)) return;
        
        var goal = goals.FirstOrDefault(g => g.Id == goalId);
        if (goal != null)
        {
            goal.CurrentValue = newValue;
        }
    }
    
    /// <summary>
    /// Updates all applicable goals based on a practice session.
    /// </summary>
    public List<GoalUpdate> UpdateGoalsFromSession(
        string studentId,
        PracticeSession session,
        StudentProfile profile)
    {
        var updates = new List<GoalUpdate>();
        
        if (!_goalsByStudent.TryGetValue(studentId, out var goals)) return updates;
        
        foreach (var goal in goals.Where(g => !g.IsComplete))
        {
            double previousValue = goal.CurrentValue;
            
            switch (goal.Type)
            {
                case GoalType.PracticeTime:
                    goal.CurrentValue += session.DurationMinutes;
                    break;
                    
                case GoalType.PieceCompletion:
                    var pieceScore = session.PiecesPracticed
                        .Where(p => p.Title.Contains(goal.Title, StringComparison.OrdinalIgnoreCase))
                        .MaxBy(p => p.BestScore);
                    if (pieceScore != null)
                    {
                        goal.CurrentValue = Math.Max(goal.CurrentValue, pieceScore.BestScore);
                    }
                    break;
                    
                case GoalType.AccuracyTarget:
                    var bestAccuracy = session.PiecesPracticed.MaxBy(p => p.BestScore);
                    if (bestAccuracy != null)
                    {
                        goal.CurrentValue = Math.Max(goal.CurrentValue, bestAccuracy.BestScore);
                    }
                    break;
                    
                case GoalType.StreakTarget:
                    goal.CurrentValue = profile.PracticeStreak;
                    break;
            }
            
            if (goal.CurrentValue != previousValue)
            {
                updates.Add(new GoalUpdate
                {
                    GoalId = goal.Id,
                    GoalTitle = goal.Title,
                    PreviousValue = previousValue,
                    NewValue = goal.CurrentValue,
                    TargetValue = goal.TargetValue,
                    IsComplete = goal.IsComplete,
                    Message = goal.IsComplete
                        ? $"🎉 Goal Complete: {goal.Title}!"
                        : $"Progress on {goal.Title}: {goal.Progress:P0}"
                });
            }
        }
        
        return updates;
    }
    
    /// <summary>
    /// Gets all goals for a student.
    /// </summary>
    public List<PracticeGoal> GetGoals(string studentId, bool includeComplete = false)
    {
        if (!_goalsByStudent.TryGetValue(studentId, out var goals))
        {
            return [];
        }
        
        return includeComplete
            ? goals.OrderByDescending(g => g.CreatedAt).ToList()
            : goals.Where(g => !g.IsComplete).OrderByDescending(g => g.CreatedAt).ToList();
    }
    
    /// <summary>
    /// Gets completed goals for a student.
    /// </summary>
    public List<PracticeGoal> GetCompletedGoals(string studentId)
    {
        if (!_goalsByStudent.TryGetValue(studentId, out var goals))
        {
            return [];
        }
        
        return goals.Where(g => g.IsComplete).ToList();
    }
    
    /// <summary>
    /// Gets overdue goals.
    /// </summary>
    public List<PracticeGoal> GetOverdueGoals(string studentId)
    {
        if (!_goalsByStudent.TryGetValue(studentId, out var goals))
        {
            return [];
        }
        
        return goals
            .Where(g => !g.IsComplete && g.TargetDate.HasValue && g.TargetDate.Value < DateTime.UtcNow)
            .ToList();
    }
    
    /// <summary>
    /// Deletes a goal.
    /// </summary>
    public bool DeleteGoal(string studentId, Guid goalId)
    {
        if (!_goalsByStudent.TryGetValue(studentId, out var goals))
        {
            return false;
        }
        
        return goals.RemoveAll(g => g.Id == goalId) > 0;
    }
    
    private double GetSkillLevel(SkillDimensions skills, string skillName)
    {
        return skillName switch
        {
            "Note Accuracy" => skills.NoteAccuracy,
            "Rhythm" => skills.Rhythm,
            "Tempo Control" => skills.TempoControl,
            "Dynamics" => skills.Dynamics,
            "Articulation" => skills.Articulation,
            "Phrasing" => skills.Phrasing,
            "Sight Reading" => skills.SightReading,
            _ => skills.Average
        };
    }
}

/// <summary>
/// A suggested goal.
/// </summary>
public record GoalSuggestion
{
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public GoalType Type { get; init; }
    public double SuggestedTarget { get; init; }
    public string Reasoning { get; init; } = "";
}

/// <summary>
/// An update to a goal.
/// </summary>
public record GoalUpdate
{
    public Guid GoalId { get; init; }
    public string GoalTitle { get; init; } = "";
    public double PreviousValue { get; init; }
    public double NewValue { get; init; }
    public double TargetValue { get; init; }
    public bool IsComplete { get; init; }
    public string Message { get; init; } = "";
}

