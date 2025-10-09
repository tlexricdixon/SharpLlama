using Contracts;
using Entities;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;

namespace ChatService;

/// <summary>
/// Service responsible for ingesting employee data into a vector or memory store for RAG (Retrieval-Augmented Generation) scenarios.
/// Gathers employee profile information, aggregates simple order statistics, builds a textual document, and stores it with metadata.
/// </summary>
public class EmployeeRagIngestionService(NorthwindStarterContext db, IMemoryService memory, ILoggerManager logger)
{
    private readonly NorthwindStarterContext _db = db;
    private readonly IMemoryService _memory = memory;
    private readonly ILoggerManager _logger = logger;

    /// <summary>
    /// Ingests all employees from the data source into the memory store.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of successfully ingested employee documents.</returns>
    public async Task<int> IngestAllAsync(CancellationToken ct = default)
    {
        var overallSw = Stopwatch.StartNew();
        _logger.LogInfo("Starting employee ingestion...");

        List<Employee> employees;
        try
        {
            employees = await _db.Employees
                .Include(e => e.Orders)
                .AsNoTracking()
                .ToListAsync(ct);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Employee ingestion canceled during data load.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to load employees from database: {ex.Message}");
            return 0;
        }

        if (employees.Count == 0)
        {
            _logger.LogInfo("No employees found to ingest.");
            return 0;
        }

        _logger.LogDebug($"Loaded {employees.Count} employees from database.");

        int successCount = 0;
        int failureCount = 0;

        for (int i = 0; i < employees.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var e = employees[i];
            var perItemSw = Stopwatch.StartNew();
            var id = $"employee-{e.EmployeeId}";

            try
            {
                _logger.LogDebug($"[{i + 1}/{employees.Count}] Building document for EmployeeId={e.EmployeeId}");

                string content;
                try
                {
                    content = BuildEmployeeDocument(e);
                }
                catch (Exception buildEx)
                {
                    failureCount++;
                    _logger.LogError($"Error building document for EmployeeId={e.EmployeeId}: {buildEx.Message}");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(content))
                {
                    failureCount++;
                    _logger.LogWarning($"Skipping EmployeeId={e.EmployeeId} because generated content was empty.");
                    continue;
                }

                string hash;
                try
                {
                    hash = Sha256(content);
                }
                catch (Exception hashEx)
                {
                    failureCount++;
                    _logger.LogError($"Error hashing document for EmployeeId={e.EmployeeId}: {hashEx.Message}");
                    continue;
                }

                var metadata = new Dictionary<string, object?>
                {
                    ["type"] = "employee",
                    ["employeeId"] = e.EmployeeId,
                    ["firstName"] = e.FirstName ?? "",
                    ["lastName"] = e.LastName ?? "",
                    ["jobTitle"] = e.JobTitle ?? "",
                    ["orderCount"] = e.Orders?.Count ?? 0,
                    ["docHash"] = hash
                };

                await _memory.StoreDocumentAsync(id, content, metadata);
                successCount++;
                _logger.LogDebug($"Ingested EmployeeId={e.EmployeeId} in {perItemSw.ElapsedMilliseconds} ms");
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning($"Ingestion canceled while processing EmployeeId={e.EmployeeId}");
                throw;
            }
            catch (Exception ex)
            {
                failureCount++;
                _logger.LogError($"Failed to ingest EmployeeId={e.EmployeeId}: {ex.Message}");
            }
        }

        overallSw.Stop();
        _logger.LogInfo($"Employee ingestion complete. Succeeded: {successCount}, Failed: {failureCount}, Total: {employees.Count}, Duration: {overallSw.Elapsed}");

        return successCount;
    }

    /// <summary>
    /// Builds a multiline textual document representing the employee's profile and simple order statistics.
    /// </summary>
    private static string BuildEmployeeDocument(Employee e)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Employee Profile");
        sb.AppendLine($"ID: {e.EmployeeId}");
        sb.AppendLine($"Name: {(e.FirstName ?? "").Trim()} {(e.LastName ?? "").Trim()}".Trim());
        if (!string.IsNullOrWhiteSpace(e.JobTitle))
            sb.AppendLine($"Job Title: {e.JobTitle}");

        if (e.Orders != null && e.Orders.Count > 0)
        {
            var totalOrders = e.Orders.Count;
            var first = e.Orders.Where(o => o.OrderDate.HasValue).OrderBy(o => o.OrderDate).FirstOrDefault();
            var last = e.Orders.Where(o => o.OrderDate.HasValue).OrderByDescending(o => o.OrderDate).FirstOrDefault();
            sb.AppendLine($"Order Count: {totalOrders}");
            if (first?.OrderDate != null) sb.AppendLine($"First Order Date: {first.OrderDate:yyyy-MM-dd}");
            if (last?.OrderDate != null) sb.AppendLine($"Most Recent Order Date: {last.OrderDate:yyyy-MM-dd}");
        }
        if (!string.IsNullOrWhiteSpace(e.EmailAddress)) sb.AppendLine($"Email: {e.EmailAddress}");
        if (!string.IsNullOrWhiteSpace(e.PrimaryPhone)) sb.AppendLine($"Primary Phone: {e.PrimaryPhone}");
        if (!string.IsNullOrWhiteSpace(e.SecondaryPhone)) sb.AppendLine($"Secondary Phone: {e.SecondaryPhone}");
        if (!string.IsNullOrWhiteSpace(e.WindowsUserName)) sb.AppendLine($"Windows User: {e.WindowsUserName}");
        if (!string.IsNullOrWhiteSpace(e.Notes))
        {
            sb.AppendLine("Notes:");
            sb.AppendLine(e.Notes);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Computes a SHA-256 hexadecimal hash string for the provided text.
    /// </summary>
    private static string Sha256(string text)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(text)));
    }
}