using UniFlowHub.Api.Data;
using UniFlowHub.Api.Data.Interfaces;
using UniFlowHub.Api.Data.Repositories;
using UniFlowHub.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Text;
using UniFlowHub.Api.Hubs;

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
            .AllowAnyMethod()
            .AllowCredentials());
});

// 🔹 Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "UniFlowHub",
        Version = "v1",
        Description = "API UniFlowHub"
    });
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();

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
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddSignalR();

builder.Services.AddScoped<IUserRepo, UserRepo>();
builder.Services.AddScoped<ISolicitacoesRHRepo, SolicitacoesRHRepo>();
builder.Services.AddScoped<IChamadosTIRepo, ChamadosTIRepo>();
builder.Services.AddScoped<IEquipamentosTIRepo, EquipamentosTIRepo>();
builder.Services.AddScoped<ISolicitacoesCompraRepo, SolicitacoesCompraRepo>();
builder.Services.AddScoped<IUnidadesRepo, UnidadesRepo>();
builder.Services.AddScoped<UsersService>();
builder.Services.AddScoped<SolicitacoesRHService>();
builder.Services.AddScoped<ChamadosTIService>();
builder.Services.AddScoped<BaseConhecimentoTIService>();
builder.Services.AddScoped<EquipamentosTIService>();
builder.Services.AddScoped<SolicitacoesCompraService>();
builder.Services.AddScoped<UnidadesService>();
builder.Services.AddScoped<OracleEmpresasService>();
builder.Services.AddScoped<CartaoPontoService>();
builder.Services.AddScoped<ControladoriaService>();
builder.Services.AddScoped<VeiculosService>();
builder.Services.AddScoped<VeiculosBiService>();
builder.Services.AddScoped<PecasBiService>();
builder.Services.AddScoped<PerfisService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<RepassesService>();
builder.Services.AddScoped<GestaoPessoasService>();

var app = builder.Build();

if (migrateMode)
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    try
    {
        logger.LogInformation("Aplicando migrations do banco interno PostgreSQL.");
        db.Database.Migrate();
        EnsureEmpresaLogoColumn(db);
        EnsureUnidadeMontadoraColumns(db);
        EnsureMontadoraRevendaConfigTable(db);
        EnsureEmpresaRevendaStatusConfigTable(db);
        logger.LogInformation("Migrations aplicadas com sucesso.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Falha ao aplicar migrations do banco interno PostgreSQL.");
        throw;
    }

    return;
}

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    try
    {
        db.Database.Migrate();
        EnsureEmpresaLogoColumn(db);
        EnsureUnidadeMontadoraColumns(db);
        EnsureMontadoraRevendaConfigTable(db);
        EnsureEmpresaRevendaStatusConfigTable(db);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Nao foi possivel aplicar migrations pendentes no startup.");
        throw;
    }
}

static void EnsureEmpresaLogoColumn(AppDbContext db)
{
    db.Database.ExecuteSqlRaw("""
        ALTER TABLE "Empresa"
        ADD COLUMN IF NOT EXISTS "LogoUrl" text NOT NULL DEFAULT '';
        """);
}

static void EnsureUnidadeMontadoraColumns(AppDbContext db)
{
    db.Database.ExecuteSqlRaw("""
        ALTER TABLE "Unidade"
        ADD COLUMN IF NOT EXISTS "Montadora" text NOT NULL DEFAULT '';

        ALTER TABLE "Unidade"
        ADD COLUMN IF NOT EXISTS "LogoMontadoraUrl" text NULL;
        """);
}

static void EnsureMontadoraRevendaConfigTable(AppDbContext db)
{
    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "MontadoraRevendaConfig" (
            "Id" integer GENERATED BY DEFAULT AS IDENTITY,
            "EmpresaNumero" integer NOT NULL,
            "RevendaNumero" integer NOT NULL,
            "Montadora" text NOT NULL,
            "LogoMontadoraUrl" text NULL,
            "Ativa" boolean NOT NULL DEFAULT true,
            "DataAtualizacao" timestamp with time zone NOT NULL,
            CONSTRAINT "PK_MontadoraRevendaConfig" PRIMARY KEY ("Id")
        );

        ALTER TABLE "MontadoraRevendaConfig"
        ADD COLUMN IF NOT EXISTS "Ativa" boolean NOT NULL DEFAULT true;

        CREATE UNIQUE INDEX IF NOT EXISTS "IX_MontadoraRevendaConfig_EmpresaNumero_RevendaNumero"
        ON "MontadoraRevendaConfig" ("EmpresaNumero", "RevendaNumero");
        """);
}

static void EnsureEmpresaRevendaStatusConfigTable(AppDbContext db)
{
    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "EmpresaRevendaStatusConfig" (
            "Id" integer GENERATED BY DEFAULT AS IDENTITY,
            "EmpresaNumero" integer NOT NULL,
            "Ativa" boolean NOT NULL DEFAULT true,
            "DataAtualizacao" timestamp with time zone NOT NULL,
            CONSTRAINT "PK_EmpresaRevendaStatusConfig" PRIMARY KEY ("Id")
        );

        CREATE UNIQUE INDEX IF NOT EXISTS "IX_EmpresaRevendaStatusConfig_EmpresaNumero"
        ON "EmpresaRevendaStatusConfig" ("EmpresaNumero");
        """);
}

// 🔹 Swagger (sempre ativo - pode limitar só para dev se quiser)
if (app.Environment.IsDevelopment())

{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "UniFlowHub v1");
        options.RoutePrefix = string.Empty;
    });
}


app.UseCors("Angular");

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChamadosTIChatHub>("/hubs/chamados-ti-chat");
app.MapFallbackToFile("index.html");

app.Run();
