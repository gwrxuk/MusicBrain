using ListeningBrain.Intelligence;
using ListeningBrain.Practice;
using System.Text.Json;

namespace ListeningBrain.Advanced;

/// <summary>
/// Mobile companion app integration API.
/// Provides lightweight endpoints for mobile clients.
/// </summary>
public class MobileIntegration
{
    private readonly CloudSyncManager _syncManager;
    private readonly MultiUserSystem _userSystem;
    
    private readonly Dictionary<string, MobileSession> _activeSessions = new();
    private readonly Dictionary<string, List<PushNotification>> _pendingNotifications = new();
    
    /// <summary>
    /// Creates a new mobile integration instance.
    /// </summary>
    public MobileIntegration(CloudSyncManager syncManager, MultiUserSystem userSystem)
    {
        _syncManager = syncManager;
        _userSystem = userSystem;
    }
    
    /// <summary>
    /// Authenticates a mobile device.
    /// </summary>
    public MobileAuthResult Authenticate(string userId, string deviceId, DeviceInfo device)
    {
        var user = _userSystem.GetUser(userId);
        if (user == null)
        {
            return new MobileAuthResult { Success = false, Error = "User not found" };
        }
        
        var session = new MobileSession
        {
            UserId = userId,
            DeviceId = deviceId,
            Device = device,
            Token = GenerateToken()
        };
        
        _activeSessions[session.Token] = session;
        
        return new MobileAuthResult
        {
            Success = true,
            Token = session.Token,
            ExpiresAt = session.ExpiresAt,
            UserName = user.Name,
            Role = user.Role.ToString()
        };
    }
    
    /// <summary>
    /// Refreshes an authentication token.
    /// </summary>
    public MobileAuthResult RefreshToken(string oldToken)
    {
        if (!_activeSessions.TryGetValue(oldToken, out var session))
        {
            return new MobileAuthResult { Success = false, Error = "Invalid token" };
        }
        
        var newSession = session with
        {
            Token = GenerateToken(),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };
        
        _activeSessions.Remove(oldToken);
        _activeSessions[newSession.Token] = newSession;
        
        return new MobileAuthResult
        {
            Success = true,
            Token = newSession.Token,
            ExpiresAt = newSession.ExpiresAt
        };
    }
    
    /// <summary>
    /// Gets dashboard data optimized for mobile.
    /// </summary>
    public MobileDashboard GetDashboard(string token, StudentProfile profile)
    {
        if (!ValidateToken(token, out var session))
        {
            return new MobileDashboard { Error = "Invalid session" };
        }
        
        return new MobileDashboard
        {
            // Quick stats
            PracticeStreak = profile.PracticeStreak,
            TotalMinutesToday = GetTodayMinutes(profile),
            WeeklyGoalProgress = CalculateWeeklyProgress(profile),
            
            // Skill summary (simplified for mobile)
            OverallSkillLevel = profile.Skills.Average,
            TopStrength = profile.GetStrengths().FirstOrDefault() ?? "Keep practicing!",
            FocusArea = profile.GetPriorityPracticeAreas().FirstOrDefault() ?? "General practice",
            
            // Today's practice
            SuggestedPracticeMinutes = GetSuggestedPracticeTime(profile),
            RecommendedPiece = GetRecommendedPiece(profile),
            
            // Recent activity
            LastPracticeDate = profile.PerformanceHistory
                .OrderByDescending(p => p.Timestamp)
                .FirstOrDefault()?.Timestamp,
            
            // Notifications
            UnreadNotifications = GetUnreadNotificationCount(session.UserId),
            
            // Quick actions
            QuickActions = GetQuickActions(profile)
        };
    }
    
    /// <summary>
    /// Gets practice history in paginated format for mobile.
    /// </summary>
    public MobilePracticeHistory GetPracticeHistory(string token, int page, int pageSize = 20)
    {
        if (!ValidateToken(token, out var session))
        {
            return new MobilePracticeHistory { Error = "Invalid session" };
        }
        
        // Would fetch from actual data store
        return new MobilePracticeHistory
        {
            Page = page,
            PageSize = pageSize,
            TotalItems = 0,
            Items = []
        };
    }
    
    /// <summary>
    /// Registers a device for push notifications.
    /// </summary>
    public bool RegisterForPush(string token, string pushToken, PushPlatform platform)
    {
        if (!ValidateToken(token, out var session))
        {
            return false;
        }
        
        session.PushToken = pushToken;
        session.PushPlatform = platform;
        
        return true;
    }
    
