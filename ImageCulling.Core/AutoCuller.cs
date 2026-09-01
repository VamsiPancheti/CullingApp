// File: AutoCuller.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace ImageCulling.Core;

public class CullingDecision
{
    public PhotoMetadata Photo { get; set; }
    public string Status { get; set; } 
    public double FinalScore { get; set; }
}

public class AutoCuller
{
    public List<CullingDecision> CullScene(Scene scene)
    {
        var decisions = new List<CullingDecision>();

        foreach (var b in scene.Photos.Where(p => p.IsRejected))
        {
            decisions.Add(new CullingDecision 
            { 
                Photo = b, 
                Status = "Rejected: " + b.RejectionReason, 
                FinalScore = 0 
            });
        }

        var candidates = scene.Photos.Where(p => !p.IsRejected).ToList();
        if (!candidates.Any()) return decisions;

        if (candidates.Count == 1)
        {
            decisions.Add(new CullingDecision 
            { 
                Photo = candidates[0], 
                Status = candidates[0].HasFaces ? "Keep (Hero - Face)" : "Keep (Unique)", 
                FinalScore = 100 
            });
            return decisions;
        }

        double maxSharpness = candidates.Max(p => p.SharpnessScore);

        var rankedCandidates = candidates.Select(p =>
        {
            double score = 0.0;

            // ADVANTAGE RULE: Face presence instantly assigns heavy precedence weights
            if (p.HasFaces)
            {
                score += 100.0; // Guaranteed top priority over non-face background clutter
            }

            // Metric 1: Sharpness (Up to 50 pts)
            if (maxSharpness > 0 && p.SharpnessScore > 0)
                score += (p.SharpnessScore / maxSharpness) * 50.0;

            // Metric 2: Exposure Balance (Up to 30 pts)
            double distanceFromIdeal = Math.Abs(128.0 - p.ExposureScore);
            score += Math.Max(0, 30.0 - (distanceFromIdeal / 4.0)); 

            return new { Photo = p, Score = score };
        })
        .OrderByDescending(x => x.Score)
        .ToList();

        // Assign Hero
        var hero = rankedCandidates.First();
        string heroStatus = hero.Photo.HasFaces ? "Keep (Hero - Face Match)" : "Keep (Hero - Best Quality)";
        
        decisions.Add(new CullingDecision 
        { 
            Photo = hero.Photo, 
            Status = heroStatus, 
            FinalScore = hero.Score 
        });

        // Mark Duplicates
        foreach (var duplicate in rankedCandidates.Skip(1))
        {
            decisions.Add(new CullingDecision 
            { 
                Photo = duplicate.Photo, 
                Status = "Delete (Duplicate)", 
                FinalScore = duplicate.Score 
            });
        }

        return decisions;
    }
}