using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderCore.Api.Modules.Messaging.Application.UseCases;
using OrderCore.Api.Modules.Messaging.Presentation.Presenters;
using OrderCore.Api.Modules.Messaging.Presentation.Requests;
using OrderCore.Api.Modules.Messaging.Presentation.Responses;
using OrderCore.Api.Shared.Presentation.Authentication;
using OrderCore.Api.Shared.Presentation.Responses;

namespace OrderCore.Api.Modules.Messaging.Presentation.Controllers;

/// <summary>
/// The messages consumers gave up on after every attempt
/// (Docs/specs/events/async-messaging.md, decision 5), for an admin to
/// inspect and then replay — once the cause is gone — or discard.
/// Admin-only, like the whole Messaging module.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("messaging/failed-messages")]
public sealed class FailedMessagesController : ControllerBase
{
    private readonly ListFailedMessagesUseCase _list;
    private readonly GetFailedMessageUseCase _get;
    private readonly ReplayFailedMessageUseCase _replay;
    private readonly DiscardFailedMessageUseCase _discard;

    public FailedMessagesController(
        ListFailedMessagesUseCase list, GetFailedMessageUseCase get, ReplayFailedMessageUseCase replay, DiscardFailedMessageUseCase discard)
    {
        _list = list;
        _get = get;
        _replay = replay;
        _discard = discard;
    }

    /// <summary>Most recent failure first; <c>status</c> narrows the list (e.g. <c>Pending</c>).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<FailedMessageSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<FailedMessageSummaryResponse>>> ListAsync(
        [FromQuery] ListFailedMessagesRequest request, CancellationToken cancellationToken) =>
        Ok(FailedMessagePresenter.ToResponse(await _list.ExecuteAsync(FailedMessagePresenter.ToInput(request), cancellationToken)));

    /// <summary>One failed message, with the envelope as it was received.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(FailedMessageDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FailedMessageDetailsResponse>> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Ok(FailedMessagePresenter.ToResponse(await _get.ExecuteAsync(id, cancellationToken)));

    /// <summary>
    /// Sends a pending message back to its consumer for a fresh round of
    /// attempts; it becomes <c>Replayed</c>. If it fails again, it comes back
    /// to this list as a new entry.
    /// </summary>
    [HttpPost("{id:guid}/replay")]
    [ProducesResponseType(typeof(FailedMessageDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FailedMessageDetailsResponse>> ReplayAsync(Guid id, CancellationToken cancellationToken) =>
        Ok(FailedMessagePresenter.ToResponse(await _replay.ExecuteAsync(id, cancellationToken)));

    /// <summary>Gives up on a pending message for good; it becomes <c>Discarded</c> and is never sent again.</summary>
    [HttpPost("{id:guid}/discard")]
    [ProducesResponseType(typeof(FailedMessageDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FailedMessageDetailsResponse>> DiscardAsync(Guid id, CancellationToken cancellationToken) =>
        Ok(FailedMessagePresenter.ToResponse(await _discard.ExecuteAsync(id, cancellationToken)));
}