    /// <summary>
    /// Sends a push notification to a user.
    /// </summary>
    public void SendPushNotification(string userId, PushNotification notification)
    {
        var sessions = _activeSessions.Values
            .Where(s => s.UserId == userId && s.PushToken != null)
            .ToList();
        
        if (sessions.Count == 0)
        {
            // Queue for later
            if (!_pendingNotifications.ContainsKey(userId))
            {
                _pendingNotifications[userId] = [];
            }
            _pendingNotifications[userId].Add(notification);
            return;
        }
        
        foreach (var session in sessions)
        {
            SendPushToDevice(session, notification);
        }
    }
    
    /// <summary>
    /// Gets pending notifications for a user.
    /// </summary>
    public List<PushNotification> GetPendingNotifications(string token)
    {
        if (!ValidateToken(token, out var session))
        {
            return [];
        }
        
        if (!_pendingNotifications.TryGetValue(session.UserId, out var notifications))
        {
            return [];
        }
        
        _pendingNotifications.Remove(session.UserId);
        return notifications;
    }
    
    /// <summary>
    /// Logs a quick practice session from mobile.
    /// </summary>
    public QuickPracticeResult LogQuickPractice(
        string token,
        string pieceId,
        int minutes,
        int? rating = null,
        string? notes = null)
    {
        if (!ValidateToken(token, out var session))
        {
            return new QuickPracticeResult { Success = false, Error = "Invalid session" };
        }
        
        // Would create a practice session
        return new QuickPracticeResult
        {
            Success = true,
            SessionId = Guid.NewGuid(),
            Message = $"Logged {minutes} minutes of practice!",
            NewStreak = 1 // Would calculate actual streak
        };
    }
    
    /// <summary>
    /// Gets offline data package for mobile.
    /// </summary>
    public OfflinePackage GetOfflinePackage(string token)
    {
        if (!ValidateToken(token, out var session))
        {
            return new OfflinePackage { Error = "Invalid session" };
        }
        
        // Package essential data for offline use
        return new OfflinePackage
        {
            GeneratedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            
            // Include essential repertoire
            Repertoire = [],
            
            // Include recent practice data
            RecentSessions = [],
            
            // Include goals
            ActiveGoals = [],
            
            // Estimated size in bytes
            SizeBytes = 0
        };
    }
    
    /// <summary>
    /// Syncs offline changes back to server.
    /// </summary>
    public async Task<OfflineSyncResult> SyncOfflineChangesAsync(string token, OfflineChanges changes)
    {
        if (!ValidateToken(token, out var session))
        {
            return new OfflineSyncResult { Success = false, Error = "Invalid session" };
        }
        
        var result = new OfflineSyncResult { Success = true };
        
        // Process practice logs
        foreach (var log in changes.PracticeLogs)
        {
            // Would save to database
            result.SessionsSynced++;
        }
        
        // Process goal updates
        foreach (var update in changes.GoalUpdates)
        {
            // Would update goals
            result.GoalsUpdated++;
        }
        
        // Trigger full sync
        await _syncManager.SyncAllAsync(session.UserId);
        
        return result;
    }
    
    /// <summary>
    /// Gets widget data for mobile home screen widgets.
    /// </summary>
    public WidgetData GetWidgetData(string token, WidgetType type)
    {
        if (!ValidateToken(token, out var session))
        {
            return new WidgetData { Error = "Invalid session" };
        }
        
        return type switch
        {
            WidgetType.DailyGoal => new WidgetData
            {
                Type = type,
                Title = "Daily Practice",
                Value = "15/30 min",
                Progress = 0.5,
                Color = "#4CAF50"
            },
            WidgetType.Streak => new WidgetData
            {
                Type = type,
                Title = "Practice Streak",
                Value = "7 days",
                Icon = "🔥",
                Color = "#FF9800"
            },
            WidgetType.NextPiece => new WidgetData
            {
                Type = type,
                Title = "Continue Practicing",
                Value = "Minuet in G",
                Subtitle = "Last practiced 2h ago",
                Color = "#2196F3"
            },
            _ => new WidgetData { Error = "Unknown widget type" }
        };
    }
    
