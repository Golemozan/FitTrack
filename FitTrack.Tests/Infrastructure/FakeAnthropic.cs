using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace FitTrack.Tests.Infrastructure;

/// <summary>
/// Anthropic API'nin yerine geçen sahte uç. Gerçek ağa hiç çıkılmaz.
/// - <c>/v1/models</c>: <see cref="ValidKeys"/> içindeki anahtara 200, diğerlerine 401.
/// - <c>/v1/messages</c>: <see cref="RevokedKeys"/> 401 alır; diğerleri <see cref="Respond"/>'un ürettiği cevabı.
/// Her çağrı hangi anahtarla yapıldıysa <see cref="Calls"/>'a yazılır — "B'nin isteği A'nın anahtarıyla gitti mi" sorusu buradan cevaplanır.
/// </summary>
public class FakeAnthropic : HttpMessageHandler
{
    public const string KeyA = "sk-ant-test-AAAAAAAAAAAAAAAAAAAAAAAAAAAA-a1a1";
    public const string KeyB = "sk-ant-test-BBBBBBBBBBBBBBBBBBBBBBBBBBBB-b2b2";

    public ConcurrentBag<string> ValidKeys { get; } = new() { KeyA, KeyB };
    public ConcurrentBag<string> RevokedKeys { get; } = new();
    public ConcurrentQueue<(string Path, string Key, JsonNode? Body)> Calls { get; } = new();

    /// <summary>Varsayılan: tek metin cevabı. Testler araç çağrısı senaryosu için değiştirir.</summary>
    public Func<JsonNode, (HttpStatusCode, JsonObject)> Respond { get; set; } = _ => Text("Koç cevabı");

    public bool Unreachable { get; set; }

    /// <summary>Telegram'a gönderilen mesajlar (sendMessage). Telegram API'si de bu sahte uçtan geçer.</summary>
    public ConcurrentQueue<(long ChatId, string Text)> TelegramSent { get; } = new();

    public IEnumerable<string> SentTo(long chatId) => TelegramSent.Where(m => m.ChatId == chatId).Select(m => m.Text);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (request.RequestUri?.Host == "api.telegram.org")
        {
            if (request.RequestUri.AbsolutePath.EndsWith("/sendMessage") && request.Content is not null)
            {
                var msg = JsonNode.Parse(await request.Content.ReadAsStringAsync(ct))!;
                TelegramSent.Enqueue(((long)msg["chat_id"]!, (string)msg["text"]!));
            }
            return Json(HttpStatusCode.OK, new JsonObject { ["ok"] = true, ["result"] = new JsonArray() });
        }

        if (request.RequestUri?.Host != "api.anthropic.com")
            return new HttpResponseMessage(HttpStatusCode.NotFound);

        if (Unreachable) throw new HttpRequestException("simulated network failure");

        var key = request.Headers.TryGetValues("x-api-key", out var v) ? v.Single() : "";
        var body = request.Content is null ? null : JsonNode.Parse(await request.Content.ReadAsStringAsync(ct));
        Calls.Enqueue((request.RequestUri.AbsolutePath, key, body));

        if (request.RequestUri.AbsolutePath == "/v1/models")
            return ValidKeys.Contains(key)
                ? Json(HttpStatusCode.OK, new JsonObject { ["data"] = new JsonArray() })
                : Json(HttpStatusCode.Unauthorized, Error("authentication_error"));

        if (request.RequestUri.AbsolutePath == "/v1/messages")
        {
            if (RevokedKeys.Contains(key) || !ValidKeys.Contains(key))
                return Json(HttpStatusCode.Unauthorized, Error("authentication_error"));
            var (status, payload) = Respond(body!);
            return Json(status, payload);
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    public static (HttpStatusCode, JsonObject) Text(string text) => (HttpStatusCode.OK, new JsonObject
    {
        ["stop_reason"] = "end_turn",
        ["content"] = new JsonArray { new JsonObject { ["type"] = "text", ["text"] = text } },
    });

    public static (HttpStatusCode, JsonObject) ToolUse(string name, JsonObject input) => (HttpStatusCode.OK, new JsonObject
    {
        ["stop_reason"] = "tool_use",
        ["content"] = new JsonArray
        {
            new JsonObject { ["type"] = "tool_use", ["id"] = "toolu_" + Guid.NewGuid().ToString("N"), ["name"] = name, ["input"] = input },
        },
    });

    private static JsonObject Error(string type) => new() { ["type"] = "error", ["error"] = new JsonObject { ["type"] = type } };

    private static HttpResponseMessage Json(HttpStatusCode status, JsonNode node) =>
        new(status) { Content = new StringContent(node.ToJsonString(), Encoding.UTF8, "application/json") };
}
