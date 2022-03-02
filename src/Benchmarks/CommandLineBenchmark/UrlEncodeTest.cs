using System.Collections.Generic;
using System.IO;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Utils;

namespace CommandLineBenchmark;

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