using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace VehicleVision.PleasanterTools.CalendarStyleCreator;

/// <summary>
/// Pleasanter の Extensions API（/api/extensions/get・create・{id}/update）へ拡張スタイルとして登録する（API 登録方式）。
/// 同名の拡張スタイルがあれば更新し、なければ作成する（重複作成しない）。内容が同じなら何もしない。
/// 必要な条件（本体 ExtensionsController.cs で確認）: テナントの AllowExtensionsApi、テナント管理者などの権限、ユーザーの API キー。
/// API キーは本文の ApiKey で送る。https のみ許可（ループバックの検証用だけ http 可）。API キーはメッセージに出さない。
/// </summary>
public sealed class ApiStyleSink(string baseUrl, string apiKey, HttpClient? client = null) : IStyleSink
{
    public const double ApiVersion = 1.1;
    private const string ExtensionType = "Style";

    public static void ValidateBaseUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) throw new UserError("Pleasanter URL must be an absolute URL.");
        if (uri.Scheme != Uri.UriSchemeHttps && !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)) throw new UserError("Pleasanter URL must be https (http is allowed only for localhost).");
    }

    public async Task WriteAsync(StyleDefinition style, CancellationToken cancel)
    {
        ValidateBaseUrl(baseUrl);
        using var owned = client == null ? new HttpClient { Timeout = TimeSpan.FromSeconds(30) } : null;
        var http = client ?? owned!;
        var existing = await FindAsync(http, style.Name, cancel);
        var body = BuildBody(style);
        if (existing == null)
        {
            await PostAsync(http, "api/extensions/create", body, cancel);
            RuntimeLog.WriteLine($"Created extended style '{style.Name}'.");
        }
        else if (existing.Body == style.Css && existing.SettingsEqual(body["ExtensionSettings"]!))
        {
            RuntimeLog.WriteLine($"Extended style '{style.Name}' is unchanged.");
        }
        else
        {
            await PostAsync(http, $"api/extensions/{existing.Id}/update", body, cancel);
            RuntimeLog.WriteLine($"Updated extended style '{style.Name}' (ExtensionId {existing.Id}).");
        }
    }

    private JsonObject BuildBody(StyleDefinition style)
    {
        // SiteIdList を省略すると全サイトに適用される（ファイル配置と同じ）。
        var settings = new JsonObject { ["Name"] = style.Name, ["Description"] = style.Description };
        if (style.SiteIds.Count > 0) settings["SiteIdList"] = new JsonArray(style.SiteIds.Select(id => (JsonNode)JsonValue.Create(id)).ToArray());
        return new JsonObject
        {
            ["ApiVersion"] = ApiVersion,
            ["ApiKey"] = apiKey,
            ["ExtensionType"] = ExtensionType,
            ["ExtensionName"] = style.Name,
            ["ExtensionSettings"] = settings,
            ["Body"] = style.Css,
            ["Description"] = style.Description,
            ["Disabled"] = false,
        };
    }

    private async Task<Existing?> FindAsync(HttpClient http, string name, CancellationToken cancel)
    {
        var request = new JsonObject
        {
            ["ApiVersion"] = ApiVersion,
            ["ApiKey"] = apiKey,
            ["View"] = new JsonObject { ["ColumnFilterHash"] = new JsonObject { ["ExtensionType"] = ExtensionType } },
        };
        var response = await PostAsync(http, "api/extensions/get", request, cancel);
        var data = response?["Response"]?["Data"] as JsonArray ?? throw new UserError("Unexpected response from the Extensions API.");
        // フィルターに頼らず、種別と名前の完全一致で選ぶ。
        var matches = data.OfType<JsonObject>().Where(o => (string?)o["ExtensionType"] == ExtensionType && (string?)o["ExtensionName"] == name).ToList();
        if (matches.Count > 1) throw new UserError($"Multiple extended styles named '{name}' exist. Remove the duplicates first.");
        if (matches.Count == 0) return null;
        return new Existing((int?)matches[0]["ExtensionId"] ?? throw new UserError("Unexpected response from the Extensions API."), (string?)matches[0]["Body"], matches[0]["ExtensionSettings"]);
    }

    private async Task<JsonObject?> PostAsync(HttpClient http, string path, JsonObject body, CancellationToken cancel)
    {
        using var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        HttpResponseMessage response;
        try { response = await http.PostAsync(new Uri(new Uri(baseUrl.TrimEnd('/') + "/"), path), content, cancel); }
        catch (HttpRequestException) { throw new UserError("Could not connect to Pleasanter."); }
        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(cancel);
            JsonObject? json = null;
            try { json = JsonNode.Parse(text) as JsonObject; } catch (JsonException) { }
            // 成功は HTTP と本文の StatusCode の両方が 200。本文のエラー詳細は秘密を含まないが、長いので状態だけを出す。
            var status = (int?)json?["StatusCode"] ?? (int)response.StatusCode;
            if (status == 403) throw new UserError("Pleasanter returned 403. Enable AllowExtensionsApi for the tenant and use an API key of a user who can manage extensions.");
            if (status == 401) throw new UserError("Pleasanter returned 401. Check the API key.");
            if (!response.IsSuccessStatusCode || status != 200) throw new UserError($"The Extensions API returned status {status}{(((string?)json?["Message"]) is { Length: > 0 and < 200 } message ? ": " + message : "")}.");
            return json;
        }
    }

    private sealed record Existing(int Id, string? Body, JsonNode? Settings)
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
                if (value is not null && !(value is JsonArray { Count: 0 })) result[key] = value.DeepClone();
            return result;
        }
    }
}
