using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace VehicleVision.PleasanterTools.CalendarStyleCreator;

/// <summary>
/// Pleasanter の Extensions API（/api/extensions/get・create・{id}/update）へ拡張スタイルとして登録する（API 登録方式）。
/// 同名の拡張スタイルがあれば更新し、なければ作成する（重複作成しない）。内容が同じなら何もしない。
/// 必要な条件（本体 ExtensionsController.cs で確認）: テナントの AllowExtensionsApi、テナント管理者などの権限、ユーザーの API キー。
/// API キーは本文の ApiKey で送る。Pleasanter の API 自体に https の制限はないため http も許可するが、API キーが平文で流れるので警告を出す。API キーはメッセージに出さない。
/// テナント: 既定は API キーの持ち主のテナント。TenantId を指定すると本文の TenantId で送る（本体 ExtensionModel.SetByApi）。登録した拡張スタイルは、テナントに関係なく本体全体に適用される（ExtensionInitializer は全テナントの拡張を読む）。
/// 一式（共通部 + 年ごと）を 1 回の一覧取得で突き合わせ、接頭辞で始まる管理対象のうち一式に含まれないものは Prune のときだけ削除する。
/// </summary>
public sealed class ApiStyleSink(string baseUrl, string apiKey, int? tenantId = null, HttpClient? client = null) : IStyleSink
{
    public const double ApiVersion = 1.1;
    private const string ExtensionType = "Style";

    public static void ValidateBaseUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) throw new UserError("Pleasanter URL must be an absolute http or https URL.");
    }

    public async Task WriteAsync(StyleSet set, CancellationToken cancel)
    {
        ValidateBaseUrl(baseUrl);
        if (new Uri(baseUrl).Scheme == Uri.UriSchemeHttp) RuntimeLog.WriteLine("WARNING: The API key is sent over http. Use https unless the network is trusted.");
        using var owned = client == null ? new HttpClient { Timeout = TimeSpan.FromSeconds(30) } : null;
        var http = client ?? owned!;
        var existing = await ListAsync(http, cancel);
        foreach (var style in set.Styles)
        {
            var matches = existing.Where(e => e.Name == style.Name).ToList();
            if (matches.Count > 1) throw new UserError($"Multiple extended styles named '{style.Name}' exist. Remove the duplicates first.");
            var body = BuildBody(style);
            if (matches.Count == 0)
            {
                await PostAsync(http, "api/extensions/create", body, cancel);
                RuntimeLog.WriteLine($"Created extended style '{style.Name}'.");
            }
            else if (matches[0].Body == style.Css && matches[0].SettingsEqual(body["ExtensionSettings"]!))
            {
                RuntimeLog.WriteLine($"Extended style '{style.Name}' is unchanged.");
            }
            else
            {
                await PostAsync(http, $"api/extensions/{matches[0].Id}/update", body, cancel);
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
                await PostAsync(http, $"api/extensions/{old.Id}/delete", Envelope(), cancel);
                RuntimeLog.WriteLine($"Deleted obsolete extended style '{name}' (ExtensionId {old.Id}).");
            }
        }
    }

    private JsonObject Envelope()
    {
        var body = new JsonObject { ["ApiVersion"] = ApiVersion, ["ApiKey"] = apiKey };
        if (tenantId != null) body["TenantId"] = tenantId;
        return body;
    }

    private JsonObject BuildBody(StyleDefinition style)
    {
        // SiteIdList を省略すると全サイトに適用される（ファイル配置と同じ）。
        var settings = new JsonObject { ["Name"] = style.Name, ["Description"] = style.Description };
        if (style.SiteIds.Count > 0) settings["SiteIdList"] = new JsonArray(style.SiteIds.Select(id => (JsonNode)JsonValue.Create(id)).ToArray());
        var body = Envelope();
        body["ExtensionType"] = ExtensionType;
        body["ExtensionName"] = style.Name;
        body["ExtensionSettings"] = settings;
        body["Body"] = style.Css;
        body["Description"] = style.Description;
        body["Disabled"] = false;
        return body;
    }

    private async Task<List<Existing>> ListAsync(HttpClient http, CancellationToken cancel)
    {
        var request = Envelope();
        request["View"] = new JsonObject { ["ColumnFilterHash"] = new JsonObject { ["ExtensionType"] = ExtensionType } };
        var response = await PostAsync(http, "api/extensions/get", request, cancel);
        var data = response?["Response"]?["Data"] as JsonArray ?? throw new UserError("Unexpected response from the Extensions API.");
        // フィルターに頼らず、種別の完全一致で選ぶ。
        return data.OfType<JsonObject>().Where(o => (string?)o["ExtensionType"] == ExtensionType && (string?)o["ExtensionName"] is { Length: > 0 })
            .Select(o => new Existing((int?)o["ExtensionId"] ?? throw new UserError("Unexpected response from the Extensions API."), (string)o["ExtensionName"]!, (string?)o["Body"], o["ExtensionSettings"])).ToList();
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
                if (value is not null && !(value is JsonArray { Count: 0 })) result[key] = value.DeepClone();
            return result;
        }
    }
}
