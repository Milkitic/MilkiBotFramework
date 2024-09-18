using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia.Media;
using Avalonia.Platform;
using SkiaSharp;

namespace MilkiBotFramework.Imaging.Avalonia;

public class LocalFontManager
{
    private const string DefaultLocale = "en-US";
    private readonly Dictionary<string, Dictionary<string, string>> _localeTypefaces = new();

    private LocalFontManager()
    {
    }

    public static LocalFontManager Instance { get; } = new();

    public string ResourceBaseUri { get; set; } = "avares://MilkiBotFramework.Imaging.Avalonia/";

    public void InitializeTypefaceMapping()
    {
        //var path = "avares://KanonBot/Assets/Fonts/";
        var path = ResourceBaseUri;
        var rootUri = new Uri(path);
        var uris = AssetLoader.GetAssets(rootUri, null);

        foreach (var uri in uris)
        {
            var relativePath = rootUri.MakeRelativeUri(uri);
            var locale = Path.GetDirectoryName(relativePath.ToString()) ?? "en-US";
            if (!_localeTypefaces.TryGetValue(locale, out var dictionary))
            {
                dictionary = new Dictionary<string, string>();
                _localeTypefaces.Add(locale, dictionary);
            }
            using var stream = AssetLoader.Open(uri);
            using var typeface = SKTypeface.FromStream(stream);
            dictionary.Add(uri.ToString(), typeface.FamilyName);
        }
    }

    public FontFamily GetFontFamily(string? locale = DefaultLocale)
    {
        locale ??= DefaultLocale;
        var sort = _localeTypefaces
            .OrderBy(k => k.Key, LocaleComparer.Instance)
            .ToDictionary(k => k.Key, k => k.Value);
        var sb = new StringBuilder();
        if (sort.TryGetValue(DefaultLocale, out var baseLocale))
        {
            AppendFontFamiliesNew(baseLocale, sb);
            sort.Remove(DefaultLocale);
        }

        if (sort.TryGetValue(locale, out var mainLocale))
        {
            AppendFontFamiliesNew(mainLocale, sb);
            sort.Remove(locale);
        }

        //foreach (var keyValuePair in sort.Values)
        //{
        //    AppendFontFamiliesNew(keyValuePair, sb);
        //}

        var fonts = sb.Length == 0 ? "default" : sb.ToString(0, sb.Length - 1);
        var fontFamily = new FontFamily(fonts);
        return fontFamily;
    }

    private void AppendFontFamiliesNew(Dictionary<string, string> baseLocale, StringBuilder sb)
    {
        var dirDict = baseLocale.Select(k =>
        {
            var uri = new Uri(k.Key);
            var subDir = Path.GetDirectoryName(uri.AbsolutePath)?.Replace(Path.DirectorySeparatorChar, '/');
            return new KeyValuePair<string, string>($"{uri.Scheme}://{uri.Authority}{subDir}", k.Value);
        }).Distinct();

        foreach (var kvp in dirDict)
        {
            sb.Append(kvp.Key);
            sb.Append('#');
            sb.Append(kvp.Value);
            sb.Append(',');
        }
    }

    private static void AppendFontFamilies(Dictionary<string, string> dict, StringBuilder sb)
    {
        foreach (var kvp in dict)
        {
            var dir = new Uri(kvp.Key);

            sb.Append(dir.Scheme + "://" + dir.Authority + Path.GetDirectoryName(dir.AbsolutePath));
            sb.Append('#');
            sb.Append(kvp.Value);
            sb.Append(',');
        }
    }
}