using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using FitTrack.API.Data;
using FitTrack.API.Models;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.API.Services;

public record CoachResult(string Reply, List<string> Actions, bool Ok);

/// <summary>
/// Koçun tek beyni. Web controller ve Telegram bot bunu sarar —
/// tool tanımları, executor, sistem promptu ve agentic döngü burada.
/// </summary>
public class CoachService
{
    private const string Model = "claude-sonnet-5";
    private const string AnthropicUrl = "https://api.anthropic.com/v1/messages";

    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<CoachService> _log;

    public CoachService(AppDbContext db, IHttpClientFactory httpFactory, IConfiguration config, ILogger<CoachService> log)
    {
        _db = db;
        _httpFactory = httpFactory;
        _config = config;
        _log = log;
    }

    public string? ApiKey => _config["Anthropic:ApiKey"] ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

    /// <summary>
    /// Agentic döngü: Claude'u çağır, istediği tool'ları çalıştır, sonuçları geri ver,
    /// tool istemeyi bırakana kadar tekrarla (runaway'e karşı sınırlı).
    /// messages: {role, content} düz metin geçmişi. Tool turları döngü içinde eklenir.
    /// </summary>
    public async Task<CoachResult> ChatAsync(JsonArray messages, string? extraSystem = null, CancellationToken ct = default)
    {
        var apiKey = ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
            return new CoachResult("", new(), false);

        var system = await BuildSystemPromptAsync();
        if (!string.IsNullOrWhiteSpace(extraSystem))
            system += "\n\n" + extraSystem;

        var actions = new List<string>();
        var reply = "";

        for (var turn = 0; turn < 8; turn++)
        {
            var payload = new JsonObject
            {
                ["model"] = Model,
                ["max_tokens"] = 4096,
                ["system"] = system,
                ["tools"] = BuildTools(),
                ["messages"] = messages.DeepClone(),
            };

            var (ok, body) = await CallAnthropicAsync(apiKey, payload, ct);
            if (!ok) return new CoachResult("", actions, false);

            JsonNode? root;
            try { root = JsonNode.Parse(body); }
            catch (Exception ex) { _log.LogError(ex, "Parse failed: {Body}", body); return new CoachResult("", actions, false); }

            var content = root?["content"] as JsonArray ?? new JsonArray();
            var stop = (string?)root?["stop_reason"];

            reply = string.Concat(content
                .Where(b => (string?)b?["type"] == "text")
                .Select(b => (string?)b?["text"] ?? ""));

            if (stop != "tool_use") break;

            // Assistant'ın tool_use turunu aynen geri ver, sonra her çağrıyı yanıtla.
            messages.Add(new JsonObject { ["role"] = "assistant", ["content"] = content.DeepClone() });

            var toolResults = new JsonArray();
            foreach (var block in content)
            {
                if ((string?)block?["type"] != "tool_use") continue;
                var id = (string?)block!["id"] ?? "";
                var name = (string?)block["name"] ?? "";
                var (resultText, domain, isError) = await ExecuteToolAsync(name, block["input"]);
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

        return new CoachResult(reply.Trim(), actions.Distinct().ToList(), true);
    }

    private async Task<(bool ok, string body)> CallAnthropicAsync(string apiKey, JsonObject payload, CancellationToken ct)
    {
        var http = _httpFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(90);
        using var request = new HttpRequestMessage(HttpMethod.Post, AnthropicUrl);
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
        try
        {
            var resp = await http.SendAsync(request, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode) { _log.LogError("Anthropic {Status}: {Body}", (int)resp.StatusCode, body); return (false, body); }
            return (true, body);
        }
        catch (Exception ex) { _log.LogError(ex, "Anthropic call failed."); return (false, ""); }
    }

    // ===================== TOOLS =====================

    private static JsonObject Tool(string name, string description, JsonObject properties, params string[] required)
        => new()
        {
            ["name"] = name,
            ["description"] = description,
            ["input_schema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = properties,
                ["required"] = new JsonArray(required.Select(r => (JsonNode)r).ToArray()),
            },
        };

    private static JsonObject NumProp() => new() { ["type"] = "number" };
    private static JsonObject StrProp(string? desc = null)
    {
        var o = new JsonObject { ["type"] = "string" };
        if (desc is not null) o["description"] = desc;
        return o;
    }
    private static JsonObject IntProp(string? desc = null)
    {
        var o = new JsonObject { ["type"] = "integer" };
        if (desc is not null) o["description"] = desc;
        return o;
    }
    private static JsonObject DateProp() => StrProp("YYYY-MM-DD formatında tarih. Boşsa bugün. 'dün', '3 gün önce' gibi ifadelerde hesaplayıp ver.");
    private static JsonObject MealTypeProp() => new() { ["type"] = "string", ["enum"] = new JsonArray { "Breakfast", "Lunch", "Dinner", "Snack" } };

    public static JsonArray BuildTools() => new()
    {
        Tool("log_food",
            "Ozan bir şey yediğinde besini günlüğe ekle. Makro tahmini için BESLENME REFERANSI tablosunu kullan. calories/protein/carbs/fat TOPLAM tüketilen miktar (100g başına değil). Birden fazla besin = her biri için ayrı çağrı.",
            new JsonObject
            {
                ["foodName"] = StrProp(), ["grams"] = NumProp(), ["calories"] = NumProp(),
                ["protein"] = NumProp(), ["carbs"] = NumProp(), ["fat"] = NumProp(),
                ["mealType"] = MealTypeProp(), ["date"] = DateProp(),
            },
            "foodName", "grams", "calories", "protein", "carbs", "fat", "mealType"),

        Tool("log_weight",
            "Ozan kilosunu söylediğinde kaydet. Aynı gün için varsa üstüne yazar. 'dün 92'ydim' gibi geçmiş tarih için date ver.",
            new JsonObject { ["weightKg"] = NumProp(), ["notes"] = StrProp(), ["date"] = DateProp() },
            "weightKg"),

        Tool("log_checkin",
            "Ozan ruh hali, enerji ya da açlık/tokluk belirttiğinde check-in kaydet. Ölçekler 1-5. Açlık: 1=çok aç, 5=çok tok.",
            new JsonObject
            {
                ["mood"] = IntProp(), ["energy"] = IntProp(), ["hunger"] = IntProp(),
                ["note"] = StrProp(), ["context"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray { "general", "pre-workout", "post-workout" } },
            },
            "mood", "energy", "hunger"),

        Tool("list_meals",
            "Bir günün yemek listesini ID'leriyle getir. 'ne yedim', 'listele' derse veya silme/düzeltme ÖNCESİNDE çağır.",
            new JsonObject { ["date"] = DateProp() }),

        Tool("delete_meal",
            "Öğünü ID'sine göre sil. 'sil', 'çıkar', 'kaldır', 'yanlış oldu' derse KULLAN. ÖNCE list_meals ile ID'leri gör. Birden fazla silme = her biri için ayrı çağrı.",
            new JsonObject { ["mealId"] = StrProp("Silinecek öğünün GUID ID'si (list_meals'ten)") },
            "mealId"),

        Tool("edit_meal",
            "Var olan öğünü GÜNCELLE (yeni kayıt ekleme!). 'düzelt', 'değiştir', 'aslında ... gramdı' derse KULLAN. ÖNCE list_meals ile ID'yi bul. TÜM alanları doldur.",
            new JsonObject
            {
                ["mealId"] = StrProp("Düzenlenecek öğünün GUID ID'si"),
                ["foodName"] = StrProp(), ["grams"] = NumProp(), ["calories"] = NumProp(),
                ["protein"] = NumProp(), ["carbs"] = NumProp(), ["fat"] = NumProp(), ["mealType"] = MealTypeProp(),
            },
            "mealId", "foodName", "grams", "calories", "protein", "carbs", "fat", "mealType"),

        Tool("get_nutrition_history",
            "Son N günün günlük kalori/makro özeti. 'bu hafta nasıldı', 'trend nasıl', 'geçmişe bak' derse KULLAN.",
            new JsonObject { ["days"] = IntProp("Kaç gün (varsayılan 7, maks 90)") }),

        Tool("get_weight_history",
            "Kilo kayıtları listesi + trend. 'kilom nasıl gidiyor', 'ne kadar verdim' derse veya kilo analizi gerektiğinde KULLAN.",
            new JsonObject { ["days"] = IntProp("Kaç gün (varsayılan 30, maks 365)") }),

        Tool("get_workout_history",
            "Son antrenmanlar: seans, hareketler, set×kg×tekrar, toplam hacim. 'geçen antrenman', 'bench ne kadardı', hacim/ilerleme analizi için KULLAN.",
            new JsonObject { ["days"] = IntProp("Kaç gün (varsayılan 14, maks 90)") }),

        Tool("get_checkin_history",
            "Geçmiş check-in'ler (ruh/enerji/açlık). Enerji-beslenme-antrenman korelasyonu kurarken KULLAN.",
            new JsonObject { ["days"] = IntProp("Kaç gün (varsayılan 14, maks 90)") }),

        Tool("remember",
            "KALICI NOT kaydet. Ozan sakatlık ('omzum ağrıyor'), kalıcı tercih ('süt sevmem'), hedef bağlamı veya önemli kişisel bilgi söylediğinde KULLAN. Chat geçmişi 3 günde silinir — bunlar kalır ve her konuşmada görünür. Gündelik şeyleri KAYDETME.",
            new JsonObject
            {
                ["category"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray { "injury", "preference", "goal", "fact" } },
                ["content"] = StrProp("Kısa, net not. Örn: 'Sol omuzda impingement — overhead press'ten kaçın (Tem 2026)'"),
            },
            "category", "content"),

        Tool("forget",
            "Kalıcı notu ID'sine göre sil. Not geçersizleştiğinde ('omzum düzeldi') veya Ozan istediğinde KULLAN. ID'ler sistem promptundaki KOÇ NOTLARI bölümünde.",
            new JsonObject { ["noteId"] = StrProp("Silinecek notun GUID ID'si") },
            "noteId"),
    };

    // ===================== EXECUTOR =====================

    public async Task<(string result, string? domain, bool isError)> ExecuteToolAsync(string name, JsonNode? input)
    {
        try
        {
            switch (name)
            {
                case "log_food":
                {
                    var date = ParseDate(Str(input, "date")) ?? DateTime.Now;
                    if (date.Date != DateTime.Now.Date) date = date.Date.AddHours(12); // geçmiş gün → öğlen
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
                    var date = ParseDate(Str(input, "date")) ?? DateTime.Now;
                    var day = date.Date;
                    var existing = await _db.WeightLogs.FirstOrDefaultAsync(w => w.LoggedAt >= day && w.LoggedAt < day.AddDays(1));
                    var kg = Num(input, "weightKg");
                    var notes = Str(input, "notes");
                    if (existing is not null)
                    {
                        existing.WeightKg = kg;
                        if (!string.IsNullOrEmpty(notes)) existing.Notes = notes;
                    }
                    else
                    {
                        _db.WeightLogs.Add(new WeightLog
                        {
                            Id = Guid.NewGuid(),
                            WeightKg = kg,
                            Notes = string.IsNullOrEmpty(notes) ? null : notes,
                            LoggedAt = day == DateTime.Now.Date ? DateTime.Now : day.AddHours(8),
                        });
                    }
                    await _db.SaveChangesAsync();
                    var dateLabel = day == DateTime.Now.Date ? "" : $" ({day:dd.MM.yyyy})";
                    return ($"Kilo kaydedildi{dateLabel}: {kg:0.0} kg.", "weight", false);
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
                    var days = DaysArg(input, 7, 90);
                    var since = DateTime.Now.Date.AddDays(-days + 1);
                    var entries = await _db.MealEntries.Where(m => m.LoggedAt >= since).ToListAsync();
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
                case "get_weight_history":
                {
                    var days = DaysArg(input, 30, 365);
                    var since = DateTime.Now.Date.AddDays(-days + 1);
                    var logs = await _db.WeightLogs
                        .Where(w => w.LoggedAt >= since)
                        .OrderBy(w => w.LoggedAt)
                        .ToListAsync();
                    if (logs.Count == 0)
                        return ($"Son {days} günde kilo kaydı yok.", null, false);
                    var lines = logs.Select(w =>
                        $"{w.LoggedAt:dd.MM.yyyy}: {w.WeightKg:0.0} kg{(string.IsNullOrEmpty(w.Notes) ? "" : $" — {w.Notes}")}");
                    var delta = logs[^1].WeightKg - logs[0].WeightKg;
                    var weeks = Math.Max((logs[^1].LoggedAt - logs[0].LoggedAt).TotalDays / 7.0, 1);
                    return ($"Son {days} gün ({logs.Count} kayıt):\n{string.Join("\n", lines)}\n\nDeğişim: {delta:+0.0;-0.0;0} kg ({delta / weeks:+0.00;-0.00;0} kg/hafta).", null, false);
                }
                case "get_workout_history":
                {
                    var days = DaysArg(input, 14, 90);
                    var since = DateTime.Now.Date.AddDays(-days + 1);
                    var sessions = await _db.WorkoutSessions
                        .Include(w => w.Exercises).ThenInclude(e => e.Sets)
                        .Where(w => w.LoggedAt >= since)
                        .OrderBy(w => w.LoggedAt)
                        .ToListAsync();
                    if (sessions.Count == 0)
                        return ($"Son {days} günde antrenman kaydı yok.", null, false);
                    var sb = new StringBuilder();
                    foreach (var s in sessions)
                    {
                        var vol = s.Exercises.Sum(e => e.Sets.Where(x => x.IsCompleted).Sum(x => x.WeightKg * x.Reps));
                        sb.AppendLine($"{s.LoggedAt:dd.MM.yyyy} — {s.Name} (hacim {vol:0} kg):");
                        foreach (var e in s.Exercises)
                        {
                            var sets = string.Join(", ", e.Sets.Where(x => x.IsCompleted).Select(x => $"{x.WeightKg:0.#}kg×{x.Reps}"));
                            sb.AppendLine($"  {e.Name}: {(string.IsNullOrEmpty(sets) ? "set tamamlanmamış" : sets)}");
                        }
                    }
                    return (sb.ToString().TrimEnd(), null, false);
                }
                case "get_checkin_history":
                {
                    var days = DaysArg(input, 14, 90);
                    var since = DateTime.Now.Date.AddDays(-days + 1);
                    var checkins = await _db.CheckIns
                        .Where(c => c.LoggedAt >= since)
                        .OrderBy(c => c.LoggedAt)
                        .ToListAsync();
                    if (checkins.Count == 0)
                        return ($"Son {days} günde check-in yok.", null, false);
                    var lines = checkins.Select(c =>
                        $"{c.LoggedAt:dd.MM HH:mm} [{c.Context}] Ruh:{c.Mood} Enerji:{c.Energy} Açlık:{c.Hunger}{(string.IsNullOrWhiteSpace(c.Note) ? "" : $" — \"{c.Note}\"")}");
                    return ($"Son {days} gün ({checkins.Count} check-in, ölçek 1-5):\n{string.Join("\n", lines)}", null, false);
                }
                case "remember":
                {
                    var content = Str(input, "content");
                    if (string.IsNullOrWhiteSpace(content))
                        return ("Boş not kaydedilmez.", null, true);
                    var note = new CoachNote
                    {
                        Id = Guid.NewGuid(),
                        Category = Str(input, "category") is { Length: > 0 } c ? c : "fact",
                        Content = content.Trim(),
                        CreatedAt = DateTime.Now,
                    };
                    _db.CoachNotes.Add(note);
                    await _db.SaveChangesAsync();
                    return ($"Not kaydedildi [{note.Category}]: {note.Content}", "note", false);
                }
                case "forget":
                {
                    var noteId = Str(input, "noteId");
                    if (!Guid.TryParse(noteId, out var gid))
                        return ($"Geçersiz not ID: {noteId}.", null, true);
                    var note = await _db.CoachNotes.FindAsync(gid);
                    if (note is null)
                        return ($"Not bulunamadı: {noteId}.", null, true);
                    _db.CoachNotes.Remove(note);
                    await _db.SaveChangesAsync();
                    return ($"Not silindi: {note.Content}", "note", false);
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

    private static int DaysArg(JsonNode? input, int def, int max)
    {
        var d = (int)Num(input, "days");
        return d <= 0 ? def : Math.Min(d, max);
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

    // ===================== SYSTEM PROMPT =====================

    /// <summary>Kompakt canlı özet: kimlik, kurallar, referanslar, bugünün verisi, koç notları.</summary>
    public async Task<string> BuildSystemPromptAsync()
    {
        var day = DateTime.Now.Date;

        var goals = await _db.UserGoals.FirstOrDefaultAsync();
        var profile = await _db.Profiles.FirstOrDefaultAsync();

        var todayMeals = await _db.MealEntries
            .Where(m => m.LoggedAt >= day && m.LoggedAt < day.AddDays(1))
            .ToListAsync();

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

        // Kilo: özet + son 5 kayıt. Tam liste get_weight_history tool'unda.
        var weights = await _db.WeightLogs.OrderBy(w => w.LoggedAt).ToListAsync();
        double? currentW = weights.Count > 0 ? weights[^1].WeightKg : null;
        double? startW = weights.Count > 0 ? weights[0].WeightKg : null;

        var session = await _db.WorkoutSessions
            .Include(w => w.Exercises).ThenInclude(e => e.Sets)
            .Where(w => w.LoggedAt >= day && w.LoggedAt < day.AddDays(1))
            .OrderByDescending(w => w.LoggedAt)
            .FirstOrDefaultAsync();

        var lastSession = await _db.WorkoutSessions
            .Include(w => w.Exercises).ThenInclude(e => e.Sets)
            .Where(w => w.LoggedAt < day)
            .OrderByDescending(w => w.LoggedAt)
            .FirstOrDefaultAsync();

        var recentCheckins = await _db.CheckIns
            .OrderByDescending(c => c.LoggedAt)
            .Take(5)
            .ToListAsync();

        var notes = await _db.CoachNotes.OrderBy(n => n.CreatedAt).ToListAsync();

        var sb = new StringBuilder();

        // === [1] KİMLİK + TARZ ===
        sb.AppendLine("Sen Koç — Ozan'ın kişisel sağlık ve fitness koçusun.");
        sb.AppendLine("Beslenme biyokimyası, egzersiz fizyolojisi ve spor bilimlerinde uzmansın.");
        sb.AppendLine("Bu Ozan'ın kişisel botu — tek kullanıcı, tam yetki, veri gizliliği derdi yok.");
        sb.AppendLine();
        sb.AppendLine("DAVRANIŞ KURALLARI (kesinlikle uyulacak):");
        sb.AppendLine("- Türkçe konuş. Doğal konuşma dili, insan gibi.");
        sb.AppendLine("- SELAMLAŞMA YOK. \"Selam Ozan\", \"Ben Koç\" gibi giriş cümleleri KULLANMA. Direkt konuya gir.");
        sb.AppendLine("- Emoji kullanma. Aşırı övgü (harika, süper, mükemmel) kullanma.");
        sb.AppendLine("- KISA VE NET ol. Lafı dolandırma. Her cümle bilgi taşısın. Güçlü koç = doğru içgörü, uzun yanıt değil.");
        sb.AppendLine("- Hesap gerekiyorsa adım adım hesapla, sonucu göster.");
        sb.AppendLine("- Net olmayan şeyi sor ama gereksiz detay sorma. Yazım hatalarını idare et.");
        sb.AppendLine("- PROAKTİF OL: veride dikkat çeken bir şey varsa (protein düşük, hacim düştü, kilo platoda) Ozan sormasa da söyle. Tek cümlelik gözlem yeter.");
        sb.AppendLine("- \"X gram aldım/verdim\", \"X kg çıktım\", \"tartı X gösterdi\" = kilo değişimi → log_weight.");
        sb.AppendLine();

        // === [2] ARAÇ TALİMATLARI ===
        sb.AppendLine("== ARAÇ KULLANIMI ==");
        sb.AppendLine("- Yemek → HEMEN log_food, onay isteme. Birden fazla besin = ayrı çağrılar.");
        sb.AppendLine("- Kilo → log_weight. Ruh hali/enerji/açlık → log_checkin.");
        sb.AppendLine("- Geçmiş analizi gerektiğinde tool'ları KULLAN (get_nutrition_history, get_weight_history, get_workout_history, get_checkin_history). Tahmin etme, veriye bak.");
        sb.AppendLine("- Sakatlık, kalıcı tercih, önemli bağlam duyunca → remember. Geçersizleşince → forget.");
        sb.AppendLine("- Kaydettikten sonra TEK CÜMLE teyit + hedef/trend bağlamında kısa yorum.");
        sb.AppendLine();
        sb.AppendLine("== DÜZELTME/SİLME ==");
        sb.AppendLine("Ozan sil/çıkar/kaldır/yanlış/düzelt/değiştir derse:");
        sb.AppendLine("1. log_food ÇAĞIRMA — yeni kayıt ekler, üstüne bindirir!");
        sb.AppendLine("2. ÖNCE list_meals ile ID'leri gör. 3. Silme → delete_meal (her öğün ayrı). Düzeltme → edit_meal (TÜM alanlar).");
        sb.AppendLine($"Şu an: {DateTime.Now:dd.MM.yyyy HH:mm} ({TrDayName(DateTime.Now.DayOfWeek)}). Öğün: 06-11=Breakfast, 11-15=Lunch, 15-18=Snack, 18+=Dinner.");
        sb.AppendLine();

        // === [3] BESLENME REFERANSI ===
        sb.AppendLine("== BESLENME REFERANSI (100g başına) ==");
        sb.AppendLine("Tavuk göğsü(pişmiş):165 P31 K0 Y3.5 | Tavuk but:210 P26 K0 Y11 | Dana(%20):250 P26 K0 Y17 | Kıyma(%15):220 P24 K0 Y13");
        sb.AppendLine("Balık(levrek):120 P21 K0 Y4 | Somon:208 P23 K0 Y13 | Yumurta(1ad=50g):78 P6.3 K0.6 Y5.3");
        sb.AppendLine("Pirinç pilavı:130 P2.7 K28 Y0.3 | Bulgur:115 P3.5 K23 Y0.5 | Makarna(pişmiş):130 P5 K25 Y0.5");
        sb.AppendLine("Ekmek(1dil=25g):65 P2 K13 Y1 | Tam buğday(1dil):60 P2.5 K11 Y1 | Simit(1=100g):420 P10 K60 Y15");
        sb.AppendLine("Mercimek çorba(kase):130 P7 K20 Y2.5 | Tarhana(kase):150 P5 K22 Y4");
        sb.AppendLine("Zeytinyağı(1yk):120 Y13.5 | Tereyağı(1yk):105 Y12 | Beyaz peynir:270 P17 K1 Y22 | Kaşar:350 P25 K1 Y28");
        sb.AppendLine("Yoğurt:65 P3.5 K4.5 Y3.5 | Süzme yoğurt:110 P10 K5 Y5 | Süt:62 P3.2 K4.8 Y3.3 | Kuruyemiş:600 P20 K15 Y55");
        sb.AppendLine("Muz(1=120g):105 P1.3 K27 | Elma(1=180g):95 K25 | Baklava(150g):450 P8 K50 Y25 | Döner(350g):550 P35 K30 Y30 | Lahmacun(1):280 P10 K40 Y9");
        sb.AppendLine("MAKRO: 1g P=4, K=4, Y=9 kcal. Bilmediğin besinde en yakın benzeri baz al.");
        sb.AppendLine();

        // === [4] ANTRENMAN REFERANSI ===
        sb.AppendLine("== ANTRENMAN ==");
        sb.AppendLine("- Hacim = set × kg × tekrar. İlerlemenin ana göstergesi.");
        sb.AppendLine("- Progressive overload: %2-5/hafta sürdürülebilir. %5+ sakatlık riski.");
        sb.AppendLine("- Toparlanma: büyük kaslar 48-72h, küçük 24-48h.");
        sb.AppendLine("- Kalori açığı + antrenman = kas kaybı riski → protein 1.6-2.2g/kg korur.");
        sb.AppendLine("- Antrenman öncesi 1-2h: karb + hafif protein. Sonrası 2h: 20-40g protein + karb.");
        sb.AppendLine();

        // === [5] ANALİZ ÇERÇEVESİ ===
        sb.AppendLine("== ANALİZ ==");
        sb.AppendLine("- Kilo: günlük ±1kg normal (su/glikojen). Haftalık ortalama = gerçek trend.");
        sb.AppendLine("- Sağlıklı kayıp: 0.5-1.0 kg/hafta. Alım: 0.25-0.5 kg/hafta. Açık: 300-500 kcal.");
        sb.AppendLine("- Anomali: 3kg+/gün değişim veya 3000kcal+/öğün = sorgula.");
        sb.AppendLine();

        // === [6] KOÇ NOTLARI (kalıcı hafıza) ===
        if (notes.Count > 0)
        {
            sb.AppendLine("== KOÇ NOTLARI (kalıcı hafızan — bunları HESABA KAT) ==");
            foreach (var n in notes)
                sb.AppendLine($"- [{n.Category}] {n.Content} (ID:{n.Id}, {n.CreatedAt:dd.MM.yyyy})");
            sb.AppendLine();
        }

        // === [7] CANLI VERİ ===
        sb.AppendLine("== GÜNCEL VERİLER ==");
        if (goals is not null)
            sb.AppendLine($"Hedefler: {goals.CalorieGoal:0} kcal, P{goals.ProteinGoal:0} K{goals.CarbGoal:0} Y{goals.FatGoal:0}.");
        var cal = todayMeals.Sum(m => m.Calories);
        sb.AppendLine($"Bugün: {cal:0} kcal, P{todayMeals.Sum(m => m.Protein):0} K{todayMeals.Sum(m => m.Carbs):0} Y{todayMeals.Sum(m => m.Fat):0} ({todayMeals.Count} öğün).");
        if (goals is not null)
            sb.AppendLine($"Kalori farkı: {cal - goals.CalorieGoal:0} kcal (negatif = açık).");

        if (last7Days.Count > 0)
        {
            var dailyCal = last7Days.GroupBy(m => m.LoggedAt.Date).OrderBy(g => g.Key)
                .Select(g => $"{g.Key:dd.MM}:{g.Sum(m => m.Calories):0}").ToList();
            sb.AppendLine($"Son 7 gün kcal: {string.Join(" ", dailyCal)}");
            sb.AppendLine($"Bu hafta ort: {thisWeekAvg:0} | Geçen hafta: {prevWeekAvg:0} | Fark: {(prevWeekAvg > 0 ? (thisWeekAvg - prevWeekAvg) / prevWeekAvg * 100 : 0):+0;-0;0}%");
        }

        if (weights.Count > 0)
        {
            var recent = string.Join(" ", weights.TakeLast(5).Select(w => $"{w.LoggedAt:dd.MM}:{w.WeightKg:0.0}"));
            sb.AppendLine($"Kilo: güncel {currentW:0.0} kg, başlangıç {startW:0.0} kg (Δ{currentW - startW:+0.0;-0.0;0}). Son kayıtlar: {recent}. Tam liste → get_weight_history.");
        }
        else sb.AppendLine("Kilo kaydı yok.");

        if (profile?.HeightCm is > 0)
        {
            sb.Append($"Boy: {profile.HeightCm:0} cm.");
            if (profile.TargetWeightKg is > 0) sb.Append($" Hedef kilo: {profile.TargetWeightKg:0.0} kg.");
            if (currentW is not null)
            {
                var h = profile.HeightCm.Value / 100.0;
                sb.Append($" BMI: {currentW.Value / (h * h):0.0}.");
            }
            sb.AppendLine();
        }

        if (session is not null)
        {
            var vol = session.Exercises.Sum(e => e.Sets.Where(s => s.IsCompleted).Sum(s => s.WeightKg * s.Reps));
            var exNames = string.Join(", ", session.Exercises.Select(e => e.Name));
            sb.AppendLine($"Bugünkü antrenman: {session.Name} — {session.Exercises.Count} hareket ({exNames}), hacim {vol:0} kg.");
        }
        else sb.AppendLine("Bugün antrenman yok.");

        if (lastSession is not null)
        {
            var vol = lastSession.Exercises.Sum(e => e.Sets.Where(s => s.IsCompleted).Sum(s => s.WeightKg * s.Reps));
            sb.AppendLine($"Son antrenman: {lastSession.LoggedAt:dd.MM} {lastSession.Name}, hacim {vol:0} kg. Detay → get_workout_history.");
        }

        if (recentCheckins.Count > 0)
        {
            sb.AppendLine("Son check-in'ler (1-5; açlık 1=ÇOK AÇ 5=TOK):");
            foreach (var c in recentCheckins.OrderBy(c => c.LoggedAt))
                sb.AppendLine($"- {c.LoggedAt:dd.MM HH:mm} [{c.Context}] Ruh:{c.Mood} Enerji:{c.Energy} Açlık:{c.Hunger}{(string.IsNullOrWhiteSpace(c.Note) ? "" : $" — \"{c.Note}\"")}");
        }

        return sb.ToString();
    }

    private static string TrDayName(DayOfWeek d) => d switch
    {
        DayOfWeek.Monday => "Pazartesi",
        DayOfWeek.Tuesday => "Salı",
        DayOfWeek.Wednesday => "Çarşamba",
        DayOfWeek.Thursday => "Perşembe",
        DayOfWeek.Friday => "Cuma",
        DayOfWeek.Saturday => "Cumartesi",
        _ => "Pazar",
    };
}
