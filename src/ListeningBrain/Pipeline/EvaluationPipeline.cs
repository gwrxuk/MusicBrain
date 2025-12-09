using ListeningBrain.Alignment;
using ListeningBrain.Core.Models;
using ListeningBrain.Evaluation;
using ListeningBrain.Feedback;

namespace ListeningBrain.Pipeline;

/// <summary>
/// Orchestrates the full evaluation pipeline from MIDI input to feedback.
/// This is the main entry point for evaluating performances.
/// </summary>
public class EvaluationPipeline
{
    private readonly IAlignmentStrategy _aligner;
    private readonly NoteAccuracyEvaluator _noteEvaluator;
    private readonly RhythmEvaluator _rhythmEvaluator;
    private readonly TempoEvaluator _tempoEvaluator;
    private readonly FeedbackGenerator _feedbackGenerator;
    
    /// <summary>
    /// Creates a new evaluation pipeline with default components.
    /// </summary>
    public EvaluationPipeline() : this(
        new HybridAligner(),
        new NoteAccuracyEvaluator(),
        new RhythmEvaluator(),
        new TempoEvaluator(),
        new FeedbackGenerator())
    {
    }
    
    /// <summary>
    /// Creates a new evaluation pipeline with custom components.
    /// </summary>
    public EvaluationPipeline(
        IAlignmentStrategy aligner,
        NoteAccuracyEvaluator noteEvaluator,
        RhythmEvaluator rhythmEvaluator,
        TempoEvaluator tempoEvaluator,
        FeedbackGenerator feedbackGenerator)
    {
        _aligner = aligner;
        _noteEvaluator = noteEvaluator;
        _rhythmEvaluator = rhythmEvaluator;
        _tempoEvaluator = tempoEvaluator;
        _feedbackGenerator = feedbackGenerator;
    }
    
    /// <summary>
    /// Alignment options to use.
    /// </summary>
    public AlignmentOptions AlignmentOptions { get; set; } = AlignmentOptions.Default;
    
    /// <summary>
    /// Evaluates a complete performance against a score.
    /// </summary>
    public FullEvaluationResult Evaluate(Score score, Performance performance)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // Validate inputs
        var scoreValidation = score.Validate();
        if (!scoreValidation.IsValid)
        {
            throw new ArgumentException(
                $"Invalid score: {string.Join(", ", scoreValidation.Errors)}");
        }
        
        // Step 1: Align performance to score
        var alignment = _aligner.Align(score, performance, AlignmentOptions);
        
        // Step 2: Evaluate note accuracy
        var noteResult = _noteEvaluator.Evaluate(alignment, score, performance);
        
        // Step 3: Evaluate rhythm
        var rhythmResult = _rhythmEvaluator.Evaluate(alignment, score, performance);
        
        // Step 4: Evaluate tempo
        var tempoResult = _tempoEvaluator.Evaluate(alignment, score, performance);
        
        // Step 5: Generate feedback
        var feedback = _feedbackGenerator.Generate(
            alignment, noteResult, rhythmResult, tempoResult, score);
        
        stopwatch.Stop();
        
        return new FullEvaluationResult
        {
            Alignment = alignment,
            NoteAccuracy = noteResult,
            Rhythm = rhythmResult,
            Tempo = tempoResult,
            Feedback = feedback,
            TotalProcessingTime = stopwatch.Elapsed
        };
    }
    
    /// <summary>
    /// Evaluates only note accuracy (faster, for quick feedback).
    /// </summary>
    public QuickEvaluationResult EvaluateNotesOnly(Score score, Performance performance)
    {
        var alignment = _aligner.Align(score, performance, AlignmentOptions);
        var noteResult = _noteEvaluator.Evaluate(alignment, score, performance);
        
        return new QuickEvaluationResult
        {
            Score = noteResult.Score,
            CorrectNotes = noteResult.CorrectNotes,
            TotalNotes = noteResult.TotalExpectedNotes,
            TopIssues = noteResult.Issues.Take(3).ToList()
        };
    }
    
    /// <summary>
    /// Creates a pipeline optimized for beginners (lenient settings).
    /// </summary>
    public static EvaluationPipeline ForBeginners()
    {
        return new EvaluationPipeline
        {
            AlignmentOptions = AlignmentOptions.Beginner
        };
    }
    
    /// <summary>
    /// Creates a pipeline optimized for advanced players (strict settings).
    /// </summary>
    public static EvaluationPipeline ForAdvanced()
    {
        var pipeline = new EvaluationPipeline(
            new HybridAligner(),
            new NoteAccuracyEvaluator { Weights = NoteAccuracyWeights.Strict },
            new RhythmEvaluator { Thresholds = RhythmThresholds.Strict },
            new TempoEvaluator(),
            new FeedbackGenerator()
        );
        pipeline.AlignmentOptions = AlignmentOptions.Strict;
        return pipeline;
    }
}

/// <summary>
/// Complete result of the evaluation pipeline.
/// </summary>
public record FullEvaluationResult
{
    /// <summary>
    /// Raw alignment result.
    /// </summary>
    public required AlignmentResult Alignment { get; init; }
    
    /// <summary>
    /// Note accuracy evaluation.
    /// </summary>
    public required NoteAccuracyResult NoteAccuracy { get; init; }
    
    /// <summary>
    /// Rhythm evaluation.
    /// </summary>
    public required RhythmResult Rhythm { get; init; }
    
    /// <summary>
    /// Tempo evaluation.
    /// </summary>
    public required TempoResult Tempo { get; init; }
    
    /// <summary>
    /// Generated feedback report.
    /// </summary>
    public required FeedbackReport Feedback { get; init; }
    
    /// <summary>
    /// Total time taken to process.
    /// </summary>
    public TimeSpan TotalProcessingTime { get; init; }
    
    /// <summary>
    /// Quick access to overall score.
    /// </summary>
    public double OverallScore => Feedback.OverallScore;
    
    /// <summary>
    /// Quick access to grade.
    /// </summary>
    public string Grade => Feedback.OverallGrade;
}

/// <summary>
/// Quick evaluation result (notes only).
/// </summary>
public record QuickEvaluationResult
{
    public double Score { get; init; }
    public int CorrectNotes { get; init; }
    public int TotalNotes { get; init; }
    public IReadOnlyList<EvaluationIssue> TopIssues { get; init; } = [];
    
    public double AccuracyPercent => TotalNotes > 0 
        ? (double)CorrectNotes / TotalNotes * 100 
        : 100;
}

