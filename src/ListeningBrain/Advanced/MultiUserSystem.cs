using ListeningBrain.Intelligence;
using ListeningBrain.Practice;

namespace ListeningBrain.Advanced;

/// <summary>
/// Multi-user system for teacher-student relationships and reviews.
/// </summary>
public class MultiUserSystem
{
    private readonly Dictionary<string, User> _users = new();
    private readonly Dictionary<string, List<string>> _teacherStudents = new();
    private readonly List<ReviewRequest> _reviewRequests = [];
    private readonly List<Review> _reviews = [];
    private readonly List<Message> _messages = [];
    
    /// <summary>
    /// Creates a new user.
    /// </summary>
    public User CreateUser(string name, string email, UserRole role)
    {
        var user = new User
        {
            Name = name,
            Email = email,
            Role = role
        };
        
        _users[user.Id] = user;
        
        if (role == UserRole.Teacher)
        {
            _teacherStudents[user.Id] = [];
        }
        
        return user;
    }
    
    /// <summary>
    /// Gets a user by ID.
    /// </summary>
    public User? GetUser(string userId)
    {
        return _users.GetValueOrDefault(userId);
    }
    
    /// <summary>
    /// Gets all users with a specific role.
    /// </summary>
    public List<User> GetUsersByRole(UserRole role)
    {
        return _users.Values.Where(u => u.Role == role).ToList();
    }
    
    /// <summary>
    /// Links a student to a teacher.
    /// </summary>
    public bool LinkStudentToTeacher(string studentId, string teacherId)
    {
        if (!_users.TryGetValue(studentId, out var student) || student.Role != UserRole.Student)
            return false;
        
        if (!_users.TryGetValue(teacherId, out var teacher) || teacher.Role != UserRole.Teacher)
            return false;
        
        if (!_teacherStudents.ContainsKey(teacherId))
        {
            _teacherStudents[teacherId] = [];
        }
        
        if (!_teacherStudents[teacherId].Contains(studentId))
        {
            _teacherStudents[teacherId].Add(studentId);
            student.TeacherId = teacherId;
        }
        
        return true;
    }
    
    /// <summary>
    /// Gets students for a teacher.
    /// </summary>
    public List<User> GetStudentsForTeacher(string teacherId)
    {
        if (!_teacherStudents.TryGetValue(teacherId, out var studentIds))
            return [];
        
        return studentIds
            .Select(id => _users.GetValueOrDefault(id))
            .Where(u => u != null)
            .Cast<User>()
            .ToList();
    }
    
    /// <summary>
    /// Gets the teacher for a student.
    /// </summary>
    public User? GetTeacherForStudent(string studentId)
    {
        if (!_users.TryGetValue(studentId, out var student))
            return null;
        
        if (student.TeacherId == null)
            return null;
        
        return _users.GetValueOrDefault(student.TeacherId);
    }
    
    /// <summary>
    /// Submits a recording for teacher review.
    /// </summary>
    public ReviewRequest SubmitForReview(
        string studentId,
        Guid recordingId,
        string? message = null)
    {
        var student = _users.GetValueOrDefault(studentId);
        if (student == null) throw new ArgumentException("Student not found");
        
        var request = new ReviewRequest
        {
            StudentId = studentId,
            RecordingId = recordingId,
            TeacherId = student.TeacherId,
            Message = message
        };
        
        _reviewRequests.Add(request);
        
        return request;
    }
    
    /// <summary>
    /// Gets pending review requests for a teacher.
    /// </summary>
    public List<ReviewRequest> GetPendingReviews(string teacherId)
    {
        return _reviewRequests
            .Where(r => r.TeacherId == teacherId && r.Status == ReviewRequestStatus.Pending)
            .OrderBy(r => r.SubmittedAt)
            .ToList();
    }
    
    /// <summary>
    /// Completes a review.
    /// </summary>
    public Review CompleteReview(
        Guid requestId,
        string teacherId,
        string feedback,
        int rating,
        List<Annotation>? annotations = null)
    {
        var request = _reviewRequests.FirstOrDefault(r => r.Id == requestId);
        if (request == null) throw new ArgumentException("Review request not found");
        
        request.Status = ReviewRequestStatus.Completed;
        request.CompletedAt = DateTime.UtcNow;
        
        var review = new Review
        {
            RequestId = requestId,
            StudentId = request.StudentId,
            TeacherId = teacherId,
            RecordingId = request.RecordingId,
            Feedback = feedback,
            Rating = rating,
            Annotations = annotations ?? []
        };
        
        _reviews.Add(review);
        
        return review;
    }
    
    /// <summary>
    /// Gets reviews for a student.
    /// </summary>
    public List<Review> GetReviewsForStudent(string studentId)
    {
        return _reviews
            .Where(r => r.StudentId == studentId)
            .OrderByDescending(r => r.CreatedAt)
            .ToList();
    }
    
