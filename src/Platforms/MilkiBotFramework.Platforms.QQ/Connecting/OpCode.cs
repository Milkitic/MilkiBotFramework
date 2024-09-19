namespace MilkiBotFramework.Platforms.QQ.Connecting;

// ReSharper disable InconsistentNaming
internal enum OpCode
{
    Dispatch = 0,
    Heartbeat = 1,
    Identify = 2,
    Resume = 6,
    Reconnect = 7,
    InvalidSession = 9,
    Hello = 10,
    HeartbeatACK = 11,
    HTTPCallbackACK = 12
}