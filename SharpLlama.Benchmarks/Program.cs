using BenchmarkDotNet.Running;

namespace SharpLlama.Benchmarks;

public class Program
{
    public static void Main(string[] args)
    {
        var summary = BenchmarkRunner.Run<ChatServiceBenchmarks>();
        Console.WriteLine(summary);
    }
}