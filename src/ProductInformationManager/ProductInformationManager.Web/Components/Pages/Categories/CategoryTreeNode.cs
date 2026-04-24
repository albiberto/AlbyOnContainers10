using ProductInformationManager.Messages;

namespace ProductInformationManager.Web.Components.Pages.Categories;

public class CategoryTreeNode(CategoryDto category) 
{
    public CategoryDto Category { get; set; } = category;
    public List<CategoryTreeNode> Children { get; } = [];
    
    public bool IsExpanded { get; set; }
    public bool IsLoading { get; set; }
    public bool HasLoadedChildren { get; set; }
}