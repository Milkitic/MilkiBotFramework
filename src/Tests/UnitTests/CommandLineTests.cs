using System;
using System.Text.Json;
using MilkiBotFramework.Plugining.CommandLine;
using Xunit;
using Xunit.Abstractions;

namespace UnitTests
{
    public class CommandLineTests
    {
        private readonly ITestOutputHelper _outputHelper;

        public CommandLineTests(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
        }

        [Theory]
        [InlineData("test")]
        [InlineData("test asdf")]
        [InlineData("test -o  -a asdf")]
        [InlineData("test  -a asdf")]
        [InlineData("recent:3 30")]
        [InlineData(" test:1 -option   [2]  -wow -what  234 -hehe \"tt:t ttadfv\"  125 fdgdsahf \"114514 191980:\" -heihei:3 -sbsb ")]
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
    }
}
