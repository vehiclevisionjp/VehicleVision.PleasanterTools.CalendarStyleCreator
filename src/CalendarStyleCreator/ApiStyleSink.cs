using System.Text.Json;
using System.Text.Json.Nodes;
using VehicleVision.PleasanterTools.DotNetClient;

namespace VehicleVision.PleasanterTools.CalendarStyleCreator;

/// <summary>
/// Pleasanter の Extensions API（/api/extensions/get・create・{id}/update）へ拡張スタイルとして登録する（API 登録方式）。
/// 同名の拡張スタイルがあれば更新し、なければ作成する（重複作成しない）。内容が同じなら何もしない。
/// 必要な条件（本体 ExtensionsController.cs で確認）: テナントの AllowExtensionsApi、テナント管理者などの権限、ユーザーの API キー。
/// Web API の呼び出しは DotNetClient（NuGet）で行う（PleasanterGateway。API キーは本文の ApiKey で送る）。URL は DotNetClient の検査に揃え、https のみ（http はループバックだけ）、資格情報・クエリ・フラグメントは不可。リダイレクトは辿らない。API キーはメッセージに出さない。
/// テナント: 既定は API キーの持ち主のテナント。TenantId を指定すると本文の TenantId で送る（本体 ExtensionModel.SetByApi）。登録した拡張スタイルは、テナントに関係なく本体全体に適用される（ExtensionInitializer は全テナントの拡張を読む）。
/// 一式（共通部 + 年ごと）を 1 回の一覧取得で突き合わせ、接頭辞で始まる管理対象のうち一式に含まれないものは Prune のときだけ削除する。
/// </summary>
public sealed class ApiStyleSink(string baseUrl, string apiKey, int? tenantId = null, HttpClient? client = null) : IStyleSink
{
    private const string ExtensionType = "Style";

    public static void ValidateBaseUrl(string url)
    {
        // DotNetClient の説明文は日本語のため、コンソールが英語固定の本ツールでは使わず、同じ規則を英語で伝える。
        if (!PleasanterGateway.IsValidBaseUrl(url))
        {
            throw new UserError("Pleasanter URL must be https (http only for localhost, 127.0.0.1 or [::1]) and must not contain credentials, a query or a fragment.");
        }
    }

    public async Task WriteAsync(StyleSet set, CancellationToken cancel)
    {
        ValidateBaseUrl(baseUrl);
        using var gateway = new PleasanterGateway(baseUrl, apiKey, client);
        var existing = await ListAsync(gateway, cancel);
        foreach (var style in set.Styles)
        {
            var matches = existing.Where(e => e.Name == style.Name).ToList();
            if (matches.Count > 1)
            {
                throw new UserError($"Multiple extended styles named '{style.Name}' exist. Remove the duplicates first.");
            }

            var body = BuildBody(style);
            if (matches.Count == 0)
            {
                await PostAsync(gateway, "api/extensions/create", body, cancel);
                RuntimeLog.WriteLine($"Created extended style '{style.Name}'.");
            }
            else if (matches[0].Body == style.Css && matches[0].SettingsEqual(body["ExtensionSettings"]!))
            {
                RuntimeLog.WriteLine($"Extended style '{style.Name}' is unchanged.");
            }
            else
            {
                await PostAsync(gateway, $"api/extensions/{matches[0].Id}/update", body, cancel);
                RuntimeLog.WriteLine($"Updated extended style '{style.Name}' (ExtensionId {matches[0].Id}).");
            }
        }
        foreach (var name in set.Obsolete(existing.Select(e => e.Name)))
        {
            if (!set.Prune)
            {
                RuntimeLog.WriteLine($"WARNING: Obsolete extended style '{name}' remains. Use /prune to delete styles that start with the prefix and are no longer generated.");
                continue;
            }
            foreach (var old in existing.Where(e => e.Name == name))
            {
                await PostAsync(gateway, $"api/extensions/{old.Id}/delete", Envelope(), cancel);
                RuntimeLog.WriteLine($"Deleted obsolete extended style '{name}' (ExtensionId {old.Id}).");
            }
        }
    }

