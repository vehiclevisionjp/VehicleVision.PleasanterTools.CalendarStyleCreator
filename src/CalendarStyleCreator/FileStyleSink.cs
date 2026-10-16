namespace VehicleVision.PleasanterTools.CalendarStyleCreator;

/// <summary>
/// 本体の App_Data/Parameters/ExtendedStyles/{Name}.css として書き出す（ファイル配置方式）。
/// 本体（Initializer.ExtendedStyles）がファイルから読むのは *.css だけで、*.json は読まれない（Pleasanter 1.5.8.1 の実機で確認）。
/// .css は適用サイトなどの指定を持てず全ページに適用されるため、サイトを限定するときは API 登録を使う。
/// 接頭辞で始まる .css だけを管理対象とし、他のファイルには触れない。
/// </summary>
public sealed class FileStyleSink(string directory) : IStyleSink
{
    public async Task WriteAsync(StyleSet set, CancellationToken cancel)
    {
        foreach (var style in set.Styles)
        {
            if (style.Name.Length == 0 || style.Name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw new UserError("Style name is not a valid file name.");
            if (style.SiteIds.Count > 0) throw new UserError("File placement applies to all sites. Use register to limit the style to specific sites.");
        }
        Directory.CreateDirectory(directory);
        foreach (var style in set.Styles)
        {
            var path = Path.Combine(directory, style.Name + ".css");
            await File.WriteAllTextAsync(path, style.Css, new System.Text.UTF8Encoding(false), cancel);
            RuntimeLog.WriteLine($"Wrote {path}");
        }
        var existing = new DirectoryInfo(directory).GetFiles(set.Prefix + "*.css").Select(f => Path.GetFileNameWithoutExtension(f.Name));
        foreach (var name in set.Obsolete(existing))
        {
            if (set.Prune)
            {
                File.Delete(Path.Combine(directory, name + ".css"));
                RuntimeLog.WriteLine($"Deleted obsolete style '{name}'.");
            }
            else RuntimeLog.WriteLine($"WARNING: Obsolete style '{name}' remains. Use /prune to delete styles that start with the prefix and are no longer generated.");
        }
    }
}
