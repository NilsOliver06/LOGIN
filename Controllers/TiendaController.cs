using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LOGIN.Data;
using LOGIN.Models;

namespace LOGIN.Controllers
{
    public class TiendaController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TiendaController(ApplicationDbContext context)
        {
            _context = context;
        }

        private bool UsuarioLogueado()
        {
            return !string.IsNullOrEmpty(HttpContext.Session.GetString("UsuarioId"));
        }

        private int GetUsuarioId()
        {
            return int.Parse(HttpContext.Session.GetString("UsuarioId") ?? "0");
        }

        // GET: Tienda (vitrina de productos)
        public async Task<IActionResult> Index()
        {
            if (!UsuarioLogueado())
                return RedirectToAction("Login", "Account");

            var productos = await _context.Productos.ToListAsync();
            var carritoCount = await _context.CarritoItems
                .Where(c => c.UsuarioId == GetUsuarioId())
                .SumAsync(c => c.Cantidad);

            ViewBag.CarritoCount = carritoCount;
            return View(productos);
        }

        // POST: Agregar al carrito
        [HttpPost]
        public async Task<IActionResult> AgregarAlCarrito(int productoId, int cantidad = 1)
        {
            if (!UsuarioLogueado())
                return Json(new { success = false, message = "Debes iniciar sesión" });

            var usuarioId = GetUsuarioId();
            var producto = await _context.Productos.FindAsync(productoId);

            if (producto == null)
                return Json(new { success = false, message = "Producto no encontrado" });

            if (producto.Cantidad < cantidad)
                return Json(new { success = false, message = "Stock insuficiente" });

            var itemExistente = await _context.CarritoItems
                .FirstOrDefaultAsync(c => c.UsuarioId == usuarioId && c.ProductoId == productoId);

            if (itemExistente != null)
            {
                itemExistente.Cantidad += cantidad;
            }
            else
            {
                var carritoItem = new CarritoItem
                {
                    UsuarioId = usuarioId,
                    ProductoId = productoId,
                    Cantidad = cantidad
                };
                _context.CarritoItems.Add(carritoItem);
            }

            await _context.SaveChangesAsync();

            var totalItems = await _context.CarritoItems
                .Where(c => c.UsuarioId == usuarioId)
                .SumAsync(c => c.Cantidad);

            return Json(new { success = true, message = "Producto agregado", count = totalItems });
        }

        // GET: Carrito
        public async Task<IActionResult> Carrito()
        {
            if (!UsuarioLogueado())
                return RedirectToAction("Login", "Account");

            var carritoItems = await _context.CarritoItems
                .Include(c => c.Producto)
                .Where(c => c.UsuarioId == GetUsuarioId())
                .ToListAsync();

            var total = carritoItems.Sum(c => c.Cantidad * c.Producto!.Precio);
            ViewBag.Total = total;

            return View(carritoItems);
        }

        // POST: Actualizar cantidad
        [HttpPost]
        public async Task<IActionResult> ActualizarCantidad(int itemId, int cantidad)
        {
            if (!UsuarioLogueado())
                return Json(new { success = false });

            var item = await _context.CarritoItems
                .Include(c => c.Producto)
                .FirstOrDefaultAsync(c => c.Id == itemId && c.UsuarioId == GetUsuarioId());

            if (item == null)
                return Json(new { success = false });

            if (cantidad <= 0)
            {
                _context.CarritoItems.Remove(item);
            }
            else
            {
                if (item.Producto != null && item.Producto.Cantidad < cantidad)
                    return Json(new { success = false, message = "Stock insuficiente" });

                item.Cantidad = cantidad;
            }

            await _context.SaveChangesAsync();

            var carritoItems = await _context.CarritoItems
                .Include(c => c.Producto)
                .Where(c => c.UsuarioId == GetUsuarioId())
                .ToListAsync();

            var total = carritoItems.Sum(c => c.Cantidad * c.Producto!.Precio);
            var totalItems = carritoItems.Sum(c => c.Cantidad);

            return Json(new
            {
                success = true,
                subtotal = item.Cantidad * (item.Producto?.Precio ?? 0),
                total = total,
                count = totalItems,
                itemRemoved = cantidad <= 0
            });
        }

        // GET: Checkout
        public async Task<IActionResult> Checkout()
        {
            if (!UsuarioLogueado())
                return RedirectToAction("Login", "Account");

            var carritoItems = await _context.CarritoItems
                .Include(c => c.Producto)
                .Where(c => c.UsuarioId == GetUsuarioId())
                .ToListAsync();

            if (!carritoItems.Any())
                return RedirectToAction("Carrito");

            ViewBag.Total = carritoItems.Sum(c => c.Cantidad * c.Producto!.Precio);
            return View();
        }

