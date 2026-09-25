using Ratatosk.API.Auth;
using Ratatosk.API.Inventory;
using Ratatosk.API.Orders;
using Ratatosk.API.Products;
using Ratatosk.API.Middleware;
using Ratatosk.Application;
using Ratatosk.Infrastructure;
using Ratatosk.API;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddAPI(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Ratatosk API v1");
    });
    app.UseReDoc(options =>
    {
        options.SpecUrl("/openapi/v1.json");
    });
}

// Register middleware
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<CleanupResponseMiddleware>();

// Map endpoints
app.MapGet("/healthz", () => Results.Ok("Healthy"));
app.MapAuthEndpoints();
app.MapProductsEndpoints();
app.MapInventoryEndpoints();
app.MapOrderEndpoints();

app.Run();
