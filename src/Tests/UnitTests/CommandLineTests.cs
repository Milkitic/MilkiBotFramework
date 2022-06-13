using System;
using System.Collections.Generic;
using System.IO;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Plugining.CommandLine;
using MilkiBotFramework.Utils;
using Xunit;
using Xunit.Abstractions;

namespace UnitTests
{
    public class UriEncodingTests
    {
        private readonly ITestOutputHelper _outputHelper;
        private readonly Dictionary<string, string> _lines;

        public UriEncodingTests(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
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

        [Fact]
        public void Test1()
        {
            var str1 = _lines.ToUrlParamString();
            var str2 = LightHttpClient.BuildQueries(_lines);

            Assert.Equal(str1, str2, StringComparer.OrdinalIgnoreCase);
        }
    }


    public class CommandLineTests
    {
        private readonly ITestOutputHelper _outputHelper;

        public CommandLineTests(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
        }

        [Theory]
        [InlineData("/test")]
        [InlineData("/test asdf asffghd")]
        [InlineData("/test asdf")]
        [InlineData("/test -o  -a asdf")]
        [InlineData("/test  -a asdf")]
        [InlineData("/recent:3 30")]
        [InlineData(" /test:1 -option   [2]  -wow -what  234 -hehe \"tt:t ttadfv\"  125 fdgdsahf \"114514 191980:\" -heihei:3 -sbsb ")]
        public void Test1(string command)
        {
            var b = new CommandLineAnalyzer();
            b.TryAnalyze(command, out var result2, out _);
            var a = new StreamCommandLineAnalyzer();
            a.TryAnalyze(command, out var result, out _);

            var json1 = result.ToString();
            var json2 = result2.ToString();

            _outputHelper.WriteLine("result1: " + json1);
            _outputHelper.WriteLine("result2: " + json2);

            Assert.Equal(json1, json2);
        }

        [Theory]
        [InlineData("/test -test -3+-3 asdf 24123 -haha")]
        public void TestNe(string command)
        {
            var a = new StreamCommandLineAnalyzer();
            a.TryAnalyze(command, out var result, out _);
            var b = new CommandLineAnalyzer();
            b.TryAnalyze(command, out var result2, out _);

            var json1 = result.ToString();
            var json2 = result2.ToString();

            _outputHelper.WriteLine("result1: " + json1);
            _outputHelper.WriteLine("result2: " + json2);

            Assert.NotEqual(json1, json2);
        }
    }
}
