using BookStoreApi.Models;
using BookStoreApi.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Read the BookStoreDatabase settings and register the service (one MongoClient for the app)
builder.Services.Configure<BookStoreDatabaseSettings>(
    builder.Configuration.GetSection("BookStoreDatabase"));
builder.Services.AddSingleton<BooksService>();

// 2. Keep JSON property names as written in C# (Name, Price), not camelCase
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = null;
});

// 3. OpenAPI document at /openapi/v1.json.
//    Version 3.0 so Swagger UI can read it (.NET 11 defaults to 3.2, which Swagger UI rejects).
builder.Services.AddOpenApi(options =>
{
    options.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0;
});

var app = builder.Build();

// 4. Swagger UI at /swagger (Development only).
//    Needs: dotnet add package Swashbuckle.AspNetCore.SwaggerUI
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "BookStore API");
    });
}

// 5. Endpoints, all under /books, shown under "Books" in Swagger
var books = app.MapGroup("/books").WithTags("Books");
//----------------------Searches books-------------------------------------

books.MapGet("/search", async (
    string? category,
    decimal? minPrice,
    decimal? maxPrice,
    BooksService service) =>
{
    var results = await service.SearchAsync(
        category, minPrice, maxPrice);

    return Results.Ok(results);
});

// --------------------------Gets most expensive books--------------------------------
books.MapGet("/top", async (int? count, BooksService service) =>
{
    int amount = count ?? 3;

    if (amount <= 0)
    {
        return Results.BadRequest("Count must be above 0.");
    }

    var results = await service.GetTopAsync(amount);

    return Results.Ok(results);
});



books.MapPost("/upload", async (IFormFile file, BooksService service) =>
{
    if (file == null || !file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
    {
        return Results.BadRequest("File must be JSON.");
    }

    List<Book>? newBooks;

    try
    {
        using var stream = file.OpenReadStream();

        newBooks = await System.Text.Json.JsonSerializer
            .DeserializeAsync<List<Book>>(stream,
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
    }
    catch (System.Text.Json.JsonException)
    {
        return Results.BadRequest("Invalid JSON file.");
    }

    if (newBooks == null)
    {
        return Results.BadRequest("Invalid JSON file.");
    }

    var invalidBooks = newBooks
        .Select((book, index) => new { book, index })
        .Where(x => x.book == null ||
                    string.IsNullOrWhiteSpace(x.book.Name) ||
                    string.IsNullOrWhiteSpace(x.book.Category) ||
                    string.IsNullOrWhiteSpace(x.book.Author) ||
                    x.book.Price <= 0)
        .Select(x => x.index + 1)
        .ToList();

    if (invalidBooks.Count > 0)
    {
        return Results.BadRequest(new
        {
            Message = "Some books are invalid.",
            InvalidBooks = invalidBooks
        });
    }

    var ids = await service.UploadBooksAsync(newBooks);

    return Results.Ok(new
    {
        InsertedCount = ids.Count,
        Ids = ids
    });
})
.DisableAntiforgery();


// GET /books
books.MapGet("/", async (BooksService service) =>
    await service.GetAsync());

// GET /books/{id}   (an ObjectId is 24 characters)
books.MapGet("/{id:length(24)}", async (string id, BooksService service) =>
    await service.GetAsync(id) is Book book
        ? Results.Ok(book)
        : Results.NotFound());

// POST /books
books.MapPost("/", async (Book newBook, BooksService service) =>
{
    await service.CreateAsync(newBook);
    return Results.Created($"/books/{newBook.Id}", newBook);
});

// PUT /books/{id}
books.MapPut("/{id:length(24)}", async (string id, Book updatedBook, BooksService service) =>
{
    var book = await service.GetAsync(id);
    if (book is null)
    {
        return Results.NotFound();
    }

    updatedBook.Id = book.Id;
    await service.UpdateAsync(id, updatedBook);
    return Results.NoContent();
});

// DELETE /books/{id}
books.MapDelete("/{id:length(24)}", async (string id, BooksService service) =>
{
    var book = await service.GetAsync(id);
    if (book is null)
    {
        return Results.NotFound();
    }

    await service.RemoveAsync(id);
    return Results.NoContent();
});

//-------------------------------Changes book price--------------------------------
books.MapPatch("/{id}/price", async (
    string id,
    PriceUpdate request,
    BooksService service) =>
{
    if (request.Price <= 0)
    {
        return Results.BadRequest("Price must be above 0.");
    }

    if (!MongoDB.Bson.ObjectId.TryParse(id, out _))
    {
        return Results.NotFound();
    }

    var updated = await service.ChangePriceAsync(
        id, request.Price);

    return updated
        ? Results.NoContent()
        : Results.NotFound();
});

app.Run();

public record PriceUpdate(decimal Price);


