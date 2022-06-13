// ReSharper disable All
#pragma warning disable CS1998
#nullable disable

using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Running;
using MilkiBotFramework.Plugining.CommandLine;

namespace CommandLineBenchmark
{
    internal class Program
    {
        static void Main(string[] args)
        {
            _ = BenchmarkRunner.Run<Benchmarks>();
            //var summary = BenchmarkRunner.Run<UrlEncodeTest>();
        }
    }

    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [MemoryDiagnoser]
    [SimpleJob(RuntimeMoniker.Net60)]
    public class GeneralTask
    {
        private string _command;

        [GlobalSetup]
        public void Setup()
        {
            _command = " test:1 -option   [2]  -wow -what  234 -hehe \"tt:t ttadfv\"  125 fdgdsahf \"114514 191980:\" -heihei:3 -sbsb ";
        }

        [Benchmark]
        [Obsolete("Obsolete")]
        public object OldAnalyzer()
        {
            var a = new StreamCommandLineAnalyzer();
            a.TryAnalyze(_command, out var result, out _);
            return result;
        }

        [Benchmark(Baseline = true)]
        public object NewAnalyzer()
        {
            var a = new CommandLineAnalyzer();
            a.TryAnalyze(_command, out var result, out _);
            return result;
        }

    }
}
