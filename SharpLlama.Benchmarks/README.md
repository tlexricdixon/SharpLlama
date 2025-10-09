# SharpLlama Benchmarks

This project contains performance benchmarks for the SharpLlama chat service using BenchmarkDotNet.

## Setup

1. **Configure Model Path**: Update the `ModelPath` in `appsettings.json` to point to your LLama model file.
   ```json
   {
     "ModelPath": "path/to/your/llama/model.bin"
   }
   ```

2. **Install Dependencies**: The project automatically references all necessary packages including BenchmarkDotNet.

## Running Benchmarks

### Run All Benchmarks
```bash
dotnet run -c Release
```

### Run Specific Benchmark Class
```bash
dotnet run -c Release -- --filter "*ChatServiceBenchmarks*"
dotnet run -c Release -- --filter "*LlamaContextBenchmarks*"
dotnet run -c Release -- --filter "*ChatHistoryBenchmarks*"
```

### Run Specific Methods
```bash
dotnet run -c Release -- --filter "*StatefulChatService_ShortMessage*"
```

## Benchmark Classes

### ChatServiceBenchmarks
Tests the performance of the main chat services:
- **StatefulChatService**: Tests sending messages of various lengths
- **StatelessChatService**: Tests processing chat history
- **Streaming**: Tests streaming responses

### LlamaContextBenchmarks
Tests low-level LLama operations:
- **Context Creation**: Measures context initialization time
- **Tokenization**: Tests text-to-token conversion performance
- **Detokenization**: Tests token-to-text conversion performance

### ChatHistoryBenchmarks
Tests chat history operations:
- **History Creation**: Measures time to build chat histories
- **History Cloning**: Tests copying chat histories of different sizes
- **Message Filtering**: Tests searching through chat history
- **Token Counting**: Measures content analysis performance

## Output

Benchmarks generate detailed reports including:
- **Mean/Median execution time**
- **Memory allocation tracking**
- **Statistical analysis** (Min/Max/Standard deviation)
- **Multiple export formats** (Markdown, HTML, AsciiDoc)

## Configuration Options

### BenchmarkDotNet Attributes Used:
- `[SimpleJob(RuntimeMoniker.Net90)]`: Runs on .NET 9.0
- `[MemoryDiagnoser]`: Tracks memory allocations
- `[MinColumn, MaxColumn, MeanColumn, MedianColumn]`: Shows statistical columns
- `[MarkdownExporter, HtmlExporter]`: Exports results in multiple formats

### Customization:
- Modify test data in `GlobalSetup()` methods
- Add new benchmark methods with `[Benchmark]` attribute
- Use `[Arguments()]` for parameterized tests
- Add `[Params()]` for testing different parameter sets

## Performance Tips

1. **Run in Release mode** for accurate measurements
2. **Close other applications** to reduce system noise
3. **Run multiple times** to ensure consistent results
4. **Use appropriate model sizes** for your benchmarking needs

## Example Usage in CI/CD

```yaml
- name: Run Benchmarks
  run: |
    cd SharpLlama.Benchmarks
    dotnet run -c Release -- --exporters json
    # Upload results to performance tracking system
```

## Benchmark Results

Results are saved in `BenchmarkDotNet.Artifacts` directory and include:
- Detailed performance reports
- Memory allocation summaries
- Statistical analysis
- Comparison charts (when available)