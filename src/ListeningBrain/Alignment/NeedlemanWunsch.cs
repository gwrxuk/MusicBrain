using ListeningBrain.Core.Models;

namespace ListeningBrain.Alignment;

/// <summary>
/// Needleman-Wunsch global sequence alignment algorithm.
/// Originally designed for bioinformatics (DNA/protein alignment), adapted for musical note alignment.
/// 
/// Better than DTW at handling:
/// - Note insertions (extra notes played)
/// - Note deletions (missed notes)
/// - Local mismatches
/// 
/// Complexity: O(n × m) time and space.
/// </summary>
public class NeedlemanWunsch : IAlignmentStrategy
{
    public string Name => "Needleman-Wunsch Sequence Alignment";
    
    // Scoring parameters
    private const double MATCH_SCORE = 2.0;
    private const double MISMATCH_PENALTY = -1.0;
    private const double GAP_OPEN_PENALTY = -2.0;
    private const double GAP_EXTEND_PENALTY = -0.5;
    
    public AlignmentResult Align(Score score, Performance performance, AlignmentOptions? options = null)
    {
        options ??= AlignmentOptions.Default;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        var scoreNotes = score.Notes.ToList();
        var perfNotes = performance.Notes.ToList();
        
        if (scoreNotes.Count == 0 || perfNotes.Count == 0)
        {
            return CreateEmptyResult(scoreNotes, perfNotes, stopwatch.Elapsed);
        }
        
        // Build scoring matrix with affine gap penalties
        var (scoreMatrix, tracebackMatrix) = BuildScoringMatrix(scoreNotes, perfNotes, options);
        
        // Traceback to find optimal alignment
        var alignment = Traceback(scoreMatrix, tracebackMatrix, scoreNotes, perfNotes, score, options);
        
        stopwatch.Stop();
        
        return new AlignmentResult
        {
            Pairs = alignment.Pairs,
            MissedNotes = alignment.Missed,
            ExtraNotes = alignment.Extra,
            TotalCost = -scoreMatrix[scoreNotes.Count, perfNotes.Count], // Convert score to cost
            NormalizedScore = CalculateNormalizedScore(alignment.Pairs, alignment.Missed.Count, alignment.Extra.Count),
            EstimatedTempoRatio = EstimateTempoRatio(alignment.Pairs, scoreNotes, perfNotes),
            TimeOffsetMs = CalculateTimeOffset(alignment.Pairs),
            AlgorithmUsed = Name,
            ComputeTime = stopwatch.Elapsed
        };
    }
    
