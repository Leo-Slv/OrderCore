using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderCore.Api.Modules.AuditLogs.Application.UseCases;
using OrderCore.Api.Modules.AuditLogs.Presentation.Presenters;
using OrderCore.Api.Modules.AuditLogs.Presentation.Requests;
using OrderCore.Api.Modules.AuditLogs.Presentation.Responses;
using OrderCore.Api.Shared.Presentation.Authentication;
using OrderCore.Api.Shared.Presentation.Responses;

namespace OrderCore.Api.Modules.AuditLogs.Presentation.Controllers;

/// <summary>
/// Thin endpoint delegating to <see cref="ListAuditLogsUseCase"/> (section
/// 39). Filtering by entity name + id gives one entity's timeline (the
/// order detail screen), by user id everything one actor did. Admin-only: an audit trail readable by anyone is not one worth
/// keeping.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
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
