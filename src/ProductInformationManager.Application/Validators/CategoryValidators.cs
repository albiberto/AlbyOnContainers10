using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ProductInformationManager.Application.Resources;
using ProductInformationManager.Infrastructure;
using ProductInformationManager.Messages;

namespace ProductInformationManager.Application.Validators;

public class CreateCategoryValidator : AbstractValidator<CreateCategory>
{
    public CreateCategoryValidator(ProductContext db)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(ValidationMessages.CategoryNameRequired)
            .MaximumLength(100).WithMessage(ValidationMessages.CategoryNameMaxLength)
            .MustAsync(async (name, ct) =>
                !await db.Categories.AnyAsync(c => c.Name == name, ct))
            .WithMessage(ValidationMessages.CategoryNameDuplicate);

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage(ValidationMessages.CategoryDescriptionMaxLength);
    }
}

public class UpdateCategoryValidator : AbstractValidator<UpdateCategory>
{
    public UpdateCategoryValidator(ProductContext db)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(ValidationMessages.CategoryNameRequired)
            .MaximumLength(100).WithMessage(ValidationMessages.CategoryNameMaxLength)
            .MustAsync(async (cmd, name, ct) =>
                !await db.Categories.AnyAsync(c => c.Name == name && c.Id != new Domain.ValueObjects.CategoryId(cmd.Id), ct))
            .WithMessage(ValidationMessages.CategoryNameDuplicate);

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage(ValidationMessages.CategoryDescriptionMaxLength);
    }
}
