using Microsoft.EntityFrameworkCore;
using LOGIN.Data;
using System.Net;

// ============================================================
// 🔥 FORZAR IPv4 PARA SUPABASE
// ============================================================
ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

// Forzar que todas las conexiones usen IPv4
AppContext.SetSwitch("System.Net.DisableIPv6", true);

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// CONFIGURACIÓN DE BASE DE DATOS - SUPABASE
// ============================================================

// Leer connection string de variable de entorno o appsettings
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

Console.WriteLine("🔗 Intentando conectar a Supabase...");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorCodesToAdd: null);
    }));

// ============================================================
// SERVICIOS DE LA APLICACIÓN
// ============================================================

builder.Services.AddControllersWithViews();
builder.Services.AddControllers();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// ============================================================
// CONFIGURACIÓN DE LA APLICACIÓN
// ============================================================

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

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.MapControllers();

// ============================================================
// INICIAR APLICACIÓN
// ============================================================

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
Console.WriteLine($"🚀 Iniciando en puerto: {port}");
app.Run($"http://*:{port}");