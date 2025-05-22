using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Ratatosk.API.Auth;
using Ratatosk.API.Products;
using Ratatosk.Application.Configuration;
using Ratatosk.Core.Primitives;
using Ratatosk.Infrastructure.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddControllers();

var authOptions = builder.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>();
Guard.AgainstNull(authOptions, nameof(authOptions));
var key = Encoding.UTF8.GetBytes(authOptions!.Secret);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = authOptions.Issuer,

            ValidateAudience = false,
            ValidAudience = authOptions.Audience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),

            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorization();

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
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/healthz", () => Results.Ok("Healthy"));
app.MapAuthEndpoints();
app.MapProductsEndpoints();

app.Run();
