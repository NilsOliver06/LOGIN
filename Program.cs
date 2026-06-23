using Microsoft.EntityFrameworkCore;
using LOGIN.Data;
using DotNetEnv;  // 👈 Para leer .env

// Cargar variables de entorno desde .env
Env.Load();

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// CONFIGURACIÓN DE BASE DE DATOS - SUPABASE (POSTGRESQL)
// ============================================================

// 📌 Leer connection string de variable de entorno o appsettings
var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
                       ?? builder.Configuration.GetConnectionString("DefaultConnection");

Console.WriteLine($"🔗 Usando conexión: {(Environment.GetEnvironmentVariable("DATABASE_URL") != null ? "🌍 Variable de entorno" : "📁 appsettings.json")}");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorCodesToAdd: null);
    }));

// ============================================================
// SERVICIOS DE LA APLICACIÓN
// ============================================================

builder.Services.AddControllersWithViews();
builder.Services.AddControllers();

// Configuración de Session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// ============================================================
// APLICAR MIGRACIONES AUTOMÁTICAMENTE AL INICIAR
// ============================================================
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        dbContext.Database.Migrate();
        Console.WriteLine("✅ Migraciones aplicadas correctamente");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Error al aplicar migraciones: {ex.Message}");
    }
}

// ============================================================
// MIDDLEWARE
// ============================================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthorization();

// ============================================================
// RUTAS
// ============================================================

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.MapControllers();

// ============================================================
// INICIAR APLICACIÓN
// ============================================================

// 📌 Puerto dinámico para Vercel
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Run($"http://*:{port}");