    /// <summary>
    /// Builds the Needleman-Wunsch scoring matrix using dynamic programming.
    /// Uses affine gap penalties (gap open + extend) for better biological realism.
    /// </summary>
    private (double[,] score, TracebackCell[,] traceback) BuildScoringMatrix(
        List<ScoreNote> scoreNotes,
        List<PerformanceNote> perfNotes,
        AlignmentOptions options)
    {
        int n = scoreNotes.Count;
        int m = perfNotes.Count;
        
        var F = new double[n + 1, m + 1];
        var traceback = new TracebackCell[n + 1, m + 1];
        
        // Initialize first row and column with gap penalties
        F[0, 0] = 0;
        traceback[0, 0] = TracebackCell.Done;
        
        for (int i = 1; i <= n; i++)
        {
            F[i, 0] = GAP_OPEN_PENALTY + (i - 1) * GAP_EXTEND_PENALTY;
            traceback[i, 0] = TracebackCell.Up; // Gap in performance
        }
        
        for (int j = 1; j <= m; j++)
        {
            F[0, j] = (GAP_OPEN_PENALTY + (j - 1) * GAP_EXTEND_PENALTY) * 0.5; // Lower penalty for extra notes
            traceback[0, j] = TracebackCell.Left; // Gap in score (extra note)
        }
        
        // Fill the matrix
        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                double matchScore = CalculateMatchScore(scoreNotes[i - 1], perfNotes[j - 1], options);
                
                double diagonal = F[i - 1, j - 1] + matchScore;
                
                // Gap in performance (missed note in score)
                double gapPerfPenalty = traceback[i - 1, j] == TracebackCell.Up 
                    ? GAP_EXTEND_PENALTY 
                    : GAP_OPEN_PENALTY;
                double up = F[i - 1, j] + gapPerfPenalty;
                
                // Gap in score (extra note in performance)
                double gapScorePenalty = traceback[i, j - 1] == TracebackCell.Left 
                    ? GAP_EXTEND_PENALTY * 0.5 
                    : GAP_OPEN_PENALTY * 0.5;
                double left = F[i, j - 1] + gapScorePenalty;
                
                // Find maximum
                if (diagonal >= up && diagonal >= left)
                {
                    F[i, j] = diagonal;
                    traceback[i, j] = TracebackCell.Diagonal;
                }
                else if (up >= left)
                {
                    F[i, j] = up;
                    traceback[i, j] = TracebackCell.Up;
                }
                else
                {
                    F[i, j] = left;
                    traceback[i, j] = TracebackCell.Left;
                }
            }
        }
        
        return (F, traceback);
    }
    
    /// <summary>
    /// Calculates the match score between a score note and performance note.
    /// Positive for good matches, negative for mismatches.
    /// </summary>
    private double CalculateMatchScore(ScoreNote scoreNote, PerformanceNote perfNote, AlignmentOptions options)
    {
        // Pitch matching (most important)
        double pitchScore;
        if (scoreNote.Pitch == perfNote.Pitch)
        {
            pitchScore = MATCH_SCORE;
        }
        else if (scoreNote.PitchClass == perfNote.PitchClass && options.AllowOctaveErrors)
        {
            // Octave error - partial credit
            pitchScore = MATCH_SCORE * 0.5;
        }
        else
        {
            // Wrong pitch - penalty based on semitone distance
            int semitones = Math.Abs(scoreNote.Pitch - perfNote.Pitch);
            pitchScore = MISMATCH_PENALTY * Math.Min(semitones / 6.0, 2.0);
        }
        
        // Timing component
        double timingDeviation = Math.Abs(perfNote.StartTimeMs - scoreNote.StartTimeMs);
        double timingScore;
        
        if (timingDeviation <= 30)
        {
            timingScore = 0.5; // Bonus for great timing
        }
        else if (timingDeviation <= options.MaxTimingDeviationMs)
        {
            // Linear penalty up to max deviation
            timingScore = 0.3 * (1 - timingDeviation / options.MaxTimingDeviationMs);
        }
        else
        {
            // Beyond tolerance - significant penalty
            timingScore = -0.5;
        }
        
        // Grace note relaxation
        if (scoreNote.IsGraceNote && options.RelaxGraceNoteTiming)
        {
            timingScore = Math.Max(0, timingScore + 0.3);
        }
        
        return pitchScore * options.PitchWeight + timingScore * (1 - options.PitchWeight);
    }
    
    /// <summary>
    /// Performs traceback to extract the optimal alignment.
    /// </summary>
    private (List<AlignedNotePair> Pairs, List<MissedNote> Missed, List<PerformanceNote> Extra) Traceback(
        double[,] scoreMatrix,
        TracebackCell[,] tracebackMatrix,
        List<ScoreNote> scoreNotes,
        List<PerformanceNote> perfNotes,
        Score score,
        AlignmentOptions options)
    {
        var pairs = new List<AlignedNotePair>();
        var missed = new List<MissedNote>();
        var extra = new List<PerformanceNote>();
        
        int i = scoreNotes.Count;
        int j = perfNotes.Count;
        
        while (i > 0 || j > 0)
        {
            var cell = tracebackMatrix[i, j];
            
            switch (cell)
            {
                case TracebackCell.Diagonal:
                    // Match (or mismatch that was better than gap)
                    var scoreNote = scoreNotes[i - 1];
                    var perfNote = perfNotes[j - 1];
                    
                    var tempo = score.GetTempoAt(scoreNote.StartTick);
                    double msPerBeat = tempo.MillisecondsPerQuarter;
                    double timingDeviationMs = perfNote.StartTimeMs - scoreNote.StartTimeMs;
                    
                    // Only pair if actually a reasonable match
                    double matchScore = CalculateMatchScore(scoreNote, perfNote, options);
                    if (matchScore > 0)
                    {
                        pairs.Add(new AlignedNotePair
                        {
                            ScoreNote = scoreNote,
                            PerformanceNote = perfNote,
                            Confidence = NormalizeMatchScore(matchScore),
                            TimingDeviationMs = timingDeviationMs,
                            TimingDeviationBeats = timingDeviationMs / msPerBeat
                        });
                    }
                    else
                    {
                        // Poor match - count as separate missed and extra
                        missed.Add(CreateMissedNote(scoreNote, perfNotes, j - 1));
                        extra.Add(perfNote.AsExtra());
                    }
                    
                    i--;
                    j--;
                    break;
                    
                case TracebackCell.Up:
                    // Gap in performance = missed note
                    missed.Add(CreateMissedNote(scoreNotes[i - 1], perfNotes, j));
                    i--;
                    break;
                    
                case TracebackCell.Left:
                    // Gap in score = extra note
                    extra.Add(perfNotes[j - 1].AsExtra());
                    j--;
                    break;
                    
                case TracebackCell.Done:
                    i = 0;
                    j = 0;
                    break;
            }
        }
        
        // Reverse since we traced backward
        pairs.Reverse();
        missed.Reverse();
        extra.Reverse();
        
        return (pairs, missed, extra);
    }
    
    private MissedNote CreateMissedNote(ScoreNote scoreNote, List<PerformanceNote> perfNotes, int nearestIndex)
    {
        var nearby = perfNotes
            .Where(p => Math.Abs(p.StartTimeMs - scoreNote.StartTimeMs) < 500)
            .ToList();
            
        return new MissedNote
        {
            ExpectedNote = scoreNote,
            NearbyPlayedNotes = nearby,
            InferredReason = InferMissReason(scoreNote, nearby)
        };
    }
    
    private MissReason InferMissReason(ScoreNote scoreNote, List<PerformanceNote> nearby)
    {
        if (scoreNote.IsGraceNote)
            return MissReason.OptionalOrnament;
        if (nearby.Count == 0)
            return MissReason.Skipped;
        if (nearby.Any(n => n.PitchClass == scoreNote.PitchClass))
            return MissReason.Substituted;
        return MissReason.TimingMismatch;
    }
    
    private double NormalizeMatchScore(double score)
    {
        // Convert match score to 0-1 confidence
        return Math.Max(0, Math.Min(1, (score + 1) / (MATCH_SCORE + 1)));
    }
    
    private double CalculateNormalizedScore(List<AlignedNotePair> pairs, int missedCount, int extraCount)
    {
        int total = pairs.Count + missedCount;
        if (total == 0) return 1.0;
        
        double correctPitches = pairs.Count(p => p.IsExactPitchMatch);
        double partialCredit = pairs.Count(p => p.IsOctaveError) * 0.5;
        double extraPenalty = extraCount * 0.1;
        
        double score = (correctPitches + partialCredit - extraPenalty) / total;
        return Math.Max(0, Math.Min(1, score));
    }
    
    private double EstimateTempoRatio(List<AlignedNotePair> pairs, List<ScoreNote> scoreNotes, List<PerformanceNote> perfNotes)
    {
        if (pairs.Count < 2) return 1.0;
        
        var ratios = new List<double>();
        for (int i = 1; i < pairs.Count; i++)
        {
            var prev = pairs[i - 1];
            var curr = pairs[i];
            
            double scoreInterval = curr.ScoreNote.StartTimeMs - prev.ScoreNote.StartTimeMs;
            double perfInterval = curr.PerformanceNote.StartTimeMs - prev.PerformanceNote.StartTimeMs;
            
            if (scoreInterval > 10) // Avoid division issues
            {
                ratios.Add(perfInterval / scoreInterval);
            }
        }
        
        if (ratios.Count == 0) return 1.0;
        ratios.Sort();
        return ratios[ratios.Count / 2];
    }
    
    private double CalculateTimeOffset(List<AlignedNotePair> pairs)
    {
        if (pairs.Count == 0) return 0;
        return pairs.Average(p => p.TimingDeviationMs);
    }
    
    private AlignmentResult CreateEmptyResult(
        List<ScoreNote> scoreNotes,
        List<PerformanceNote> perfNotes,
        TimeSpan computeTime)
    {
        return new AlignmentResult
        {
            Pairs = [],
            MissedNotes = scoreNotes.Select(s => new MissedNote 
            { 
                ExpectedNote = s, 
                InferredReason = MissReason.Skipped 
            }).ToList(),
            ExtraNotes = perfNotes.Select(p => p.AsExtra()).ToList(),
            TotalCost = double.MaxValue,
            NormalizedScore = 0,
            AlgorithmUsed = Name,
            ComputeTime = computeTime
        };
    }
}

/// <summary>
/// Traceback direction in Needleman-Wunsch matrix.
/// </summary>
internal enum TracebackCell
{
    Done,      // Start of matrix
    Diagonal,  // Match/mismatch
    Up,        // Gap in sequence 2 (performance)
    Left       // Gap in sequence 1 (score)
}