    /// <summary>
    /// Sends a message between users.
    /// </summary>
    public Message SendMessage(string fromId, string toId, string content, MessageType type = MessageType.Text)
    {
        var message = new Message
        {
            FromId = fromId,
            ToId = toId,
            Content = content,
            Type = type
        };
        
        _messages.Add(message);
        
        return message;
    }
    
    /// <summary>
    /// Gets conversation between two users.
    /// </summary>
    public List<Message> GetConversation(string userId1, string userId2)
    {
        return _messages
            .Where(m => (m.FromId == userId1 && m.ToId == userId2) ||
                       (m.FromId == userId2 && m.ToId == userId1))
            .OrderBy(m => m.SentAt)
            .ToList();
    }
    
    /// <summary>
    /// Gets unread messages for a user.
    /// </summary>
    public List<Message> GetUnreadMessages(string userId)
    {
        return _messages
            .Where(m => m.ToId == userId && !m.ReadAt.HasValue)
            .OrderByDescending(m => m.SentAt)
            .ToList();
    }
    
    /// <summary>
    /// Marks messages as read.
    /// </summary>
    public void MarkAsRead(string userId, string fromId)
    {
        foreach (var message in _messages.Where(m => m.ToId == userId && m.FromId == fromId && !m.ReadAt.HasValue))
        {
            message.ReadAt = DateTime.UtcNow;
        }
    }
    
    /// <summary>
    /// Creates an assignment for a student.
    /// </summary>
    public Assignment CreateAssignment(
        string teacherId,
        string studentId,
        string title,
        string description,
        string? pieceId = null,
        DateTime? dueDate = null)
    {
        var assignment = new Assignment
        {
            TeacherId = teacherId,
            StudentId = studentId,
            Title = title,
            Description = description,
            PieceId = pieceId,
            DueDate = dueDate
        };
        
        // Store assignment (would be in _assignments dictionary in full implementation)
        return assignment;
    }
    
    /// <summary>
    /// Gets student dashboard data.
    /// </summary>
    public StudentDashboard GetStudentDashboard(string studentId, StudentProfile profile)
    {
        var teacher = GetTeacherForStudent(studentId);
        var recentReviews = GetReviewsForStudent(studentId).Take(5).ToList();
        var unreadMessages = GetUnreadMessages(studentId);
        
        return new StudentDashboard
        {
            StudentId = studentId,
            StudentName = _users[studentId].Name,
            TeacherName = teacher?.Name,
            RecentReviews = recentReviews,
            UnreadMessageCount = unreadMessages.Count,
            PendingAssignments = [], // Would be populated from assignments
            SkillSummary = new SkillSummary
            {
                OverallLevel = profile.Skills.Average,
                Strengths = profile.GetStrengths(),
                AreasForGrowth = profile.GetPriorityPracticeAreas().Take(3).ToList()
            },
            PracticeStreak = profile.PracticeStreak,
            TotalPracticeMinutes = profile.PerformanceHistory.Sum(p => p.DurationMinutes)
        };
    }
    
    /// <summary>
    /// Gets teacher dashboard data.
    /// </summary>
    public TeacherDashboard GetTeacherDashboard(string teacherId)
    {
        var students = GetStudentsForTeacher(teacherId);
        var pendingReviews = GetPendingReviews(teacherId);
        var unreadMessages = GetUnreadMessages(teacherId);
        
        return new TeacherDashboard
        {
            TeacherId = teacherId,
            TeacherName = _users[teacherId].Name,
            TotalStudents = students.Count,
            ActiveStudents = students.Count(s => s.LastActiveAt > DateTime.UtcNow.AddDays(-7)),
            PendingReviewCount = pendingReviews.Count,
            UnreadMessageCount = unreadMessages.Count,
            RecentActivity = GenerateRecentActivity(students),
            StudentsNeedingAttention = IdentifyStudentsNeedingAttention(students)
        };
    }
    
    private List<ActivityItem> GenerateRecentActivity(List<User> students)
    {
        var activities = new List<ActivityItem>();
        
        // Would aggregate recent recordings, reviews, messages
        foreach (var student in students.Take(10))
        {
            activities.Add(new ActivityItem
            {
                UserId = student.Id,
                UserName = student.Name,
                Description = "Recent practice session",
                Timestamp = DateTime.UtcNow.AddHours(-new Random().Next(1, 48))
            });
        }
        
        return activities.OrderByDescending(a => a.Timestamp).Take(10).ToList();
    }
    
