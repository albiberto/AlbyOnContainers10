using ProductInformationManager.Messages;

namespace ProductInformationManager.Web.Components.Pages.Categories.Dialogs;

public class CategoryEditDialogInput
{
    public CategoryDto? CategoryToEdit { get; set; }
    public Guid? ParentIdForCreate { get; set; }
    public string? ParentName { get; set; }
}