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
    private const string Model = "claude-haiku-4-5-20251001";
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
                ["max_tokens"] = 1024,
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
            ["description"] = "Ozan bir şey yediğini söylediğinde besini günlüğe ekle. Makroları bilmiyorsan besin adı ve gramına göre en iyi beslenme tahminini yap. calories/protein/carbs/fat VERİLEN TOPLAM MİKTAR için (100g başına değil). Öğünü sadate/bağlama göre seç.",
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
                        LoggedAt = DateTime.Now,
                    };
                    _db.MealEntries.Add(entry);
                    await _db.SaveChangesAsync();
                    return ($"Eklendi: {entry.FoodName} {entry.Grams:0}g, {entry.Calories:0} kcal (P{entry.Protein:0}/K{entry.Carbs:0}/Y{entry.Fat:0}).", "nutrition", false);
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
        sb.AppendLine("Sen Ozan'ın kişisel sağlık ve fitness koçusun. Adın Koç.");
        sb.AppendLine("Tarz: Türkçe. AZ VE ÖZ konuş — maksimum 3 cümle. Soru sorma, lafı uzatma, motive edici konuşmalar yapma. SADECE veriye dayalı bilgi ver.");
        sb.AppendLine("Emoji kullanma. 'Harika', 'süper', 'Harikasın' gibi kelimeler kullanma.");
        sb.AppendLine();
        sb.AppendLine("== ARAÇLAR (önemli) ==");
        sb.AppendLine("Ozan bir şey yediğini söylediğinde `log_food` aracıyla besini KENDİN ekle — onay isteme, direkt kaydet. Birden fazla besin varsa her biri için ayrı çağır. Makroyu bilmiyorsan besin ve gramına göre tahmin et.");
        sb.AppendLine("Kilo söylerse `log_weight`, ruh hali/enerji/açlık belirtirse `log_checkin` çağır. Kaydettikten sonra ne eklediğini kısaca teyit et ve yorumla (hedefe etkisi vb.).");
        sb.AppendLine($"Şu anki saat: {DateTime.Now:HH:mm}. Öğünü saate göre seç (sabah=Breakfast, öğle=Lunch, akşam=Dinner, ara=Snack) ya da Ozan söylerse ona göre.");
        sb.AppendLine();
        sb.AppendLine("== GÜNCEL VERİLER (bugün) ==");

        if (goals is not null)
            sb.AppendLine($"Hedefler: {goals.CalorieGoal:0} kcal, Protein {goals.ProteinGoal:0}g, Karb {goals.CarbGoal:0}g, Yağ {goals.FatGoal:0}g.");
        sb.AppendLine($"Bugün alınan: {cal:0} kcal, Protein {pro:0}g, Karb {carb:0}g, Yağ {fat:0}g.");
        if (goals is not null)
            sb.AppendLine($"Kalori farkı: {cal - goals.CalorieGoal:0} kcal (negatif = açık).");

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
