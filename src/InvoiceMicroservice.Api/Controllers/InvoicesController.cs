using FluentValidation;
using InvoiceMicroservice.Application.Commands.EmitInvoice;
using InvoiceMicroservice.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace InvoiceMicroservice.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InvoicesController : ControllerBase
{
    private readonly IValidator<EmitInvoiceCommand> _validator;
    private readonly EmitInvoiceCommandHandler _handler;
    private readonly IInvoiceRepository _repository;

    public InvoicesController(
        IValidator<EmitInvoiceCommand> validator,
        EmitInvoiceCommandHandler handler,
        IInvoiceRepository repository)
    {
        _validator = validator;
        _handler = handler;
        _repository = repository;
    }

    /// <summary>
    /// Creates a new invoice for emission
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateInvoice(
        [FromBody] EmitInvoiceCommand command,
        CancellationToken ct)
    {
        var validationResult = await _validator.ValidateAsync(command, ct);
        if (!validationResult.IsValid)
        {
            var errors = new Dictionary<string, string[]>();
            foreach (var error in validationResult.Errors)
            {
                if (!errors.ContainsKey(error.PropertyName))
                    errors[error.PropertyName] = new[] { error.ErrorMessage };
                else
                    errors[error.PropertyName] = errors[error.PropertyName].Append(error.ErrorMessage).ToArray();
            }
            return ValidationProblem(new ValidationProblemDetails(errors));
        }

        // Delegate to the handler - it handles persistence, XML generation, and IPM submission
        var invoiceId = await _handler.HandleAsync(command, ct);

        var location = $"/api/invoices/{invoiceId}";
        return Accepted(location, new { Id = invoiceId, Status = "Pending" });
    }

    /// <summary>
    /// Gets the status and details of an invoice by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInvoiceStatus(
        Guid id,
        CancellationToken ct)
    {
        var invoice = await _repository.GetByIdAsync(id, ct);
        if (invoice is null)
        {
            return NotFound();
        }

        return Ok(new
        {
            invoice.Id,
            invoice.Status,
            invoice.ExternalInvoiceId,
            // invoice.ExternalProtocol,
            // invoice.VerificationCode,
            invoice.XmlPayload,
            // invoice.ResponsePayload,
            // invoice.ErrorMessage,
            invoice.RetryCount,
            invoice.CreatedAt,
            invoice.IssuedAt,
            invoice.UpdatedAt
        });
    }
}
