using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ProductInformationManager.Application.Resources;
using ProductInformationManager.Domain.ValueObjects;
using ProductInformationManager.Infrastructure;
using ProductInformationManager.Messages;

namespace ProductInformationManager.Application.Validators;

public class CreateAttributeTypeValidator : AbstractValidator<CreateAttributeType>
{
    public CreateAttributeTypeValidator(ProductContext db)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(ValidationMessages.AttributeTypeNameRequired)
            .MaximumLength(100).WithMessage(ValidationMessages.AttributeTypeNameMaxLength)
            .MustAsync(async (name, ct) =>
                !await db.AttributeTypes.AnyAsync(a => a.Name == name, ct))
            .WithMessage(ValidationMessages.AttributeTypeNameDuplicate);

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage(ValidationMessages.AttributeTypeDescriptionMaxLength);
    }
}

public class UpdateAttributeTypeValidator : AbstractValidator<UpdateAttributeType>
{
    public UpdateAttributeTypeValidator(ProductContext db)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(ValidationMessages.AttributeTypeNameRequired)
            .MaximumLength(100).WithMessage(ValidationMessages.AttributeTypeNameMaxLength)
            .MustAsync(async (cmd, name, ct) =>
                !await db.AttributeTypes.AnyAsync(a => a.Name == name && a.Id != new AttributeTypeId(cmd.Id), ct))
            .WithMessage(ValidationMessages.AttributeTypeNameDuplicate);

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage(ValidationMessages.AttributeTypeDescriptionMaxLength);
    }
}

public class CreateAttributeValidator : AbstractValidator<CreateAttribute>
{
    public CreateAttributeValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(ValidationMessages.AttributeNameRequired);

        RuleFor(x => x.Value)
            .NotEmpty().WithMessage(ValidationMessages.AttributeValueRequired);
    }
}
