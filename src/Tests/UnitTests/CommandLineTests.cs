// ReSharper disable All
#pragma warning disable CS1998
#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MilkiBotFramework;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Plugining.CommandLine;
using MilkiBotFramework.Plugining;
using MilkiBotFramework.Plugining.Loading;
using MilkiBotFramework.Messaging;
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
        [Obsolete("Obsolete")]
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
        [InlineData("/help -app")]
        [InlineData("/help --app")]
        public void LongOptionCanBeReadAndIsRenderedWithDoubleDash(string command)
        {
            var result = Analyze(command);
            var options = GetOptions(result);

            Assert.Equal("help", result.Command.Value.ToString());
            Assert.True(options.ContainsKey("app"));
            Assert.Equal("help --app", result.ToString());
        }

        [Fact]
        public void ShortOptionCanReadValue()
        {
            var result = Analyze("/test -o value");
            var options = GetOptions(result);

            Assert.Equal("value", options["o"]);
            Assert.Empty(result.Arguments);
        }

        [Fact]
        public void ShortOptionGroupIsKeptRawByAnalyzer()
        {
            var result = Analyze("/test -abc next");
            var options = GetOptions(result);

            Assert.True(options.ContainsKey("abc"));
            Assert.Contains("next", result.Arguments.Select(k => k.ToString()));
        }

        [Fact]
        public void ShortOptionGroupExpandsOnlyWhenEveryItemIsBoolShortOption()
        {
            var options = NormalizeOptions(
                RawOptions(("abc", null)),
                Option("a", typeof(bool), false),
                Option("b", typeof(bool), false),
                Option("c", typeof(bool), false));

            Assert.Equal(new[] { "a", "b", "c" }, options.Keys.OrderBy(k => k).ToArray());
        }

        [Fact]
        public void ShortOptionGroupDoesNotExpandWhenAnyItemIsNotBoolShortOption()
        {
            var options = NormalizeOptions(
                RawOptions(("abc", null)),
                Option("a", typeof(bool), false),
                Option("b", typeof(string), null),
                Option("c", typeof(bool), false));

            Assert.Equal(new[] { "abc" }, options.Keys.ToArray());
        }

        [Fact]
        public void NegativeNumbersAreArgumentsOrOptionValues()
        {
            var result = Analyze("/test -1 -2 -o -3");
            var options = GetOptions(result);

            Assert.Equal(new[] { "-1", "-2" }, result.Arguments.Select(k => k.ToString()).ToArray());
            Assert.Equal("-3", options["o"]);
        }

        [Fact]
        public void QuotedArgumentKeepsSpaces()
        {
            var result = Analyze("/test \"hello world\" 'osu mania'");

            Assert.Equal(new[] { "hello world", "osu mania" }, result.Arguments.Select(k => k.ToString()).ToArray());
        }

        [Fact]
        public void UnclosedQuoteReturnsCommandLineException()
        {
            var analyzer = new CommandLineAnalyzer(new BotOptions());

            var success = analyzer.TryAnalyze("/test \"hello", out var result, out var exception);

            Assert.False(success);
            Assert.Null(result);
            Assert.NotNull(exception);
            Assert.Contains("Unclosed quote", exception.Message);
        }

        [Fact]
        public void DuplicateOptionReturnsCommandLineException()
        {
            var analyzer = new CommandLineAnalyzer(new BotOptions());

            var success = analyzer.TryAnalyze("/help --app --app", out var result, out var exception);

            Assert.False(success);
            Assert.Null(result);
            Assert.NotNull(exception);
            Assert.Contains("Duplicate option", exception.Message);
        }

        [Fact]
        public void TerminatorStopsOptionParsing()
        {
            var result = Analyze("/test -- -abc --name");

            Assert.Empty(result.Options);
            Assert.Equal(new[] { "-abc", "--name" }, result.Arguments.Select(k => k.ToString()).ToArray());
        }

        [Fact]
        public void RecentColonSyntaxRemainsPartOfCommandName()
        {
            var result = Analyze("/recent:3 30");

            Assert.Equal("recent:3", result.Command.Value.ToString());
            Assert.Equal(new[] { "30" }, result.Arguments.Select(k => k.ToString()).ToArray());
        }

        private static CommandLineResult Analyze(string command)
        {
            var analyzer = new CommandLineAnalyzer(new BotOptions());
            var success = analyzer.TryAnalyze(command, out var result, out var exception);
            Assert.True(success, exception?.Message);
            Assert.NotNull(result);
            return result;
        }

        private static Dictionary<string, string> GetOptions(CommandLineResult result)
        {
            return result.Options.ToDictionary(k => k.Key.ToString(), k => k.Value?.ToString());
        }

        private static Dictionary<ReadOnlyMemory<char>, ReadOnlyMemory<char>?> RawOptions(
            params (string Key, string Value)[] options)
        {
            return options.ToDictionary(k => k.Key.AsMemory(), k => k.Value?.AsMemory());
        }

        private static Dictionary<string, ReadOnlyMemory<char>?> NormalizeOptions(
            Dictionary<ReadOnlyMemory<char>, ReadOnlyMemory<char>?> rawOptions,
            params CommandParameterInfo[] parameterInfos)
        {
            var method = typeof(CommandInjector).GetMethod("CreateOptionDictionary",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);

            var commandInfo = new CommandInfo("test",
                "",
                typeof(CommandLineTests).GetMethod(nameof(DummyCommand), BindingFlags.NonPublic | BindingFlags.Static),
                CommandReturnType.Void,
                MessageAuthority.Public,
                MessageType.Private | MessageType.Channel,
                parameterInfos);

            try
            {
                return (Dictionary<string, ReadOnlyMemory<char>?>)method.Invoke(null, new object[] { commandInfo, rawOptions, parameterInfos });
            }
            catch (TargetInvocationException ex)
            {
                throw ex.InnerException ?? ex;
            }
        }

        private static CommandParameterInfo Option(string name, Type type, object defaultValue)
        {
            var parameterInfo = new CommandParameterInfo();
            Set(parameterInfo, nameof(CommandParameterInfo.Name), name);
            Set(parameterInfo, nameof(CommandParameterInfo.ParameterName), name);
            Set(parameterInfo, nameof(CommandParameterInfo.ParameterType), type);
            Set(parameterInfo, nameof(CommandParameterInfo.DefaultValue), defaultValue);
            Set(parameterInfo, nameof(CommandParameterInfo.IsArgument), false);
            Set(parameterInfo, nameof(CommandParameterInfo.IsServiceArgument), false);
            Set(parameterInfo, nameof(CommandParameterInfo.ValueConverter), DefaultParameterConverter.Instance);
            return parameterInfo;
        }

        private static void Set(CommandParameterInfo parameterInfo, string name, object value)
        {
            typeof(CommandParameterInfo).GetProperty(name)!.SetValue(parameterInfo, value);
        }

        private static void DummyCommand()
        {
        }
    }
}