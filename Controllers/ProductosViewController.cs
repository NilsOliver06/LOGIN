using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LOGIN.Data;
using LOGIN.Models;

namespace LOGIN.Controllers
{
    public class ProductosViewController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProductosViewController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ============================================================
        // 🔒 VALIDACIÓN DE ADMINISTRADOR
        // ============================================================
        private bool EsAdmin()
        {
            var rol = HttpContext.Session.GetString("UsuarioRol");
            return rol == "Admin";
        }

        private bool UsuarioLogueado()
        {
            return !string.IsNullOrEmpty(HttpContext.Session.GetString("UsuarioId"));
        }

        // ============================================================
        // 📋 GET: ProductosView/Index
        // ============================================================
        public async Task<IActionResult> Index()
        {
            if (!UsuarioLogueado())
                return RedirectToAction("Login", "Account");

            if (!EsAdmin())
                return RedirectToAction("Index", "Home");

            var productos = await _context.Productos.ToListAsync();
            return View(productos);
        }

        // ============================================================
        // 🔍 GET: ProductosView/Details/5
        // ============================================================
        public async Task<IActionResult> Details(int? id)
        {
            if (!UsuarioLogueado())
                return RedirectToAction("Login", "Account");

            if (!EsAdmin())
                return RedirectToAction("Index", "Home");

            if (id == null)
                return NotFound();

            var producto = await _context.Productos.FirstOrDefaultAsync(m => m.Id == id);
            if (producto == null)
                return NotFound();

            return View(producto);
        }

        // ============================================================
        // ➕ GET: ProductosView/Create
        // ============================================================
        public IActionResult Create()
        {
            if (!UsuarioLogueado())
                return RedirectToAction("Login", "Account");

            if (!EsAdmin())
                return RedirectToAction("Index", "Home");

            return View();
        }

        // ============================================================
        // ➕ POST: ProductosView/Create
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Producto producto)
        {
            if (!UsuarioLogueado())
                return RedirectToAction("Login", "Account");

            if (!EsAdmin())
                return RedirectToAction("Index", "Home");

            if (ModelState.IsValid)
            {
                producto.FechaRegistro = DateTime.UtcNow;
                _context.Add(producto);
                await _context.SaveChangesAsync();
                TempData["Mensaje"] = "✅ Producto creado exitosamente";
                return RedirectToAction(nameof(Index));
            }
            return View(producto);
        }

        // ============================================================
        // ✏️ GET: ProductosView/Edit/5
        // ============================================================
        public async Task<IActionResult> Edit(int? id)
        {
            if (!UsuarioLogueado())
                return RedirectToAction("Login", "Account");

            if (!EsAdmin())
                return RedirectToAction("Index", "Home");

            if (id == null)
                return NotFound();

            var producto = await _context.Productos.FindAsync(id);
            if (producto == null)
                return NotFound();

            return View(producto);
        }

        // ============================================================
        // ✏️ POST: ProductosView/Edit/5
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Producto producto)
        {
            if (!UsuarioLogueado())
                return RedirectToAction("Login", "Account");

            if (!EsAdmin())
                return RedirectToAction("Index", "Home");

            if (id != producto.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var existente = await _context.Productos.FindAsync(id);
                    if (existente == null)
                        return NotFound();

                    existente.Nombre = producto.Nombre;
                    existente.Descripcion = producto.Descripcion;
                    existente.Cantidad = producto.Cantidad;
                    existente.Precio = producto.Precio;

                    _context.Update(existente);
                    await _context.SaveChangesAsync();
                    TempData["Mensaje"] = "✅ Producto actualizado exitosamente";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProductoExists(producto.Id))
                        return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(producto);
        }

        // ============================================================
        // 🗑️ GET: ProductosView/Delete/5
        // ============================================================
        public async Task<IActionResult> Delete(int? id)
        {
            if (!UsuarioLogueado())
                return RedirectToAction("Login", "Account");

            if (!EsAdmin())
                return RedirectToAction("Index", "Home");

            if (id == null)
                return NotFound();

            var producto = await _context.Productos.FirstOrDefaultAsync(m => m.Id == id);
            if (producto == null)
                return NotFound();

            return View(producto);
        }

        // ============================================================
        // 🗑️ POST: ProductosView/Delete/5
        // ============================================================
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!UsuarioLogueado())
                return RedirectToAction("Login", "Account");

            if (!EsAdmin())
                return RedirectToAction("Index", "Home");

            var producto = await _context.Productos.FindAsync(id);
            if (producto != null)
            {
                _context.Productos.Remove(producto);
                await _context.SaveChangesAsync();
                TempData["Mensaje"] = "✅ Producto eliminado exitosamente";
            }
            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // 🔍 MÉTODO AUXILIAR
        // ============================================================
        private bool ProductoExists(int id)
        {
            return _context.Productos.Any(e => e.Id == id);
        }
    }
}