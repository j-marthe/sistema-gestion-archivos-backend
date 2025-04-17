using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using SistemaGestionArchivosBackend.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Agrega los controladores
builder.Services.AddControllers();

// Swagger / OpenAPI
builder.Services.AddOpenApi();

// Inyección del AuthService (ADO.NET)
builder.Services.AddScoped<AuthService>();

// Inyección del BlobStorageService
builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();

// Configuración de JWT
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var key = builder.Configuration["Jwt:Key"];
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key!))
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// app.UseHttpsRedirection(); // opcional

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
