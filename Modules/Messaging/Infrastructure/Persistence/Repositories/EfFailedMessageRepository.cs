using OrderCore.Api.Modules.Messaging.Application.Contracts;
using OrderCore.Api.Modules.Messaging.Domain.Entities;
using OrderCore.Api.Modules.Messaging.Infrastructure.Persistence.Mappers;

namespace OrderCore.Api.Modules.Messaging.Infrastructure.Persistence.Repositories;

public sealed class EfFailedMessageRepository : IFailedMessageRepository
{
    private readonly MessagingDbContext _dbContext;

    public EfFailedMessageRepository(MessagingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(FailedMessage failedMessage, CancellationToken cancellationToken) =>
        await _dbContext.FailedMessages.AddAsync(FailedMessageMapper.ToPersistence(failedMessage), cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => _dbContext.SaveChangesAsync(cancellationToken);
}
