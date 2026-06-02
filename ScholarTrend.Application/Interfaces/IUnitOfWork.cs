namespace ScholarTrend.Application.Interfaces;

/// <summary>
/// Unit of Work pattern to coordinate multiple repository operations in a single transaction.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    Task<int> SaveChangesAsync();
}
