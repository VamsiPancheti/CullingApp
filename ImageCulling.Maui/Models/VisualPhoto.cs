// File: Models/VisualPhoto.cs
using System;
using System.IO;
using Microsoft.Maui.Controls;

namespace ImageCulling.Maui.Models;    

public class VisualPhoto
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName => Path.GetFileName(FilePath);
    public double ShutterSpeed { get; set; }
    public double SharpnessScore { get; set; }
    public string Status { get; set; } = string.Empty; // "Keep (Hero)", "Delete (Duplicate)", etc.
    public bool IsSelectedForDeletion => Status.StartsWith("Delete");

    // Convert raw thumbnail byte array into a MAUI-bindable ImageSource
    public ImageSource ThumbnailSource { get; set; }

    public string StatusBadgeColor => Status switch
    {
        "Keep (Hero)" => "#2ECC71",     // Bright Green
        "Keep (Unique)" => "#3498DB",   // Soft Blue
        "Delete (Blurry)" => "#E74C3C",  // Red
        _ => "#E67E22"                  // Orange (Duplicate)
    };
}