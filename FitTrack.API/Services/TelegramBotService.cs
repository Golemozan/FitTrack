using System.Text;
using System.Text.Json.Nodes;
using FitTrack.API.Data;
using FitTrack.API.Models;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.API.Services;

/// <summary>
/// Simple Telegram polling bot. Runs as a hosted service so it starts with the app,
/// owns its own scope lifecycle, and doesn't depend on controller instances.
/// </summary>
public class TelegramBotService : BackgroundService
{
    private readonly IServiceScopeFactory _scope;
    private readonly IConfiguration _cfg;
    private readonly ILogger<TelegramBotService> _log;
    private const string Model = "claude-haiku-4-5-20251001";
    private const string AnthropicUrl = "https://api.anthropic.com/v1/messages";

    public TelegramBotService(IServiceScopeFactory scope, IConfiguration cfg, ILogger<TelegramBotService> log)
    {
        _scope = scope;
        _cfg = cfg;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var token = _cfg["Telegram:BotToken"] ?? "";
        if (string.IsNullOrEmpty(token))
        {
            _log.LogWarning("Telegram bot token not configured. Bot will not start.");
            return;
        }

        var apiKey = _cfg["Anthropic:ApiKey"] ?? "";
        if (string.IsNullOrEmpty(apiKey))
        {
            _log.LogWarning("Anthropic API key not configured. Bot will not start.");
            return;
        }

        // Acknowledge any pending updates so we start fresh.
        using var http = new HttpClient();
        try
        {
            await http.GetAsync($"https://api.telegram.org/bot{token}/deleteWebhook?drop_pending_updates=true", ct);
        }
        catch { }

        long lastId = 0;

        _log.LogInformation("Telegram bot polling started.");

        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var c = new HttpClient { Timeout = TimeSpan.FromSeconds(35) };
                var resp = await c.GetAsync(
                    $"https://api.telegram.org/bot{token}/getUpdates?timeout=30&offset={lastId + 1}", ct);
                var body = await resp.Content.ReadAsStringAsync(ct);

                var root = JsonNode.Parse(body);
                if ((bool?)root?["ok"] != true) { await Task.Delay(2000, ct); continue; }

                var updates = root["result"]?.AsArray();
                if (updates is null) { await Task.Delay(1000, ct); continue; }

                foreach (var upd in updates)
                {
                    var updateId = (long?)upd?["update_id"] ?? 0;
                    if (updateId > lastId) lastId = updateId;

                    try
                    {
                        await HandleUpdate(token, apiKey, upd, ct);
                    }
                    catch (Exception ex)
                    {
                        _log.LogError(ex, "Error handling update {Id}", updateId);
                    }
                }
            }
            catch (TaskCanceledException) { break; }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Polling error. Retrying in 5s...");
                await Task.Delay(5000, ct);
            }
        }
    }

    async Task HandleUpdate(string token, string apiKey, JsonNode? upd, CancellationToken ct)
    {
        var msg = upd?["message"];
        var text = (string?)msg?["text"];
        var chatId = (long?)msg?["chat"]?["id"];

        if (string.IsNullOrEmpty(text) || chatId is null) return;
        if (text.StartsWith("/start"))
        {
            await SendTelegram(token, chatId.Value, "Selam Ozan! Ben koçun. 🏋️\n\nBana ne yediğini, nasıl hissettiğini, kilonu söyle. Hepsini takip ederim.\n\nTest et: \"300 gram tavuk yedim\" yaz.");
            return;
        }
        if (text.StartsWith("/")) return;

        // Typing indicator
        using var http = new HttpClient();
        await http.GetAsync($"https://api.telegram.org/bot{token}/sendChatAction?chat_id={chatId}&action=typing", ct);

        using var scope = _scope.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Save user message
        db.CoachMessages.Add(new CoachMessageRecord
        {
            Id = Guid.NewGuid(),
            Role = "user",
            Content = text,
            CreatedAt = DateTime.Now
        });
        await db.SaveChangesAsync(ct);

        // Build system prompt
        var system = await BuildPrompt(db);
        var since = DateTime.Now.Date.AddDays(-2);
        var history = await db.CoachMessages
            .Where(m => m.CreatedAt >= since)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);

        var messages = new JsonArray();
        foreach (var h in history.TakeLast(20))
            messages.Add(new JsonObject { ["role"] = h.Role, ["content"] = h.Content });

        // Agentic loop
        string reply = "";

        using var ai = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

        for (var turn = 0; turn < 6; turn++)
        {
            var payload = new JsonObject
            {
                ["model"] = Model,
                ["max_tokens"] = 1024,
                ["system"] = system,
                ["tools"] = Tools(),
                ["messages"] = messages.DeepClone(),
            };

            var (ok, responseBody) = await CallClaude(ai, apiKey, payload);
            if (!ok) { await SendTelegram(token, chatId.Value, "Koç şu an cevap veremedi."); return; }

            JsonNode? r;
            try { r = JsonNode.Parse(responseBody); } catch { return; }

            var content = r?["content"]?.AsArray() ?? new JsonArray();
            var stop = (string?)r?["stop_reason"];

            reply = string.Concat(content
                .Where(b => (string?)b?["type"] == "text")
                .Select(b => (string?)b?["text"] ?? ""));

            if (stop != "tool_use") break;

            // Echo assistant turn + execute tools
            messages.Add(new JsonObject { ["role"] = "assistant", ["content"] = content.DeepClone() });

            var toolResults = new JsonArray();
            foreach (var b in content)
            {
                if ((string?)b?["type"] != "tool_use") continue;
                var toolId = (string?)b?["id"] ?? "";
                var toolName = (string?)b?["name"] ?? "";
                var (result, _, _) = await ExecuteTool(db, toolName, b["input"]);
                toolResults.Add(new JsonObject
                {
                    ["type"] = "tool_result",
                    ["tool_use_id"] = toolId,
                    ["content"] = result
                });
            }
            messages.Add(new JsonObject { ["role"] = "user", ["content"] = toolResults });
        }

        if (!string.IsNullOrWhiteSpace(reply))
        {
            db.CoachMessages.Add(new CoachMessageRecord
            {
                Id = Guid.NewGuid(),
                Role = "assistant",
                Content = reply.Trim(),
                CreatedAt = DateTime.Now
            });
            await db.SaveChangesAsync(ct);

            await SendTelegram(token, chatId.Value, reply.Trim());
        }
    }

    async Task SendTelegram(string token, long chat, string text)
    {
        try
        {
            if (text.Length > 4000) text = text[..4000];
            using var http = new HttpClient();
            var body = new JsonObject { ["chat_id"] = chat, ["text"] = text }.ToJsonString();
            await http.PostAsync(
                $"https://api.telegram.org/bot{token}/sendMessage",
                new StringContent(body, Encoding.UTF8, "application/json"));
        }
        catch (Exception ex) { _log.LogError(ex, "Send telegram failed."); }
    }

    async Task<(bool, string)> CallClaude(HttpClient http, string key, JsonObject payload)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, AnthropicUrl);
            req.Headers.Add("x-api-key", key);
            req.Headers.Add("anthropic-version", "2023-06-01");
            req.Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");

            var resp = await http.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();
            return (resp.IsSuccessStatusCode, body);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Claude call failed.");
            return (false, "");
        }
    }

    static JsonArray Tools() => new()
    {
        new JsonObject
        {
            ["name"] = "log_food",
            ["description"] = "Ozan bir şey yediğini söylediğinde besini ekle. Makroları tahmin et. calories/protein/carbs/fat TOPLAM değer. Öğünü saate göre seç.",
            ["input_schema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["foodName"] = new JsonObject { ["type"] = "string" },
                    ["grams"] = new JsonObject { ["type"] = "number" },
                    ["calories"] = new JsonObject { ["type"] = "number" },
                    ["protein"] = new JsonObject { ["type"] = "number" },
                    ["carbs"] = new JsonObject { ["type"] = "number" },
                    ["fat"] = new JsonObject { ["type"] = "number" },
                    ["mealType"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray { "Breakfast", "Lunch", "Dinner", "Snack" } }
                },
                ["required"] = new JsonArray { "foodName", "grams", "calories", "protein", "carbs", "fat", "mealType" }
            }
        },
        new JsonObject
        {
            ["name"] = "log_weight",
            ["description"] = "Ozan kilosunu söylediğinde kaydet.",
            ["input_schema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["weightKg"] = new JsonObject { ["type"] = "number" },
                    ["notes"] = new JsonObject { ["type"] = "string" }
                },
                ["required"] = new JsonArray { "weightKg" }
            }
        },
        new JsonObject
        {
            ["name"] = "log_checkin",
            ["description"] = "Ozan ruh hali, enerji veya açlık durumunu belirttiğinde check-in kaydet. Açlık: 1=çok aç, 5=çok tok.",
            ["input_schema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["mood"] = new JsonObject { ["type"] = "integer" },
                    ["energy"] = new JsonObject { ["type"] = "integer" },
                    ["hunger"] = new JsonObject { ["type"] = "integer" },
                    ["note"] = new JsonObject { ["type"] = "string" },
                    ["context"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray { "general", "pre-workout", "post-workout" } }
                },
                ["required"] = new JsonArray { "mood", "energy", "hunger" }
            }
        }
    };

    async Task<(string result, string? domain, bool isError)> ExecuteTool(AppDbContext db, string name, JsonNode? inp)
    {
        try
        {
            switch (name)
            {
                case "log_food":
                    var fe = new MealEntry
                    {
                        Id = Guid.NewGuid(),
                        FoodName = Str(inp, "foodName"),
                        Grams = Num(inp, "grams"),
                        Calories = Num(inp, "calories"),
                        Protein = Num(inp, "protein"),
                        Carbs = Num(inp, "carbs"),
                        Fat = Num(inp, "fat"),
                        MealType = Enum.TryParse<MealType>(Str(inp, "mealType"), out var mt) ? mt : MealType.Snack,
                        LoggedAt = DateTime.Now
                    };
                    db.MealEntries.Add(fe);
                    await db.SaveChangesAsync();
                    return ($"✓ {fe.FoodName} {fe.Grams:0}g ({fe.Calories:0} kcal, P{fe.Protein:0} K{fe.Carbs:0} Y{fe.Fat:0})", "nutrition", false);

                case "log_weight":
                    var day = DateTime.Now.Date;
                    var kg = Num(inp, "weightKg");
                    var nt = Str(inp, "notes");
                    var ex = await db.WeightLogs.FirstOrDefaultAsync(w => w.LoggedAt >= day && w.LoggedAt < day.AddDays(1));
                    if (ex != null)
                    {
                        ex.WeightKg = kg;
                        if (!string.IsNullOrEmpty(nt)) ex.Notes = nt;
                    }
                    else
                    {
                        db.WeightLogs.Add(new WeightLog { Id = Guid.NewGuid(), WeightKg = kg, Notes = string.IsNullOrEmpty(nt) ? null : nt, LoggedAt = DateTime.Now });
                    }
                    await db.SaveChangesAsync();
                    return ($"✓ Kilo: {kg:0.0} kg", "weight", false);

                case "log_checkin":
                    var ci = new CheckIn
                    {
                        Id = Guid.NewGuid(),
                        Mood = Clamp((int)Num(inp, "mood"), 1, 5),
                        Energy = Clamp((int)Num(inp, "energy"), 1, 5),
                        Hunger = Clamp((int)Num(inp, "hunger"), 1, 5),
                        Note = Str(inp, "note"),
                        Context = string.IsNullOrEmpty(Str(inp, "context")) ? "general" : Str(inp, "context"),
                        LoggedAt = DateTime.Now
                    };
                    db.CheckIns.Add(ci);
                    await db.SaveChangesAsync();
                    return ($"✓ Check-in: Ruh {ci.Mood}, Enerji {ci.Energy}, Açlık {ci.Hunger}", "checkin", false);

                default:
                    return ("Bilinmeyen araç", null, true);
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Tool execution failed: {Name}", name);
            return ("Hata oluştu", null, true);
        }
    }

    static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;

    static double Num(JsonNode? n, string k)
    {
        var v = n?[k];
        if (v is null) return 0;
        try { return v.GetValue<double>(); }
        catch
        {
            return double.TryParse(v.ToString(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0;
        }
    }

    static string Str(JsonNode? n, string k)
    {
        var v = n?[k];
        if (v is null) return "";
        try { return v.GetValue<string>(); } catch { return v?.ToString() ?? ""; }
    }

    async Task<string> BuildPrompt(AppDbContext db)
    {
        var day = DateTime.Now.Date;
        var goals = await db.UserGoals.FirstOrDefaultAsync();
        var profile = await db.Profiles.FirstOrDefaultAsync();
        var meals = await db.MealEntries.Where(m => m.LoggedAt >= day && m.LoggedAt < day.AddDays(1)).ToListAsync();
        var weights = await db.WeightLogs.OrderBy(w => w.LoggedAt).ToListAsync();
        double? cw = weights.Count > 0 ? weights[^1].WeightKg : null;
        double? sw = weights.Count > 0 ? weights[0].WeightKg : null;

        var sb = new StringBuilder();
        sb.AppendLine("Ozan'ın Telegram koçusun. KISA konuş (max 2-3 cümle). Emoji YOK. 'Harika/süper' YOK. Sadece veri.");
        sb.AppendLine("Yemek söylerse → log_food. Kilo → log_weight. Ruh hali/enerji/açlık → log_checkin. Direkt yap, onay sorma.");
        sb.AppendLine($"Saat: {DateTime.Now:HH:mm}.");

        if (goals != null)
            sb.AppendLine($"Hedef: {goals.CalorieGoal:0}kcal P{goals.ProteinGoal:0} K{goals.CarbGoal:0} Y{goals.FatGoal:0}");
        sb.AppendLine($"Bugün: {meals.Sum(m => m.Calories):0}kcal P{meals.Sum(m => m.Protein):0} K{meals.Sum(m => m.Carbs):0} Y{meals.Sum(m => m.Fat):0}");

        if (weights.Count > 0)
        {
            sb.Append($"Kilo: {cw:0.0}kg (baş: {sw:0.0}, Δ{cw - sw:+0.0;-0.0})");
            if (weights.Count > 1) sb.Append($" [{weights.Count} kayıt]");
            sb.AppendLine();
            foreach (var w in weights.TakeLast(10))
                sb.AppendLine($"  {w.LoggedAt:dd.MM}: {w.WeightKg:0.0}kg");
        }

        if (profile?.HeightCm > 0)
        {
            var bmi = cw / ((profile.HeightCm.Value / 100) * (profile.HeightCm.Value / 100));
            sb.AppendLine($"Boy: {profile.HeightCm:0}cm Hedef: {profile.TargetWeightKg:0.0}kg BMI: {bmi:0.0}");
        }

        return sb.ToString();
    }
}
