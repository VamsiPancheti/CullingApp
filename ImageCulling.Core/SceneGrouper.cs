// File: SceneGrouper.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace ImageCulling.Core;

public class Scene
{
    public int SceneId { get; set; }
    public List<PhotoMetadata> Photos { get; set; } = new();
    public List<PhotoMetadata> CulledPhotos => Photos.Where(p => !p.IsRejected).ToList();
    public List<PhotoMetadata> RejectedPhotos => Photos.Where(p => p.IsRejected).ToList();
}

public class SceneGrouper
{
    // Increased threshold for strict similarity
    private const double SemanticSimilarityThreshold = 0.75; 
    
    // A burst is a rapid sequence, not a 10-minute window!
    private static readonly TimeSpan MaxTimeGap = TimeSpan.FromSeconds(60); 

    public List<Scene> GroupIntoScenes(List<PhotoMetadata> photos)
    {
        var sortedPhotos = photos.OrderBy(p => p.ShotDate).ToList();
        var scenes = new List<Scene>();

        if (!sortedPhotos.Any()) return scenes;

        int sceneIdCounter = 1;
        var currentScene = new Scene { SceneId = sceneIdCounter };
        currentScene.Photos.Add(sortedPhotos[0]);
        scenes.Add(currentScene);

        for (int i = 1; i < sortedPhotos.Count; i++)
        {
            var prevPhoto = sortedPhotos[i - 1];
            var currentPhoto = sortedPhotos[i];

            TimeSpan timeGap = currentPhoto.ShotDate - prevPhoto.ShotDate;
            double similarity = CalculateCosineSimilarity(currentPhoto.Vector, prevPhoto.Vector);

            // If time gap is too large, OR they look totally different, break the scene
            if (timeGap > MaxTimeGap || similarity < SemanticSimilarityThreshold)
            {
                sceneIdCounter++;
                currentScene = new Scene { SceneId = sceneIdCounter };
                scenes.Add(currentScene);
            }

            currentScene.Photos.Add(currentPhoto);
        }

        return scenes;
    }

    private double CalculateCosineSimilarity(float[] vectorA, float[] vectorB)
    {
        // Fallback if ONNX model is missing/mocked
        if (vectorA == null || vectorB == null || vectorA.Length == 0 || vectorA.All(v => v == 0)) 
            return 0.0; 

        double dotProduct = 0.0;
        double normA = 0.0;
        double normB = 0.0;

        for (int i = 0; i < vectorA.Length; i++)
        {
            dotProduct += vectorA[i] * vectorB[i];
            normA += Math.Pow(vectorA[i], 2);
            normB += Math.Pow(vectorB[i], 2);
        }

        if (normA == 0 || normB == 0) return 0.0;
        return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}