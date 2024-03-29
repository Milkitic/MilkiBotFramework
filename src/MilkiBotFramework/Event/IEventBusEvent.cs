﻿using System.Text.Json;

namespace MilkiBotFramework.Event
{
    /// <summary>
    /// 空接口限制，以增加维护性
    /// </summary>
    public interface IEventBusEvent
    {
        // ReSharper disable once ReturnTypeCanBeNotNullable
        public string? ToString() => JsonSerializer.Serialize(this);
    }
}