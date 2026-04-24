using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ProductInformationManager.Application.Resources;
using ProductInformationManager.Domain.ValueObjects;
using ProductInformationManager.Infrastructure;
using ProductInformationManager.Messages;

namespace ProductInformationManager.Application.Validators;

public class CreateProductValidator : AbstractValidator<CreateProduct>
{
    public CreateProductValidator(ProductContext db)
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(ValidationMessages.ProductNameRequired)
            .MaximumLength(200).WithMessage(ValidationMessages.ProductNameMaxLength);

        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage(ValidationMessages.ProductCategoryRequired)
            .DependentRules(() =>
            {
                RuleFor(x => x.CategoryId)
                    .MustAsync(async (categoryId, ct) =>
                        await db.Categories.AnyAsync(c => c.Id == new CategoryId(categoryId), ct))
                    .WithMessage(ValidationMessages.ProductCategoryNotFound);
            });
    }
}
