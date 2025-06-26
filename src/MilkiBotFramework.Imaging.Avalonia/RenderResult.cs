using System;
using System.Buffers;
using Avalonia;
using Avalonia.Platform;

namespace MilkiBotFramework.Imaging.Avalonia;

public readonly struct RenderResult(
    byte[] buffer,
    int length,
    PixelSize pixelSize,
    PixelFormat pixelFormat) : IDisposable
{
    public byte[] Buffer { get; } = buffer;
    public int Length { get; } = length;
    public PixelSize PixelSize { get; } = pixelSize;
    public PixelFormat PixelFormat { get; } = pixelFormat;

    public void Dispose()
    {
        ArrayPool<byte>.Shared.Return(Buffer);
    }
}