    private List<StudentAlert> IdentifyStudentsNeedingAttention(List<User> students)
    {
        var alerts = new List<StudentAlert>();
        
        foreach (var student in students)
        {
            if (student.LastActiveAt < DateTime.UtcNow.AddDays(-7))
            {
                alerts.Add(new StudentAlert
                {
                    StudentId = student.Id,
                    StudentName = student.Name,
                    Type = AlertType.Inactive,
                    Message = $"No practice in {(DateTime.UtcNow - student.LastActiveAt).Days} days"
                });
            }
        }
        
        return alerts;
    }
}

/// <summary>
/// A user in the system.
/// </summary>
public class User
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public UserRole Role { get; set; }
    public string? TeacherId { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;
    public UserSettings Settings { get; set; } = new();
}

/// <summary>
/// User role.
/// </summary>
public enum UserRole
{
    Student,
    Teacher,
    Admin
}

/// <summary>
/// User settings.
/// </summary>
public class UserSettings
{
    public bool EmailNotifications { get; set; } = true;
    public bool PushNotifications { get; set; } = true;
    public string Timezone { get; set; } = "UTC";
    public string Language { get; set; } = "en";
}

/// <summary>
/// A review request from student to teacher.
/// </summary>
public class ReviewRequest
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string StudentId { get; init; } = "";
    public string? TeacherId { get; init; }
    public Guid RecordingId { get; init; }
    public string? Message { get; init; }
    public DateTime SubmittedAt { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public ReviewRequestStatus Status { get; set; } = ReviewRequestStatus.Pending;
}

/// <summary>
/// Review request status.
/// </summary>
public enum ReviewRequestStatus
{
    Pending,
    InProgress,
    Completed,
    Rejected
}

/// <summary>
/// A teacher review.
/// </summary>
public record Review
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid RequestId { get; init; }
    public string StudentId { get; init; } = "";
    public string TeacherId { get; init; } = "";
    public Guid RecordingId { get; init; }
    public string Feedback { get; init; } = "";
    public int Rating { get; init; }
    public List<Annotation> Annotations { get; init; } = [];
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// A message between users.
/// </summary>
public class Message
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string FromId { get; init; } = "";
    public string ToId { get; init; } = "";
    public string Content { get; init; } = "";
    public MessageType Type { get; init; }
    public DateTime SentAt { get; init; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
}

/// <summary>
/// Message type.
/// </summary>
public enum MessageType
{
    Text,
    RecordingShare,
    Assignment,
    Feedback,
    Encouragement
}

/// <summary>
/// An assignment from teacher to student.
/// </summary>
public record Assignment
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string TeacherId { get; init; } = "";
    public string StudentId { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string? PieceId { get; init; }
    public DateTime? DueDate { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public AssignmentStatus Status { get; set; } = AssignmentStatus.Pending;
}

/// <summary>
/// Assignment status.
/// </summary>
public enum AssignmentStatus
{
    Pending,
    InProgress,
    Submitted,
    Reviewed,
    Completed
}

/// <summary>
/// Student dashboard data.
/// </summary>
public record StudentDashboard
{
    public string StudentId { get; init; } = "";
    public string StudentName { get; init; } = "";
    public string? TeacherName { get; init; }
    public List<Review> RecentReviews { get; init; } = [];
    public int UnreadMessageCount { get; init; }
    public List<Assignment> PendingAssignments { get; init; } = [];
    public SkillSummary SkillSummary { get; init; } = new();
    public int PracticeStreak { get; init; }
    public int TotalPracticeMinutes { get; init; }
}

/// <summary>
/// Skill summary for dashboard.
/// </summary>
public record SkillSummary
{
    public double OverallLevel { get; init; }
    public List<string> Strengths { get; init; } = [];
    public List<string> AreasForGrowth { get; init; } = [];
}

/// <summary>
/// Teacher dashboard data.
/// </summary>
public record TeacherDashboard
{
    public string TeacherId { get; init; } = "";
    public string TeacherName { get; init; } = "";
    public int TotalStudents { get; init; }
    public int ActiveStudents { get; init; }
    public int PendingReviewCount { get; init; }
    public int UnreadMessageCount { get; init; }
    public List<ActivityItem> RecentActivity { get; init; } = [];
    public List<StudentAlert> StudentsNeedingAttention { get; init; } = [];
}

/// <summary>
/// Recent activity item.
/// </summary>
public record ActivityItem
{
    public string UserId { get; init; } = "";
    public string UserName { get; init; } = "";
    public string Description { get; init; } = "";
    public DateTime Timestamp { get; init; }
}

/// <summary>
/// Alert about a student needing attention.
/// </summary>
public record StudentAlert
{
    public string StudentId { get; init; } = "";
    public string StudentName { get; init; } = "";
    public AlertType Type { get; init; }
    public string Message { get; init; } = "";
}

/// <summary>
/// Type of student alert.
/// </summary>
public enum AlertType
{
    Inactive,
    Struggling,
    OverdueAssignment,
    NeedsReview
}

