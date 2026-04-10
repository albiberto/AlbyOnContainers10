using MassTransit;
using Microsoft.EntityFrameworkCore;
using ProductInformationManager.Application.Resources;
using ProductInformationManager.Domain;
using ProductInformationManager.Domain.ValueObjects;
using ProductInformationManager.Infrastructure;
using ProductInformationManager.Messages;

namespace ProductInformationManager.Application;

public class AddCategoryDescriptionRuleConsumer(ProductContext db) : IConsumer<AddCategoryDescriptionRule>
{
    public async Task Consume(ConsumeContext<AddCategoryDescriptionRule> context)
    {
        var command = context.Message;
        var categoryId = new CategoryId(command.CategoryId);
        var descriptionTypeId = new DescriptionTypeId(command.DescriptionTypeId);

        var category = await db.Categories
            .Include(c => c.DescriptionRules)
            .FirstOrDefaultAsync(c => c.Id == categoryId, context.CancellationToken);

        if (category is null)
        {
            await context.RespondAsync(new AddCategoryDescriptionRuleResult(false, ValidationMessages.CategoryNotFound));
            return;
        }

        var descriptionType = await db.DescriptionTypes.FindAsync([descriptionTypeId], context.CancellationToken);
        if (descriptionType is null)
        {
            await context.RespondAsync(new AddCategoryDescriptionRuleResult(false, ValidationMessages.DescriptionTypeNotFound));
            return;
        }

        // Check if already associated
        if (category.DescriptionRules.Any(r => r.DescriptionTypeId == descriptionTypeId))
        {
            await context.RespondAsync(new AddCategoryDescriptionRuleResult(false, ValidationMessages.CategoryDescriptionRuleAlreadyExists));
            return;
        }

        category.AddDescriptionRule(descriptionType, command.IsMandatory);
        await db.SaveChangesAsync(context.CancellationToken);

        await context.RespondAsync(new AddCategoryDescriptionRuleResult(true));
    }
}

public class RemoveCategoryDescriptionRuleConsumer(ProductContext db) : IConsumer<RemoveCategoryDescriptionRule>
{
    public async Task Consume(ConsumeContext<RemoveCategoryDescriptionRule> context)
    {
        var command = context.Message;
        var categoryId = new CategoryId(command.CategoryId);
        var descriptionTypeId = new DescriptionTypeId(command.DescriptionTypeId);

        var rule = await db.CategoryDescriptionRules
            .FirstOrDefaultAsync(r => r.CategoryId == categoryId && r.DescriptionTypeId == descriptionTypeId, context.CancellationToken);

        if (rule is null)
        {
            await context.RespondAsync(new RemoveCategoryDescriptionRuleResult(false, ValidationMessages.CategoryDescriptionRuleNotFound));
            return;
        }

        // Check if any product in this category uses values from this description type
        var valueIdsOfType = db.DescriptionValues
            .Where(v => v.DescriptionTypeId == descriptionTypeId)
            .Select(v => v.Id);

        var isUsed = await db.Set<ProductDescription>()
            .AnyAsync(pd =>
                db.Products.Any(p => p.CategoryId == categoryId && p.Id == pd.ProductId) &&
                valueIdsOfType.Contains(pd.DescriptionValueId),
                context.CancellationToken);

        if (isUsed)
        {
            await context.RespondAsync(new RemoveCategoryDescriptionRuleResult(false, ValidationMessages.CategoryDescriptionRuleDeleteInUse));
            return;
        }

        db.CategoryDescriptionRules.Remove(rule);
        await db.SaveChangesAsync(context.CancellationToken);

        await context.RespondAsync(new RemoveCategoryDescriptionRuleResult(true));
    }
}

public class UpdateCategoryDescriptionRuleMandatoryConsumer(ProductContext db) : IConsumer<UpdateCategoryDescriptionRuleMandatory>
{
    public async Task Consume(ConsumeContext<UpdateCategoryDescriptionRuleMandatory> context)
    {
        var command = context.Message;
        var categoryId = new CategoryId(command.CategoryId);
        var descriptionTypeId = new DescriptionTypeId(command.DescriptionTypeId);

        var rule = await db.CategoryDescriptionRules
            .FirstOrDefaultAsync(r => r.CategoryId == categoryId && r.DescriptionTypeId == descriptionTypeId, context.CancellationToken);

        if (rule is null)
        {
            await context.RespondAsync(new UpdateCategoryDescriptionRuleMandatoryResult(false, ValidationMessages.CategoryDescriptionRuleNotFound));
            return;
        }

        rule.ChangeMandatoryStatus(command.IsMandatory);
        await db.SaveChangesAsync(context.CancellationToken);

        await context.RespondAsync(new UpdateCategoryDescriptionRuleMandatoryResult(true));
    }
}
