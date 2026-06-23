using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LOGIN.Models;
using LOGIN.Data;

namespace LOGIN.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ============================================================
        // LOGIN
        // ============================================================

        [HttpGet]
        public IActionResult Login()
        {
            if (!string.IsNullOrEmpty(HttpContext.Session.GetString("UsuarioId")))
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                var usuario = await _context.Usuarios
                    .FirstOrDefaultAsync(u => u.Email == model.Email && u.Password == model.Password);

                if (usuario != null)
                {
                    HttpContext.Session.SetString("UsuarioId", usuario.Id.ToString());
                    HttpContext.Session.SetString("UsuarioNombre", usuario.Nombre ?? "");
                    HttpContext.Session.SetString("UsuarioEmail", usuario.Email ?? "");

                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    ModelState.AddModelError("", "Email o contraseña incorrectos");
                }
            }
            return View(model);
        }

        // ============================================================
        // REGISTER
        // ============================================================

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(Usuario usuario)
        {
            if (ModelState.IsValid)
            {
                var existe = await _context.Usuarios.AnyAsync(u => u.Email == usuario.Email);
                if (existe)
                {
                    ModelState.AddModelError("Email", "Este email ya está registrado");
                    return View(usuario);
                }

                usuario.FechaRegistro = DateTime.UtcNow;
                _context.Usuarios.Add(usuario);
                await _context.SaveChangesAsync();

                TempData["Mensaje"] = "Registro exitoso. ¡Inicia sesión!";
                return RedirectToAction("Login");
            }
            return View(usuario);
        }

        // ============================================================
        // LOGOUT
        // ============================================================

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        // ============================================================
        // CRUD USUARIOS - NUEVOS MÉTODOS
        // ============================================================

        // GET: Account/Index (Lista de usuarios - solo Admin)
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // Verificar si es Admin
            var usuarioId = HttpContext.Session.GetString("UsuarioId");
            if (string.IsNullOrEmpty(usuarioId))
                return RedirectToAction("Login", "Account");

            var usuario = await _context.Usuarios.FindAsync(int.Parse(usuarioId));
            if (usuario?.Email != "admin@candyshoes.pe")
                return RedirectToAction("Login", "Account");

            var usuarios = await _context.Usuarios.ToListAsync();
            return View(usuarios);
        }

        // GET: Account/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            var usuarioId = HttpContext.Session.GetString("UsuarioId");
            if (string.IsNullOrEmpty(usuarioId))
                return RedirectToAction("Login", "Account");

            // Verificar si es Admin o el propio usuario
            var usuarioActual = await _context.Usuarios.FindAsync(int.Parse(usuarioId));
            bool esAdmin = usuarioActual?.Email == "admin@candyshoes.pe";

            if (id == null)
            {
                // Si no se pasa ID, editar el propio perfil
                id = int.Parse(usuarioId);
            }
            else
            {
                // Si se pasa ID diferente, solo Admin puede editar
                if (!esAdmin && id != int.Parse(usuarioId))
                    return RedirectToAction("Login", "Account");
            }

            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null)
                return NotFound();

            return View(usuario);
        }

        // POST: Account/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Usuario usuario)
        {
            var usuarioId = HttpContext.Session.GetString("UsuarioId");
            if (string.IsNullOrEmpty(usuarioId))
                return RedirectToAction("Login", "Account");

            if (id != usuario.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var existente = await _context.Usuarios.FindAsync(id);
                    if (existente == null)
                        return NotFound();

                    // Actualizar datos
                    existente.Nombre = usuario.Nombre;
                    existente.Email = usuario.Email;
                    existente.Edad = usuario.Edad;
                    existente.Ciudad = usuario.Ciudad;

                    // Solo actualizar contraseña si se proporcionó una nueva
                    if (!string.IsNullOrEmpty(usuario.Password))
                    {
                        existente.Password = usuario.Password;
                    }

                    _context.Update(existente);
                    await _context.SaveChangesAsync();

                    // Actualizar sesión si es el propio usuario
                    if (id == int.Parse(usuarioId))
                    {
                        HttpContext.Session.SetString("UsuarioNombre", existente.Nombre ?? "");
                        HttpContext.Session.SetString("UsuarioEmail", existente.Email ?? "");
                    }

                    TempData["Mensaje"] = "✅ Perfil actualizado correctamente";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!UsuarioExists(usuario.Id))
                        return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(usuario);
        }

        // GET: Account/Delete/5
        [HttpGet]
        public async Task<IActionResult> Delete(int? id)
        {
            var usuarioId = HttpContext.Session.GetString("UsuarioId");
            if (string.IsNullOrEmpty(usuarioId))
                return RedirectToAction("Login", "Account");

            var usuarioActual = await _context.Usuarios.FindAsync(int.Parse(usuarioId));
            bool esAdmin = usuarioActual?.Email == "admin@candyshoes.pe";

            if (id == null)
                return NotFound();

            // No permitir eliminar al Admin
            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null)
                return NotFound();

            if (usuario.Email == "admin@candyshoes.pe")
            {
                TempData["Error"] = "⚠️ No se puede eliminar la cuenta de Administrador";
                return RedirectToAction(nameof(Index));
            }

            // Solo Admin puede eliminar o el propio usuario
            if (!esAdmin && id != int.Parse(usuarioId))
            {
                TempData["Error"] = "⚠️ No tienes permiso para eliminar esta cuenta";
                return RedirectToAction(nameof(Index));
            }

            return View(usuario);
        }

        // POST: Account/Delete/5
        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var usuarioId = HttpContext.Session.GetString("UsuarioId");
            if (string.IsNullOrEmpty(usuarioId))
                return RedirectToAction("Login", "Account");

            var usuarioActual = await _context.Usuarios.FindAsync(int.Parse(usuarioId));
            bool esAdmin = usuarioActual?.Email == "admin@candyshoes.pe";

            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null)
                return NotFound();

            if (usuario.Email == "admin@candyshoes.pe")
            {
                TempData["Error"] = "⚠️ No se puede eliminar la cuenta de Administrador";
                return RedirectToAction(nameof(Index));
            }

            if (!esAdmin && id != int.Parse(usuarioId))
            {
                TempData["Error"] = "⚠️ No tienes permiso para eliminar esta cuenta";
                return RedirectToAction(nameof(Index));
            }

            _context.Usuarios.Remove(usuario);
            await _context.SaveChangesAsync();

            // Si el usuario se eliminó a sí mismo, cerrar sesión
            if (id == int.Parse(usuarioId))
            {
                HttpContext.Session.Clear();
                TempData["Mensaje"] = "✅ Tu cuenta ha sido eliminada";
                return RedirectToAction("Login");
            }

            TempData["Mensaje"] = "✅ Usuario eliminado correctamente";
            return RedirectToAction(nameof(Index));
        }

        private bool UsuarioExists(int id)
        {
            return _context.Usuarios.Any(e => e.Id == id);
        }
    }
}