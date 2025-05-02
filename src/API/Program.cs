using Ratatosk.API.Products;
using Ratatosk.Application.Configuration;
using Ratatosk.Infrastructure.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline.
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


app.UseHttpsRedirection();
app.MapProductsEndpoints();

app.Run();