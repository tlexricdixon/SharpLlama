
using System;
using System.Text;
using System.Text.RegularExpressions;
using SharpLlama.Entities;

namespace SharpLlama.ChatService
{
    public static class EmployeeChunkBuilder
    {
        public sealed class OrderSummary
        {
            public int OrderCount { get; set; }
            public decimal TotalSales { get; set; }
            public string? TopRegion { get; set; }
            public DateTime? FirstOrderDate { get; set; }
            public DateTime? LastOrderDate { get; set; }
        }

        private static string Safe(string? s) => string.IsNullOrWhiteSpace(s) ? "" : s.Trim();

        private static string StripHtmlOrRtf(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";
            var s = input.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
            s = Regex.Replace(s, "<.*?>", string.Empty);
            s = Regex.Replace(s, @"\\[a-zA-Z]+\d*", " ");
            s = Regex.Replace(s, @"\s{2,}", " ").Trim();
            return s;
        }

        private static string LimitToTokens(string text, int maxTokens = 480)
        {
            if (string.IsNullOrEmpty(text)) return "";
            int maxLen = Math.Max(64, maxTokens * 4);
            if (text.Length <= maxLen) return text;
            return text.Substring(0, maxLen) + "... [TRUNCATED]";
        }

        public static string BuildProfileChunk(Employee e, string? supervisorName)
        {
            var sb = new StringBuilder();
            sb.AppendLine("EMPLOYEE PROFILE");
            sb.AppendLine("----------------");
            sb.AppendLine($"Name: {Safe(e.FirstName)} {Safe(e.LastName)}");
            sb.AppendLine($"Title: {Safe(e.Title) ?? Safe(e.JobTitle) ?? "Unknown"}");
            sb.AppendLine($"Email: {Safe(e.EmailAddress) ?? "Unknown"}");
            sb.AppendLine($"Primary Phone: {Safe(e.PrimaryPhone) ?? "Unknown"}");
            sb.AppendLine($"Supervisor: {supervisorName ?? "Unknown"}");
            if (e.AddedOn.HasValue) sb.AppendLine($"Added On: {e.AddedOn.Value:yyyy-MM-dd}");
            if (e.ModifiedOn.HasValue) sb.AppendLine($"Modified On: {e.ModifiedOn.Value:yyyy-MM-dd}");
            sb.AppendLine($"EmployeeId: {e.EmployeeId}");
            return LimitToTokens(sb.ToString());
        }

        public static string BuildNotesChunk(Employee e)
        {
            var notes = string.IsNullOrWhiteSpace(e.Notes) ? "No notes." : StripHtmlOrRtf(e.Notes!);
            var sb = new StringBuilder();
            sb.AppendLine("EMPLOYEE NOTES");
            sb.AppendLine("--------------");
            sb.AppendLine($"Name: {Safe(e.FirstName)} {Safe(e.LastName)}");
            sb.AppendLine($"Notes: {notes}");
            sb.AppendLine($"EmployeeId: {e.EmployeeId}");
            return LimitToTokens(sb.ToString());
        }

        public static string BuildOrdersChunk(Employee e, OrderSummary? orders)
        {
            var sb = new StringBuilder();
            sb.AppendLine("EMPLOYEE ORDERS SUMMARY");
            sb.AppendLine("-----------------------");
            sb.AppendLine($"Name: {Safe(e.FirstName)} {Safe(e.LastName)}");
            if (orders is null)
            {
                sb.AppendLine("Order Count: 0");
            }
            else
            {
                sb.AppendLine($"Order Count: {orders.OrderCount}");
                if (orders.TotalSales > 0) sb.AppendLine($"Total Sales: {orders.TotalSales:C}");
                if (!string.IsNullOrWhiteSpace(orders.TopRegion)) sb.AppendLine($"Top Region: {orders.TopRegion}");
                if (orders.FirstOrderDate.HasValue || orders.LastOrderDate.HasValue)
                {
                    sb.AppendLine($"Order Range: {orders.FirstOrderDate?.ToShortDateString() ?? "N/A"} → {orders.LastOrderDate?.ToShortDateString() ?? "N/A"}");
                }
            }
            sb.AppendLine($"EmployeeId: {e.EmployeeId}");
            return LimitToTokens(sb.ToString());
        }
    }
}


