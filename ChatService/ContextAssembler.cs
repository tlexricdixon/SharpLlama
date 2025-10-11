using SharpLlama.Contracts;
using System.Text;

namespace SharpLlama.ChatService;

public class ContextAssembler
{
    private readonly IKragStore _rag;
    public ContextAssembler(IKragStore rag) => _rag = rag;

    public async Task<string> BuildAsync(string userQuery, int topK = 3)
    {
        var chunks = await _rag.SearchAsync(userQuery, topK);
        var sb = new StringBuilder();
        foreach (var c in chunks)
        {
            sb.AppendLine($"--- {c.TableName}: {c.EntityName}");
            sb.AppendLine(c.Text);
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
