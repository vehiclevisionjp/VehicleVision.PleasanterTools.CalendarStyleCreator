using System.Text.Json.Nodes;
using VehicleVision.PleasanterTools.DotNetClient;

namespace VehicleVision.PleasanterTools.CalendarStyleCreator;

/// <summary>
/// DotNetClient の <see cref="PleasanterApiClient.PostJsonAsync"/> を包み、本ツールが必要とする補いを足す。
/// 型付きのメソッド（<c>client.Items.*</c>）ではなく <c>PostJsonAsync</c> を使う。型付きは最初の呼び出しの前に版の問い合わせ（DotNetClient #90）を
/// 1 回行い、通信が増えるため。本文（ExtensionSettings など）も JSON のまま扱える。
/// 通信は <see cref="PleasanterHttp.CreateClient"/>（リダイレクトを辿らない・Cookie 無し）の上で行う。API キーは本文に載るため、
/// <c>BaseUrl</c> の検査と秘密を出さない扱いは DotNetClient に任せる。再試行はしない。Retry-After は読まない（本ツールは 1 回きりの実行で、失敗は利用者へ返すため）。
/// </summary>
internal sealed class PleasanterGateway : IDisposable
{
    private readonly HttpClient? _owned;
    private readonly PleasanterApiClient _client;

    public PleasanterGateway(string baseUrl, string apiKey, HttpClient? client = null)
    {
        _owned = client == null ? PleasanterHttp.CreateClient() : null;
        // 版の問い合わせはしない（追加の通信を避ける）。ApiVersion は DotNetClient の既定（1.1）。
        _client = new PleasanterApiClient(client ?? _owned!, new PleasanterOptions { BaseUrl = baseUrl, ApiKey = apiKey, DetectServerVersion = false });
    }

    /// <summary>BaseUrl が DotNetClient の検査（https のみ・ループバックだけ http・資格情報／クエリ／フラグメント不可）を満たすか。</summary>
    public static bool IsValidBaseUrl(string url)
    {
        try
        {
            new PleasanterOptions { BaseUrl = url, ApiKey = "x" }.Validate();
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// 呼び出し側の取り消し以外の通信の失敗は、例外にならず <see cref="PleasanterErrorKind.Unknown"/>（StatusCode なし）で返る。
    /// HTTP が 200 でも本文の <c>StatusCode</c> が 200 以外なら、DotNetClient 0.3.0（#95）が失敗として分類する
    /// （<see cref="PleasanterResponse.ApplicationStatusCode"/>。<see cref="PleasanterResponse.StatusCode"/> は HTTP のまま）。
    /// </summary>
    public Task<PleasanterResponse> PostAsync(string path, JsonObject body, CancellationToken cancel)
        => _client.PostJsonAsync(path, body, cancel);

    public void Dispose() => _owned?.Dispose();
}
