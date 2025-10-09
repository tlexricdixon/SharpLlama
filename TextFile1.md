# SharpLlama – Working Reference Overview

## 1. Primary Functional Areas
- API Layer: `EmployeeQueryController`
  - Structured query endpoint (`GET /api/EmployeeQuery/structured`)
  - Hybrid endpoint (`POST /api/EmployeeQuery/hybrid`) → structured first, optional RAG fallback.
  - Uses defensive validation, cancellation handling, and logs elapsed time.
- Structured Answering: `StructuredEmployeeQueryService` (not shown here, but consumed).
- RAG / Generative Fallback: `IRagChatService` + ingestion pipeline (`EmployeeRagIngestionService` builds indexed documents from employees).
- Input Validation: `InputValidationPlugin` (Semantic Kernel function) sanitizes and rejects unsafe input patterns.
- Observability Config: `OtlpOptions` storing OTLP endpoints for metrics and traces.
- Benchmarks: `LlamaContextBenchmarks` (tokenization + context creation micro-benchmarks).
- Tests: `SharpLlama.Tests` project includes xUnit, FluentAssertions, Moq, WebApplicationFactory, Bogus for data generation.

## 2. Notable Implementation Traits
- Modern C# (primary constructor controllers, records).
- Explicit cancellation → maps to HTTP 499 (non-standard but intentional).
- Anonymous response objects (flexible but weakly typed for clients).
- RAG ingestion builds deterministic “employee profile” documents with light enrichment (basic order stats).
- Benchmark project loads model once; reuses context for tokenization; creates separate context in a dedicated benchmark.
- OpenTelemetry packages present; `OtlpOptions` suggests configurable collector endpoints (HTTP 4318 metrics, gRPC 4317 traces defaults reversed—see Improvements).

## 3. Strengths
- Clear separation: structured vs generative.
- Safe failure mode (empty string → `404` instead of hallucinated content).
- Validation plugin positioned early (prevents wasteful downstream calls).
- Benchmarks encourage performance discipline.
- Observability stack groundwork is in place (OTel packages + options).
- Uses SHA-256 hashing for document integrity / change detection in ingestion.

## 4. Gaps / Risks
| Area | Observation | Risk |
|------|-------------|------|
| Response Contracts | Anonymous objects per action | Harder to version / test / document |
| Logging | String interpolation; limited structured fields | Reduced queryability in logs |
| OTLP Options | Port usage reversed vs conventional defaults (metrics usually 4318 HTTP, traces 4317 gRPC) but endpoints both typed as `[Url]` strings; may mask misconfig | Silent misrouting or transport mismatch |
| Validation Plugin | Async method does only sync work; partial snippet suggests more logic below; no CT support | Harder to extend / cancel |
| Security / PII | Ingestion emits phones/emails directly | Potential leakage if downstream prompt escapes |
| Benchmark Scope | Only context creation + tokenize; no end-to-end generation or warm vs cold | Incomplete perf picture |
| Cancellation Mapping | 499 is non-standard | Some clients/tools may not recognize semantics |
| Duplication | Question length checks repeated | Harder to maintain |
| Observability Depth | No span attributes around decision branch (structured vs RAG) | Weaker trace analysis |
| Hashing | SHA-256 okay; no salting or normalization of whitespace prior | Duplicate near-identical docs if whitespace differs |

## 5. Recommended Enhancements (Prioritized)
1. Contract Hardening
   - Introduce `EmployeeQueryResponse` record and reuse across endpoints.
   - Add `ProblemDetails.type` URIs (e.g., `https://docs/errors/validation`).
2. Validation Consolidation
   - Private helper or minimal filter: `(bool isValid, IActionResult? error) ValidateQuestion(string? q)`.
3. Logging / Telemetry
   - Structured logging: `_logger.LogInfo("Hybrid start {Len} {Fallback}", len, includeFallback);`
   - Add OpenTelemetry activity tags: `Activity.Current?.SetTag("query.source", "structured|rag");`
4. Observability Config
   - Separate `OtlpOptions` sections: `MetricsEndpoint` vs `TracesEndpoint` with protocol clarity (HTTP vs gRPC).
   - Validate scheme (e.g., disallow `http://` for production traces if TLS required).
5. Security / PII Controls
   - Add masking policy (e.g., redact phone/email unless caller authorized).
   - Provide `IncludeSensitive` flag in ingestion or retrieval.
6. Fallback Path Resiliency
   - Apply Polly (timeout + circuit breaker) around `_ragChatService.SendAsync`.
   - Soft degrade with `source = "rag-fallback-error"` + partial diagnostic when circuit open.
7. Caching
   - Memory cache structured answers (normalized key: lowercase trimmed question).
   - Consider semantic cache for RAG: hash of normalized question → last answer + timestamp.
8. Streaming UX
   - Add `/api/EmployeeQuery/hybrid/stream` using Server-Sent Events if model supports incremental tokens.
9. Benchmark Expansion
   - Add generation latency benchmark (prompt → tokens).
   - Parametrize context size (`[Params(512, 1024)]`) and message length instead of multiple `[Arguments]`.
10. Testing
    - Controller tests for: structured hit, structured miss + fallback disabled, structured miss + fallback unavailable, cancellation token.
    - Validation plugin tests: boundary length, disallowed patterns, script stripping.
11. Ingestion Robustness
    - Normalize whitespace before hashing (`Regex.Replace(text, @"\s+", " ").Trim()`).
    - Add version marker line (e.g., `SchemaVersion: 1`).
12. Error Surface
    - Replace `ex.Message` logging with full exception object to retain stack trace (`LogError(ex, "...")` if interface expanded).
13. Rate Limiting Granularity
    - Distinct policy for hybrid endpoint (higher cost).
14. Options Binding
    - Bind `OtlpOptions` via `services.AddOptions<OtlpOptions>().Bind(Configuration.GetSection("Otlp"));` + `ValidateOnStart();`

## 6. Sample Refactors

### Shared Response DTO