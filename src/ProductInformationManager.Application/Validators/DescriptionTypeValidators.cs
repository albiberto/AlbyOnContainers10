using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ProductInformationManager.Application.Resources;
using ProductInformationManager.Domain.ValueObjects;
using ProductInformationManager.Infrastructure;
using ProductInformationManager.Messages;

namespace ProductInformationManager.Application.Validators;

public class CreateDescriptionTypeValidator : AbstractValidator<CreateDescriptionType>
{
    public CreateDescriptionTypeValidator(ProductContext db)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(ValidationMessages.DescriptionTypeNameRequired)
            .MaximumLength(100).WithMessage(ValidationMessages.DescriptionTypeNameMaxLength)
            .MustAsync(async (name, ct) =>
                !await db.DescriptionTypes.AnyAsync(
                    d => d.Name.ToLower() == name.ToLower(), ct))
            .WithMessage(ValidationMessages.DescriptionTypeNameDuplicate);

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage(ValidationMessages.DescriptionTypeDescriptionMaxLength);

        RuleFor(x => x.CategoryIds)
            .Must((cmd, ids) => cmd.IsGlobal || (ids is not null && ids.Count > 0))
            .WithMessage(ValidationMessages.DescriptionTypeCategoriesRequired);
    }
}

public class UpdateDescriptionTypeValidator : AbstractValidator<UpdateDescriptionType>
{
    public UpdateDescriptionTypeValidator(ProductContext db)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(ValidationMessages.DescriptionTypeNameRequired)
            .MaximumLength(100).WithMessage(ValidationMessages.DescriptionTypeNameMaxLength)
            .MustAsync(async (cmd, name, ct) =>
                !await db.DescriptionTypes.AnyAsync(
                    d => d.Name.ToLower() == name.ToLower() && d.Id != new DescriptionTypeId(cmd.Id), ct))
            .WithMessage(ValidationMessages.DescriptionTypeNameDuplicate);

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage(ValidationMessages.DescriptionTypeDescriptionMaxLength);

        RuleFor(x => x.CategoryIds)
            .Must((cmd, ids) => cmd.IsGlobal || (ids is not null && ids.Count > 0))
            .WithMessage(ValidationMessages.DescriptionTypeCategoriesRequired);
    }
}

public class AddDescriptionValueValidator : AbstractValidator<AddDescriptionValue>
{
    public AddDescriptionValueValidator()
    {
        RuleFor(x => x.Value)
            .NotEmpty().WithMessage(ValidationMessages.DescriptionValueRequired)
            .MaximumLength(500).WithMessage(ValidationMessages.DescriptionValueMaxLength);
    }
}
