namespace VehicleVision.PleasanterTools.CalendarStyleCreator;

/// <summary>
/// Pleasanter の Extensions API（/api/extensions/create・{id}/update）へ登録する（API 登録方式）。
/// TODO: 同名の拡張を get で探して更新、なければ作成する（重複作成しない）。
/// 本体の ExtensionApiModel は ExtensionType="Style"・ExtensionName・ExtensionSettings・Body を持つ。
/// 認証・AllowExtensionsApi・必要な権限は一次情報で確認してから実装する。
/// </summary>
public sealed class ApiStyleSink(string baseUrl, string apiKey) : IStyleSink
{
    public string BaseUrl { get; } = baseUrl;
    public string ApiKey { get; } = apiKey;

    public Task WriteAsync(StyleDefinition style, CancellationToken cancel) =>
        throw new UserError("Extensions API registration is not implemented yet. Use generate.");
}
