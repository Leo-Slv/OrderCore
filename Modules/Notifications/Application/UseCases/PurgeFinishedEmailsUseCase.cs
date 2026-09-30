using OrderCore.Api.Modules.Notifications.Domain.Repositories;

namespace OrderCore.Api.Modules.Notifications.Application.UseCases;

/// <summary>
/// Removes the records of e-mails sent or given up on longer ago than the
/// retention (90 days by default). Their content is already erased; what is
/// left only answers "was it sent?" for a while. Deletes in batches.
/// </summary>
public sealed class PurgeFinishedEmailsUseCase
{
    private readonly IEmailMessageRepository _repository;
    private readonly TimeProvider _timeProvider;

    public PurgeFinishedEmailsUseCase(IEmailMessageRepository repository, TimeProvider timeProvider)
    {
        _repository = repository;
        _timeProvider = timeProvider;
    }

    /// <returns>How many records were removed.</returns>
    public async Task<int> ExecuteAsync(TimeSpan retention, int batchSize, CancellationToken cancellationToken)
    {
        var cutoff = _timeProvider.GetUtcNow() - retention;
        var total = 0;
        int deleted;
        do
        {
            deleted = await _repository.DeleteFinishedBeforeAsync(cutoff, batchSize, cancellationToken);
            total += deleted;
        }
        while (deleted == batchSize);

        return total;
    }
}
