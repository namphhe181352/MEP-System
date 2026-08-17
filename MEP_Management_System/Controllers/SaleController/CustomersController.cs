using System;
using System.Security.Claims;
using System.Threading.Tasks;
using MEP_Management_System.Authorization;
using MEP_Management_System.DTOs.SaleDTOs;
using MEP_Management_System.Services.SaleServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MEP_Management_System.Controllers.SaleController;

[ApiController]
[Route("api/pos/customers")]
[Authorize]
[Tags("Quản lý bán hàng (Sale)")]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _customerService;

    public CustomersController(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    private Guid GetBranchId() =>
        Guid.Parse(User.FindFirst("branchId")?.Value ?? throw new Exception("branchId not found in token"));

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value ?? throw new Exception("userId not found"));

    // GET /api/pos/customers
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<PageResultDto<CustomerDto>>> GetCustomers(
        [FromQuery] string? search, [FromQuery] string? group,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var branchId = GetBranchId();
        var (items, total) = await _customerService.GetCustomersAsync(branchId, search, group, page, pageSize);

        Response.Headers["X-Total-Count"] = total.ToString();

        return Ok(new PageResultDto<CustomerDto>
        {
            Items = items,
            PageNumber = page,
            PageSize = pageSize,
            TotalCount = total
        });
    }

    // GET /api/pos/customers/all?search=
    [HttpGet("all")]
    [Authorize]
    public async Task<ActionResult<List<CustomerDto>>> GetAllCustomers([FromQuery] string? search)
    {
        var branchId = GetBranchId();
        var customers = await _customerService.GetAllCustomersAsync(branchId, search);
        return Ok(customers);
    }

    // POST /api/pos/customers
    [HttpPost]
    [HasPermission(PermissionConstants.CUSTOMER_CREATE)]
    public async Task<ActionResult<CustomerDto>> CreateCustomer([FromBody] CreateCustomerRequest request)
    {
        var (customer, errorCode, errorMessage) = await _customerService.CreateCustomerAsync(GetBranchId(), GetUserId(), request);

        if (customer == null)
        {
            if (errorCode == "MSG-68")
                return Conflict(ApiErrorResponse.Create(errorCode, errorMessage!));
            return BadRequest(ApiErrorResponse.Create(errorCode!, errorMessage!));
        }

        return CreatedAtAction(nameof(GetCustomer), new { id = customer.CustomerId }, customer);
    }

    // PUT /api/pos/customers/{id}
    [HttpPut("{id:guid}")]
    [HasPermission(PermissionConstants.CUSTOMER_UPDATE)]
    public async Task<ActionResult<CustomerDto>> UpdateCustomer(Guid id, [FromBody] UpdateCustomerRequest request)
    {
        var (customer, errorCode, errorMessage) = await _customerService.UpdateCustomerAsync(id, GetBranchId(), request);

        if (customer == null)
        {
            if (errorCode == "MSG-01")
                return NotFound(ApiErrorResponse.Create(errorCode, errorMessage!));
            if (errorCode == "MSG-68")
                return Conflict(ApiErrorResponse.Create(errorCode, errorMessage!));
            return BadRequest(ApiErrorResponse.Create(errorCode!, errorMessage!));
        }

        return Ok(customer);
    }

    // GET /api/pos/customers/{id}
    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<ActionResult<CustomerDto>> GetCustomer(Guid id)
    {
        var customer = await _customerService.GetCustomerAsync(id, GetBranchId());
        if (customer == null)
            return NotFound(ApiErrorResponse.Create("MSG-01", "Customer not found"));
        return Ok(customer);
    }

    // DELETE /api/pos/customers/{id}
    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionConstants.CUSTOMER_DELETE)]
    public async Task<IActionResult> DeleteCustomer(Guid id)
    {
        var (success, errorCode, errorMessage, softDelete) = await _customerService.DeleteCustomerAsync(id, GetBranchId());

        if (!success)
        {
            if (errorCode == "MSG-01")
                return NotFound(ApiErrorResponse.Create(errorCode, errorMessage!));
            return BadRequest(ApiErrorResponse.Create(errorCode!, errorMessage!));
        }

        return Ok(new { success = true, message = softDelete ? "Customer blocked" : "Customer deleted", softDelete });
    }

    // GET /api/pos/customers/{id}/orders
    [HttpGet("{id:guid}/orders")]
    [Authorize]
    public async Task<ActionResult<PageResultDto<CustomerOrderDto>>> GetCustomerOrders(
        Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var (items, total) = await _customerService.GetCustomerOrdersAsync(id, GetBranchId(), page, pageSize);

        return Ok(new PageResultDto<CustomerOrderDto>
        {
            Items = items,
            PageNumber = page,
            PageSize = pageSize,
            TotalCount = total
        });
    }
}
