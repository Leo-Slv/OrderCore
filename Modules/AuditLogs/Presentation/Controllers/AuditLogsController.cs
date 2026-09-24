using Microsoft.AspNetCore.Mvc;
using OrderCore.Api.Modules.AuditLogs.Application.UseCases;
using OrderCore.Api.Modules.AuditLogs.Presentation.Presenters;
using OrderCore.Api.Modules.AuditLogs.Presentation.Requests;
using OrderCore.Api.Modules.AuditLogs.Presentation.Responses;
using OrderCore.Api.Shared.Presentation.Responses;

namespace OrderCore.Api.Modules.AuditLogs.Presentation.Controllers;

/// <summary>
/// Thin endpoint delegating to <see cref="ListAuditLogsUseCase"/> (section
/// 39). This is currently unauthenticated: OrderCore has no auth module
/// yet (section 32). Once one exists, this endpoint must be restricted to
/// an administrative/audit-read policy before it is exposed anywhere but
/// local development — an audit trail readable by anyone is not one worth
/// keeping.
/// </summary>
[ApiController]
[Route("audit-logs")]
public sealed class AuditLogsController : ControllerBase
{
    private readonly ListAuditLogsUseCase _listAuditLogsUseCase;

    public AuditLogsController(ListAuditLogsUseCase listAuditLogsUseCase)
    {
        _listAuditLogsUseCase = listAuditLogsUseCase;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<AuditLogResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<AuditLogResponse>>> ListAsync(
        [FromQuery] ListAuditLogsRequest request,
        CancellationToken cancellationToken)
    {
        var output = await _listAuditLogsUseCase.ExecuteAsync(AuditLogPresenter.ToInput(request), cancellationToken);

        return Ok(AuditLogPresenter.ToResponse(output));
    }
}
