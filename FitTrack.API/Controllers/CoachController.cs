using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using FitTrack.API.Data;
using FitTrack.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.API.Controllers;

[ApiController]
[Route("api/coach")]
public class CoachController : ControllerBase
{
    private const string Model = "claude-sonnet-5";
    private const string AnthropicUrl = "https://api.anthropic.com/v1/messages";

    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<CoachController> _log;

    public CoachController(AppDbContext db, IHttpClientFactory httpFactory, IConfiguration config, ILogger<CoachController> log)
    {
        _db = db;
        _httpFactory = httpFactory;
        _config = config;
        _log = log;
    }

    // GET /api/coach/history → last 3 days of conversation
    [HttpGet("history")]
    public async Task<ActionResult<List<object>>> History()
    {
        var since = DateTime.Now.Date.AddDays(-2);
        var messages = await _db.CoachMessages
            .Where(m => m.CreatedAt >= since)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new { m.Role, m.Content })
            .ToListAsync();
        return Ok(messages);
    }

    // POST /api/coach/chat
    [HttpPost("chat")]
    public async Task<ActionResult<CoachChatResponse>> Chat(CoachChatRequest req)
    {
        var apiKey = _config["Anthropic:ApiKey"] ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            return StatusCode(500, new { error = "Anthropic API anahtarı ayarlı değil (Anthropic:ApiKey)." });

        if (req.Messages.Count == 0)
            return BadRequest(new { error = "Boş mesaj." });

        // Save the user's last message to history.
        var lastUser = req.Messages.LastOrDefault(m => m.Role == "user");
        if (lastUser is not null)
            _db.CoachMessages.Add(new CoachMessageRecord { Id = Guid.NewGuid(), Role = "user", Content = lastUser.Content, CreatedAt = DateTime.Now });

        var system = await BuildSystemPromptAsync();

        // Running conversation as JsonNodes (client sends text-only; tool turns get
        // appended in the loop below).
        var messages = new JsonArray();
        foreach (var m in req.Messages)
        {
            if (m.Role is not ("user" or "assistant") || string.IsNullOrWhiteSpace(m.Content)) continue;
            messages.Add(new JsonObject { ["role"] = m.Role, ["content"] = m.Content });
        }
        if (messages.Count == 0) return BadRequest(new { error = "Geçerli mesaj yok." });

        var actions = new List<string>();
        var reply = "";

        // Agentic loop: call Claude, run any tools it requests, feed results back,
        // repeat until it stops asking for tools (bounded to avoid runaways).
        for (var turn = 0; turn < 6; turn++)
        {
            var payload = new JsonObject
            {
                ["model"] = Model,
                ["max_tokens"] = 4096,
                ["system"] = system,
                ["tools"] = BuildTools(),
                ["messages"] = JsonNode.Parse(messages.ToJsonString()),
            };

            var (ok, body) = await CallAnthropicAsync(apiKey, payload);
            if (!ok) return StatusCode(502, new { error = "Koç yanıt veremedi." });

            JsonNode? root;
            try { root = JsonNode.Parse(body); }
            catch (Exception ex) { _log.LogError(ex, "Parse failed: {Body}", body); return StatusCode(502, new { error = "Koç yanıtı çözümlenemedi." }); }

            var content = root?["content"] as JsonArray ?? new JsonArray();
            var stop = (string?)root?["stop_reason"];

            // Gather any text the model produced this turn.
            reply = string.Concat(content
                .Where(b => (string?)b?["type"] == "text")
                .Select(b => (string?)b?["text"] ?? ""));

            if (stop != "tool_use") break;

            // Echo the assistant's tool_use turn back verbatim, then answer each call.
            messages.Add(new JsonObject { ["role"] = "assistant", ["content"] = JsonNode.Parse(content.ToJsonString()) });

            var toolResults = new JsonArray();
            foreach (var block in content)
            {
                if ((string?)block?["type"] != "tool_use") continue;
                var id = (string?)block!["id"] ?? "";
                var name = (string?)block["name"] ?? "";
                var input = block["input"];
                var (resultText, domain, isError) = await ExecuteToolAsync(name, input);
                if (domain is not null && !isError) actions.Add(domain);
                toolResults.Add(new JsonObject
                {
                    ["type"] = "tool_result",
                    ["tool_use_id"] = id,
                    ["content"] = resultText,
                    ["is_error"] = isError,
                });
            }
            messages.Add(new JsonObject { ["role"] = "user", ["content"] = toolResults });
        }

        // Save coach reply to history.
        if (!string.IsNullOrWhiteSpace(reply))
        {
            _db.CoachMessages.Add(new CoachMessageRecord { Id = Guid.NewGuid(), Role = "assistant", Content = reply.Trim(), CreatedAt = DateTime.Now });
        }
        await _db.SaveChangesAsync();

        return Ok(new CoachChatResponse { Reply = reply.Trim(), Actions = actions.Distinct().ToList() });
    }

    private async Task<(bool ok, string body)> CallAnthropicAsync(string apiKey, JsonObject payload)
    {
        var http = _httpFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(60);
        using var request = new HttpRequestMessage(HttpMethod.Post, AnthropicUrl);
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
        try
        {
            var resp = await http.SendAsync(request);
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode) { _log.LogError("Anthropic {Status}: {Body}", (int)resp.StatusCode, body); return (false, body); }
            return (true, body);
        }
        catch (Exception ex) { _log.LogError(ex, "Anthropic call failed."); return (false, ""); }
    }

    // Tools the coach can call to log data on Ozan's behalf.
    private static JsonArray BuildTools() => new()
    {
        new JsonObject
        {
            ["name"] = "log_food",
            ["description"] = "Ozan bir şey yediğinde besini günlüğe ekle. Makro tahmini için sistem promptundaki BESLENME REFERANSI tablosunu kullan. calories/protein/carbs/fat TOPLAM tüketilen miktar için olmalı (100g başına değil). date: YYYY-MM-DD formatında, boşsa bugün. Ozan 'dün', 'önceki gün', '3 gün önce' gibi geçmiş zaman belirtirse date'i ona göre ata.",
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
                    ["date"] = new JsonObject { ["type"] = "string", ["description"] = "YYYY-MM-DD formatında tarih. Boşsa bugün. Geçmiş gün için kullan." },
                },
                ["required"] = new JsonArray { "foodName", "grams", "calories", "protein", "carbs", "fat", "mealType" },
            },
        },
        new JsonObject
        {
            ["name"] = "log_weight",
            ["description"] = "Ozan güncel kilosunu söylediğinde kaydet. Aynı gün için varsa üstüne yazar.",
            ["input_schema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["weightKg"] = new JsonObject { ["type"] = "number" },
                    ["notes"] = new JsonObject { ["type"] = "string" },
                },
                ["required"] = new JsonArray { "weightKg" },
            },
        },
        new JsonObject
        {
            ["name"] = "log_checkin",
            ["description"] = "Ozan ruh hali, enerji ya da açlık/tokluk durumunu belirttiğinde bir check-in kaydet. Ölçekler 1-5. Açlık: 1=çok aç, 5=çok tok.",
            ["input_schema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["mood"] = new JsonObject { ["type"] = "integer" },
                    ["energy"] = new JsonObject { ["type"] = "integer" },
                    ["hunger"] = new JsonObject { ["type"] = "integer" },
                    ["note"] = new JsonObject { ["type"] = "string" },
                    ["context"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray { "general", "pre-workout", "post-workout" } },
                },
                ["required"] = new JsonArray { "mood", "energy", "hunger" },
            },
        },
        new JsonObject
        {
            ["name"] = "list_meals",
            ["description"] = "Yemek listesini getir. Ozan 'ne yedim', 'listele', 'neler var' derse veya bir şeyi silmeden/düzeltmeden önce çağır. date: YYYY-MM-DD formatında, boşsa bugün. Geçmiş günler için date belirt.",
            ["input_schema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["date"] = new JsonObject { ["type"] = "string", ["description"] = "YYYY-MM-DD formatında tarih. Boşsa bugün." },
                },
                ["required"] = new JsonArray(),
            },
        },
        new JsonObject
        {
            ["name"] = "delete_meal",
            ["description"] = "Öğünü ID'sine göre sil. Ozan 'sil', 'çıkar', 'kaldır', 'yanlış oldu' derse KULLAN. ÖNCE list_meals ile ID'leri gör, SONRA bununla sil. BİRDEN FAZLA öğünü silmen gerekiyorsa HER BİRİ İÇİN AYRI çağır.",
            ["input_schema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["mealId"] = new JsonObject { ["type"] = "string", ["description"] = "Silinecek öğünün GUID ID'si (list_meals'ten al)" },
                },
                ["required"] = new JsonArray { "mealId" },
            },
        },
        new JsonObject
        {
            ["name"] = "edit_meal",
            ["description"] = "Var olan öğünü ID'sine göre DÜZENLE. Ozan 'şu yanlış', 'düzelt', 'değiştir', 'aslında ... gramdı' derse KULLAN. YENİ KAYIT EKLEME — mevcut kaydı GÜNCELLE. ÖNCE list_meals ile ID'yi bul, SONRA bununla güncelle. TÜM alanları doldur (sadece değişenleri değil).",
            ["input_schema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["mealId"] = new JsonObject { ["type"] = "string", ["description"] = "Düzenlenecek öğünün GUID ID'si (list_meals'ten al)" },
                    ["foodName"] = new JsonObject { ["type"] = "string" },
                    ["grams"] = new JsonObject { ["type"] = "number" },
                    ["calories"] = new JsonObject { ["type"] = "number" },
                    ["protein"] = new JsonObject { ["type"] = "number" },
                    ["carbs"] = new JsonObject { ["type"] = "number" },
                    ["fat"] = new JsonObject { ["type"] = "number" },
                    ["mealType"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray { "Breakfast", "Lunch", "Dinner", "Snack" } },
                },
                ["required"] = new JsonArray { "mealId", "foodName", "grams", "calories", "protein", "carbs", "fat", "mealType" },
            },
        },
        new JsonObject
        {
            ["name"] = "get_nutrition_history",
            ["description"] = "Son N günün günlük kalori/makro özetini getir. Ozan 'bu hafta nasıldı', 'son 1 ayda ne kadar yedim', 'trend nasıl', 'geçmişe bak' gibi sorular sorduğunda KULLAN. Her gün için tarih, toplam kalori, protein, karbonhidrat, yağ ve öğün sayısı döner.",
            ["input_schema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["days"] = new JsonObject { ["type"] = "integer", ["description"] = "Kaç günlük geçmiş (varsayılan 7, maksimum 90)" },
                },
                ["required"] = new JsonArray(),
            },
        },
    };

    // Execute one tool call → (human-readable result, invalidation domain, isError).
    private async Task<(string result, string? domain, bool isError)> ExecuteToolAsync(string name, JsonNode? input)
    {
        try
        {
            switch (name)
            {
                case "log_food":
                {
                    var date = ParseDate(Str(input, "date")) ?? DateTime.Now;
                    var entry = new MealEntry
                    {
                        Id = Guid.NewGuid(),
                        FoodName = Str(input, "foodName"),
                        Grams = Num(input, "grams"),
                        Calories = Num(input, "calories"),
                        Protein = Num(input, "protein"),
                        Carbs = Num(input, "carbs"),
                        Fat = Num(input, "fat"),
                        MealType = Enum.TryParse<MealType>(Str(input, "mealType"), out var mt) ? mt : MealType.Snack,
                        LoggedAt = date,
                    };
                    _db.MealEntries.Add(entry);
                    await _db.SaveChangesAsync();
                    var dateLabel = date.Date == DateTime.Now.Date ? "" : $" ({date:dd.MM.yyyy})";
                    return ($"Eklendi{dateLabel}: {entry.FoodName} {entry.Grams:0}g, {entry.Calories:0} kcal (P{entry.Protein:0}/K{entry.Carbs:0}/Y{entry.Fat:0}).", "nutrition", false);
                }
                case "log_weight":
                {
                    var day = DateTime.UtcNow.Date;
                    var existing = await _db.WeightLogs.FirstOrDefaultAsync(w => w.LoggedAt >= day && w.LoggedAt < day.AddDays(1));
                    var kg = Num(input, "weightKg");
                    var notes = input?["notes"] is null ? null : Str(input, "notes");
                    if (existing is not null)
                    {
                        existing.WeightKg = kg;
                        existing.Notes = notes;
                        existing.LoggedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        _db.WeightLogs.Add(new WeightLog { Id = Guid.NewGuid(), WeightKg = kg, Notes = notes, LoggedAt = DateTime.UtcNow });
                    }
                    await _db.SaveChangesAsync();
                    return ($"Kilo kaydedildi: {kg:0.0} kg.", "weight", false);
                }
                case "log_checkin":
                {
                    var entry = new CheckIn
                    {
                        Id = Guid.NewGuid(),
                        Mood = Math.Clamp((int)Num(input, "mood"), 1, 5),
                        Energy = Math.Clamp((int)Num(input, "energy"), 1, 5),
                        Hunger = Math.Clamp((int)Num(input, "hunger"), 1, 5),
                        Note = input?["note"] is null ? null : Str(input, "note"),
                        Context = input?["context"] is null ? "general" : Str(input, "context"),
                        LoggedAt = DateTime.Now,
                    };
                    _db.CheckIns.Add(entry);
                    await _db.SaveChangesAsync();
                    return ($"Check-in kaydedildi (ruh {entry.Mood}, enerji {entry.Energy}, açlık {entry.Hunger}).", "checkin", false);
                }
                case "list_meals":
                {
                    var date = ParseDate(Str(input, "date")) ?? DateTime.Now;
                    var day = date.Date;
                    var meals = await _db.MealEntries
                        .Where(m => m.LoggedAt >= day && m.LoggedAt < day.AddDays(1))
                        .OrderBy(m => m.LoggedAt)
                        .ToListAsync();
                    var dateLabel = day == DateTime.Now.Date ? "Bugün" : day.ToString("dd.MM.yyyy");
                    if (meals.Count == 0)
                        return ($"{dateLabel} yemek kaydı yok.", null, false);
                    var lines = meals.Select(m =>
                        $"ID:{m.Id} [{m.MealType}] {m.FoodName} {m.Grams:0}g = {m.Calories:0} kcal (P{m.Protein:0}/K{m.Carbs:0}/Y{m.Fat:0}) {m.LoggedAt:HH:mm}");
                    var total = $"TOPLAM: {meals.Sum(m => m.Calories):0} kcal, P{meals.Sum(m => m.Protein):0} K{meals.Sum(m => m.Carbs):0} Y{meals.Sum(m => m.Fat):0}";
                    return ($"{dateLabel}:\n{string.Join("\n", lines)}\n{total}", null, false);
                }
                case "delete_meal":
                {
                    var mealId = Str(input, "mealId");
                    if (!Guid.TryParse(mealId, out var gid))
                        return ($"Geçersiz ID: {mealId}. Önce list_meals ile ID'leri gör.", null, true);
                    var meal = await _db.MealEntries.FindAsync(gid);
                    if (meal is null)
                        return ($"ID:{mealId} bulunamadı. Silinmiş olabilir ya da başka bir güne ait.", null, true);
                    var desc = $"{meal.FoodName} {meal.Grams:0}g ({meal.Calories:0} kcal)";
                    _db.MealEntries.Remove(meal);
                    await _db.SaveChangesAsync();
                    return ($"Silindi: {desc}.", "nutrition", false);
                }
                case "edit_meal":
                {
                    var mealId = Str(input, "mealId");
                    if (!Guid.TryParse(mealId, out var gid))
                        return ($"Geçersiz ID: {mealId}. Önce list_meals ile ID'leri gör.", null, true);
                    var meal = await _db.MealEntries.FindAsync(gid);
                    if (meal is null)
                        return ($"ID:{mealId} bulunamadı. Düzenlenemez.", null, true);
                    var oldDesc = $"{meal.FoodName} {meal.Grams:0}g ({meal.Calories:0} kcal)";
                    meal.FoodName = Str(input, "foodName");
                    meal.Grams = Num(input, "grams");
                    meal.Calories = Num(input, "calories");
                    meal.Protein = Num(input, "protein");
                    meal.Carbs = Num(input, "carbs");
                    meal.Fat = Num(input, "fat");
                    meal.MealType = Enum.TryParse<MealType>(Str(input, "mealType"), out var mt) ? mt : MealType.Snack;
                    await _db.SaveChangesAsync();
                    var newDesc = $"{meal.FoodName} {meal.Grams:0}g = {meal.Calories:0} kcal (P{meal.Protein:0}/K{meal.Carbs:0}/Y{meal.Fat:0})";
                    return ($"Güncellendi: [{oldDesc}] → [{newDesc}].", "nutrition", false);
                }
                case "get_nutrition_history":
                {
                    var days = (int)Math.Clamp(Num(input, "days"), 1, 90);
                    if (days == 0) days = 7;
                    var since = DateTime.Now.Date.AddDays(-days + 1);
                    var entries = await _db.MealEntries
                        .Where(m => m.LoggedAt >= since)
                        .ToListAsync();
                    var daily = entries
                        .GroupBy(m => m.LoggedAt.Date)
                        .OrderBy(g => g.Key)
                        .Select(g => new
                        {
                            Date = g.Key.ToString("dd.MM.yyyy"),
                            Calories = g.Sum(m => m.Calories),
                            Protein = g.Sum(m => m.Protein),
                            Carbs = g.Sum(m => m.Carbs),
                            Fat = g.Sum(m => m.Fat),
                            Count = g.Count(),
                        })
                        .ToList();
                    if (daily.Count == 0)
                        return ($"Son {days} günde yemek kaydı yok.", null, false);
                    var lines = daily.Select(d =>
                        $"{d.Date}: {d.Calories:0} kcal, P{d.Protein:0} K{d.Carbs:0} Y{d.Fat:0} ({d.Count} öğün)");
                    var avg = daily.Average(d => d.Calories);
                    return ($"Son {days} gün:\n{string.Join("\n", lines)}\n\nGünlük ortalama: {avg:0} kcal.", null, false);
                }
                default:
                    return ($"Bilinmeyen araç: {name}", null, true);
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Tool {Name} failed.", name);
            return ("Kaydedilemedi (hata).", null, true);
        }
    }

    private static double Num(JsonNode? n, string key)
    {
        var v = n?[key];
        if (v is null) return 0;
        try { return v.GetValue<double>(); } catch { }
        return double.TryParse(v.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0;
    }

    private static string Str(JsonNode? n, string key)
    {
        var v = n?[key];
        if (v is null) return "";
        try { return v.GetValue<string>(); } catch { return v.ToString(); }
    }

    private static DateTime? ParseDate(string dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr)) return null;
        if (DateTime.TryParseExact(dateStr.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return d;
        return null;
    }

    // Assemble a compact live snapshot of Ozan's day for the coach's system prompt.
    private async Task<string> BuildSystemPromptAsync()
    {
        var day = DateTime.Now.Date;

        var goals = await _db.UserGoals.FirstOrDefaultAsync();
        var profile = await _db.Profiles.FirstOrDefaultAsync();

        var todayMeals = await _db.MealEntries
            .Where(m => m.LoggedAt >= day && m.LoggedAt < day.AddDays(1))
            .ToListAsync();
        var cal = todayMeals.Sum(m => m.Calories);
        var pro = todayMeals.Sum(m => m.Protein);
        var carb = todayMeals.Sum(m => m.Carbs);
        var fat = todayMeals.Sum(m => m.Fat);

        // Last 7 and 14 days for weekly comparison
        var weekAgo = day.AddDays(-7);
        var twoWeeksAgo = day.AddDays(-14);
        var last7Days = await _db.MealEntries
            .Where(m => m.LoggedAt >= weekAgo && m.LoggedAt < day)
            .ToListAsync();
        var prev7Days = await _db.MealEntries
            .Where(m => m.LoggedAt >= twoWeeksAgo && m.LoggedAt < weekAgo)
            .ToListAsync();
        var thisWeekAvg = last7Days.Count > 0 ? last7Days.Sum(m => m.Calories) / 7.0 : 0;
        var prevWeekAvg = prev7Days.Count > 0 ? prev7Days.Sum(m => m.Calories) / 7.0 : 0;
        var weeklyHistory = last7Days
            .GroupBy(m => m.LoggedAt.Date)
            .OrderBy(g => g.Key)
            .Select(g => $"{g.Key:dd.MM}: {g.Sum(m => m.Calories):0} kcal (P{g.Sum(m => m.Protein):0}/K{g.Sum(m => m.Carbs):0}/Y{g.Sum(m => m.Fat):0})")
            .ToList();

        var weights = await _db.WeightLogs.OrderBy(w => w.LoggedAt).ToListAsync();
        double? currentW = weights.Count > 0 ? weights[^1].WeightKg : null;
        double? startW = weights.Count > 0 ? weights[0].WeightKg : null;

        var session = await _db.WorkoutSessions
            .Include(w => w.Exercises).ThenInclude(e => e.Sets)
            .Where(w => w.LoggedAt >= day && w.LoggedAt < day.AddDays(1))
            .OrderByDescending(w => w.LoggedAt)
            .FirstOrDefaultAsync();

        var recentCheckins = await _db.CheckIns
            .OrderByDescending(c => c.LoggedAt)
            .Take(8)
            .ToListAsync();

        var sb = new StringBuilder();

        // === [1] KİMLİK + TARZ ===
        sb.AppendLine("Sen Koç — Ozan'ın kişisel sağlık ve fitness koçusun.");
        sb.AppendLine("Beslenme biyokimyası, egzersiz fizyolojisi ve spor bilimlerinde uzmansın.");
        sb.AppendLine();
        sb.AppendLine("DAVRANIŞ KURALLARI (kesinlikle uyulacak):");
        sb.AppendLine("- Türkçe konuş. Doğal konuşma dili, insan gibi.");
        sb.AppendLine("- SELAMLAŞMA YOK. \"Selam Ozan\", \"Ben Koç\" gibi giriş cümleleri KULLANMA. Direkt konuya gir.");
        sb.AppendLine("- Emoji kullanma.");
        sb.AppendLine("- Aşırı övgü kelimeleri (harika, süper, mükemmel) kullanma.");
        sb.AppendLine("- KISA VE NET ol. Lafı dolandırma. Her cümle bilgi taşısın.");
        sb.AppendLine("- Ozan bir şey sorduğunda HESAPLAMA YAPMAN gerekiyorsa, adım adım hesapla ve sonucu göster.");
        sb.AppendLine("- Bir şey net değilse sor. Ama gereksiz detay sorma.");
        sb.AppendLine("- Ozan'ın yazım hatalarını ya da eksik bilgilerini idare et — ne demek istediğini anlamaya çalış.");
        sb.AppendLine("- \"X gram aldım/verdim\", \"X kg çıktım\", \"tartı X gösterdi\" = kilo değişimi. Bunu `log_weight` olarak kaydet.");
        sb.AppendLine();

        // === [2] ARAÇ TALİMATLARI ===
        sb.AppendLine("== ARAÇ KULLANIMI ==");
        sb.AppendLine("- Ozan bir şey yediğini söylerse HEMEN `log_food` ile kaydet. Onay isteme.");
        sb.AppendLine("- Birden fazla besin varsa her biri için ayrı `log_food` çağır.");
        sb.AppendLine("- Makro için aşağıdaki BESLENME REFERANSI tablosunu kullan. Bilmediğin besinde en yakın benzeri baz al.");
        sb.AppendLine("- Kaydettikten sonra: ne kaydettiğini TEK CÜMLE ile teyit et, sonra hemen hedef/trend bağlamında yorum yap.");
        sb.AppendLine("- Kilo söylerse `log_weight`, ruh hali/enerji/açlık belirtirse `log_checkin` çağır.");
        sb.AppendLine();
        sb.AppendLine("== DÜZELTME/SİLME ==");
        sb.AppendLine("Ozan sil/çıkar/kaldır/yanlış/düzelt/değiştir derse:");
        sb.AppendLine("1. `log_food` ÇAĞIRMA — bu yeni kayıt ekler, üstüne bindirir!");
        sb.AppendLine("2. ÖNCE `list_meals` ile ID'leri gör.");
        sb.AppendLine("3. Silme → `delete_meal` (her öğün için ayrı). Düzeltme → `edit_meal` (TÜM alanları doldur).");
        sb.AppendLine("4. İşlem sonrası kısaca teyit et.");
        sb.AppendLine($"Saat: {DateTime.Now:HH:mm}. Öğün: 06-11=Breakfast, 11-15=Lunch, 15-18=Snack, 18+=Dinner.");
        sb.AppendLine();

        // === [3] BESLENME REFERANSI ===
        sb.AppendLine("== BESLENME REFERANSI (100g başına yaklaşık değerler) ==");
        sb.AppendLine("Tavuk göğsü (pişmiş): 165kcal, P31g, K0g, Y3.5g");
        sb.AppendLine("Tavuk but (pişmiş): 210kcal, P26g, K0g, Y11g");
        sb.AppendLine("Kırmızı et (dana, %20 yağlı): 250kcal, P26g, K0g, Y17g");
        sb.AppendLine("Kıyma (dana, %15 yağ): 220kcal, P24g, K0g, Y13g");
        sb.AppendLine("Balık (levrek/çupra, ızgara): 120kcal, P21g, K0g, Y4g");
        sb.AppendLine("Somon (füme/pişmiş): 208kcal, P23g, K0g, Y13g");
        sb.AppendLine("Yumurta (1 adet=50g): 78kcal, P6.3g, K0.6g, Y5.3g — gramla hesapla");
        sb.AppendLine("Pirinç pilavı: 130kcal, P2.7g, K28g, Y0.3g");
        sb.AppendLine("Bulgur pilavı: 115kcal, P3.5g, K23g, Y0.5g");
        sb.AppendLine("Makarna (pişmiş): 130kcal, P5g, K25g, Y0.5g");
        sb.AppendLine("Ekmek (beyaz, 1 dilim=25g): 65kcal, P2g, K13g, Y1g");
        sb.AppendLine("Tam buğday ekmeği (1 dilim=25g): 60kcal, P2.5g, K11g, Y1g");
        sb.AppendLine("Simit (1 adet=100g): 420kcal, P10g, K60g, Y15g");
        sb.AppendLine("Mercimek çorbası (1 kase=250ml): 130kcal, P7g, K20g, Y2.5g");
        sb.AppendLine("Tarhana çorbası (1 kase=250ml): 150kcal, P5g, K22g, Y4g");
        sb.AppendLine("Zeytinyağı (1 yk=15ml): 120kcal, P0g, K0g, Y13.5g");
        sb.AppendLine("Tereyağı (1 yk=15g): 105kcal, P0g, K0g, Y12g");
        sb.AppendLine("Peynir (beyaz, tam yağlı): 270kcal, P17g, K1g, Y22g");
        sb.AppendLine("Kaşar peyniri: 350kcal, P25g, K1g, Y28g");
        sb.AppendLine("Yoğurt (tam yağlı): 65kcal, P3.5g, K4.5g, Y3.5g");
        sb.AppendLine("Süzme yoğurt: 110kcal, P10g, K5g, Y5g");
        sb.AppendLine("Süt (tam yağlı): 62kcal, P3.2g, K4.8g, Y3.3g");
        sb.AppendLine("Kuruyemiş (karışık): 600kcal, P20g, K15g, Y55g");
        sb.AppendLine("Muz (1 adet=120g): 105kcal, P1.3g, K27g, Y0.4g");
        sb.AppendLine("Elma (1 adet=180g): 95kcal, P0.5g, K25g, Y0.3g");
        sb.AppendLine("Tatlı (baklava, 1 porsiyon=150g): 450kcal, P8g, K50g, Y25g");
        sb.AppendLine("Döner/iskender (1 porsiyon=350g): 550kcal, P35g, K30g, Y30g");
        sb.AppendLine("Lahmacun (1 adet=120g): 280kcal, P10g, K40g, Y9g");
        sb.AppendLine();
        sb.AppendLine("MAKRO KALORİ: 1g protein = 4 kcal, 1g karbonhidrat = 4 kcal, 1g yağ = 9 kcal.");
        sb.AppendLine("Bu tablo makro tahmini için referans. Bilmediğin besinde en yakın benzeri baz al.");
        sb.AppendLine();

        // === [4] ANTRENMAN REFERANSI ===
        sb.AppendLine("== ANTRENMAN BİLGİSİ ==");
        sb.AppendLine("- Hacim (volume) = set × ağırlık(kg) × tekrar. İlerlemenin ana göstergesidir.");
        sb.AppendLine("- Progressive overload: haftada %2-5 hacim artışı sürdürülebilir ilerlemedir. %5+ hızlı, sakatlık riski var.");
        sb.AppendLine("- Kas grubu toparlanması: büyük kaslar (göğüs/sırt/bacak) 48-72 saat, küçük kaslar (kol/omuz) 24-48 saat.");
        sb.AppendLine("- Antrenman + kalori açığı = kas kaybı riski. Yüksek protein (1.6-2.2g/kg vücut ağırlığı) koruma sağlar.");
        sb.AppendLine("- Antrenman öncesi (1-2 saat): karbonhidrat ağırlıklı + hafif protein — enerji ve kas koruması.");
        sb.AppendLine("- Antrenman sonrası (ilk 2 saat): protein 20-40g + karbonhidrat — kas protein sentezi ve glikojen yenileme.");
        sb.AppendLine("- Hacim düşüşü + aynı beslenme = yağlanma riski. Hacim düşerken kaloriyi de ayarla.");
        sb.AppendLine();

        // === [5] VERİ OKUMA ÇERÇEVESİ ===
        sb.AppendLine("== ANALİZ REFERANSI ==");
        sb.AppendLine("- Kilo: günlük ±1kg normal (su/glikojen). Haftalık ortalama = gerçek trend.");
        sb.AppendLine("- Kilo kaybı: 0.5-1.0 kg/hafta sağlıklı. Daha hızlısı = kas kaybı + metabolizma yavaşlaması.");
        sb.AppendLine("- Kilo alımı: 0.25-0.5 kg/hafta. Fazlası yağ.");
        sb.AppendLine("- Sürdürülebilir günlük açık: 300-500 kcal.");
        sb.AppendLine("- Protein: 1.6-2.2g/kg vücut ağırlığı.");
        sb.AppendLine("- Anomali: tek günde 3kg+ değişim veya tek öğünde 3000kcal+ = sorgula.");
        sb.AppendLine();

        // === [6] CANLI VERİ ===
        sb.AppendLine("== GÜNCEL VERİLER (bugün) ==");

        if (goals is not null)
            sb.AppendLine($"Hedefler: {goals.CalorieGoal:0} kcal, Protein {goals.ProteinGoal:0}g, Karb {goals.CarbGoal:0}g, Yağ {goals.FatGoal:0}g.");
        sb.AppendLine($"Bugün alınan: {cal:0} kcal, Protein {pro:0}g, Karb {carb:0}g, Yağ {fat:0}g.");
        if (goals is not null)
            sb.AppendLine($"Kalori farkı: {cal - goals.CalorieGoal:0} kcal (negatif = açık).");

        // Weekly comparison — 7-day summary
        if (weeklyHistory.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("== SON 7 GÜN KALORİ ==");
            foreach (var line in weeklyHistory)
                sb.AppendLine($"- {line}");
            sb.AppendLine($"Bu hafta ort: {thisWeekAvg:0} kcal/gün | Geçen hafta ort: {prevWeekAvg:0} kcal/gün | Fark: {(prevWeekAvg > 0 ? (thisWeekAvg - prevWeekAvg) / prevWeekAvg * 100 : 0):+0;-0;0}%");
        }

        // Full weight history
        if (weights.Count > 0)
        {
            sb.AppendLine("== KİLO GEÇMİŞİ (tüm kayıtlar) ==");
            foreach (var w in weights)
            {
                sb.AppendLine($"- {w.LoggedAt:dd.MM.yyyy}: {w.WeightKg:0.0} kg");
            }
            sb.AppendLine($"Güncel: {currentW:0.0} kg, Başlangıç: {startW:0.0} kg, Toplam değişim: {currentW - startW:+0.0;-0.0;0} kg.");
            sb.AppendLine();
        }
        else sb.AppendLine("Kilo kaydı yok.");

        if (profile?.HeightCm is > 0)
        {
            sb.Append($"Boy: {profile.HeightCm:0} cm.");
            if (profile.TargetWeightKg is > 0) sb.Append($" Hedef kilo: {profile.TargetWeightKg:0.0} kg.");
            if (currentW is not null)
            {
                var h = profile.HeightCm.Value / 100.0;
                var bmi = currentW.Value / (h * h);
                sb.Append($" BMI: {bmi:0.0}.");
            }
            sb.AppendLine();
        }

        if (session is not null)
        {
            var vol = session.Exercises.Sum(e => e.Sets.Where(s => s.IsCompleted).Sum(s => s.WeightKg * s.Reps));
            var exNames = string.Join(", ", session.Exercises.Select(e => e.Name));
            sb.AppendLine($"Bugünkü antrenman: {session.Name} — {session.Exercises.Count} hareket ({exNames}), tamamlanan hacim {vol:0} kg.");
        }
        else sb.AppendLine("Bugün antrenman yok.");

        if (recentCheckins.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("== SON CHECK-IN'LER ==");
            sb.AppendLine("Ölçekler 1-5. Ruh: 1=kötü,5=harika. Enerji: 1=bitkin,5=enerjik. Açlık: 1=ÇOK AÇ, 5=ÇOK TOK.");
            foreach (var c in recentCheckins)
            {
                var when = c.LoggedAt.ToLocalTime().ToString("dd.MM HH:mm");
                sb.Append($"- {when} [{c.Context}] Ruh:{c.Mood} Enerji:{c.Energy} Açlık:{c.Hunger}");
                if (!string.IsNullOrWhiteSpace(c.Note)) sb.Append($" — \"{c.Note}\"");
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }
}
