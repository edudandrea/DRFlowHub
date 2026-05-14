using DRFlowHub.Api.Data;
using DRFlowHub.Api.Data.Interfaces;
using DRFlowHub.Api.Data.Repositories;
using DRFlowHub.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Text;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);
var migrateMode = args.Any(arg => string.Equals(arg, "--migrate", StringComparison.OrdinalIgnoreCase));

if (migrateMode)
{
    builder.Logging.ClearProviders();
    builder.Logging.AddSimpleConsole();
}

// 🔹 Controllers
builder.Services.AddControllers();
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .SelectMany(entry => entry.Value!.Errors.Select(error =>
            {
                var field = string.IsNullOrWhiteSpace(entry.Key) ? "Campo" : entry.Key;
                var message = string.IsNullOrWhiteSpace(error.ErrorMessage)
                    ? "valor invalido"
                    : error.ErrorMessage;
                return $"{field}: {message}";
            }))
            .ToList();

        return new BadRequestObjectResult(errors.Count > 0
            ? string.Join(" ", errors)
            : "Confira os campos informados.");
    };
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("Angular", policy =>
        policy.WithOrigins("http://localhost:4200", "http://127.0.0.1:4200")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

// 🔹 Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "DRFlowHub",
        Version = "v1",
        Description = "API DRFlowHub"
    });
});

builder.Services.AddHttpContextAccessor();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddScoped<IUserRepo, UserRepo>();
builder.Services.AddScoped<ISolicitacoesRHRepo, SolicitacoesRHRepo>();
builder.Services.AddScoped<IChamadosTIRepo, ChamadosTIRepo>();
builder.Services.AddScoped<IEquipamentosTIRepo, EquipamentosTIRepo>();
builder.Services.AddScoped<ISolicitacoesCompraRepo, SolicitacoesCompraRepo>();
builder.Services.AddScoped<IUnidadesRepo, UnidadesRepo>();
builder.Services.AddScoped<UsersService>();
builder.Services.AddScoped<SolicitacoesRHService>();
builder.Services.AddScoped<ChamadosTIService>();
builder.Services.AddScoped<EquipamentosTIService>();
builder.Services.AddScoped<SolicitacoesCompraService>();
builder.Services.AddScoped<UnidadesService>();
builder.Services.AddScoped<CartaoPontoService>();
builder.Services.AddScoped<ControladoriaService>();
builder.Services.AddScoped<VeiculosService>();
builder.Services.AddScoped<PecasBiService>();
builder.Services.AddScoped<PerfisService>();
builder.Services.AddScoped<AuthService>();

var app = builder.Build();

if (migrateMode)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    return;
}

// 🔹 Swagger (sempre ativo - pode limitar só para dev se quiser)
if (app.Environment.IsDevelopment())

{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "DRFlowHub v1");
        options.RoutePrefix = string.Empty;
    });
}


app.UseCors("Angular");

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
