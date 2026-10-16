using System.Text.Encodings.Web;
using System.Text.Json;

namespace VehicleVision.PleasanterTools.CalendarStyleCreator;

/// <summary>
/// 本体の App_Data/Parameters/ExtendedStyles/{Name}.json として書き出す（ファイル配置方式）。
/// プロパティは ExtendedBase / ExtendedStyle（Name・Description・SiteIdList・Style）に合わせる。
/// </summary>
public sealed class FileStyleSink(string directory) : IStyleSink
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public async Task WriteAsync(StyleDefinition style, CancellationToken cancel)
    {
        if (style.Name.Length == 0 || style.Name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw new UserError("Style name is not a valid file name.");
        Directory.CreateDirectory(directory);
        var document = new Dictionary<string, object?>
        {
            ["Name"] = style.Name,
            ["Description"] = style.Description,
            ["SiteIdList"] = style.SiteIds,
            ["Style"] = style.Css,
        };
        // SiteIdList が空の場合は全サイトに適用されるため、空のときはキーごと省略して意図を明確にする。
        if (style.SiteIds.Count == 0) document.Remove("SiteIdList");
        var path = Path.Combine(directory, style.Name + ".json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(document, JsonOptions) + "\n", cancel);
        RuntimeLog.WriteLine($"Wrote {path}");
    }
}
