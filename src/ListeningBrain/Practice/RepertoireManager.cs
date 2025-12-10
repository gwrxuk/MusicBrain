using ListeningBrain.Intelligence;

namespace ListeningBrain.Practice;

/// <summary>
/// Manages student repertoire and piece tracking.
/// </summary>
public class RepertoireManager
{
    private readonly AdaptiveDifficultyAssessor _difficultyAssessor;
    
    /// <summary>
    /// Creates a new repertoire manager.
    /// </summary>
    public RepertoireManager(AdaptiveDifficultyAssessor difficultyAssessor)
    {
        _difficultyAssessor = difficultyAssessor;
    }
    
    /// <summary>
    /// Adds a piece to a student's repertoire.
    /// </summary>
    public RepertoireItem AddToRepertoire(
        StudentProfile profile,
        string pieceId,
        string title,
        string? composer = null,
        double? knownDifficulty = null)
    {
        var existingItem = profile.Repertoire.FirstOrDefault(r => r.PieceId == pieceId);
        if (existingItem != null)
        {
            return existingItem;
        }
        
        var item = new RepertoireItem
        {
            PieceId = pieceId,
            Title = title,
            Composer = composer,
            AddedAt = DateTime.UtcNow,
            Status = RepertoireStatus.Learning,
            Difficulty = knownDifficulty ?? 5 // Default if unknown
        };
        
        profile.Repertoire.Add(item);
        return item;
    }
    
    /// <summary>
    /// Updates repertoire item after practice.
    /// </summary>
    public void UpdateFromPerformance(
        StudentProfile profile,
        string pieceId,
        double score,
        List<int>? problemMeasures = null)
    {
        var item = profile.Repertoire.FirstOrDefault(r => r.PieceId == pieceId);
        if (item == null) return;
        
        item.LastPracticedAt = DateTime.UtcNow;
        item.TotalPracticeCount++;
        item.BestScore = Math.Max(item.BestScore, score);
        item.LastScore = score;
        
        if (problemMeasures != null)
        {
            item.ProblemMeasures = problemMeasures;
        }
        
        // Update status based on scores
        item.Status = DetermineStatus(item);
        
        if (item.Status == RepertoireStatus.Mastered && !item.MasteredAt.HasValue)
        {
            item.MasteredAt = DateTime.UtcNow;
        }
    }
    
    /// <summary>
    /// Gets suggested pieces for the student.
    /// </summary>
    public List<PieceSuggestion> SuggestNewPieces(
        StudentProfile profile,
        int count = 5)
    {
        var suggestions = new List<PieceSuggestion>();
        var recommendation = _difficultyAssessor.RecommendDifficultyLevel(profile.Skills);
        var targetDifficulty = recommendation.RecommendedDifficulty;
        
        // Get pieces from repertoire database (simulated)
        var availablePieces = GetAvailablePieces();
        
        // Filter by difficulty range
        var appropriatePieces = availablePieces
            .Where(p => Math.Abs(p.Difficulty - targetDifficulty) <= 1.5)
            .Where(p => !profile.Repertoire.Any(r => r.PieceId == p.Id))
            .ToList();
        
        // Score and rank pieces
        foreach (var piece in appropriatePieces)
        {
            var matchScore = CalculatePieceMatchScore(piece, profile, targetDifficulty);
            suggestions.Add(new PieceSuggestion
            {
                PieceId = piece.Id,
                Title = piece.Title,
                Composer = piece.Composer,
                Difficulty = piece.Difficulty,
                MatchScore = matchScore,
                Reasoning = GeneratePieceReasoning(piece, profile)
            });
        }
        
        return suggestions
            .OrderByDescending(s => s.MatchScore)
            .Take(count)
            .ToList();
    }
    
    /// <summary>
    /// Gets repertoire summary for a student.
    /// </summary>
    public RepertoireSummary GetRepertoireSummary(StudentProfile profile)
    {
        var repertoire = profile.Repertoire;
        
        return new RepertoireSummary
        {
            TotalPieces = repertoire.Count,
            Mastered = repertoire.Count(r => r.Status == RepertoireStatus.Mastered),
            Learning = repertoire.Count(r => r.Status == RepertoireStatus.Learning),
            Practicing = repertoire.Count(r => r.Status == RepertoireStatus.Practicing),
            OnHold = repertoire.Count(r => r.Status == RepertoireStatus.OnHold),
            NeedingReview = GetPiecesNeedingReview(repertoire).Count,
            
            CurrentlyLearning = repertoire
                .Where(r => r.Status == RepertoireStatus.Learning)
                .OrderByDescending(r => r.LastPracticedAt)
                .Take(3)
                .ToList(),
            
            RecentlyMastered = repertoire
                .Where(r => r.Status == RepertoireStatus.Mastered)
                .OrderByDescending(r => r.MasteredAt)
                .Take(3)
                .ToList(),
            
            NeedsReview = GetPiecesNeedingReview(repertoire).Take(3).ToList(),
            
            ByComposer = repertoire
                .Where(r => !string.IsNullOrEmpty(r.Composer))
                .GroupBy(r => r.Composer!)
                .ToDictionary(g => g.Key, g => g.Count()),
            
            DifficultyDistribution = repertoire
                .GroupBy(r => (int)r.Difficulty)
                .ToDictionary(g => g.Key, g => g.Count()),
            
            AverageDifficulty = repertoire.Count > 0 ? repertoire.Average(r => r.Difficulty) : 0
        };
    }
    
