using ChatService.Plugins;
using LLama;
using LLama.Common;
using LLama.Sampling;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using SharpLlama.ChatService;
using SharpLlama.Contracts; // Added for quantum-inspired parallel aggregation
using SharpLlama.Entities;
using SharpLlama.Infrastructure;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using static LLama.LLamaTransforms;

namespace SharpLlama.ChatService
{
    public class RagChatService : IRagChatService, IDisposable
    {
        private const string ServiceType = "RAG";
        private static readonly ActivitySource s_activitySource = new("ChatService.RagChatService");

        private readonly ILoggerManager _logger;
        private readonly IKragStore _ragStore;
        private readonly IChatResponseCache _cache;
        private readonly IChatMetrics _metrics;
        private readonly StructuredEmployeeQueryService? _structuredEmployeeQueryService;
        private readonly IRagDiagnosticsCollector _diagCollector;

        private readonly string _modelPath;
        private readonly string _modelName;
        private readonly int _contextSize;
        private readonly TimeSpan _requestTimeout;
        private readonly TimeSpan _memorySearchTimeout;

        private readonly Kernel _kernel;
        private readonly List<ISemanticKernelPlugin> _plugins;

        private readonly LLamaContext _context;
        private readonly ChatSession _session;
        private readonly SemaphoreSlim _inferenceLock = new(1, 1);

        private bool _disposed;

        private readonly ModelOptions _modelOptions;
        private readonly ChatServiceOptions _chatOptions;

        // Quantum-inspired tuning knobs (lightweight & experimental)
        private const int QuantumMaxExpansions = 10;
        private const int QuantumPerExpansionLimit = 4;
        private const double QuantumMinRelevance = 0.40;
        private const int QuantumFinalPartitions = 14;

        private static void ValidateOptions(ModelOptions model, ChatServiceOptions chat)
        {
            if (string.IsNullOrWhiteSpace(model.ModelPath))
                throw new ArgumentException("ModelPath required.");
            if (model.ContextSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(model.ContextSize));
            if (chat.RequestTimeoutSeconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(chat.RequestTimeoutSeconds));
            if (chat.AntiPrompts is null || chat.AntiPrompts.Length == 0)
                throw new ArgumentException("At least one AntiPrompt required.");
        }