    private JsonObject Envelope()
    {
        var body = new JsonObject();  // ApiKey・ApiVersion は DotNetClient が足す
        if (tenantId != null)
        {
            body["TenantId"] = tenantId;
        }

        return body;
    }

    private JsonObject BuildBody(StyleDefinition style)
    {
        // SiteIdList を省略すると全サイトに適用される（ファイル配置と同じ）。
        var settings = new JsonObject { ["Name"] = style.Name, ["Description"] = style.Description };
        if (style.SiteIds.Count > 0)
        {
            settings["SiteIdList"] = new JsonArray(style.SiteIds.Select(id => (JsonNode)JsonValue.Create(id)).ToArray());
        }

        var body = Envelope();
        body["ExtensionType"] = ExtensionType;
        body["ExtensionName"] = style.Name;
        body["ExtensionSettings"] = settings;
        body["Body"] = style.Css;
        body["Description"] = style.Description;
        body["Disabled"] = false;
        return body;
    }

    private async Task<List<Existing>> ListAsync(PleasanterGateway gateway, CancellationToken cancel)
    {
        var request = Envelope();
        request["View"] = new JsonObject { ["ColumnFilterHash"] = new JsonObject { ["ExtensionType"] = ExtensionType } };
        var response = await PostAsync(gateway, "api/extensions/get", request, cancel);
        var data = response?["Response"]?["Data"] as JsonArray ?? throw new UserError("Unexpected response from the Extensions API.");
        // フィルターに頼らず、種別の完全一致で選ぶ。
        return data.OfType<JsonObject>().Where(o => (string?)o["ExtensionType"] == ExtensionType && (string?)o["ExtensionName"] is { Length: > 0 })
            .Select(o => new Existing((int?)o["ExtensionId"] ?? throw new UserError("Unexpected response from the Extensions API."), (string)o["ExtensionName"]!, (string?)o["Body"], o["ExtensionSettings"])).ToList();
    }

    private static async Task<JsonObject?> PostAsync(PleasanterGateway http, string path, JsonObject body, CancellationToken cancel)
    {
        var response = await http.PostAsync(path, body, cancel);
        // 通信の失敗（接続・タイムアウトなど）は Unknown で返る（DotNetClient）。取り消しだけが例外で伝わる。
        if (response.StatusCode is not { } status)
        {
            throw new UserError("Could not connect to Pleasanter.");
        }

        // 成功は HTTP と本文の StatusCode の両方が 200（本文の補いは PleasanterGateway）。本文のエラー詳細は秘密を含まないが、長いので状態だけを出す。
        if (status == 403)
        {
            throw new UserError("Pleasanter returned 403. Enable AllowExtensionsApi for the tenant and use an API key of a user who can manage extensions.");
        }

        if (response.ErrorKind == PleasanterErrorKind.Unauthorized)
        {
            throw new UserError("Pleasanter returned 401. Check the API key.");
        }

        if (!response.IsSuccess)
        {
            throw new UserError($"The Extensions API returned status {status}{(response.Message is { Length: > 0 and < 200 } message ? ": " + message : "")}.");
        }

        return response.Body as JsonObject;
    }

    private sealed record Existing(int Id, string Name, string? Body, JsonNode? Settings)
    {
        public bool SettingsEqual(JsonNode desired)
        {
            var current = Settings switch { JsonValue v when v.TryGetValue<string>(out var s) => TryParse(s), { } n => n, _ => null };
            return current is JsonObject c && JsonNode.DeepEquals(Normalize(c), Normalize((JsonObject)desired));
        }

        private static JsonNode? TryParse(string text) { try { return JsonNode.Parse(text); } catch (JsonException) { return null; } }

        // 空の一覧や null は「指定なし」と同じ扱いにして比較する。
        private static JsonObject Normalize(JsonObject source)
        {
            var result = new JsonObject();
            foreach (var (key, value) in source.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                if (value is not null && !(value is JsonArray { Count: 0 }))
                {
                    result[key] = value.DeepClone();
                }
            }

            return result;
        }
    }
}