    /// <summary>
    /// Handles deep link from mobile app.
    /// </summary>
    public DeepLinkResult HandleDeepLink(string token, string deepLink)
    {
        if (!ValidateToken(token, out var session))
        {
            return new DeepLinkResult { Success = false, Error = "Invalid session" };
        }
        
        // Parse deep link
        // Format: pianocoach://action/param1/param2
        var parts = deepLink.Replace("pianocoach://", "").Split('/');
        
        if (parts.Length == 0)
        {
            return new DeepLinkResult { Success = false, Error = "Invalid deep link" };
        }
        
        var action = parts[0];
        
        return action switch
        {
            "practice" => new DeepLinkResult
            {
                Success = true,
                Action = "NavigateToPractice",
                Parameters = new Dictionary<string, string>
                {
                    ["pieceId"] = parts.Length > 1 ? parts[1] : ""
                }
            },
            "recording" => new DeepLinkResult
            {
                Success = true,
                Action = "NavigateToRecording",
                Parameters = new Dictionary<string, string>
                {
                    ["recordingId"] = parts.Length > 1 ? parts[1] : ""
                }
            },
            "review" => new DeepLinkResult
            {
                Success = true,
                Action = "NavigateToReview",
                Parameters = new Dictionary<string, string>
                {
                    ["reviewId"] = parts.Length > 1 ? parts[1] : ""
                }
            },
            _ => new DeepLinkResult { Success = false, Error = $"Unknown action: {action}" }
        };
    }
    
    private bool ValidateToken(string token, out MobileSession session)
    {
        if (_activeSessions.TryGetValue(token, out var s) && s.ExpiresAt > DateTime.UtcNow)
        {
            session = s;
            return true;
        }
        
        session = null!;
        return false;
    }
    
    private string GenerateToken()
    {
        return Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace("/", "_")
            .Replace("+", "-")
            .TrimEnd('=');
    }
    
    private int GetTodayMinutes(StudentProfile profile)
    {
        return profile.PerformanceHistory
            .Where(p => p.Timestamp.Date == DateTime.UtcNow.Date)
            .Sum(p => p.DurationMinutes);
    }
    
    private double CalculateWeeklyProgress(StudentProfile profile)
    {
        var weekMinutes = profile.PerformanceHistory
            .Where(p => p.Timestamp > DateTime.UtcNow.AddDays(-7))
            .Sum(p => p.DurationMinutes);
        
        const int weeklyGoal = 150; // 150 minutes per week
        return Math.Min(1.0, (double)weekMinutes / weeklyGoal);
    }
    
    private int GetSuggestedPracticeTime(StudentProfile profile)
    {
        var todayMinutes = GetTodayMinutes(profile);
        return Math.Max(0, 30 - todayMinutes);
    }
    
    private string? GetRecommendedPiece(StudentProfile profile)
    {
        return profile.Repertoire
            .Where(r => r.Status == RepertoireStatus.Learning)
            .OrderBy(r => r.LastPracticedAt)
            .FirstOrDefault()?.Title;
    }
    
    private int GetUnreadNotificationCount(string userId)
    {
        return _pendingNotifications.GetValueOrDefault(userId)?.Count ?? 0;
    }
    
    private List<QuickAction> GetQuickActions(StudentProfile profile)
    {
        var actions = new List<QuickAction>();
        
        var currentPiece = profile.Repertoire
            .FirstOrDefault(r => r.Status == RepertoireStatus.Learning);
        
        if (currentPiece != null)
        {
            actions.Add(new QuickAction
            {
                Id = "continue",
                Label = "Continue Practice",
                Icon = "▶️",
                DeepLink = $"pianocoach://practice/{currentPiece.PieceId}"
            });
        }
        
        actions.Add(new QuickAction
        {
            Id = "log",
            Label = "Log Practice",
            Icon = "📝",
            DeepLink = "pianocoach://log"
        });
        
        actions.Add(new QuickAction
        {
            Id = "record",
            Label = "Record",
            Icon = "🎤",
            DeepLink = "pianocoach://record"
        });
        
        return actions;
    }
    
    private void SendPushToDevice(MobileSession session, PushNotification notification)
    {
        // Would integrate with APNs/FCM
        // Placeholder for actual implementation
    }
}

/// <summary>
/// Mobile session information.
/// </summary>
public record MobileSession
{
    public string UserId { get; init; } = "";
    public string DeviceId { get; init; } = "";
    public DeviceInfo Device { get; init; } = new();
    public string Token { get; init; } = "";
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; init; } = DateTime.UtcNow.AddDays(30);
    public string? PushToken { get; set; }
    public PushPlatform? PushPlatform { get; set; }
}

/// <summary>
/// Device information.
/// </summary>
public record DeviceInfo
{
    public string Model { get; init; } = "";
    public string OS { get; init; } = "";
    public string OSVersion { get; init; } = "";
    public string AppVersion { get; init; } = "";
}