        public RagChatService(
            IOptions<ModelOptions> modelOptions,
            IOptions<ChatServiceOptions> chatOptions,
            ILoggerManager logger,
            ILLamaWeightManager weightManager,
            IKragStore ragStore,
            IChatResponseCache? cache = null,
            IChatMetrics? metrics = null,
            IEnumerable<ISemanticKernelPlugin>? plugins = null,
            StructuredEmployeeQueryService? structuredEmployeeQueryService = null,
            IRagDiagnosticsCollector? diagnosticsCollector = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _ragStore = ragStore ?? throw new ArgumentNullException(nameof(ragStore));
            _cache = cache ?? new NullChatResponseCache();
            _metrics = metrics ?? new NullChatMetrics();
            _structuredEmployeeQueryService = structuredEmployeeQueryService;
            _diagCollector = diagnosticsCollector != null ? diagnosticsCollector : new NullRagDiagnosticsCollector();

            _modelOptions = (modelOptions ?? throw new ArgumentNullException(nameof(modelOptions))).Value;
            _chatOptions = (chatOptions ?? throw new ArgumentNullException(nameof(chatOptions))).Value;
            ValidateOptions(_modelOptions, _chatOptions);

            _modelPath = ResolveModelPath(_modelOptions.ModelPath);
            _modelName = Path.GetFileNameWithoutExtension(_modelPath);
            _contextSize = _modelOptions.ContextSize;

            _requestTimeout = TimeSpan.FromSeconds(_chatOptions.RequestTimeoutSeconds);
            _memorySearchTimeout = TimeSpan.FromSeconds(
                Math.Min(_chatOptions.MemorySearchTimeoutSeconds, _chatOptions.RequestTimeoutSeconds / 2));

            _kernel = Kernel.CreateBuilder().Build();

            _plugins = new List<ISemanticKernelPlugin> { new RagPlugin(_ragStore, _logger) };
            if (plugins != null)
            {
                foreach (var p in plugins)
                {
                    if (p != null) _plugins.Add(p);
                }
            }
            else
            {
                _plugins.Add(new InputValidationPlugin());
                _plugins.Add(new ContextEnhancementPlugin(_cache));
                _plugins.Add(new ResponseFormattingPlugin());
            }

            foreach (var plugin in _plugins)
            {
                try
                {
                    _kernel.Plugins.AddFromObject(plugin, plugin.Name);
                    _logger.LogDebug($"Registered plugin: {plugin.Name}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed registering plugin {plugin.Name}: {ex.Message}");
                }
            }

            var modelParams = new ModelParams(_modelPath)
            {
                ContextSize = (uint)_contextSize
            };

            try
            {
                var weights = weightManager.GetOrCreateWeights(_modelPath, modelParams);
                _context = new LLamaContext(weights, modelParams);
                var executor = new InteractiveExecutor(_context);
                _session = new ChatSession(executor)
                    .WithOutputTransform(new KeywordTextOutputStreamTransform(new[] { "User:", "Assistant:" }, redundancyLength: 8))
                    .WithHistoryTransform(new HistoryTransform());

                _logger.LogInfo($"evt=ServiceInit service={ServiceType} model={_modelName} contextSize={_contextSize} reqTimeoutSec={_requestTimeout.TotalSeconds} memSearchTimeoutSec={_memorySearchTimeout.TotalSeconds} antiPrompts=\"{string.Join('|', _chatOptions.AntiPrompts)}\" maxTokens={_chatOptions.MaxTokens} temp={_chatOptions.Temperature} topP={_chatOptions.TopP} repeatPenalty={_chatOptions.RepeatPenalty} plugins={_plugins.Count}");
            }
            catch
            {
                _context?.Dispose();
                throw;
            }
        }

        public Task<string> SendAsync(ChatHistory history, CancellationToken cancellationToken = default)
            => SendWithRagAsync(history, null, cancellationToken);

        public async Task<string> SendWithRagAsync(ChatHistory history, string? collectionName = null, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (history is null) throw new ArgumentNullException(nameof(history));
            if (history.Messages.Count == 0)
                throw new ArgumentException("ChatHistory must contain at least one message.", nameof(history));

            var sw = Stopwatch.StartNew();
            _metrics.IncrementRequestCount(ServiceType);

            var activity = s_activitySource.StartActivity("chat.rag.send", ActivityKind.Server);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var requestId = Guid.NewGuid();
                activity?.SetTag("chat.request.id", requestId);
                activity?.SetTag("llm.model.name", _modelName);
                activity?.SetTag("llm.context.window.size", _contextSize);
                activity?.SetTag("chat.service", ServiceType);
                activity?.SetTag("chat.messages.count", history.Messages.Count);

                var cacheKey = GenerateRagCacheKey(history);
                var cached = await _cache.GetCachedResponseAsync(cacheKey).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(cached))
                {
                    _metrics.IncrementCacheHit(ServiceType);
                    activity?.SetTag("chat.cache.hit", true);
                    activity?.SetTag("chat.response.cached", true);
                    _metrics.RecordResponseLength(ServiceType, cached.Length);
                    _metrics.RecordResponseTime(ServiceType, sw.Elapsed);
                    return cached;
                }
                _metrics.IncrementCacheMiss(ServiceType);
                activity?.SetTag("chat.cache.hit", false);

