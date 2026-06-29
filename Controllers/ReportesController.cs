using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LOGIN.Data;
using LOGIN.Models;
using OfficeOpenXml;
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
        // 📄 REPORTE DE VENTAS - EXCEL
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> VentasExcel(DateTime? fechaInicio, DateTime? fechaFin)
        {
            try
            {
                if (!EsAdmin())
                    return RedirectToAction("Index", "Home");

                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                var query = _context.Pedidos
                    .Include(p => p.Usuario)
                    .Include(p => p.Detalles!)
                        .ThenInclude(d => d.Producto)
                    .AsQueryable();

                if (fechaInicio.HasValue)
                    query = query.Where(p => p.FechaPedido >= fechaInicio.Value);
                if (fechaFin.HasValue)
                    query = query.Where(p => p.FechaPedido <= fechaFin.Value);

                var pedidos = await query.OrderByDescending(p => p.FechaPedido).ToListAsync();

                using var package = new ExcelPackage();
                var worksheet = package.Workbook.Worksheets.Add("Ventas");

                worksheet.Cells[1, 1].Value = "ID Pedido";
                worksheet.Cells[1, 2].Value = "Fecha";
                worksheet.Cells[1, 3].Value = "Cliente";
                worksheet.Cells[1, 4].Value = "Total";
                worksheet.Cells[1, 5].Value = "Estado";
                worksheet.Cells[1, 6].Value = "Método Pago";
                worksheet.Cells[1, 7].Value = "Productos";

                using var headerRange = worksheet.Cells[1, 1, 1, 7];
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                headerRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);

                int row = 2;
                foreach (var pedido in pedidos)
                {
                    worksheet.Cells[row, 1].Value = pedido.Id;
                    worksheet.Cells[row, 2].Value = pedido.FechaPedido.ToString("dd/MM/yyyy HH:mm");
                    worksheet.Cells[row, 3].Value = pedido.Usuario?.Nombre ?? "N/A";
                    worksheet.Cells[row, 4].Value = pedido.Total;
                    worksheet.Cells[row, 4].Style.Numberformat.Format = "#,##0.00";
                    worksheet.Cells[row, 5].Value = pedido.Estado.ToString();
                    worksheet.Cells[row, 6].Value = pedido.MetodoPago ?? "N/A";

                    var productos = string.Join(", ", pedido.Detalles?.Select(d => $"{d.Producto?.Nombre} (x{d.Cantidad})") ?? new List<string>());
                    worksheet.Cells[row, 7].Value = productos;
                    row++;
                }

                worksheet.Cells.AutoFitColumns();

                var stream = new MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0;

                var fileName = $"Ventas_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
                return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar reporte de ventas en Excel");
                TempData["Error"] = $"Error: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // ============================================================
        // 📄 REPORTE DE VENTAS - PDF
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

                if (fechaInicio.HasValue)
                    query = query.Where(p => p.FechaPedido >= fechaInicio.Value);
                if (fechaFin.HasValue)
                    query = query.Where(p => p.FechaPedido <= fechaFin.Value);

                var pedidos = await query.OrderByDescending(p => p.FechaPedido).ToListAsync();

                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(2, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(10));

                        page.Header()
                            .Text("📊 REPORTE DE VENTAS - CANDY SHOES")
                            .SemiBold().FontSize(18).FontColor(Colors.Red.Medium);

                        page.Content()
                            .Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(2);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Text("ID").Bold();
                                    header.Cell().Text("Fecha").Bold();
                                    header.Cell().Text("Cliente").Bold();
                                    header.Cell().Text("Total").Bold();
                                    header.Cell().Text("Estado").Bold();
                                });

                                foreach (var pedido in pedidos)
                                {
                                    table.Cell().Text(pedido.Id.ToString());
                                    table.Cell().Text(pedido.FechaPedido.ToString("dd/MM/yyyy"));
                                    table.Cell().Text(pedido.Usuario?.Nombre ?? "N/A");
                                    table.Cell().Text($"S/ {pedido.Total:F2}");
                                    table.Cell().Text(pedido.Estado.ToString());
                                }
                            });

                        page.Footer()
                            .AlignCenter()
                            .Text($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm} - Candy Shoes");
                    });
                });

                var stream = new MemoryStream();
                document.GeneratePdf(stream);
                stream.Position = 0;

                var fileName = $"Ventas_{DateTime.Now:yyyyMMddHHmmss}.pdf";
                return File(stream, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar reporte de ventas en PDF");
                TempData["Error"] = $"Error: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // ============================================================
        // 📄 REPORTE DE PRODUCTOS - EXCEL
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> ProductosExcel()
        {
            try
            {
                if (!EsAdmin())
                    return RedirectToAction("Index", "Home");

                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                var productos = await _context.Productos.OrderBy(p => p.Nombre).ToListAsync();

                using var package = new ExcelPackage();
                var worksheet = package.Workbook.Worksheets.Add("Productos");

                worksheet.Cells[1, 1].Value = "ID";
                worksheet.Cells[1, 2].Value = "Nombre";
                worksheet.Cells[1, 3].Value = "Descripción";
                worksheet.Cells[1, 4].Value = "Cantidad";
                worksheet.Cells[1, 5].Value = "Precio";
                worksheet.Cells[1, 6].Value = "Fecha Registro";

                using var headerRange = worksheet.Cells[1, 1, 1, 6];
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                headerRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);

                int row = 2;
                foreach (var producto in productos)
                {
                    worksheet.Cells[row, 1].Value = producto.Id;
                    worksheet.Cells[row, 2].Value = producto.Nombre;
                    worksheet.Cells[row, 3].Value = producto.Descripcion ?? "";
                    worksheet.Cells[row, 4].Value = producto.Cantidad;
                    worksheet.Cells[row, 5].Value = producto.Precio;
                    worksheet.Cells[row, 5].Style.Numberformat.Format = "#,##0.00";
                    worksheet.Cells[row, 6].Value = producto.FechaRegistro.ToString("dd/MM/yyyy");
                    row++;
                }

                worksheet.Cells.AutoFitColumns();

                var stream = new MemoryStream();
                package.SaveAs(stream);
                stream.Position = 0;

                var fileName = $"Productos_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
                return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar reporte de productos en Excel");
                TempData["Error"] = $"Error: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // ============================================================
        // 📄 REPORTE DE PRODUCTOS - PDF
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

                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(2, Unit.Centimetre);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(10));

                        page.Header()
                            .Text("📦 REPORTE DE PRODUCTOS - CANDY SHOES")
                            .SemiBold().FontSize(18).FontColor(Colors.Blue.Medium);

                        page.Content()
                            .Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(1);
                                    columns.RelativeColumn(2);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Text("ID").Bold();
                                    header.Cell().Text("Nombre").Bold();
                                    header.Cell().Text("Stock").Bold();
                                    header.Cell().Text("Precio").Bold();
                                });

                                foreach (var producto in productos)
                                {
                                    table.Cell().Text(producto.Id.ToString());
                                    table.Cell().Text(producto.Nombre);
                                    table.Cell().Text(producto.Cantidad.ToString());
                                    table.Cell().Text($"S/ {producto.Precio:F2}");
                                }
                            });

                        page.Footer()
                            .AlignCenter()
                            .Text($"Generado el {DateTime.Now:dd/MM/yyyy HH:mm} - Candy Shoes");
                    });
                });

                var stream = new MemoryStream();
                document.GeneratePdf(stream);
                stream.Position = 0;

                var fileName = $"Productos_{DateTime.Now:yyyyMMddHHmmss}.pdf";
                return File(stream, "application/pdf", fileName);
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