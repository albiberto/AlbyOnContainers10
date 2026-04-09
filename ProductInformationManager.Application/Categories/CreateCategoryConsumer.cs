using System.Text.RegularExpressions;
using MassTransit;

namespace ProductInformationManager.Application.Categories;

public partial class CreateCategoryConsumer(ProductContext db) : IConsumer<CreateCategory>
{
    public async Task Consume(ConsumeContext<CreateCategory> context)
    {
        var command = context.Message;

        string path;
        if (command.ParentId is not null)
        {
            var parent = await db.Categories.FindAsync([command.ParentId.Value], context.CancellationToken);
            if (parent is null)
            {
                await context.RespondAsync(new CreateCategoryResult(Guid.Empty, string.Empty));
                return;
            }
            path = $"{parent.Path}.{NormalizePath(command.Name)}";
        }
        else
        {
            path = NormalizePath(command.Name);
        }

        var category = new ProductInformationManager.Domain.Category(command.Name, path, command.Description, command.ParentId);

        db.Categories.Add(category);
        await db.SaveChangesAsync(context.CancellationToken);

        await context.RespondAsync(new CreateCategoryResult(category.Id, category.Path));
    }

    private static string NormalizePath(string name) =>
        PathNormalizer().Replace(name.Trim().ToLowerInvariant(), "_").Trim('_');

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex PathNormalizer();
}