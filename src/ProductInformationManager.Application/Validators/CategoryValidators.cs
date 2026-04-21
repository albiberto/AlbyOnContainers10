using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ProductInformationManager.Domain.ValueObjects;
using ProductInformationManager.Infrastructure;
using ProductInformationManager.Messages;

namespace ProductInformationManager.Application.Validators;

public class CreateCategoryValidator : AbstractValidator<CreateCategory>
{
    public CreateCategoryValidator(ProductContext db)
    {
        // Stage 1: Formal Validation (Stateless, Fast)
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Category name is required.")
            .MinimumLength(4).WithMessage("Category name must be at least 4 characters long.")
            .MaximumLength(100).WithMessage("Category name cannot exceed 100 characters.")
            .DependentRules(() =>
            {
                RuleFor(x => x.Name)
                    .MustAsync(async (name, ct) => !await db.Categories.AnyAsync(c => EF.Functions.ILike(c.Name, name), ct))
                    .WithMessage("A category with this name already exists in the system.");

                When(x => x.ParentId.HasValue, () =>
                {
                    RuleFor(x => x.ParentId)
                        .MustAsync(async (parentId, ct) => await db.Categories.AnyAsync(c => c.Id == new CategoryId(parentId!.Value), ct))
                        .WithMessage("Parent category does not exist.");
                });
            });

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Category description cannot exceed 500 characters.");
    }
}

public class UpdateCategoryValidator : AbstractValidator<UpdateCategory>
{
    public UpdateCategoryValidator(ProductContext db)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Category name is required.")
            .MinimumLength(4).WithMessage("Category name must be at least 4 characters long.")
            .MaximumLength(100).WithMessage("Category name cannot exceed 100 characters.")
            .DependentRules(() =>
            {
                RuleFor(x => x)
                    .MustAsync(async (cmd, ct) => 
                        !await db.Categories.AnyAsync(c => c.Id != new CategoryId(cmd.Id) && EF.Functions.ILike(c.Name, cmd.Name), ct))
                    .WithMessage("A category with this name already exists in the system.");
            });

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Category description cannot exceed 500 characters.");
    }
}

public class DeleteCategoryValidator : AbstractValidator<DeleteCategory>
{
    public DeleteCategoryValidator(ProductContext db)
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Category ID is required.")
            .DependentRules(() =>
            {
                RuleFor(x => x.Id)
                    .MustAsync(async (id, ct) => 
                    {
                        var catId = new CategoryId(id);
                        var hasChildren = await db.Categories.AnyAsync(c => c.ParentId == catId, ct);
                        return !hasChildren;
                    })
                    .WithMessage("Cannot delete this category because it contains sub-categories.");
            });
    }
}