    /// <summary>
    /// Archives a piece (removes from active repertoire).
    /// </summary>
    public void ArchivePiece(StudentProfile profile, string pieceId)
    {
        var item = profile.Repertoire.FirstOrDefault(r => r.PieceId == pieceId);
        if (item != null)
        {
            item.Status = RepertoireStatus.Archived;
        }
    }
    
    /// <summary>
    /// Puts a piece on hold.
    /// </summary>
    public void PutOnHold(StudentProfile profile, string pieceId, string? reason = null)
    {
        var item = profile.Repertoire.FirstOrDefault(r => r.PieceId == pieceId);
        if (item != null)
        {
            item.Status = RepertoireStatus.OnHold;
            item.Notes = reason;
        }
    }
    
    /// <summary>
    /// Resumes a piece that was on hold.
    /// </summary>
    public void ResumePiece(StudentProfile profile, string pieceId)
    {
        var item = profile.Repertoire.FirstOrDefault(r => r.PieceId == pieceId);
        if (item != null && item.Status == RepertoireStatus.OnHold)
        {
            item.Status = RepertoireStatus.Learning;
        }
    }
    
    /// <summary>
    /// Gets pieces that need maintenance review.
    /// </summary>
    public List<RepertoireItem> GetPiecesNeedingReview(List<RepertoireItem> repertoire)
    {
        var reviewThreshold = TimeSpan.FromDays(14);
        
        return repertoire
            .Where(r => r.Status == RepertoireStatus.Mastered || r.Status == RepertoireStatus.Practicing)
            .Where(r => r.LastPracticedAt.HasValue && 
                       DateTime.UtcNow - r.LastPracticedAt.Value > reviewThreshold)
            .OrderBy(r => r.LastPracticedAt)
            .ToList();
    }
    
    /// <summary>
    /// Gets practice priority for repertoire.
    /// </summary>
    public List<RepertoireItem> GetPracticePriority(StudentProfile profile)
    {
        var prioritized = new List<(RepertoireItem Item, double Score)>();
        
        foreach (var item in profile.Repertoire.Where(r => r.Status != RepertoireStatus.Archived))
        {
            double priorityScore = 0;
            
            // Currently learning gets highest priority
            if (item.Status == RepertoireStatus.Learning)
            {
                priorityScore += 100;
            }
            
            // Recently practiced pieces (momentum)
            if (item.LastPracticedAt.HasValue)
            {
                var daysSince = (DateTime.UtcNow - item.LastPracticedAt.Value).TotalDays;
                
                if (item.Status == RepertoireStatus.Learning && daysSince > 2)
                {
                    priorityScore += 50; // Need to maintain momentum
                }
                
                if (item.Status == RepertoireStatus.Mastered && daysSince > 14)
                {
                    priorityScore += 30; // Needs review
                }
            }
            
            // Problem measures increase priority
            if (item.ProblemMeasures.Count > 0)
            {
                priorityScore += item.ProblemMeasures.Count * 5;
            }
            
            // Low scores increase priority
            if (item.LastScore < 80)
            {
                priorityScore += 20;
            }
            
            prioritized.Add((item, priorityScore));
        }
        
        return prioritized
            .OrderByDescending(p => p.Score)
            .Select(p => p.Item)
            .ToList();
    }
    
    private RepertoireStatus DetermineStatus(RepertoireItem item)
    {
        // Check for mastery
        if (item.BestScore >= 90 && item.TotalPracticeCount >= 5)
        {
            return RepertoireStatus.Mastered;
        }
        
        // Check if actively practicing
        if (item.TotalPracticeCount >= 3 && item.BestScore >= 70)
        {
            return RepertoireStatus.Practicing;
        }
        
        return RepertoireStatus.Learning;
    }
    
    private double CalculatePieceMatchScore(PieceInfo piece, StudentProfile profile, double targetDifficulty)
    {
        double score = 100;
        
        // Difficulty match
        score -= Math.Abs(piece.Difficulty - targetDifficulty) * 10;
        
        // Variety bonus (different composer)
        if (!profile.Repertoire.Any(r => r.Composer == piece.Composer))
        {
            score += 10;
        }
        
        // Style variety
        // Could be expanded with more metadata
        
        return Math.Max(0, score);
    }
    