                var userQuery = GetLastUserMessage(history);
                string structuredAnswer = string.Empty;
                if (_structuredEmployeeQueryService != null && !string.IsNullOrWhiteSpace(userQuery))
                {
                    try
                    {
                        structuredAnswer = await _structuredEmployeeQueryService
                            .TryAnswerAsync(userQuery, cancellationToken)
                            .ConfigureAwait(false);

                        if (!string.IsNullOrEmpty(structuredAnswer))
                        {
                            activity?.SetTag("chat.structured.hit", true);
                            _logger.LogDebug($"[{requestId}] Structured answer length={structuredAnswer.Length}");
                        }
                        else
                        {
                            activity?.SetTag("chat.structured.hit", false);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Structured query failure (ignored): {ex.Message}");
                        activity?.SetTag("chat.structured.error", ex.GetType().Name);
                    }
                }

                string relevantContext = string.Empty;

                if (string.IsNullOrEmpty(structuredAnswer))
                {
                    bool quantumMode = IsQuantumModeRequested(userQuery);
                    activity?.SetTag("chat.quantum.mode", quantumMode);
                    if (quantumMode)
                    {
                        relevantContext = await QuantumInspiredSearchAsync(userQuery, requestId, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        relevantContext = await SearchRelevantContextSafe(
                            userQuery,
                            requestId,
                            collectionName,
                            cancellationToken).ConfigureAwait(false);
                    }

                    activity?.SetTag("chat.context.included", !string.IsNullOrEmpty(relevantContext));
                }
                else
                {
                    activity?.SetTag("chat.context.skipped_due_to_structured_answer", true);
                }

                var enhancedHistory = await CreateEnhancedHistory(history, relevantContext, structuredAnswer)
                    .ConfigureAwait(false);

                enhancedHistory = await ApplyPrePluginsAsync(enhancedHistory, cancellationToken)
                    .ConfigureAwait(false);

                var response = await GenerateAsync(enhancedHistory, requestId, cancellationToken)
                    .ConfigureAwait(false);

                response = await ApplyPostPluginsAsync(response, cancellationToken)
                    .ConfigureAwait(false);

                await _cache.SetCachedResponseAsync(cacheKey, response, TimeSpan.FromMinutes(10))
                    .ConfigureAwait(false);

                _metrics.RecordResponseLength(ServiceType, response.Length);
                _metrics.RecordResponseTime(ServiceType, sw.Elapsed);

                return response;
            }
            catch (OperationCanceledException)
            {
                _metrics.IncrementErrorCount(ServiceType, "Canceled");
                activity?.SetTag("chat.canceled", true);
                _logger.LogWarning("Request canceled.");
                throw;
            }
            catch (Exception ex)
            {
                _metrics.IncrementErrorCount(ServiceType, "Unhandled");
                activity?.SetTag("chat.error", ex.GetType().Name);
                _logger.LogError($"Unhandled error: {ex.Message}");
                throw;
            }
            finally
            {
                activity?.Dispose();
            }
        }

        private async Task<ChatHistory> ApplyPrePluginsAsync(ChatHistory history, CancellationToken ct)
        {
            if (history.Messages.Count == 0) return history;

            var last = history.Messages.Last();
            if (last.AuthorRole != AuthorRole.User) return history;

            string current = last.Content;
            foreach (var plugin in _plugins)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (await plugin.CanHandleAsync(current).ConfigureAwait(false))
                    {
                        var transformed = await plugin.ExecuteAsync(current, _kernel).ConfigureAwait(false);
                        if (!string.IsNullOrWhiteSpace(transformed) && transformed != current)
                        {
                            current = transformed;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Pre-plugin {plugin.Name} error (ignored): {ex.Message}");
                }
            }

            if (current == last.Content) return history;

            var newHistory = new ChatHistory();
            foreach (var m in history.Messages.Take(history.Messages.Count - 1))
                newHistory.AddMessage(m.AuthorRole, m.Content);
            newHistory.AddMessage(last.AuthorRole, current);
            return newHistory;
        }

        private async Task<string> ApplyPostPluginsAsync(string response, CancellationToken ct)
        {
            string current = response;
            foreach (var plugin in _plugins)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (await plugin.CanHandleAsync(current).ConfigureAwait(false))
                    {
                        var transformed = await plugin.ExecuteAsync(current, _kernel).ConfigureAwait(false);
                        if (!string.IsNullOrWhiteSpace(transformed))
                            current = transformed;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Post-plugin {plugin.Name} error (ignored): {ex.Message}");
                }
            }
            return current;
        }

