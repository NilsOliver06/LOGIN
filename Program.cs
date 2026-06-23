using Microsoft.EntityFrameworkCore;
using LOGIN.Data;
using LOGIN.Models;
using System.Net;

// ============================================================
// 🔥 FORZAR IPv4 PARA SUPABASE
// ============================================================
ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
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

// Configuración de Session
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
        // Aplicar migraciones
        dbContext.Database.Migrate();
        Console.WriteLine("✅ Migraciones aplicadas correctamente");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Error al aplicar migraciones: {ex.Message}");
    }
}

// ============================================================
// 👑 CREAR ADMINISTRADOR POR DEFECTO
// ============================================================
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    try
    {
        var adminEmail = "admin@candyshoes.pe";
        var adminExists = dbContext.Usuarios.Any(u => u.Email == adminEmail);

        if (!adminExists)
        {
            var admin = new Usuario
            {
                Nombre = "Administrador",
                Email = adminEmail,
                Password = "Admin123!",
                Edad = 30,
                Ciudad = "Lima",
                Rol = "Admin",
                FechaRegistro = DateTime.UtcNow
            };

            dbContext.Usuarios.Add(admin);
            dbContext.SaveChanges();
            Console.WriteLine("✅ Administrador creado correctamente");
            Console.WriteLine($"   Email: {adminEmail}");
            Console.WriteLine($"   Contraseña: Admin123!");
        }
        else
        {
            Console.WriteLine("ℹ️ Administrador ya existe");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️ Error al crear administrador: {ex.Message}");
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

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
Console.WriteLine($"🚀 Iniciando en puerto: {port}");
app.Run($"http://*:{port}");