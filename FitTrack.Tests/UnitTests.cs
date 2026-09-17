using System.Security.Cryptography;
using FitTrack.API.Controllers;
using FitTrack.API.Data;
using FitTrack.API.Models;
using FitTrack.API.Security;
using FitTrack.API.Services;
using FitTrack.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace FitTrack.Tests;

public class KeyProtectorTests
{
    private static KeyProtector Make(string? current, string? previous = null, string env = "Production")
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FITTRACK_ENCRYPTION_KEY"] = current,
            ["FITTRACK_ENCRYPTION_KEY_PREVIOUS"] = previous,
        }).Build();
        return new KeyProtector(cfg, new FakeEnv(env), NullLogger<KeyProtector>.Instance);
    }

    private static string NewKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    [Fact]
    public void Roundtrip_and_nondeterministic()
    {
        var p = Make(NewKey());
        var user = Guid.NewGuid();
        var c1 = p.Protect("sk-ant-secret", user);
        var c2 = p.Protect("sk-ant-secret", user);
        Assert.NotEqual(c1, c2); // rastgele nonce
        Assert.True(p.TryUnprotect(c1, user, out var plain, out var rewrap));
        Assert.Equal("sk-ant-secret", plain);
        Assert.False(rewrap);
    }

    [Fact]
    public void Bound_to_user_id()
    {
        var p = Make(NewKey());
        var c = p.Protect("sk-ant-secret", Guid.NewGuid());
        Assert.False(p.TryUnprotect(c, Guid.NewGuid(), out _, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("v2:abc:AAAA")]
    [InlineData("v1:deadbeef:AAAA")]
    [InlineData("v1")]
    public void Rejects_malformed(string stored)
    {
        Assert.False(Make(NewKey()).TryUnprotect(stored, Guid.NewGuid(), out _, out _));
    }

    [Fact]
    public void Rejects_tampering_and_bad_base64()
    {
        var p = Make(NewKey());
        var user = Guid.NewGuid();
        var parts = p.Protect("sk-ant-secret", user).Split(':');
        var blob = Convert.FromBase64String(parts[2]);
        blob[^1] ^= 0xFF;
        Assert.False(p.TryUnprotect($"{parts[0]}:{parts[1]}:{Convert.ToBase64String(blob)}", user, out _, out _));
        Assert.False(p.TryUnprotect($"{parts[0]}:{parts[1]}:!!notbase64!!", user, out _, out _));
        Assert.False(p.TryUnprotect($"{parts[0]}:{parts[1]}:AAAA", user, out _, out _));
    }

    [Fact]
    public void Different_master_key_cannot_decrypt()
    {
        var user = Guid.NewGuid();
        var c = Make(NewKey()).Protect("sk-ant-secret", user);
        Assert.False(Make(NewKey()).TryUnprotect(c, user, out _, out _));
    }

    [Fact]
    public void Rotation_reads_previous_and_requests_rewrap()
    {
        var oldKey = NewKey();
        var user = Guid.NewGuid();
        var c = Make(oldKey).Protect("sk-ant-secret", user);
        var rotated = Make(NewKey(), previous: oldKey);
        Assert.True(rotated.TryUnprotect(c, user, out var plain, out var rewrap));
        Assert.Equal("sk-ant-secret", plain);
        Assert.True(rewrap);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-base64!!")]
    [InlineData("AAAA")] // 3 bayt
    public void Missing_or_invalid_master_key_disables_in_production(string? key)
    {
        var p = Make(key);
        Assert.False(p.IsAvailable);
        Assert.Throws<InvalidOperationException>(() => p.Protect("x", Guid.NewGuid()));
        Assert.False(p.TryUnprotect("v1:x:AAAA", Guid.NewGuid(), out _, out _));
    }

    private sealed class FakeEnv(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "FitTrack";
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}

public class ValidationTests
{
    [Theory]
    [InlineData(" User@Example.COM ", "user@example.com")]
    [InlineData("a.b+c@d.co", "a.b+c@d.co")]
    [InlineData("no-at-sign", null)]
    [InlineData("x@localhost", null)]
    [InlineData("Name <a@b.com>", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void NormalizeEmail(string? input, string? expected) => Assert.Equal(expected, AuthController.NormalizeEmail(input));

    [Fact]
    public void Email_length_limit() => Assert.Null(AuthController.NormalizeEmail(new string('a', 250) + "@b.com"));

    [Theory]
    [InlineData("sk-ant-api03-abcdefghijklmnop", true)]
    [InlineData("short", false)]
    [InlineData("sk-ant-with space-abcdefghijk", false)]
    [InlineData("sk-ant-ünicode-abcdefghijklmn", false)]
    public void LooksLikeKey(string key, bool expected) => Assert.Equal(expected, ApiKeyService.LooksLikeKey(key));

    [Fact]
    public void Link_codes_are_random_unambiguous_and_hash_case_insensitively()
    {
        var codes = Enumerable.Range(0, 200).Select(_ => TelegramBotService.NewLinkCode()).ToList();
        Assert.All(codes, c => Assert.Matches("^[A-HJ-NP-Z2-9]{8}$", c));
        Assert.True(codes.Distinct().Count() > 195);
        Assert.Equal(TelegramBotService.HashLinkCode("ABCD2345"), TelegramBotService.HashLinkCode(" abcd2345 "));
        Assert.DoesNotContain("ABCD2345", TelegramBotService.HashLinkCode("ABCD2345"));
    }

    [Fact]
    public void Telegram_enabled_only_with_token_and_polling()
    {
        IConfiguration C(string? token, string? polling) => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Telegram:BotToken"] = token, ["Telegram:Polling"] = polling }).Build();
        Assert.False(TelegramBotService.IsEnabled(C(null, "true")));
        Assert.False(TelegramBotService.IsEnabled(C("t", null)));
        Assert.False(TelegramBotService.IsEnabled(C("t", "false")));
        Assert.True(TelegramBotService.IsEnabled(C("t", "true")));
    }
}

/// <summary>Veritabanı katmanındaki sahiplik kalkanı: controller hata yapsa bile yazma engellenir.</summary>
public class OwnershipGuardTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "fittrack-tests", Guid.NewGuid().ToString("N"));

    private (AppDbContext Db, CurrentUser User) Context()
    {
        Directory.CreateDirectory(_dir);
        var user = new CurrentUser(new HttpContextAccessor());
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={Path.Combine(_dir, "g.db")};Pooling=False").Options;
        var db = new AppDbContext(options, user);
        db.Database.EnsureCreated();
        return (db, user);
    }

    [Fact]
    public void Anonymous_context_cannot_write_or_read_owned_rows()
    {
        var (db, _) = Context();
        db.MealEntries.Add(new MealEntry { Id = Guid.NewGuid(), FoodName = "x" });
        Assert.Throws<UnauthorizedAccessException>(() => db.SaveChanges());
        Assert.Empty(db.MealEntries.ToList());
    }

    [Fact]
    public void Insert_is_stamped_and_foreign_owner_rejected()
    {
        var (db, user) = Context();
        var me = Guid.NewGuid();
        user.ActAs(me);
        var mine = new MealEntry { Id = Guid.NewGuid(), FoodName = "mine" };
        db.MealEntries.Add(mine);
        db.SaveChanges();
        Assert.Equal(me, mine.UserId);

        db.MealEntries.Add(new MealEntry { Id = Guid.NewGuid(), FoodName = "theirs", UserId = Guid.NewGuid() });
        Assert.Throws<UnauthorizedAccessException>(() => db.SaveChanges());
    }

    [Fact]
    public async Task Cannot_modify_or_reassign_foreign_rows_even_when_attached()
    {
        var (db, user) = Context();
        var me = Guid.NewGuid();
        user.ActAs(me);

        var foreign = new MealEntry { Id = Guid.NewGuid(), FoodName = "foreign", UserId = Guid.NewGuid() };
        db.Attach(foreign);
        foreign.FoodName = "hacked";
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        var mine = new MealEntry { Id = Guid.NewGuid(), FoodName = "mine" };
        db.MealEntries.Add(mine);
        await db.SaveChangesAsync();
        mine.UserId = Guid.NewGuid(); // sahibini değiştirme denemesi
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        var deleteForeign = new WeightLog { Id = Guid.NewGuid(), UserId = Guid.NewGuid() };
        db.Attach(deleteForeign);
        db.Remove(deleteForeign);
        Assert.Throws<UnauthorizedAccessException>(() => db.SaveChanges());
    }

    [Fact]
    public void Child_rows_require_owned_parent()
    {
        var (db, user) = Context();
        var me = Guid.NewGuid();
        user.ActAs(me);

        db.Exercises.Add(new Exercise { Id = Guid.NewGuid(), WorkoutSessionId = Guid.NewGuid(), Name = "orphan" });
        Assert.Throws<UnauthorizedAccessException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();

        db.ExerciseSets.Add(new ExerciseSet { Id = Guid.NewGuid(), ExerciseId = Guid.NewGuid() });
        Assert.Throws<UnauthorizedAccessException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();

        // Aynı SaveChanges içinde eklenen kendi seansı + hareketi + seti geçerli
        var s = new WorkoutSession { Id = Guid.NewGuid(), Name = "push" };
        var e = new Exercise { Id = Guid.NewGuid(), WorkoutSessionId = s.Id, Name = "bench" };
        var set = new ExerciseSet { Id = Guid.NewGuid(), ExerciseId = e.Id, Reps = 5 };
        db.AddRange(s, e, set);
        db.SaveChanges();
        Assert.Single(db.ExerciseSets.ToList());
    }

    [Fact]
    public void ActAs_is_single_assignment()
    {
        var user = new CurrentUser(new HttpContextAccessor());
        var id = Guid.NewGuid();
        user.ActAs(id);
        user.ActAs(id); // aynı kullanıcı tekrar: sorun yok
        Assert.Throws<InvalidOperationException>(() => user.ActAs(Guid.NewGuid()));
        Assert.Equal(id, user.Require());
        Assert.Throws<UnauthorizedAccessException>(() => new CurrentUser(new HttpContextAccessor()).Require());
    }

    [Fact]
    public void ActAs_refused_inside_http_request()
    {
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        Assert.Throws<InvalidOperationException>(() => new CurrentUser(accessor).ActAs(Guid.NewGuid()));
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, true); } catch { }
    }
}
