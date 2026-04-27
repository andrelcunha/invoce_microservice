using System.Security.Claims;
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
    private readonly IInvoiceEmissionJobRepository _jobs;
    private readonly IInvoiceEmissionResultRepository _results;

    public InvoicesController(
        IValidator<EmitInvoiceCommand> validator,
        EmitInvoiceCommandHandler handler,
        IInvoiceEmissionJobRepository jobs,
        IInvoiceEmissionResultRepository results)
    {
        _validator = validator;
        _handler = handler;
        _jobs = jobs;
        _results = results;
    }

    /// <summary>
    /// Enqueues an invoice emission job.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateInvoice(
        [FromBody] EmitInvoiceCommand command,
        CancellationToken ct)
    {
        var authenticatedClientId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.Equals(command.ClientId, authenticatedClientId, StringComparison.Ordinal))
            return Forbid();

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

        var jobId = await _handler.HandleAsync(command, ct);

        var location = $"/api/invoices/{jobId}";
        return Accepted(location, new { JobId = jobId, JobStatus = "Pending" });
    }

    /// <summary>
    /// Gets job/result status and provider details by job ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInvoiceStatus(
        Guid id,
        CancellationToken ct)
    {
        var job = await _jobs.GetByIdAsync(id, ct);
        if (job is null)
        {
            return NotFound();
        }

        var result = await _results.GetByJobIdAsync(id, ct);

        return Ok(new
        {
            JobId = job.Id,
            JobStatus = job.Status.ToString(),
            job.Attempts,
            job.MaxAttempts,
            job.LastError,
            job.CreatedAt,
            job.UpdatedAt,
            Result = result is null
                ? null
                : new
                {
                    result.CodStatus,
                    result.StatusDescription,
                    result.NumeroDfe,
                    result.SerieDfe,
                    result.Protocolo,
                    result.ChaveAcesso,
                    result.VerificationCode,
                    result.DocumentUrl,
                    result.IssuedAt,
                    result.ProviderProcessedAt,
                    result.ErrorMessage,
                    result.CreatedAt,
                    result.UpdatedAt
                }
        });
    }
}
