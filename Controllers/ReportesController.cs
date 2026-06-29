using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LOGIN.Data;
using LOGIN.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace LOGIN.Controllers
{
    public class ReportesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ReportesController> _logger;

        public ReportesController(ApplicationDbContext context, ILogger<ReportesController> logger)
        {
            _context = context;
            _logger = logger;
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
        // 📋 GET: Reportes/Index
        // ============================================================
        [HttpGet]
        public IActionResult Index()
        {
            if (!UsuarioLogueado())
                return RedirectToAction("Login", "Account");

            if (!EsAdmin())
                return RedirectToAction("Index", "Home");

            return View();
        }

        // ============================================================
        // 📄 REPORTE DE VENTAS - PDF (SINTAXIS CORREGIDA)
        // ============================================================
        // ============================================================
        // 📄 REPORTE DE VENTAS - PDF (SINTAXIS CORREGIDA + SOPORTE POSTGRES UTC)
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> VentasPdf(DateTime? fechaInicio, DateTime? fechaFin)
        {
            try
            {
                if (!EsAdmin())
                    return RedirectToAction("Index", "Home");

                QuestPDF.Settings.License = LicenseType.Community;

                var query = _context.Pedidos
                    .Include(p => p.Usuario)
                    .Include(p => p.Detalles!)
                        .ThenInclude(d => d.Producto)
                    .AsQueryable();

                // ✅ SOLUCIÓN AL ERROR DE POSTGRESQL: Forzar las fechas de los filtros a UTC
                if (fechaInicio.HasValue)
                {
                    var inicioUtc = DateTime.SpecifyKind(fechaInicio.Value, DateTimeKind.Utc);
                    query = query.Where(p => p.FechaPedido >= inicioUtc);
                }

                if (fechaFin.HasValue)
                {
                    // Forzamos el fin de día en formato UTC
                    var finUtc = DateTime.SpecifyKind(fechaFin.Value, DateTimeKind.Utc);
                    query = query.Where(p => p.FechaPedido <= finUtc);
                }

                var pedidos = await query.OrderByDescending(p => p.FechaPedido).ToListAsync();
                decimal granTotal = pedidos.Sum(p => p.Total);

                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(1.5f, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontFamily("Helvetica").FontSize(9));

                        // ENCABEZADO
                        page.Header().Column(column =>
                        {
                            column.Item().Row(row =>
                            {
                                row.RelativeItem().Column(c =>
                                {
                                    c.Item().Text("CANDY SHOES").Bold().FontSize(20).FontColor("#D63384");
                                    c.Item().Text("Reporte Ejecutivo de Ventas").FontSize(11).Italic().FontColor(Colors.Grey.Medium);
                                });

                                row.ConstantItem(150).Column(c =>
                                {
                                    c.Item().AlignRight().Text($"Generado: {DateTime.Now:dd/MM/yyyy}").FontSize(9);
                                    if (fechaInicio.HasValue || fechaFin.HasValue)
                                    {
                                        string desde = fechaInicio.HasValue ? fechaInicio.Value.ToString("dd/MM/yyyy") : "Inicio";
                                        string hasta = fechaFin.HasValue ? fechaFin.Value.ToString("dd/MM/yyyy") : "Hoy";
                                        c.Item().AlignRight().Text($"Filtro: {desde} - {hasta}").FontSize(8).FontColor(Colors.Grey.Darken1);
                                    }
                                });
                            });

                            column.Item().PaddingTop(5).PaddingBottom(15).LineHorizontal(1).LineColor("#D63384");
                        });

                        // CONTENIDO
                        page.Content().PaddingTop(10).Column(column =>
                        {
                            column.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(40);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                });

                                // Cabecera de la Tabla
                                table.Header(header =>
                                {
                                    header.Cell().Background("#D63384").Padding(6).Text("ID").Bold().FontColor(Colors.White);
                                    header.Cell().Background("#D63384").Padding(6).Text("Fecha").Bold().FontColor(Colors.White);
                                    header.Cell().Background("#D63384").Padding(6).Text("Cliente").Bold().FontColor(Colors.White);
                                    header.Cell().Background("#D63384").Padding(6).Text("Estado").Bold().FontColor(Colors.White);
                                    header.Cell().Background("#D63384").Padding(6).AlignRight().Text("Total").Bold().FontColor(Colors.White);
                                });

                                // Filas de la Tabla
                                bool alternarFila = false;
                                foreach (var pedido in pedidos)
                                {
                                    var fondoFila = alternarFila ? Colors.Grey.Lighten4 : Colors.White;

                                    table.Cell().Background(fondoFila).Padding(6).Text(pedido.Id.ToString());
                                    table.Cell().Background(fondoFila).Padding(6).Text(pedido.FechaPedido.ToLocalTime().ToString("dd/MM/yyyy HH:mm")); // Convertimos a local al mostrar en pantalla
                                    table.Cell().Background(fondoFila).Padding(6).Text(pedido.Usuario?.Nombre ?? "N/A");
                                    table.Cell().Background(fondoFila).Padding(6).Text(pedido.Estado.ToString());
                                    table.Cell().Background(fondoFila).Padding(6).AlignRight().Text($"Bs. {pedido.Total:N2}");

                                    alternarFila = !alternarFila;
                                }
                            });

                            // Cuadro de Resumen totalizador al final
                            column.Item().AlignRight().PaddingTop(15).Width(150).Background(Colors.Grey.Lighten4).Padding(8).Row(r =>
                            {
                                r.RelativeItem().Text("GRAN TOTAL:").Bold().FontSize(10);
                                r.RelativeItem().AlignRight().Text($"Bs. {granTotal:N2}").Bold().FontSize(10).FontColor("#D63384");
                            });
                        });

                        // PIE DE PÁGINA
                        page.Footer().Column(c =>
                        {
                            c.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                            c.Item().PaddingTop(5).Row(row =>
                            {
                                row.RelativeItem().Text("Candy Shoes - Sistema de Gestión Interna").FontColor(Colors.Grey.Medium).FontSize(8);
                                row.RelativeItem().AlignRight().Text(t => {
                                    t.Span("Pág. ");
                                    t.CurrentPageNumber();
                                });
                            });
                        });
                    });
                });

                var stream = new MemoryStream();
                document.GeneratePdf(stream);
                stream.Position = 0;

                return File(stream, "application/pdf", $"Ventas_{DateTime.Now:yyyyMMdd}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar reporte de ventas en PDF");
                TempData["Error"] = $"Error: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // ============================================================
        // 📄 REPORTE DE PRODUCTOS - PDF (SINTAXIS CORREGIDA)
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> ProductosPdf()
        {
            try
            {
                if (!EsAdmin())
                    return RedirectToAction("Index", "Home");

                QuestPDF.Settings.License = LicenseType.Community;

                var productos = await _context.Productos.OrderBy(p => p.Nombre).ToListAsync();
                int totalItems = productos.Sum(p => p.Cantidad);

                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(1.5f, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontFamily("Helvetica").FontSize(9)); // ✅ Corregido

                        // ENCABEZADO
                        page.Header().Column(column =>
                        {
                            column.Item().Row(row =>
                            {
                                row.RelativeItem().Column(c =>
                                {
                                    c.Item().Text("CANDY SHOES").Bold().FontSize(20).FontColor("#0D6EFD");
                                    c.Item().Text("Reporte de Inventario y Almacén").FontSize(11).Italic().FontColor(Colors.Grey.Medium);
                                });

                                row.ConstantItem(120).AlignRight().Text($"Fecha: {DateTime.Now:dd/MM/yyyy}").FontSize(9);
                            });

                            column.Item().PaddingTop(5).PaddingBottom(15).LineHorizontal(1).LineColor("#0D6EFD");
                        });

                        // CONTENIDO
                        page.Content().PaddingTop(10).Column(column =>
                        {
                            column.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(40);
                                    columns.RelativeColumn(4);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                });

                                // Cabecera
                                table.Header(header =>
                                {
                                    header.Cell().Background("#0D6EFD").Padding(6).Text("ID").Bold().FontColor(Colors.White);
                                    header.Cell().Background("#0D6EFD").Padding(6).Text("Descripción Producto").Bold().FontColor(Colors.White);
                                    header.Cell().Background("#0D6EFD").Padding(6).AlignCenter().Text("Stock").Bold().FontColor(Colors.White);
                                    header.Cell().Background("#0D6EFD").Padding(6).AlignRight().Text("Precio").Bold().FontColor(Colors.White);
                                });

                                // Filas
                                bool alternarFila = false;
                                foreach (var producto in productos)
                                {
                                    // ✅ Corregido: Usando solo Colors para evitar error CS0172
                                    var fondoFila = alternarFila ? Colors.Grey.Lighten4 : Colors.White;

                                    table.Cell().Background(fondoFila).Padding(6).Text(producto.Id.ToString());
                                    table.Cell().Background(fondoFila).Padding(6).Text(producto.Nombre);

                                    var stockCell = table.Cell().Background(fondoFila).Padding(6).AlignCenter();
                                    if (producto.Cantidad == 0)
                                        stockCell.Text("Agotado").Bold().FontColor(Colors.Red.Medium);
                                    else
                                        stockCell.Text(producto.Cantidad.ToString());

                                    table.Cell().Background(fondoFila).Padding(6).AlignRight().Text($"Bs. {producto.Precio:N2}");

                                    alternarFila = !alternarFila;
                                }
                            });

                            // Resumen de Stock Total
                            column.Item().AlignRight().PaddingTop(15).Width(160).Background(Colors.Grey.Lighten4).Padding(8).Row(r =>
                            {
                                r.RelativeItem().Text("Total Unidades:").Bold().FontSize(10);
                                r.RelativeItem().AlignRight().Text($"{totalItems} uds.").Bold().FontSize(10).FontColor("#0D6EFD");
                            });
                        });

                        // PIE DE PÁGINA
                        page.Footer().Column(c =>
                        {
                            c.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                            c.Item().PaddingTop(5).Row(row =>
                            {
                                row.RelativeItem().Text("Candy Shoes - Control de Inventarios").FontColor(Colors.Grey.Medium).FontSize(8);
                                // ✅ Corregido: Sintaxis clásica para números de página en QuestPDF
                                row.RelativeItem().AlignRight().Text(t => {
                                    t.Span("Pág. ");
                                    t.CurrentPageNumber();
                                });
                            });
                        });
                    });
                });

                var stream = new MemoryStream();
                document.GeneratePdf(stream);
                stream.Position = 0;

                return File(stream, "application/pdf", $"Inventario_{DateTime.Now:yyyyMMdd}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar reporte de productos en PDF");
                TempData["Error"] = $"Error: {ex.Message}";
                return RedirectToAction("Index");
            }
        }
    }
}