/// <summary>
/// Mobile authentication result.
/// </summary>
public record MobileAuthResult
{
    public bool Success { get; init; }
    public string? Token { get; init; }
    public DateTime ExpiresAt { get; init; }
    public string? UserName { get; init; }
    public string? Role { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// Mobile dashboard data.
/// </summary>
public record MobileDashboard
{
    public int PracticeStreak { get; init; }
    public int TotalMinutesToday { get; init; }
    public double WeeklyGoalProgress { get; init; }
    public double OverallSkillLevel { get; init; }
    public string TopStrength { get; init; } = "";
    public string FocusArea { get; init; } = "";
    public int SuggestedPracticeMinutes { get; init; }
    public string? RecommendedPiece { get; init; }
    public DateTime? LastPracticeDate { get; init; }
    public int UnreadNotifications { get; init; }
    public List<QuickAction> QuickActions { get; init; } = [];
    public string? Error { get; init; }
}

/// <summary>
/// Quick action for mobile.
/// </summary>
public record QuickAction
{
    public string Id { get; init; } = "";
    public string Label { get; init; } = "";
    public string Icon { get; init; } = "";
    public string DeepLink { get; init; } = "";
}

/// <summary>
/// Mobile practice history.
/// </summary>
public record MobilePracticeHistory
{
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public List<MobilePracticeItem> Items { get; init; } = [];
    public string? Error { get; init; }
}

/// <summary>
/// Practice item for mobile display.
/// </summary>
public record MobilePracticeItem
{
    public Guid Id { get; init; }
    public string PieceTitle { get; init; } = "";
    public int Minutes { get; init; }
    public double Score { get; init; }
    public DateTime Date { get; init; }
}

/// <summary>
/// Push notification platforms.
/// </summary>
public enum PushPlatform
{
    iOS,
    Android,
    Web
}

/// <summary>
/// Push notification.
/// </summary>
public record PushNotification
{
    public string Title { get; init; } = "";
    public string Body { get; init; } = "";
    public NotificationType Type { get; init; }
    public string? DeepLink { get; init; }
    public Dictionary<string, string> Data { get; init; } = new();
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Notification types.
/// </summary>
public enum NotificationType
{
    PracticeReminder,
    StreakAlert,
    ReviewReceived,
    AssignmentDue,
    Achievement,
    TeacherMessage
}

/// <summary>
/// Result of quick practice logging.
/// </summary>
public record QuickPracticeResult
{
    public bool Success { get; init; }
    public Guid SessionId { get; init; }
    public string Message { get; init; } = "";
    public int NewStreak { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// Offline data package.
/// </summary>
public record OfflinePackage
{
    public DateTime GeneratedAt { get; init; }
    public DateTime ExpiresAt { get; init; }
    public List<RepertoireItem> Repertoire { get; init; } = [];
    public List<PracticeSession> RecentSessions { get; init; } = [];
    public List<PracticeGoal> ActiveGoals { get; init; } = [];
    public long SizeBytes { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// Offline changes to sync.
/// </summary>
public record OfflineChanges
{
    public List<OfflinePracticeLog> PracticeLogs { get; init; } = [];
    public List<OfflineGoalUpdate> GoalUpdates { get; init; } = [];
}

/// <summary>
/// Offline practice log entry.
/// </summary>
public record OfflinePracticeLog
{
    public Guid LocalId { get; init; }
    public string PieceId { get; init; } = "";
    public int Minutes { get; init; }
    public DateTime Timestamp { get; init; }
    public string? Notes { get; init; }
}

/// <summary>
/// Offline goal update.
/// </summary>
public record OfflineGoalUpdate
{
    public Guid GoalId { get; init; }
    public double NewValue { get; init; }
    public DateTime Timestamp { get; init; }
}

/// <summary>
/// Result of offline sync.
/// </summary>
public record OfflineSyncResult
{
    public bool Success { get; init; }
    public int SessionsSynced { get; init; }
    public int GoalsUpdated { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// Widget types for mobile home screen.
/// </summary>
public enum WidgetType
{
    DailyGoal,
    Streak,
    NextPiece,
    SkillLevel
}

/// <summary>
/// Widget data for mobile.
/// </summary>
public record WidgetData
{
    public WidgetType Type { get; init; }
    public string Title { get; init; } = "";
    public string Value { get; init; } = "";
    public string? Subtitle { get; init; }
    public string? Icon { get; init; }
    public double? Progress { get; init; }
    public string Color { get; init; } = "#000000";
    public string? Error { get; init; }
}

/// <summary>
/// Result of deep link handling.
/// </summary>
public record DeepLinkResult
{
    public bool Success { get; init; }
    public string Action { get; init; } = "";
    public Dictionary<string, string> Parameters { get; init; } = new();
    public string? Error { get; init; }
}

