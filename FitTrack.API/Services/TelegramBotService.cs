using System.Globalization;
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
    private const string Model = "claude-sonnet-5";
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
                ["max_tokens"] = 2048,
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
            ["description"] = "Ozan bir şey yediğinde besini günlüğe ekle. Makro tahmini için sistem promptundaki BESLENME REFERANSI tablosunu kullan. calories/protein/carbs/fat TOPLAM tüketilen miktar için. Öğünü saate göre seç. date: YYYY-MM-DD, boşsa bugün.",
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
                    ["mealType"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray { "Breakfast", "Lunch", "Dinner", "Snack" } },
                    ["date"] = new JsonObject { ["type"] = "string", ["description"] = "YYYY-MM-DD formatında tarih. Boşsa bugün." }
                },
                ["required"] = new JsonArray { "foodName", "grams", "calories", "protein", "carbs", "fat", "mealType" }
            }
        },
        new JsonObject
        {
            ["name"] = "log_weight",
            ["description"] = "Ozan kilosunu söylediğinde kaydet. Aynı gün varsa üstüne yazar.",
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
        },
        new JsonObject
        {
            ["name"] = "list_meals",
            ["description"] = "Yemek listesini getir. date: YYYY-MM-DD, boşsa bugün. Geçmiş günler için date belirt.",
            ["input_schema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["date"] = new JsonObject { ["type"] = "string", ["description"] = "YYYY-MM-DD formatında tarih. Boşsa bugün." }
                },
                ["required"] = new JsonArray(),
            }
        },
        new JsonObject
        {
            ["name"] = "delete_meal",
            ["description"] = "Öğünü ID'sine göre sil. Ozan 'sil', 'çıkar', 'kaldır' derse KULLAN. ÖNCE list_meals ile ID'leri gör.",
            ["input_schema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["mealId"] = new JsonObject { ["type"] = "string", ["description"] = "Silinecek öğünün GUID ID'si" }
                },
                ["required"] = new JsonArray { "mealId" }
            }
        },
        new JsonObject
        {
            ["name"] = "edit_meal",
            ["description"] = "Var olan öğünü ID'sine göre düzenle. 'düzelt', 'değiştir', 'aslında ... gramdı' derse KULLAN. ÖNCE list_meals ile ID'yi bul.",
            ["input_schema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["mealId"] = new JsonObject { ["type"] = "string", ["description"] = "Düzenlenecek öğünün GUID ID'si" },
                    ["foodName"] = new JsonObject { ["type"] = "string" },
                    ["grams"] = new JsonObject { ["type"] = "number" },
                    ["calories"] = new JsonObject { ["type"] = "number" },
                    ["protein"] = new JsonObject { ["type"] = "number" },
                    ["carbs"] = new JsonObject { ["type"] = "number" },
                    ["fat"] = new JsonObject { ["type"] = "number" },
                    ["mealType"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray { "Breakfast", "Lunch", "Dinner", "Snack" } }
                },
                ["required"] = new JsonArray { "mealId", "foodName", "grams", "calories", "protein", "carbs", "fat", "mealType" }
            }
        },
        new JsonObject
        {
            ["name"] = "get_nutrition_history",
            ["description"] = "Son N günün günlük kalori/makro özetini getir. Ozan 'bu hafta nasıldı', 'son 1 ayda ne kadar yedim', 'trend nasıl' derse KULLAN. Her gün için tarih, toplam kalori, P/K/Y, öğün sayısı döner.",
            ["input_schema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["days"] = new JsonObject { ["type"] = "integer", ["description"] = "Kaç günlük geçmiş (varsayılan 7, maks 90)" }
                },
                ["required"] = new JsonArray()
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
                    var date = ParseDateTg(Str(inp, "date")) ?? DateTime.Now;
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
                        LoggedAt = date
                    };
                    db.MealEntries.Add(fe);
                    await db.SaveChangesAsync();
                    var dateLabel = date.Date == DateTime.Now.Date ? "" : $" ({date:dd.MM})";
                    return ($"✓{dateLabel} {fe.FoodName} {fe.Grams:0}g ({fe.Calories:0} kcal, P{fe.Protein:0} K{fe.Carbs:0} Y{fe.Fat:0})", "nutrition", false);

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

                case "list_meals":
                    var tgDate = ParseDateTg(Str(inp, "date")) ?? DateTime.Now;
                    var today = tgDate.Date;
                    var allMeals = await db.MealEntries
                        .Where(m => m.LoggedAt >= today && m.LoggedAt < today.AddDays(1))
                        .OrderBy(m => m.LoggedAt)
                        .ToListAsync();
                    var tgDateLabel = today == DateTime.Now.Date ? "Bugün" : today.ToString("dd.MM.yyyy");
                    if (allMeals.Count == 0)
                        return ($"{tgDateLabel} yemek kaydı yok.", null, false);
                    var lines = allMeals.Select(m =>
                        $"ID:{m.Id} [{m.MealType}] {m.FoodName} {m.Grams:0}g = {m.Calories:0} kcal (P{m.Protein:0}/K{m.Carbs:0}/Y{m.Fat:0}) {m.LoggedAt:HH:mm}");
                    var total = $"TOPLAM: {allMeals.Sum(m => m.Calories):0} kcal, P{allMeals.Sum(m => m.Protein):0} K{allMeals.Sum(m => m.Carbs):0} Y{allMeals.Sum(m => m.Fat):0}";
                    return ($"{tgDateLabel}:\n{string.Join("\n", lines)}\n{total}", null, false);

                case "delete_meal":
                    var delId = Str(inp, "mealId");
                    if (!Guid.TryParse(delId, out var delGuid))
                        return ($"Geçersiz ID: {delId}. Önce list_meals ile ID'leri gör.", null, true);
                    var delMeal = await db.MealEntries.FindAsync(delGuid);
                    if (delMeal is null)
                        return ($"ID:{delId} bulunamadı.", null, true);
                    var delDesc = $"{delMeal.FoodName} {delMeal.Grams:0}g ({delMeal.Calories:0} kcal)";
                    db.MealEntries.Remove(delMeal);
                    await db.SaveChangesAsync();
                    return ($"✓ Silindi: {delDesc}.", "nutrition", false);

                case "edit_meal":
                    var editId = Str(inp, "mealId");
                    if (!Guid.TryParse(editId, out var editGuid))
                        return ($"Geçersiz ID: {editId}. Önce list_meals ile ID'leri gör.", null, true);
                    var editMeal = await db.MealEntries.FindAsync(editGuid);
                    if (editMeal is null)
                        return ($"ID:{editId} bulunamadı.", null, true);
                    var oldDesc = $"{editMeal.FoodName} {editMeal.Grams:0}g ({editMeal.Calories:0} kcal)";
                    editMeal.FoodName = Str(inp, "foodName");
                    editMeal.Grams = Num(inp, "grams");
                    editMeal.Calories = Num(inp, "calories");
                    editMeal.Protein = Num(inp, "protein");
                    editMeal.Carbs = Num(inp, "carbs");
                    editMeal.Fat = Num(inp, "fat");
                    editMeal.MealType = Enum.TryParse<MealType>(Str(inp, "mealType"), out var editMt) ? editMt : MealType.Snack;
                    await db.SaveChangesAsync();
                    var newDesc = $"{editMeal.FoodName} {editMeal.Grams:0}g = {editMeal.Calories:0} kcal (P{editMeal.Protein:0}/K{editMeal.Carbs:0}/Y{editMeal.Fat:0})";
                    return ($"✓ Güncellendi: [{oldDesc}] → [{newDesc}].", "nutrition", false);

                case "get_nutrition_history":
                    var hDays = (int)Math.Clamp(Num(inp, "days"), 1, 90);
                    if (hDays == 0) hDays = 7;
                    var hSince = DateTime.Now.Date.AddDays(-hDays + 1);
                    var hEntries = await db.MealEntries.Where(m => m.LoggedAt >= hSince).ToListAsync();
                    var hDaily = hEntries.GroupBy(m => m.LoggedAt.Date).OrderBy(g => g.Key)
                        .Select(g => new { Date = g.Key.ToString("dd.MM.yyyy"), Cal = g.Sum(m => m.Calories), P = g.Sum(m => m.Protein), C = g.Sum(m => m.Carbs), F = g.Sum(m => m.Fat), Cnt = g.Count() })
                        .ToList();
                    if (hDaily.Count == 0) return ($"Son {hDays} günde yemek kaydı yok.", null, false);
                    var hLines = hDaily.Select(d => $"{d.Date}: {d.Cal:0} kcal, P{d.P:0} K{d.C:0} Y{d.F:0} ({d.Cnt} öğün)");
                    var hAvg = hDaily.Average(d => d.Cal);
                    return ($"Son {hDays} gün:\n{string.Join("\n", hLines)}\n\nGünlük ortalama: {hAvg:0} kcal.", null, false);

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

    static DateTime? ParseDateTg(string dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr)) return null;
        if (DateTime.TryParseExact(dateStr.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return d;
        return null;
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

        // Weekly comparison
        var weekAgo = day.AddDays(-7);
        var twoWeeksAgo = day.AddDays(-14);
        var last7Days = await db.MealEntries.Where(m => m.LoggedAt >= weekAgo && m.LoggedAt < day).ToListAsync();
        var prev7Days = await db.MealEntries.Where(m => m.LoggedAt >= twoWeeksAgo && m.LoggedAt < weekAgo).ToListAsync();
        var thisWeekAvg = last7Days.Count > 0 ? last7Days.Sum(m => m.Calories) / 7.0 : 0;
        var prevWeekAvg = prev7Days.Count > 0 ? prev7Days.Sum(m => m.Calories) / 7.0 : 0;

        var sb = new StringBuilder();

        // Kimlik + domain bilgisi (Telegram'a uygun compact)
        sb.AppendLine("Ozan'ın fitness koçusun. Spor bilimleri ve beslenme biyokimyası deneyimlisin.");
        sb.AppendLine("Doğal Türkçe konuş. Direkt ve net ol. Gerekirse detaylı analiz yap.");
        sb.AppendLine("SELAMLAŞMA YOK. \"Selam Ozan\", \"Ben Koç\" gibi girişler YAPMA. Konuya direkt gir.");
        sb.AppendLine("Veriye dayalı, bilimsel, ölçülü. Emoji YOK. Aşırı övgü YOK.");
        sb.AppendLine("Ozan bir şey yediğini söylerse HEMEN log_food ile kaydet, onay sorma.");
        sb.AppendLine();

        // Araç talimatları (6 tool)
        sb.AppendLine("== ARAÇLAR ==");
        sb.AppendLine("Yemek → log_food (makroları beslenme referans tablosundan tahmin et). Kilo → log_weight. Ruh hali/enerji/açlık → log_checkin. Direkt yap, onay sorma.");
        sb.AppendLine("Düzeltme/silme: ASLA log_food çağırma. ÖNCE list_meals → sonra delete_meal/edit_meal.");
        sb.AppendLine("Ozan sayısal veri verdiğinde (kalori, gram, kilo) HESAPLAMA YAP ve sonucu göster. Yarım bırakma.");
        sb.AppendLine($"Saat: {DateTime.Now:HH:mm}.");
        sb.AppendLine();

        // Beslenme referansı (compact)
        sb.AppendLine("== BESLENME REFERANSI (100g başına) ==");
        sb.AppendLine("Tavuk göğsü:165kcal P31 K0 Y3.5 | Kırmızı et:250 P26 K0 Y17 | Kıyma(%15):220 P24 K0 Y13");
        sb.AppendLine("Yumurta(1ad):78 P6.3 K0.6 Y5.3 | Peynir beyaz:270 P17 K1 Y22 | Yoğurt:65 P3.5 K4.5 Y3.5");
        sb.AppendLine("Pirinç pilavı:130 P2.7 K28 Y0.3 | Bulgur:115 P3.5 K23 Y0.5 | Makarna(p):130 P5 K25 Y0.5");
        sb.AppendLine("Ekmek(1dil):65 P2 K13 Y1 | Simit(1):420 P10 K60 Y15 | Mercimek çorba:130 P7 K20 Y2.5");
        sb.AppendLine("Zeytinyağı(1yk):120 P0 K0 Y13.5 | Tereyağı(1yk):105 P0 K0 Y12 | Kuruyemiş:600 P20 K15 Y55");
        sb.AppendLine("Baklava(150g):450 P8 K50 Y25 | Döner(350g):550 P35 K30 Y30 | Lahmacun(1):280 P10 K40 Y9");
        sb.AppendLine("1g P=4 K=4 Y=9 kcal. Bilmediğin besinde en yakın benzeri baz al.");
        sb.AppendLine();

        // Antrenman bilgisi (compact)
        sb.AppendLine("== ANTRENMAN ==");
        sb.AppendLine("Hacim=set×kg×tekrar. Progressive overload: haftada %2-5 artış. Büyük kas 48-72h, küçük 24-48h dinlenme.");
        sb.AppendLine("Kalori açığı+antrenman=kas kaybı riski → protein 1.6-2.2g/kg korur. Antrenman sonrası 2h içinde 20-40g protein.");
        sb.AppendLine();

        // Veri okuma (compact)
        sb.AppendLine("== ANALİZ ==");
        sb.AppendLine("Kilo: günlük±1kg normal, haftalık ortalama trend gösterir. Sağlıklı kayıp:0.5-1kg/hafta. Açık:300-500kcal.");
        sb.AppendLine("Düşük enerji+düşük kalori=yetersiz beslenme. Anormallik varsa sorgula (3kg/gün, 3000kcal/öğün).");
        sb.AppendLine();

        // Canlı veri
        if (meals.Count > 0)
            sb.AppendLine($"Bugün: {meals.Sum(m => m.Calories):0}kcal P{meals.Sum(m => m.Protein):0} K{meals.Sum(m => m.Carbs):0} Y{meals.Sum(m => m.Fat):0}");
        if (goals != null)
            sb.AppendLine($"Hedef: {goals.CalorieGoal:0}kcal P{goals.ProteinGoal:0} K{goals.CarbGoal:0} Y{goals.FatGoal:0}");

        // Weekly calorie summary
        if (last7Days.Count > 0)
        {
            var dailyCal = last7Days.GroupBy(m => m.LoggedAt.Date).OrderBy(g => g.Key)
                .Select(g => $"{g.Key:dd.MM}:{g.Sum(m => m.Calories):0}kcal").ToList();
            sb.AppendLine($"Son 7 gün: {string.Join(" ", dailyCal)}");
            sb.AppendLine($"Bu hafta ort: {thisWeekAvg:0} kcal/gün | Geçen hafta ort: {prevWeekAvg:0} kcal/gün | Değişim: {(prevWeekAvg > 0 ? (thisWeekAvg - prevWeekAvg) / prevWeekAvg * 100 : 0):+0;-0;0}%");
        }

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
