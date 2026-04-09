using MassTransit;
using Microsoft.EntityFrameworkCore;
using ProductInformationManager.Domain;
using ProductInformationManager.Domain.Exceptions;
using ProductInformationManager.Domain.ValueObjects;
using ProductInformationManager.Infrastructure;
using ProductInformationManager.Messages.Products;

namespace ProductInformationManager.Application;

public class CreateProductConsumer(ProductContext db) : IConsumer<CreateProduct>
{
    public async Task Consume(ConsumeContext<CreateProduct> context)
    {
        var command = context.Message;
        
        // Creazione tramite costruttore del Dominio (protegge invarianti e genera ID)
        var product = new Product(command.Name, command.Sku, new CategoryId(command.CategoryId));

        db.Products.Add(product);
        await db.SaveChangesAsync(context.CancellationToken);

        await context.RespondAsync(new CreateProductResult(product.Id.Value));
    }
}

public class UpdateProductDetailsConsumer(ProductContext db) : IConsumer<UpdateProductDetails>
{
    public async Task Consume(ConsumeContext<UpdateProductDetails> context)
    {
        var command = context.Message;
        var product = await db.Products.FindAsync([new ProductId(command.Id)], context.CancellationToken);

        if (product is null) return;

        product.UpdateDetails(command.Name, command.Description); // Logica di Dominio
        await db.SaveChangesAsync(context.CancellationToken);
    }
}

public class ChangeProductStatusConsumer(ProductContext db) : IConsumer<ChangeProductStatus>
{
    public async Task Consume(ConsumeContext<ChangeProductStatus> context)
    {
        var product = await db.Products.FindAsync([new ProductId(context.Message.Id)], context.CancellationToken);
        if (product is null) return;

        if (context.Message.IsActive) product.Activate();
        else product.Deactivate();

        await db.SaveChangesAsync(context.CancellationToken);
    }
}

public class SetProductDescriptionConsumer(ProductContext db) : IConsumer<SetProductDescription>
{
    public async Task Consume(ConsumeContext<SetProductDescription> context)
    {
        var command = context.Message;
        
        // Per rispettare il DDD, dobbiamo caricare Aggregato e relative dipendenze
        var product = await db.Products
            .Include(p => p.Descriptions) // Carichiamo la collection per permettere all'aggregato di rimpiazzare il valore
            .FirstOrDefaultAsync(p => p.Id == new ProductId(command.ProductId), context.CancellationToken);

        var type = await db.DescriptionTypes.FindAsync([new DescriptionTypeId(command.DescriptionTypeId)], context.CancellationToken);
        var value = await db.DescriptionValues.FindAsync([new DescriptionValueId(command.DescriptionValueId)], context.CancellationToken);

        if (product is null || type is null || value is null)
            throw new DomainException("Entità non trovate per l'assegnazione della descrizione.");

        // Delega all'Aggregato: farà lui la validazione se il valore appartiene al tipo e sostituirà il vecchio!
        product.SetDescription(type, value);

        await db.SaveChangesAsync(context.CancellationToken);
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

        product.AddAttribute(attribute); // Logica di Dominio
        await db.SaveChangesAsync(context.CancellationToken);
    }
}