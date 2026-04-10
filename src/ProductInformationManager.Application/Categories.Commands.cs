using System.Text.RegularExpressions;
using FluentValidation;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using ProductInformationManager.Application.Resources;
using ProductInformationManager.Domain;
using ProductInformationManager.Domain.ValueObjects;
using ProductInformationManager.Infrastructure;
using ProductInformationManager.Messages;

namespace ProductInformationManager.Application;

public partial class CreateCategoryConsumer(ProductContext db, IValidator<CreateCategory> validator) : IConsumer<CreateCategory>
{
    public async Task Consume(ConsumeContext<CreateCategory> context)
    {
        var command = context.Message;

        var validation = await validator.ValidateAsync(command, context.CancellationToken);
        if (!validation.IsValid)
        {
            await context.RespondAsync(new CreateCategoryResult(false, ErrorMessage: validation.Errors[0].ErrorMessage));
            return;
        }

        CategoryId? parentId = command.ParentId.HasValue ? new CategoryId(command.ParentId.Value) : null;

        string path;
        if (parentId is not null)
        {
            var parent = await db.Categories.FindAsync([parentId], context.CancellationToken);
            if (parent is null)
            {
                await context.RespondAsync(new CreateCategoryResult(false, ErrorMessage: ValidationMessages.CategoryNotFound));
                return;
            }
            path = $"{parent.Path}.{NormalizePath(command.Name)}";
        }
        else
        {
            path = NormalizePath(command.Name);
        }

        var category = new Category(command.Name, path, command.Description, parentId);

        db.Categories.Add(category);
        await db.SaveChangesAsync(context.CancellationToken);

        await context.RespondAsync(new CreateCategoryResult(true, category.Id.Value, category.Path));
    }

    private static string NormalizePath(string name) =>
        PathNormalizer().Replace(name.Trim().ToLowerInvariant(), "_").Trim('_');

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex PathNormalizer();
}

public class UpdateCategoryConsumer(ProductContext db, IValidator<UpdateCategory> validator) : IConsumer<UpdateCategory>
{
    public async Task Consume(ConsumeContext<UpdateCategory> context)
    {
        var command = context.Message;

        var validation = await validator.ValidateAsync(command, context.CancellationToken);
        if (!validation.IsValid)
        {
            await context.RespondAsync(new UpdateCategoryResult(false, validation.Errors[0].ErrorMessage));
            return;
        }

        var categoryId = new CategoryId(command.Id);
        var category = await db.Categories.FindAsync([categoryId], context.CancellationToken);

        if (category is null)
        {
            await context.RespondAsync(new UpdateCategoryResult(false, ValidationMessages.CategoryNotFound));
            return;
        }

        category.Rename(command.Name, command.Description);

        await db.SaveChangesAsync(context.CancellationToken);
        await context.RespondAsync(new UpdateCategoryResult(true));
    }
}

public class DeleteCategoryConsumer(ProductContext db) : IConsumer<DeleteCategory>
{
    public async Task Consume(ConsumeContext<DeleteCategory> context)
    {
        var categoryId = new CategoryId(context.Message.Id);
        var category = await db.Categories.FindAsync([categoryId], context.CancellationToken);

        if (category is null)
        {
            await context.RespondAsync(new DeleteCategoryResult(false, ValidationMessages.CategoryNotFound));
            return;
        }

        // Check for children
        var hasChildren = await db.Categories.AnyAsync(c => c.ParentId == categoryId, context.CancellationToken);
        if (hasChildren)
        {
            await context.RespondAsync(new DeleteCategoryResult(false, ValidationMessages.CategoryDeleteHasChildren));
            return;
        }

        // Check for associated products
        var hasProducts = await db.Products.AnyAsync(p => p.CategoryId == categoryId, context.CancellationToken);
        if (hasProducts)
        {
            await context.RespondAsync(new DeleteCategoryResult(false, ValidationMessages.CategoryDeleteHasProducts));
            return;
        }

        // Check for associated description rules
        var hasDescriptionRules = await db.CategoryDescriptionRules.AnyAsync(r => r.CategoryId == categoryId, context.CancellationToken);
        if (hasDescriptionRules)
        {
            await context.RespondAsync(new DeleteCategoryResult(false, ValidationMessages.CategoryDeleteHasDescriptions));
            return;
        }

        db.Categories.Remove(category);
        await db.SaveChangesAsync(context.CancellationToken);

        await context.RespondAsync(new DeleteCategoryResult(true));
    }
}
