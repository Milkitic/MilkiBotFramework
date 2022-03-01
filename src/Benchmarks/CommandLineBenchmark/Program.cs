using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Running;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Plugining.CommandLine;
using MilkiBotFramework.Utils;

namespace CommandLineBenchmark
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<UrlEncodeTest>();
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
    public class UrlEncodeTest
    {
        private Dictionary<string, string> _lines;

        [GlobalSetup]
        public void Setup()
        {
            var lines = File.ReadAllLines("passwords.txt");
            var count = lines.Length - lines.Length % 2;
            var dic = new Dictionary<string, string>();
            for (int i = 0; i < count; i += 2)
            {
                var line1 = lines[i];
                var line2 = lines[i + 1];
                dic.Add(line1, line2);
            }

            _lines = dic;
        }

        [Benchmark(Baseline = true)]
        public object New()
        {
            return LightHttpClient.BuildQueries(_lines);
        }

        [Benchmark]
        public object Old()
        {
            return _lines.ToUrlParamString();
        }
    }
}
