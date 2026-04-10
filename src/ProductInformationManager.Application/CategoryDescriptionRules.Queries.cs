using MassTransit;
using Microsoft.EntityFrameworkCore;
using ProductInformationManager.Domain.ValueObjects;
using ProductInformationManager.Infrastructure;
using ProductInformationManager.Messages;

namespace ProductInformationManager.Application;

public class GetCategoryDescriptionsConsumer(ProductContext db) : IConsumer<GetCategoryDescriptions>
{
    public async Task Consume(ConsumeContext<GetCategoryDescriptions> context)
    {
        var categoryId = new CategoryId(context.Message.CategoryId);
        var descriptions = new List<CategoryDescriptionRuleDto>();
        var seenTypeIds = new HashSet<Guid>();

        // Walk up the hierarchy collecting description rules
        var currentId = categoryId;
        var isFirst = true;

        while (currentId is not null)
        {
            var category = await db.Categories
                .AsNoTracking()
                .Include(c => c.DescriptionRules)
                    .ThenInclude(r => r.DescriptionType)
                        .ThenInclude(dt => dt.Values)
                .FirstOrDefaultAsync(c => c.Id == currentId, context.CancellationToken);

            if (category is null) break;

            foreach (var rule in category.DescriptionRules)
            {
                // Only add if we haven't seen this type yet (child overrides parent)
                if (seenTypeIds.Add(rule.DescriptionTypeId.Value))
                {
                    descriptions.Add(new CategoryDescriptionRuleDto(
                        rule.DescriptionTypeId.Value,
                        rule.DescriptionType.Name,
                        rule.IsMandatory,
                        IsInherited: !isFirst,
                        SourceCategoryId: category.Id.Value,
                        SourceCategoryName: category.Name,
                        Values: rule.DescriptionType.Values
                            .OrderBy(v => v.Value)
                            .Select(v => new DescriptionValueDto(v.Id.Value, v.Value))
                            .ToList()
                    ));
                }
            }

            currentId = category.ParentId;
            isFirst = false;
        }

        await context.RespondAsync(new GetCategoryDescriptionsResult(descriptions));
    }
}
