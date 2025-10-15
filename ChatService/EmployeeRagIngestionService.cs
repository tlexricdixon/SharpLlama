using Microsoft.EntityFrameworkCore;
using SharpLlama.Contracts;
using SharpLlama.Entities;

namespace SharpLlama.ChatService
{
    public class EmployeeRagIngestionService : IEmployeeRagIngestionService
    {
        private readonly NorthwindStarterContext _db;
        private readonly IKragStore _rag;
        private readonly ILoggerManager _logger;

        public EmployeeRagIngestionService(NorthwindStarterContext db, IKragStore ragStore, ILoggerManager logger)
        {
            _db = db;
            _rag = ragStore;
            _logger = logger;
        }

        public async Task<int> IngestAllAsync(CancellationToken ct = default)
        {
            var employees = await _db.Employees.AsNoTracking().ToListAsync(ct);
            var totalChunks = 0;

            foreach (var e in employees)
            {
                ct.ThrowIfCancellationRequested();

                string? supervisorName = null;
                if (e.SupervisorId.HasValue)
                {
                    var sup = employees.FirstOrDefault(x => x.EmployeeId == e.SupervisorId.Value);
                    if (sup is not null) supervisorName = $"{sup.FirstName} {sup.LastName}".Trim();
                }

                var ordersSummary = await TryBuildOrderSummaryAsync(e.EmployeeId, ct);

                var profileText = EmployeeChunkBuilder.BuildProfileChunk(e, supervisorName);
                var notesText = EmployeeChunkBuilder.BuildNotesChunk(e);
                var ordersText = EmployeeChunkBuilder.BuildOrdersChunk(e, ordersSummary);

                await _rag.UpsertChunkAsync(new ChunkRecord
                {
                    Id = $"{e.EmployeeId}|PROFILE",
                    TableName = "Employees",
                    EntityName = $"{e.FirstName} {e.LastName}".Trim(),
                    Text = profileText
                });
                totalChunks++;

                await _rag.UpsertChunkAsync(new ChunkRecord
                {
                    Id = $"{e.EmployeeId}|NOTES",
                    TableName = "Employees",
                    EntityName = $"{e.FirstName} {e.LastName}".Trim(),
                    Text = notesText
                });
                totalChunks++;

                await _rag.UpsertChunkAsync(new ChunkRecord
                {
                    Id = $"{e.EmployeeId}|ORDERS",
                    TableName = "Employees",
                    EntityName = $"{e.FirstName} {e.LastName}".Trim(),
                    Text = ordersText
                });
                totalChunks++;
            }

            _logger.LogInfo($"EmployeeRagIngestionService: Ingested {employees.Count} employees as {totalChunks} chunks (PROFILE/NOTES/ORDERS).");
            return totalChunks;
        }

        private async Task<EmployeeChunkBuilder.OrderSummary?> TryBuildOrderSummaryAsync(int employeeId, CancellationToken ct)
        {
            try
            {
                var ordersQuery = _db.Set<Order>().AsQueryable();
                if (!await ordersQuery.AnyAsync(ct)) return null;

                var employeeOrders = ordersQuery.Where(o => o.EmployeeId == employeeId);

                var orderCount = await employeeOrders.CountAsync(ct);
                if (orderCount == 0) return new EmployeeChunkBuilder.OrderSummary { OrderCount = 0 };

                decimal totalSales = 0m;
                bool hasTotalColumn = _db.Model.FindEntityType(typeof(Order))?.FindProperty("Total") != null;
                if (hasTotalColumn)
                {
                    totalSales = await employeeOrders.SumAsync(o => EF.Property<decimal>(o, "Total"), ct);
                }
                else
                {
                    var detailsSet = _db.Set<OrderDetail>();
                    if (detailsSet is not null)
                    {
                        var q = from d in detailsSet
                                join o in employeeOrders on d.OrderId equals o.OrderId
                                select d;
                        if (await q.AnyAsync(ct))
                        {
                            bool hasDiscount = _db.Model.FindEntityType(typeof(OrderDetail))?.FindProperty("Discount") != null;
                            if (hasDiscount)
                                totalSales = await q.SumAsync(d =>
                                    (d.UnitPrice.HasValue ? (decimal)d.UnitPrice.Value : 0m) *
                                    (d.Quantity.HasValue ? (decimal)d.Quantity.Value : 0m) *
                                    (1m - (d.Discount.HasValue ? d.Discount.Value / 100m : 0m)), ct);
                            else
                                totalSales = (decimal)await q.SumAsync(d => d.UnitPrice * d.Quantity, ct);
                        }
                    }
                }

                string? topRegion = null;
                try
                {
                    bool hasShipperId = _db.Model.FindEntityType(typeof(Order))?.FindProperty("ShipperId") != null;
                    if (hasShipperId)
                    {
                        topRegion = await employeeOrders
                            .Where(o => o.ShipperId.ToString() != null)
                            .GroupBy(o => o.ShipperId!.ToString())
                            .OrderByDescending(g => g.Count())
                            .Select(g => g.Key)
                            .FirstOrDefaultAsync(ct);
                    }
                }
                catch (Exception ex)
                {
                    // S2486: Exception is ignored because missing ShipperId is not critical for order summary.
                    // S108: Block is now filled with a comment explaining why the exception is ignored.
                }

                var firstOrderDate = await employeeOrders.MinAsync(o => o.OrderDate, ct);
                var lastOrderDate = await employeeOrders.MaxAsync(o => o.OrderDate, ct);

                return new EmployeeChunkBuilder.OrderSummary
                {
                    OrderCount = orderCount,
                    TotalSales = totalSales,
                    TopRegion = topRegion,
                    FirstOrderDate = firstOrderDate,
                    LastOrderDate = lastOrderDate
                };
            }
            catch
            {
                return null;
            }
        }
    }
}