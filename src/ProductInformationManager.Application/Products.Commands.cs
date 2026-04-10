using FluentValidation;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using ProductInformationManager.Application.Resources;
using ProductInformationManager.Domain;
using ProductInformationManager.Domain.Exceptions;
using ProductInformationManager.Domain.ValueObjects;
using ProductInformationManager.Infrastructure;
using ProductInformationManager.Messages;

namespace ProductInformationManager.Application;

public class CreateProductConsumer(ProductContext db, IValidator<CreateProduct> validator) : IConsumer<CreateProduct>
{
    public async Task Consume(ConsumeContext<CreateProduct> context)
    {
        var command = context.Message;

        var validation = await validator.ValidateAsync(command, context.CancellationToken);
        if (!validation.IsValid)
        {
            await context.RespondAsync(new CreateProductResult(false, ErrorMessage: validation.Errors[0].ErrorMessage));
            return;
        }

        var product = new Product(command.Name, command.Sku, new CategoryId(command.CategoryId));

        db.Products.Add(product);
        await db.SaveChangesAsync(context.CancellationToken);

        await context.RespondAsync(new CreateProductResult(true, product.Id.Value));
    }
}

public class UpdateProductDetailsConsumer(ProductContext db) : IConsumer<UpdateProductDetails>
{
    public async Task Consume(ConsumeContext<UpdateProductDetails> context)
    {
        var command = context.Message;
        var product = await db.Products.FindAsync([new ProductId(command.Id)], context.CancellationToken);

        if (product is null)
        {
            await context.RespondAsync(new UpdateProductDetailsResult(false, ValidationMessages.ProductNotFound));
            return;
        }

        product.UpdateDetails(command.Name, command.Description);
        await db.SaveChangesAsync(context.CancellationToken);

        await context.RespondAsync(new UpdateProductDetailsResult(true));
    }
}

public class ChangeProductCategoryConsumer(ProductContext db) : IConsumer<ChangeProductCategory>
{
    public async Task Consume(ConsumeContext<ChangeProductCategory> context)
    {
        var command = context.Message;
        var product = await db.Products.FindAsync([new ProductId(command.Id)], context.CancellationToken);

        if (product is null)
        {
            await context.RespondAsync(new ChangeProductCategoryResult(false, ValidationMessages.ProductNotFound));
            return;
        }

        var categoryExists = await db.Categories.AnyAsync(c => c.Id == new CategoryId(command.CategoryId), context.CancellationToken);
        if (!categoryExists)
        {
            await context.RespondAsync(new ChangeProductCategoryResult(false, ValidationMessages.ProductCategoryNotFound));
            return;
        }

        product.ChangeCategory(new CategoryId(command.CategoryId));
        await db.SaveChangesAsync(context.CancellationToken);

        await context.RespondAsync(new ChangeProductCategoryResult(true));
    }
}

public class ChangeProductStatusConsumer(ProductContext db) : IConsumer<ChangeProductStatus>
{
    public async Task Consume(ConsumeContext<ChangeProductStatus> context)
    {
        var product = await db.Products.FindAsync([new ProductId(context.Message.Id)], context.CancellationToken);
        if (product is null)
        {
            await context.RespondAsync(new ChangeProductStatusResult(false, ValidationMessages.ProductNotFound));
            return;
        }

        if (context.Message.IsActive) product.Activate();
        else product.Deactivate();

        await db.SaveChangesAsync(context.CancellationToken);
        await context.RespondAsync(new ChangeProductStatusResult(true));
    }
}

public class SetProductDescriptionConsumer(ProductContext db) : IConsumer<SetProductDescription>
{
    public async Task Consume(ConsumeContext<SetProductDescription> context)
    {
        var command = context.Message;
        
        var product = await db.Products
            .Include(p => p.Descriptions)
            .FirstOrDefaultAsync(p => p.Id == new ProductId(command.ProductId), context.CancellationToken);

        var type = await db.DescriptionTypes.FindAsync([new DescriptionTypeId(command.DescriptionTypeId)], context.CancellationToken);
        var value = await db.DescriptionValues.FindAsync([new DescriptionValueId(command.DescriptionValueId)], context.CancellationToken);

        if (product is null || type is null || value is null)
        {
            await context.RespondAsync(new SetProductDescriptionResult(false, "Entity not found for description assignment."));
            return;
        }

        product.SetDescription(type, value);
        await db.SaveChangesAsync(context.CancellationToken);

        await context.RespondAsync(new SetProductDescriptionResult(true));
    }
}

public class AddProductAttributeConsumer(ProductContext db) : IConsumer<AddProductAttribute>
{
    public async Task Consume(ConsumeContext<AddProductAttribute> context)
    {
        var command = context.Message;
        
        var product = await db.Products
            .Include(p => p.Attributes)
            .FirstOrDefaultAsync(p => p.Id == new ProductId(command.ProductId), context.CancellationToken);
            
        var attribute = await db.Attributes.FindAsync([new AttributeId(command.AttributeId)], context.CancellationToken);

        if (product is null || attribute is null) return;

        product.AddAttribute(attribute);
        await db.SaveChangesAsync(context.CancellationToken);
    }
}

public class DeleteProductConsumer(ProductContext db) : IConsumer<DeleteProduct>
{
    public async Task Consume(ConsumeContext<DeleteProduct> context)
    {
        var productId = new ProductId(context.Message.Id);
        var product = await db.Products.FindAsync([productId], context.CancellationToken);

        if (product is null)
        {
            await context.RespondAsync(new DeleteProductResult(false, ValidationMessages.ProductNotFound));
            return;
        }

        db.Products.Remove(product);
        await db.SaveChangesAsync(context.CancellationToken);

        await context.RespondAsync(new DeleteProductResult(true));
    }
}
