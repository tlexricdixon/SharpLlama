
namespace SharpLlama.Contracts
{
    public interface IEmployeeRagIngestionService
    {
        Task<int> IngestAllAsync(CancellationToken ct = default);
    }
}