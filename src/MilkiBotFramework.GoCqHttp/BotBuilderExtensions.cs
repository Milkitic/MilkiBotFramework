using System;
using MilkiBotFramework.GoCqHttp.Connecting;
using MilkiBotFramework.GoCqHttp.ContractsManaging;
using MilkiBotFramework.GoCqHttp.Dispatching;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Messaging.RichMessages;
using MilkiBotFramework.Plugining.CommandLine;

namespace MilkiBotFramework.GoCqHttp
{
    public static class BotBuilderExtensions
    {
        public static BotBuilder UseGoCqHttp(this BotBuilder builder, string uri)
        {
            return builder
                .UseConnector<GoCqWsClient>(uri)
                .UseDispatcher<GoCqDispatcher>()
                .UseCommandLineAnalyzer<CommandLineAnalyzer>(new GoCqParameterConverter())
                .UseRichMessageConverter<GoCqMessageConverter>()
                .UseContractsManager<GoCqContractsManager>()
                .UseMessageApi<GoCqApi>();
        }
    }

    public class GoCqMessageConverter : IRichMessageConverter
    {
        public string Encode(IRichMessage message)
        {
            throw new NotImplementedException();
        }

        public RichMessage Decode(ReadOnlyMemory<char> message)
        {
            throw new NotImplementedException();
        }
    }
}