    private string GeneratePieceReasoning(PieceInfo piece, StudentProfile profile)
    {
        var reasons = new List<string>();
        
        var masteredByComposer = profile.Repertoire
            .Count(r => r.Composer == piece.Composer && r.Status == RepertoireStatus.Mastered);
        
        if (masteredByComposer > 0)
        {
            reasons.Add($"You've mastered {masteredByComposer} other piece(s) by {piece.Composer}");
        }
        else
        {
            reasons.Add($"A chance to explore {piece.Composer}'s music");
        }
        
        if (piece.Skills.Any())
        {
            reasons.Add($"Good for practicing: {string.Join(", ", piece.Skills.Take(2))}");
        }
        
        return string.Join(". ", reasons);
    }
    
    // Simulated piece database
    private List<PieceInfo> GetAvailablePieces()
    {
        return new List<PieceInfo>
        {
            // Beginner (1-3)
            new() { Id = "minuet-g", Title = "Minuet in G", Composer = "Bach", Difficulty = 2, Skills = ["Basic counterpoint", "Hand coordination"] },
            new() { Id = "fur-elise", Title = "Für Elise", Composer = "Beethoven", Difficulty = 3, Skills = ["Arpeggios", "Expression"] },
            new() { Id = "prelude-c", Title = "Prelude in C Major", Composer = "Bach", Difficulty = 2, Skills = ["Arpeggios", "Consistency"] },
            
            // Intermediate (4-6)
            new() { Id = "invention-1", Title = "Invention No. 1", Composer = "Bach", Difficulty = 4, Skills = ["Two-part counterpoint", "Independence"] },
            new() { Id = "sonatina-36-1", Title = "Sonatina Op. 36 No. 1", Composer = "Clementi", Difficulty = 4, Skills = ["Sonata form", "Classical style"] },
            new() { Id = "waltz-a-min", Title = "Waltz in A minor", Composer = "Chopin", Difficulty = 5, Skills = ["Rubato", "Romantic expression"] },
            new() { Id = "nocturne-9-2", Title = "Nocturne Op. 9 No. 2", Composer = "Chopin", Difficulty = 6, Skills = ["Cantabile", "Pedaling"] },
            new() { Id = "sonata-k545", Title = "Piano Sonata K. 545", Composer = "Mozart", Difficulty = 5, Skills = ["Classical phrasing", "Alberti bass"] },
            
            // Advanced (7-8)
            new() { Id = "ballade-1", Title = "Ballade No. 1", Composer = "Chopin", Difficulty = 8, Skills = ["Complex textures", "Drama"] },
            new() { Id = "sonata-pathet", Title = "Sonata Pathétique", Composer = "Beethoven", Difficulty = 7, Skills = ["Orchestral thinking", "Dynamic range"] },
            new() { Id = "wtc1-prel-fug", Title = "WTC Book 1 Prelude & Fugue in C", Composer = "Bach", Difficulty = 7, Skills = ["Fugue", "Voicing"] },
            
            // Concert Level (9-10)
            new() { Id = "rach-prelude", Title = "Prelude Op. 23 No. 5", Composer = "Rachmaninoff", Difficulty = 9, Skills = ["Power", "Russian romanticism"] },
            new() { Id = "liszt-consolation", Title = "Consolation No. 3", Composer = "Liszt", Difficulty = 8, Skills = ["Lyrical playing", "Touch"] }
        };
    }
}

/// <summary>
/// Information about a piece in the database.
/// </summary>
public record PieceInfo
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Composer { get; init; } = "";
    public double Difficulty { get; init; }
    public List<string> Skills { get; init; } = [];
}

/// <summary>
/// A piece suggestion for the student.
/// </summary>
public record PieceSuggestion
{
    public string PieceId { get; init; } = "";
    public string Title { get; init; } = "";
    public string? Composer { get; init; }
    public double Difficulty { get; init; }
    public double MatchScore { get; init; }
    public string Reasoning { get; init; } = "";
}

/// <summary>
/// Summary of a student's repertoire.
/// </summary>
public record RepertoireSummary
{
    public int TotalPieces { get; init; }
    public int Mastered { get; init; }
    public int Learning { get; init; }
    public int Practicing { get; init; }
    public int OnHold { get; init; }
    public int NeedingReview { get; init; }
    public List<RepertoireItem> CurrentlyLearning { get; init; } = [];
    public List<RepertoireItem> RecentlyMastered { get; init; } = [];
    public List<RepertoireItem> NeedsReview { get; init; } = [];
    public Dictionary<string, int> ByComposer { get; init; } = new();
    public Dictionary<int, int> DifficultyDistribution { get; init; } = new();
    public double AverageDifficulty { get; init; }
}

