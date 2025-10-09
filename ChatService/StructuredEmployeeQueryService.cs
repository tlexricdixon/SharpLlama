using System.Text;
using Entities;
using Microsoft.EntityFrameworkCore;

namespace ChatService;

/// <summary>
/// Provides lightweight, pattern-based structured (EF Core) query handling for
/// simple, frequently asked employee-related natural language questions (e.g., counts, lists, by id).
/// Returns an empty string when no deterministic pattern matches so that callers can
/// gracefully fall back to a semantic / RAG pipeline.
/// </summary>
/// <remarks>
/// Design goals:
/// 1. Fast: avoids unnecessary model invocation when a direct query suffices.
/// 2. Safe: only emits raw data (no instructions or meta text) so the caller can wrap it safely.
/// 3. Conservative: if parsing confidence is low, returns empty string instead of guessing.
/// Parsing is intentionally naive and keyword-based; it is not meant to be exhaustive.
/// </remarks>
public class StructuredEmployeeQueryService
{
    /// <summary>
    /// Backing database context used for executing structured queries.
    /// </summary>
    private readonly NorthwindStarterContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="StructuredEmployeeQueryService"/> class.
    /// </summary>
    /// <param name="db">EF Core database context (Northwind schema).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="db"/> is <c>null</c>.</exception>
    public StructuredEmployeeQueryService(NorthwindStarterContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    /// <summary>
    /// Attempts to answer a natural language employee question using deterministic structured logic.
    /// </summary>
    /// <param name="question">The user-provided natural language query.</param>
    /// <param name="cancellationToken">Token to observe while awaiting async database operations.</param>
    /// <returns>
    /// A data-only string if a known pattern (count, list, or employee by id) is matched; otherwise an empty string.
    /// Returned text intentionally excludes explanatory prose to allow safe downstream prompting.
    /// </returns>
    /// <remarks>
    /// Supported patterns (case-insensitive, keyword driven):
    /// 1. Count employees: contains "how many" or "count" and "employee".
    /// 2. List employees: contains "list" / "show" / "get" and "employee"; may include a crude title filter
    ///    using markers: " in ", " with title ", " title ".
    /// 3. Specific employee by id: contains "employee id" or "employeeId" plus a numeric token after an 'id' marker.
    /// Any failure or ambiguity yields an empty string to signal fallback to semantic retrieval.
    /// </remarks>
    public async Task<string> TryAnswerAsync(string question, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
            return string.Empty;

        var q = question.Trim().ToLowerInvariant();

        // Pattern: count employees
        if ((q.Contains("how many") || q.Contains("count")) && q.Contains("employee"))
        {
            var count = await _db.Employees.CountAsync(cancellationToken).ConfigureAwait(false);
            return $"EmployeeCount: {count}";
        }

        // Pattern: list employees (optionally filtered by job title keyword after 'in', 'with', or 'title')
        if ((q.Contains("list") || q.Contains("show") || q.Contains("get")) && q.Contains("employee"))
        {
            // Try to extract a job title filter (very naive heuristic)
            // Examples: "list employees in sales", "show employees with title manager", "list employees title sales representative"
            string? titleFilter = ExtractTitleFilter(q);
            var query = _db.Employees.AsQueryable();

            if (!string.IsNullOrEmpty(titleFilter))
            {
                // Simple contains match on JobTitle (case-insensitive)
                query = query.Where(e => e.JobTitle != null && EF.Functions.Like(e.JobTitle.ToLower(), $"%{titleFilter}%"));
            }

            var employees = await query
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .Select(e => new
                {
                    e.EmployeeId,
                    e.FirstName,
                    e.LastName,
                    e.JobTitle,
                    e.EmailAddress
                })
                .Take(50) // hard cap to keep output concise
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (employees.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("Employees:");
            sb.AppendLine("EmployeeId | FirstName | LastName | JobTitle | EmailAddress");
            foreach (var e in employees)
            {
                sb.Append(e.EmployeeId).Append(" | ")
                  .Append(e.FirstName ?? "").Append(" | ")
                  .Append(e.LastName ?? "").Append(" | ")
                  .Append(e.JobTitle ?? "").Append(" | ")
                  .AppendLine(e.EmailAddress ?? "");
            }
            if (!string.IsNullOrEmpty(titleFilter))
                sb.AppendLine($"FilterApplied: JobTitle CONTAINS \"{titleFilter}\"");
            return sb.ToString().TrimEnd();
        }

        // Pattern: employees with a specific exact employee id
        if (q.Contains("employee id") || q.Contains("employeeId"))
        {
            var id = ExtractEmployeeId(q);
            if (id != null)
            {
                var emp = await _db.Employees
                    .Where(e => e.EmployeeId == id.Value)
                    .Select(e => new
                    {
                        e.EmployeeId,
                        e.FirstName,
                        e.LastName,
                        e.JobTitle,
                        e.EmailAddress,
                        e.PrimaryPhone,
                        e.SupervisorId
                    })
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (emp == null) return string.Empty;

                var sb = new StringBuilder();
                sb.AppendLine("Employee:");
                sb.AppendLine($"EmployeeId: {emp.EmployeeId}");
                sb.AppendLine($"FirstName: {emp.FirstName}");
                sb.AppendLine($"LastName: {emp.LastName}");
                sb.AppendLine($"JobTitle: {emp.JobTitle}");
                sb.AppendLine($"EmailAddress: {emp.EmailAddress}");
                sb.AppendLine($"PrimaryPhone: {emp.PrimaryPhone}");
                sb.AppendLine($"SupervisorId: {emp.SupervisorId}");
                return sb.ToString().TrimEnd();
            }
        }

        // No recognized structured query pattern
        return string.Empty;
    }

    /// <summary>
    /// Attempts to extract a job title fragment from the normalized (lowercased) question text.
    /// </summary>
    /// <param name="q">Lowercased, trimmed question text.</param>
    /// <returns>
    /// A title fragment (<= 60 chars) suitable for a case-insensitive contains match; otherwise <c>null</c>.
    /// </returns>
    /// <remarks>
    /// Heuristics:
    /// - Searches for first occurrence of any marker: " in ", " with title ", " title ".
    /// - Captures the substring after the marker until a stop word (employee/employees/that/who/which) or end.
    /// - Trims result and enforces a length ceiling to reduce noisy matches.
    /// </remarks>
    private static string? ExtractTitleFilter(string q)
    {
        // crude heuristics
        string[] markers = [" in ", " with title ", " title "];
        foreach (var marker in markers)
        {
            var idx = q.IndexOf(marker, StringComparison.Ordinal);
            if (idx >= 0)
            {
                var fragment = q[(idx + marker.Length)..].Trim();
                // stop at 'employee', 'employees', 'that', 'who', 'which'
                var stopWords = new[] { " employee", " employees", " that", " who", " which" };
                foreach (var stop in stopWords)
                {
                    var stopIdx = fragment.IndexOf(stop, StringComparison.Ordinal);
                    if (stopIdx > 0)
                        fragment = fragment[..stopIdx];
                }
                fragment = fragment.Trim();
                if (fragment.Length > 0 && fragment.Length <= 60)
                    return fragment;
            }
        }
        return null;
    }

    /// <summary>
    /// Extracts a candidate employee ID from the normalized (lowercased) question text.
    /// </summary>
    /// <param name="q">Lowercased, tokenizable question text.</param>
    /// <returns>
    /// A <see cref="short"/> employee id if one unambiguous numeric token is found in a valid position; otherwise <c>null</c>.
    /// </returns>
    /// <remarks>
    /// Strategy:
    /// 1. Tokenize on whitespace and punctuation.
    /// 2. Look for tokens equal to or ending with 'id' and parse the subsequent token as a number.
    /// 3. Fallback: if exactly one numeric token appears in the entire question, return it.
    /// This favors precision; multiple numeric tokens => returns null to avoid incorrect selection.
    /// </remarks>
    private static short? ExtractEmployeeId(string q)
    {
        // look for tokens that are pure numbers following 'id'
        var tokens = q.Split([' ', '\t', '\r', '\n', ',', '.', ';', ':'], StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < tokens.Length; i++)
        {
            if (tokens[i] is "id" or "id?" or "id:" || tokens[i].EndsWith("id", StringComparison.Ordinal))
            {
                if (i + 1 < tokens.Length && short.TryParse(tokens[i + 1], out var idVal))
                    return idVal;
            }
        }
        // fallback: any standalone number if exactly one
        var numeric = tokens.Where(t => short.TryParse(t, out _)).Distinct().ToList();
        if (numeric.Count == 1 && short.TryParse(numeric[0], out var lone))
            return lone;
        return null;
    }
}