using System.ComponentModel;

namespace MilkiBotFramework.Platforms.Mock;

/// <summary>
///     Mock 平台配置选项
/// </summary>
public class MockBotOptions : BotOptions
{
    [Description("Mock 平台配置")]
    public MockPlatformConfig Config { get; set; } = new();
}

/// <summary>
///     Mock 平台配置详情
/// </summary>
public class MockPlatformConfig
{
    /// <summary>
    ///     Bot 自身的用户ID
    /// </summary>
    public string BotUserId { get; set; } = "mock_bot_001";

    /// <summary>
    ///     Bot 的用户名称
    /// </summary>
    public string BotUserName { get; set; } = "Mock Bot";

    /// <summary>
    ///     虚拟群组ID
    /// </summary>
    public string GroupId { get; set; } = "mock_group_001";

    /// <summary>
    ///     虚拟群组名称
    /// </summary>
    public string GroupName { get; set; } = "Mock Test Group";

    /// <summary>
    ///     虚拟用户ID
    /// </summary>
    public string UserId { get; set; } = "mock_user_001";

    /// <summary>
    ///     虚拟用户名称
    /// </summary>
    public string UserName { get; set; } = "Mock User";
}