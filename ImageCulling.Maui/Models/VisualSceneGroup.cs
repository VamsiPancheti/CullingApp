// File: VisualSceneGroup.cs
using ImageCulling.Maui.Models;
using System.Collections.Generic;

namespace ImageCulling.Maui.Models;

// Inheriting from List<VisualPhoto> unlocks native CollectionView grouping virtualization
public class VisualSceneGroup : List<VisualPhoto>
{
    public int SceneId { get; set; }
    public string SceneTitle => $"📸 Scene #{SceneId} ({Count} Photos)";

    // Pass the items directly down to the base list constructor
    public VisualSceneGroup(int sceneId, IEnumerable<VisualPhoto> photos) : base(photos)
    {
        SceneId = sceneId;
    }
}