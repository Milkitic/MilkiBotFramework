using System;
using System.Text.Json;
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
            var summary = BenchmarkRunner.Run<GeneralTask2>();
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

    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [MemoryDiagnoser]
    [SimpleJob(RuntimeMoniker.Net60)]
    public class GeneralTask2
    {
        private Type _taskType;

        [GlobalSetup]
        public void Setup()
        {
            _taskType = typeof(GeneralTask);
        }

        [Benchmark(Baseline = true)]
        public object TypeOfCall()
        {
            return typeof(GeneralTask);
        }

        [Benchmark]
        public object StaticCall()
        {
            return _taskType;
        }

        [Benchmark]
        public object GetTypeCall()
        {
            return GetType();
        }
    }
}
