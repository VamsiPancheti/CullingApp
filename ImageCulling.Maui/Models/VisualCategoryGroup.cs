using System.Collections.Generic;

namespace ImageCulling.Maui.Models;

public class VisualCategoryGroup : List<VisualPhoto>
{
    public string CategoryName { get; private set; }

    public VisualCategoryGroup(string categoryName, List<VisualPhoto> photos) : base(photos)
    {
        CategoryName = categoryName;
    }
}