using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using System.Text;
using System.Text.Json.Serialization; // Added for cycle handling
using WebApplication2.Data;

var builder = WebApplication.CreateBuilder(args);

// Fixed: Added JsonOptions to prevent 500 serialization loop crashes
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddOpenApi();
builder.Services.AddScoped<ActivityLogger>();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
);

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is missing");
var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("Jwt:Issuer is missing");
var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("Jwt:Audience is missing");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey)
            )
        };
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowNext", policy =>
        policy.WithOrigins(
            "http://localhost:3000",
            "https://qa-flow-eight.vercel.app"
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()
    );
});

var app = builder.Build();

// Auto-run migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

// Show Scalar in both dev and production
app.MapOpenApi();
app.MapScalarApiReference();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection(); // only redirect in production when behind HTTPS proxy
}

app.UseStaticFiles();
app.UseCors("AllowNext");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();