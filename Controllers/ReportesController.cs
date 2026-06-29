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

            // 🔥 CONFIGURAR LICENCIA DE EPPlus (para toda la aplicación)
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            // 🔥 CONFIGURAR LICENCIA DE QuestPDF
            QuestPDF.Settings.License = LicenseType.Community;
        }

        // ... (resto de los métodos iguales) ...
    }
}