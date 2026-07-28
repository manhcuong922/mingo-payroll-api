using Microsoft.AspNetCore.Mvc;
using MPS.API.Authorization;
using MPS.Application.Common.Payroll;
using MPS.Application.Common.Payroll.Formula;
using MPS.Domain.Constants;

namespace MPS.API.Controllers;

/// <summary>Danh mục field + hàm dựng sẵn cho trình dựng công thức (guided builder).</summary>
[ApiController]
[Route("api/payroll/catalog")]
public class PayrollCatalogController : ControllerBase
{
    private readonly IPayrollFieldCatalog _catalog;
    private readonly IFormulaFunctionRegistry _functions;

    public PayrollCatalogController(IPayrollFieldCatalog catalog, IFormulaFunctionRegistry functions)
    {
        _catalog = catalog;
        _functions = functions;
    }

    [HttpGet("fields")]
    [HasPermission(Permissions.PayrollFormulaSetReadAll)]
    public IActionResult GetFields() => Ok(_catalog.GetAll().Select(f => new
    {
        f.Key,
        f.Label,
        f.SourceModule,
        DataType = f.DataType.ToString(),
    }));

    [HttpGet("functions")]
    [HasPermission(Permissions.PayrollFormulaSetReadAll)]
    public IActionResult GetFunctions() => Ok(_functions.Names.OrderBy(n => n));
}
