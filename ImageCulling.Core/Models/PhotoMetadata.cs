// File: PhotoMetadata.cs
using System;

namespace ImageCulling.Core;

public class PhotoMetadata
{
    public string FilePath { get; set; }
    public float[] Vector { get; set; }
    public DateTime ShotDate { get; set; }
    public double ShutterSpeed { get; set; }
    
    // Core Metrics
    public double SharpnessScore { get; set; }
    public double ExposureScore { get; set; }
    
    // Face Detection Advantage
    public int FaceCount { get; set; }
    public bool HasFaces => FaceCount > 0;

    public bool IsRejected { get; set; }
    public string RejectionReason { get; set; }
}