        private async Task<string> GenerateAsync(ChatHistory history, Guid requestId, CancellationToken cancellationToken)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_requestTimeout);

            using var activity = s_activitySource.StartActivity("chat.rag.generate", ActivityKind.Internal);
            activity?.SetTag("llm.model.name", _modelName);
            activity?.SetTag("llm.context.window.size", _contextSize);
            activity?.SetTag("chat.service", ServiceType);

            var sb = new StringBuilder(1024);

            await _inferenceLock.WaitAsync(cts.Token).ConfigureAwait(false);
            try
            {
                var promptAggregate = string.Join("\n", history.Messages.Select(m => $"{m.AuthorRole}:{m.Content}"));
                int promptTokens = _context.Tokenize(promptAggregate).Length;
                activity?.SetTag("llm.usage.input_tokens", promptTokens);
                ChatOtelMetrics.RecordPromptTokens(promptTokens, ServiceType, _contextSize);

                var inference = new InferenceParams
                {
                    AntiPrompts = _chatOptions.AntiPrompts,
                    MaxTokens = _chatOptions.MaxTokens,
                    SamplingPipeline = new DefaultSamplingPipeline
                    {
                        RepeatPenalty = (float)_chatOptions.RepeatPenalty
                    }
                };

                var result = _session.ChatAsync(history, inference);

                await foreach (var r in result.WithCancellation(cts.Token).ConfigureAwait(false))
                {
                    sb.Append(r);
                }

                int outputTokens = _context.Tokenize(sb.ToString()).Length;
                activity?.SetTag("llm.usage.output_tokens", outputTokens);
                activity?.SetTag("llm.usage.total_tokens", promptTokens + outputTokens);
                double pct = Math.Round((double)(promptTokens + outputTokens) / _contextSize * 100, 2);
                activity?.SetTag("llm.context.usage.percent", pct);

                ChatOtelMetrics.RecordOutputTokens(outputTokens, ServiceType, _contextSize);

                _logger.LogInfo($"evt=GenerationComplete service={ServiceType} reqId={requestId} promptTokens={promptTokens} outputTokens={outputTokens} ctxPct={pct} totalTokens={promptTokens + outputTokens}");
                return sb.ToString();
            }
            catch (OperationCanceledException)
            {
                activity?.SetTag("llm.generation.canceled", true);
                _metrics.IncrementErrorCount(ServiceType, "GenerationCanceled");
                _logger.LogWarning($"evt=GenerationCanceled service={ServiceType} reqId={requestId}");
                throw;
            }
            catch (Exception ex)
            {
                _metrics.IncrementErrorCount(ServiceType, "Generation");
                activity?.SetTag("llm.generation.error", ex.GetType().Name);
                _logger.LogError($"evt=GenerationError service={ServiceType} reqId={requestId} errorType={ex.GetType().Name} message=\"{ex.Message}\"");
                throw;
            }
            finally
            {
                _inferenceLock.Release();
            }
        }

        private async Task<string> SearchRelevantContextSafe(string query, Guid requestId, string? collectionName, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(query))
                return string.Empty;

            try
            {
                var results = await _ragStore.SearchAsync(query, topK: 6);
                if (!results.Any())
                {
                    _logger.LogDebug($"[{requestId}] RAG found no results.");
                    return string.Empty;
                }

                var sb = new StringBuilder("Relevant context:\n");
                foreach (var chunk in results)
                {
                    sb.AppendLine($"- [{chunk.TableName}:{chunk.EntityName}] {chunk.Text}");
                }

                _logger.LogDebug($"[{requestId}] RAG results {results.Count()} chunks.");
                return sb.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError($"[{requestId}] RAG search failed: {ex.Message}");
                return string.Empty;
            }
        }


        // --------------- Quantum-Inspired Experimental Retrieval ----------------
        private bool IsQuantumModeRequested(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return false;
            // Simple triggers; intentionally explicit to avoid accidental activation
            return query.Contains("#quantum", StringComparison.OrdinalIgnoreCase)
                || query.Contains("#qi", StringComparison.OrdinalIgnoreCase)
                || query.Contains(" quantum ", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<string> QuantumInspiredSearchAsync(string query, Guid requestId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(query))
                return string.Empty;

            try
            {
                var expansions = GenerateQuantumExpansions(query)
                    .Take(QuantumMaxExpansions)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (expansions.Count == 0)
                    return string.Empty;

                var bag = new ConcurrentBag<ChunkRecord>();

                var tasks = expansions.Select(async exp =>
                {
                    try
                    {
                        var partial = await _ragStore.SearchAsync(exp, topK: QuantumPerExpansionLimit);
                        foreach (var c in partial)
                            bag.Add(c);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"[QI:{requestId}] expansion '{exp}' error: {ex.Message}");
                    }
                });

                await Task.WhenAll(tasks);

                if (bag.IsEmpty)
                {
                    _logger.LogDebug($"[QI:{requestId}] no results across expansions.");
                    return string.Empty;
                }

                var grouped = bag.GroupBy(c => c.Text)
                                 .Select(g => new { Text = g.Key, Hits = g.Count() })
                                 .OrderByDescending(x => x.Hits)
                                 .Take(QuantumFinalPartitions)
                                 .ToList();

                var sb = new StringBuilder();
                sb.AppendLine("Quantum inspired context (RAG hybrid):\n");
                foreach (var g in grouped)
                {
                    sb.AppendLine($"- {g.Text}");
                }

                sb.AppendLine()
                  .Append($"[debug:qiexpansions]={expansions.Count} totalChunks={bag.Count}");

                return sb.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError($"[QI:{requestId}] quantum RAG search error: {ex.Message}");
                return string.Empty;
            }
        }


        private sealed class QuantumPartitionScore
        {
            public string Text = string.Empty;
            public int Hits;
            public double Probability;
            public HashSet<string> Expansions { get; } = new(StringComparer.OrdinalIgnoreCase);
        }

        private static IEnumerable<string> GenerateQuantumExpansions(string query)
        {
            // Base
            yield return query;

            var cleaned = query.Replace("#quantum", "", StringComparison.OrdinalIgnoreCase)
                               .Replace("#qi", "", StringComparison.OrdinalIgnoreCase)
                               .Trim();

            var tokens = cleaned
                .Split(new[] { ' ', '\t', '\r', '\n', ',', '.', ';', ':', '-', '_' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(t => t.Length > 1)
                .ToArray();

            if (tokens.Length == 0)
                yield break;

            // Stopwords (minimal)
            var stop = new HashSet<string>(new[] { "the", "a", "of", "for", "and", "to", "in", "on", "at", "with", "show", "list", "get" }, StringComparer.OrdinalIgnoreCase);

            // 1-token focus expansions
            foreach (var t in tokens.Where(t => !stop.Contains(t)))
                yield return t;

            // Bigram expansions
            for (int i = 0; i < tokens.Length - 1 && i < 6; i++)
            {
                var a = tokens[i];
                var b = tokens[i + 1];
                if (!stop.Contains(a) || !stop.Contains(b))
                    yield return a + " " + b;
            }

            // Synonym simple substitutions
            var synonyms = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["company"] = new[] { "organization", "enterprise", "firm" },
                ["employees"] = new[] { "staff", "personnel" },
                ["revenue"] = new[] { "sales", "turnover" },
                ["growth"] = new[] { "expansion", "increase" },
                ["profit"] = new[] { "earnings", "income" }
            };

            foreach (var t in tokens)
            {
                if (synonyms.TryGetValue(t, out var syns))
                {
                    foreach (var s in syns)
                        yield return cleaned.Replace(t, s, StringComparison.OrdinalIgnoreCase);
                }
            }

            // Token deletion variants (remove one salient token)
            if (tokens.Length > 2)
            {
                for (int i = 0; i < tokens.Length && i < 5; i++)
                {
                    if (stop.Contains(tokens[i])) continue;
                    var reduced = tokens.Where((_, idx) => idx != i);
                    yield return string.Join(' ', reduced);
                }
            }
        }
        // ----------------------------------------------------------------------

        private static async Task<ChatHistory> CreateEnhancedHistory(ChatHistory originalHistory, string relevantContext, string structuredAnswer)
        {
            var enhanced = new ChatHistory();

            if (!string.IsNullOrEmpty(structuredAnswer))
            {
                enhanced.AddMessage(AuthorRole.System,
                    "Deterministic data (use directly if relevant):\n" + structuredAnswer + "\n");
            }

            if (!string.IsNullOrEmpty(relevantContext))
            {
                enhanced.AddMessage(AuthorRole.System,
                    "Retrieved semantic context. Use if relevant; otherwise ignore. Do not fabricate.\n\n" +
                    relevantContext + "\n");
            }

            foreach (var m in originalHistory.Messages)
                enhanced.AddMessage(m.AuthorRole, m.Content);

            return await Task.FromResult(enhanced);
        }

        private string GenerateRagCacheKey(ChatHistory history)
        {
            var joined = string.Join("|", history.Messages.Select(m => $"{m.AuthorRole}:{m.Content}"));
            return _cache.GenerateCacheKey(joined, ServiceType);
        }

        //public async Task<bool> AddDocumentAsync(string documentId, string content, Dictionary<string, object>? metadata = null)
        //{
        //    ThrowIfDisposed();
        //    _logger.LogDebug($"AddDocumentAsync start id={documentId} len={content?.Length}");
        //    if (string.IsNullOrWhiteSpace(documentId) || string.IsNullOrWhiteSpace(content))
        //    {
        //        _logger.LogWarning("AddDocumentAsync invalid input.");
        //        return false;
        //    }

        //    try
        //    {
        //        await _ragStore.StoreDocumentAsync(documentId, content, metadata).ConfigureAwait(false);
        //        _logger.LogInfo($"Document added: {documentId}");
        //        return true;
        //    }
        //    catch (Exception ex)
        //    {
        //        _metrics.IncrementErrorCount(ServiceType, "AddDocument");
        //        _logger.LogError($"AddDocument failed ({documentId}): {ex.Message}");
        //        return false;
        //    }
        //}

        //public async Task<bool> DeleteDocumentAsync(string documentId)
        //{
        //    ThrowIfDisposed();
        //    _logger.LogDebug($"DeleteDocumentAsync start id={documentId}");
        //    try
        //    {
        //        var result = await _ragStore.DeleteDocumentAsync(documentId).ConfigureAwait(false);
        //        if (!result)
        //            _logger.LogWarning($"DeleteDocumentAsync returned false for {documentId}");
        //        return result;
        //    }
        //    catch (Exception ex)
        //    {
        //        _metrics.IncrementErrorCount(ServiceType, "DeleteDocument");
        //        _logger.LogError($"DeleteDocument failed ({documentId}): {ex.Message}");
        //        return false;
        //    }
        //}

        private static string GetLastUserMessage(ChatHistory history)
            => history.Messages.LastOrDefault(m => m.AuthorRole == AuthorRole.User)?.Content ?? string.Empty;

        private static string ResolveModelPath(string configuredPath)
        {
            if (string.IsNullOrWhiteSpace(configuredPath))
                throw new ArgumentException("Model path empty.", nameof(configuredPath));

            if (Path.IsPathRooted(configuredPath) && File.Exists(configuredPath))
                return configuredPath;

            var baseDir = AppContext.BaseDirectory;
            var attempt1 = Path.GetFullPath(Path.Combine(baseDir, configuredPath));
            if (File.Exists(attempt1)) return attempt1;

            var attempt2 = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", configuredPath));
            if (File.Exists(attempt2)) return attempt2;

            return Path.GetFullPath(configuredPath);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(RagChatService));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                _context?.Dispose();
                _inferenceLock.Dispose();
                _logger.LogDebug("RagChatService disposed.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Dispose error: {ex.Message}");
            }
            GC.SuppressFinalize(this);
        }
    }
}