        // POST: Confirmar pedido
        [HttpPost]
        public async Task<IActionResult> ConfirmarPedido(string direccion, string metodoPago)
        {
            if (!UsuarioLogueado())
                return RedirectToAction("Login", "Account");

            var usuarioId = GetUsuarioId();
            var carritoItems = await _context.CarritoItems
                .Include(c => c.Producto)
                .Where(c => c.UsuarioId == usuarioId)
                .ToListAsync();

            if (!carritoItems.Any())
                return RedirectToAction("Carrito");

            // Verificar stock
            foreach (var item in carritoItems)
            {
                if (item.Producto!.Cantidad < item.Cantidad)
                {
                    TempData["Error"] = $"Stock insuficiente para {item.Producto.Nombre}";
                    return RedirectToAction("Carrito");
                }
            }

            // Crear pedido
            var total = carritoItems.Sum(c => c.Cantidad * c.Producto!.Precio);
            var pedido = new Pedido
            {
                UsuarioId = usuarioId,
                Total = total,
                DireccionEnvio = direccion,
                MetodoPago = metodoPago,
                NumeroReferencia = "REF-" + DateTime.Now.Ticks.ToString().Substring(0, 8),
                Estado = EstadoPedido.Confirmado
            };

            _context.Pedidos.Add(pedido);
            await _context.SaveChangesAsync();

            // Crear detalles y actualizar stock
            foreach (var item in carritoItems)
            {
                var detalle = new PedidoDetalle
                {
                    PedidoId = pedido.Id,
                    ProductoId = item.ProductoId,
                    Cantidad = item.Cantidad,
                    PrecioUnitario = item.Producto!.Precio
                };
                _context.PedidoDetalles.Add(detalle);

                // Actualizar stock
                item.Producto.Cantidad -= item.Cantidad;
                _context.Entry(item.Producto).State = EntityState.Modified;
            }

            // Limpiar carrito
            _context.CarritoItems.RemoveRange(carritoItems);
            await _context.SaveChangesAsync();

            TempData["Mensaje"] = "¡Pedido realizado con éxito!";
            return RedirectToAction("MisCompras");
        }

        // GET: Mis Compras
        public async Task<IActionResult> MisCompras()
        {
            if (!UsuarioLogueado())
                return RedirectToAction("Login", "Account");

            var pedidos = await _context.Pedidos
                .Include(p => p.Detalles!)
                .ThenInclude(d => d.Producto)
                .Where(p => p.UsuarioId == GetUsuarioId())
                .OrderByDescending(p => p.FechaPedido)
                .ToListAsync();

            return View(pedidos);
        }

        // ============================================================
        // CRUD DE PEDIDOS (ADMIN)
        // ============================================================

        // GET: Tienda/AdminPedidos
        [HttpGet]
        public async Task<IActionResult> AdminPedidos()
        {
            var usuarioId = HttpContext.Session.GetString("UsuarioId");
            if (string.IsNullOrEmpty(usuarioId))
                return RedirectToAction("Login", "Account");

            var usuario = await _context.Usuarios.FindAsync(int.Parse(usuarioId));
            if (usuario?.Email != "admin@candyshoes.pe")
                return RedirectToAction("Login", "Account");

            var pedidos = await _context.Pedidos
                .Include(p => p.Usuario)
                .Include(p => p.Detalles!)
                    .ThenInclude(d => d.Producto)
                .OrderByDescending(p => p.FechaPedido)
                .ToListAsync();

            return View(pedidos);
        }

        // GET: Tienda/Details/5 (Detalles del PEDIDO)
        [HttpGet]
        public async Task<IActionResult> Details(int? id)
        {
            var usuarioId = HttpContext.Session.GetString("UsuarioId");
            if (string.IsNullOrEmpty(usuarioId))
                return RedirectToAction("Login", "Account");

            if (id == null)
                return NotFound();

            var pedido = await _context.Pedidos
                .Include(p => p.Usuario)
                .Include(p => p.Detalles!)
                    .ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (pedido == null)
                return NotFound();

            var usuarioActual = await _context.Usuarios.FindAsync(int.Parse(usuarioId));
            bool esAdmin = usuarioActual?.Email == "admin@candyshoes.pe";

            if (!esAdmin && pedido.UsuarioId != int.Parse(usuarioId))
                return RedirectToAction("MisCompras");

            // Forzamos explícitamente a que busque la vista "Details" (de pedidos)
            return View("Details", pedido);
        }

        // GET: Tienda/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            var usuarioId = HttpContext.Session.GetString("UsuarioId");
            if (string.IsNullOrEmpty(usuarioId))
                return RedirectToAction("Login", "Account");

            var usuario = await _context.Usuarios.FindAsync(int.Parse(usuarioId));
            if (usuario?.Email != "admin@candyshoes.pe")
                return RedirectToAction("Login", "Account");

            if (id == null)
                return NotFound();

            var pedido = await _context.Pedidos
                .Include(p => p.Usuario)
                .Include(p => p.Detalles!)
                    .ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (pedido == null)
                return NotFound();

