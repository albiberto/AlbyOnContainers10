using ProductInformationManager.Messages;

namespace ProductInformationManager.Web.Components.Pages.Categories;

public class CategoryTreeManager
{
    private readonly Lock _lock = new();

    public List<CategoryTreeNode> Roots { get; private set; } = [];

    public bool IsEmpty
    {
        get
        {
            lock (_lock) return Roots.Count == 0;
        }
    }

    public void Initialize(IEnumerable<CategoryDto> rootCategories)
    {
        lock (_lock)
        {
            Roots = rootCategories
                .Select(category => new CategoryTreeNode(category))
                .OrderBy(node => node.Category.Name)
                .ToList();
        }
    }

    public void SetChildren(Guid parentId, IEnumerable<CategoryDto> children)
    {
        lock (_lock)
        {
            var parent = FindNodeRecursive(Roots, parentId);
            if (parent is null) return;

            var childrenList = children.ToList();
            parent.Children.Clear();
            parent.Children.AddRange(childrenList
                .Select(category => new CategoryTreeNode(category))
                .OrderBy(node => node.Category.Name));
            
            parent.HasLoadedChildren = true;
            parent.Category = parent.Category with { HasChildren = childrenList.Count > 0 };
        }
    }

    public bool AddNode(CategoryDto dto)
    {
        lock (_lock)
        {
            if (FindNodeRecursive(Roots, dto.Id) is not null) return false;

            var newNode = new CategoryTreeNode(dto);

            if (dto.ParentId is null)
            {
                Roots.Add(newNode);
                SortNodes(Roots);
            }
            else
            {
                var parent = FindNodeRecursive(Roots, dto.ParentId.Value);
                if (parent == null) return false;
                
                parent.Category = parent.Category with { HasChildren = true };

                if (!parent.HasLoadedChildren && !parent.IsExpanded) return true;
                
                parent.Children.Add(newNode);
                parent.HasLoadedChildren = true;
                SortNodes(parent.Children);
            }
            return true;
        }
    }

    public bool UpdateNode(CategoryDto updatedDto)
    {
        lock (_lock)
        {
            var node = FindNodeRecursive(Roots, updatedDto.Id);
            if (node is null) return false;

            if (node.Category.Name == updatedDto.Name && 
                node.Category.Description == updatedDto.Description &&
                node.Category.Path == updatedDto.Path)
            {
                return false; 
            }

            var oldName = node.Category.Name;

            node.Category = node.Category with 
            { 
                Name = updatedDto.Name, 
                Description = updatedDto.Description,
                Path = updatedDto.Path
            };

            if (string.Equals(oldName, updatedDto.Name, StringComparison.OrdinalIgnoreCase)) return true;
            
            if (updatedDto.ParentId is null)
            {
                SortNodes(Roots);
            }
            else
            {
                var parent = FindNodeRecursive(Roots, updatedDto.ParentId.Value);
                if (parent is not null) SortNodes(parent.Children);
            }

            return true;
        }
    }

    public (bool Removed, string? Name) RemoveNode(Guid id)
    {
        lock (_lock)
        {
            return RemoveNodeRecursive(Roots, id);
        }
    }

    public CategoryTreeNode? FindNode(Guid id)
    {
        lock (_lock)
        {
            return FindNodeRecursive(Roots, id);
        }
    }

    private static CategoryTreeNode? FindNodeRecursive(List<CategoryTreeNode> nodes, Guid id)
    {
        foreach (var node in nodes)
        {
            if (node.Category.Id == id) return node;
            
            var found = FindNodeRecursive(node.Children, id);
            if (found != null) return found;
        }
        return null;
    }

    private static (bool Removed, string? Name) RemoveNodeRecursive(List<CategoryTreeNode> nodes, Guid id)
    {
        var toRemove = nodes.FirstOrDefault(n => n.Category.Id == id);
        if (toRemove != null)
        {
            nodes.Remove(toRemove);
            return (true, toRemove.Category.Name);
        }
        
        foreach (var node in nodes)
        {
            var result = RemoveNodeRecursive(node.Children, id);

            if (!result.Removed) continue;
            
            if (node.Children.Count == 0)
            {
                node.HasLoadedChildren = false;
                node.Category = node.Category with { HasChildren = false };
            }
            return result;
        }
        
        return (false, null);
    }

    private static void SortNodes(List<CategoryTreeNode> nodes)
    {
        nodes.Sort((a, b) => string.Compare(a.Category.Name, b.Category.Name, StringComparison.OrdinalIgnoreCase));
    }
}