            return View(pedido);
        }

        // POST: Tienda/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Pedido pedido)
        {
            var usuarioId = HttpContext.Session.GetString("UsuarioId");
            if (string.IsNullOrEmpty(usuarioId))
                return RedirectToAction("Login", "Account");

            var usuario = await _context.Usuarios.FindAsync(int.Parse(usuarioId));
            if (usuario?.Email != "admin@candyshoes.pe")
                return RedirectToAction("Login", "Account");

            if (id != pedido.Id)
                return NotFound();

            try
            {
                var existente = await _context.Pedidos.FindAsync(id);
                if (existente == null)
                    return NotFound();

                existente.Estado = pedido.Estado;

                _context.Update(existente);
                await _context.SaveChangesAsync();

                TempData["Mensaje"] = $"✅ Estado del pedido #{id} actualizado a {existente.Estado}";
                return RedirectToAction(nameof(AdminPedidos));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!PedidoExists(pedido.Id))
                    return NotFound();
                throw;
            }
        }

        // GET: Tienda/Cancelar/5
        [HttpGet]
        public async Task<IActionResult> Cancelar(int? id)
        {
            var usuarioId = HttpContext.Session.GetString("UsuarioId");
            if (string.IsNullOrEmpty(usuarioId))
                return RedirectToAction("Login", "Account");

            if (id == null)
                return NotFound();

            var pedido = await _context.Pedidos
                .Include(p => p.Usuario)
                .Include(p => p.Detalles!)
                    .ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (pedido == null)
                return NotFound();

            var usuarioActual = await _context.Usuarios.FindAsync(int.Parse(usuarioId));
            bool esAdmin = usuarioActual?.Email == "admin@candyshoes.pe";

            if (!esAdmin && pedido.UsuarioId != int.Parse(usuarioId))
                return RedirectToAction("MisCompras");

            if (pedido.Estado == EstadoPedido.Entregado)
            {
                TempData["Error"] = "⚠️ No se puede cancelar un pedido que ya fue entregado";
                return RedirectToAction("MisCompras");
            }

            return View(pedido);
        }

        // POST: Tienda/Cancelar/5
        [HttpPost]
        [ActionName("Cancelar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelarConfirmed(int id)
        {
            var usuarioId = HttpContext.Session.GetString("UsuarioId");
            if (string.IsNullOrEmpty(usuarioId))
                return RedirectToAction("Login", "Account");

            var pedido = await _context.Pedidos
                .Include(p => p.Detalles!)
                .ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (pedido == null)
                return NotFound();

            var usuarioActual = await _context.Usuarios.FindAsync(int.Parse(usuarioId));
            bool esAdmin = usuarioActual?.Email == "admin@candyshoes.pe";

            if (!esAdmin && pedido.UsuarioId != int.Parse(usuarioId))
                return RedirectToAction("MisCompras");

            if (pedido.Estado == EstadoPedido.Entregado)
            {
                TempData["Error"] = "⚠️ No se puede cancelar un pedido que ya fue entregado";
                return RedirectToAction("MisCompras");
            }

            pedido.Estado = EstadoPedido.Cancelado;

            foreach (var detalle in pedido.Detalles!)
            {
                var producto = await _context.Productos.FindAsync(detalle.ProductoId);
                if (producto != null)
                {
                    producto.Cantidad += detalle.Cantidad;
                    _context.Update(producto);
                }
            }

            _context.Update(pedido);
            await _context.SaveChangesAsync();

            TempData["Mensaje"] = $"✅ Pedido #{id} cancelado correctamente";

            if (esAdmin)
                return RedirectToAction(nameof(AdminPedidos));
            else
                return RedirectToAction(nameof(MisCompras));
        }

        // GET: Tienda/PedidoDetails/5
        [HttpGet]
        public async Task<IActionResult> PedidoDetails(int? id)
        {
            var usuarioId = HttpContext.Session.GetString("UsuarioId");
            if (string.IsNullOrEmpty(usuarioId))
                return RedirectToAction("Login", "Account");

            if (id == null)
                return NotFound();

            var pedido = await _context.Pedidos
                .Include(p => p.Usuario)
                .Include(p => p.Detalles!)
                    .ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (pedido == null)
                return NotFound();

            var usuarioActual = await _context.Usuarios.FindAsync(int.Parse(usuarioId));
            bool esAdminUser = usuarioActual?.Email == "admin@candyshoes.pe";

            if (!esAdminUser && pedido.UsuarioId != int.Parse(usuarioId))
                return RedirectToAction("MisCompras");

            return View("PedidoDetails", pedido);
        }

        // ============================================================
        // 🔥 ACCIÓN REPARADA: DETALLES DE UN PRODUCTO
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> ProductoDetails(int? id)
        {
            if (!UsuarioLogueado())
                return RedirectToAction("Login", "Account");

            if (id == null)
                return NotFound();

            var producto = await _context.Productos.FirstOrDefaultAsync(p => p.Id == id);

            if (producto == null)
                return NotFound();

            ViewBag.CarritoCount = await _context.CarritoItems
                .Where(c => c.UsuarioId == GetUsuarioId())
                .SumAsync(c => c.Cantidad);

            // Forzamos explícitamente a buscar el archivo "ProductoDetails"
            return View("ProductoDetails", producto);
        }

        private bool PedidoExists(int id)
        {
            return _context.Pedidos.Any(e => e.Id == id);
        }
    }
}