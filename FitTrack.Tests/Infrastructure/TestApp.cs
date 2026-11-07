using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using FitTrack.API.Data;
using FitTrack.API.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace FitTrack.Tests.Infrastructure;

/// <summary>
/// Gerçek uygulamayı (Program.cs, tüm middleware, gerçek SQLite dosyası) bellek içi sunucuda ayağa kaldırır.
/// Her fabrika kendi geçici veri klasörünü kullanır; testler birbirinin verisini görmez.
/// Dış dünyaya tek çıkış (Anthropic) <see cref="FakeAnthropic"/> ile değiştirilir.
/// </summary>
public class TestApp : WebApplicationFactory<Program>
{
    public const string LegacyPassword = "eski-uygulama-parolasi";

    public string DataDir { get; } = Path.Combine(Path.GetTempPath(), "fittrack-tests", Guid.NewGuid().ToString("N"));
    public FakeAnthropic Anthropic { get; } = new();
    public string EncryptionKey { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    protected virtual string EnvironmentName => "Development";
    protected virtual bool WithEncryptionKey => true;
    protected virtual int AuthLimit => 100_000;
    protected virtual int SensitiveLimit => 100_000;
    // Genel limiter testlerde kapalı değil, yalnız yüksek: kendisi GlobalLimiterTests'te varsayılan ve düşük değerlerle sınanır.
    protected virtual int? GlobalLimit => 1_000_000;
    protected virtual int? GlobalWriteLimit => 1_000_000;
    protected virtual int RegisterLimit => 100_000;
    protected virtual int? RegisterIpLimit => 100_000;
    protected virtual bool CaptureLogs => false;
    protected virtual bool TelegramEnabled => false;
    public const string TelegramToken = "test-bot-token";

    /// <summary>Testin bağlantı adresini belirler (gerçek uygulamada böyle bir başlık yok, bkz. <see cref="ClientIpProbe"/>).</summary>
    public const string TestClientIpHeader = "X-Test-Client-Ip";
    public const string SeenIpHeader = "X-Test-Seen-Ip";
    public const string SeenSchemeHeader = "X-Test-Seen-Scheme";

    /// <summary><see cref="CaptureLogs"/> açıkken uygulamanın yazdığı her log satırı.</summary>
    public CapturedLogs Logs { get; } = new();
    /// <summary>Program.cs'in kaydettiği, testte çalıştırılmayan arka plan servisleri.</summary>
    public List<Type> RegisteredBackgroundServices { get; } = new();

    public string DbPath => Path.Combine(DataDir, "fittrack.db");

    public TestApp()
    {
        Directory.CreateDirectory(DataDir);
        BeforeStart();
    }

    /// <summary>Uygulama açılmadan veritabanını hazırlamak isteyen alt sınıflar için (ör. eski şema).</summary>
    protected virtual void BeforeStart() { }

    /// <summary>Alt sınıfların ek yapılandırması (ör. ortamdan gelen log ayarları).</summary>
    protected virtual void ExtraSettings(IWebHostBuilder builder) { }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);
        builder.UseSetting("FITTRACK_DATA_DIR", DataDir);
        builder.UseSetting("FITTRACK_API_KEY", LegacyPassword);
        builder.UseSetting("FITTRACK_ENCRYPTION_KEY", WithEncryptionKey ? EncryptionKey : "");
        builder.UseSetting("RateLimit:Auth", AuthLimit.ToString());
        builder.UseSetting("RateLimit:Sensitive", SensitiveLimit.ToString());
        builder.UseSetting("RateLimit:Coach", "100000");
        builder.UseSetting("RateLimit:Legacy", "100000");
        builder.UseSetting("RateLimit:Register", RegisterLimit.ToString());
        if (RegisterIpLimit is { } registerIp) builder.UseSetting("RateLimit:RegisterIp", registerIp.ToString());
        if (GlobalLimit is { } global) builder.UseSetting("RateLimit:Global", global.ToString());
        if (GlobalWriteLimit is { } globalWrite) builder.UseSetting("RateLimit:GlobalWrite", globalWrite.ToString());
        builder.UseSetting("RateLimit:Destructive", SensitiveLimit.ToString());
        builder.UseSetting("Telegram:BotToken", TelegramEnabled ? TelegramToken : "");
        builder.UseSetting("Telegram:Polling", TelegramEnabled ? "true" : "false");
        ExtraSettings(builder);

        builder.ConfigureServices(services =>
        {
            // Arka plan döngüleri testte kendiliğinden koşmaz; testler onları doğrudan çağırır.
            foreach (var hosted in services.Where(d => d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)
                         && d.ImplementationType?.Namespace == "FitTrack.API.Services").ToList())
            {
                RegisteredBackgroundServices.Add(hosted.ImplementationType!);
                services.Remove(hosted);
            }
            services.ConfigureAll<HttpClientFactoryOptions>(o =>
                o.HttpMessageHandlerBuilderActions.Add(b => b.PrimaryHandler = Anthropic));
            services.AddSingleton<IStartupFilter>(new ClientIpProbe());
            if (CaptureLogs) services.AddSingleton<ILoggerProvider>(Logs);
        });
    }

    /// <summary>Tarayıcı gibi davranan istemci: çerez tutar, CSRF başlığını gönderir.</summary>
    public HttpClient Browser(bool csrfHeader = true)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
            AllowAutoRedirect = false,
        });
        if (csrfHeader) client.DefaultRequestHeaders.Add(CsrfHeaderMiddleware.HeaderName, CsrfHeaderMiddleware.HeaderValue);
        return client;
    }

    public async Task<(HttpClient Client, Guid Id, string Email, string Password)> RegisterAsync(string name = "Test")
    {
        var client = Browser();
        var email = $"{name.ToLowerInvariant()}-{Guid.NewGuid():N}@test.com";
        var password = "test-parola-" + Guid.NewGuid().ToString("N")[..8];
        var resp = await client.PostAsJsonAsync("/api/auth/register", new { email, password, displayName = name });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return (client, body.GetProperty("id").GetGuid(), email, password);
    }

    public FitTrack.API.Services.TelegramBotService TelegramBot() =>
        ActivatorUtilities.CreateInstance<FitTrack.API.Services.TelegramBotService>(Services);

    public FitTrack.API.Services.DailyCoachService DailyCoach() =>
        ActivatorUtilities.CreateInstance<FitTrack.API.Services.DailyCoachService>(Services);

    public SqliteConnection OpenDb()
    {
        var conn = new SqliteConnection($"Data Source={DbPath};Pooling=False");
        conn.Open();
        return conn;
    }

    public long Scalar(string sql, params (string Name, object Value)[] args)
    {
        using var conn = OpenDb();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (n, v) in args) cmd.Parameters.AddWithValue(n, v);
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    public void Exec(string sql, params (string Name, object Value)[] args)
    {
        using var conn = OpenDb();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (n, v) in args) cmd.Parameters.AddWithValue(n, v);
        cmd.ExecuteNonQuery();
    }

    public static string SqlGuid(Guid id) => SchemaUpgrade.SqlGuid(id);

    /// <summary>
    /// TestServer'da gerçek bağlantı adresi yok. Bu adım zincirin en başında durur: başlıkla bağlantı adresi
    /// verilebilir (test, farklı adreslerden gelen istemciyi böyle kurar) ve yanıtta uygulamanın sonunda
    /// gördüğü IP ile şema okunur.
    /// </summary>
    private sealed class ClientIpProbe : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (ctx, nextMiddleware) =>
            {
                if (ctx.Request.Headers.TryGetValue(TestClientIpHeader, out var ip))
                    ctx.Connection.RemoteIpAddress = IPAddress.Parse(ip.ToString());
                ctx.Response.OnStarting(() =>
                {
                    ctx.Response.Headers[SeenIpHeader] = ctx.Connection.RemoteIpAddress?.ToString() ?? "";
                    ctx.Response.Headers[SeenSchemeHeader] = ctx.Request.Scheme;
                    return Task.CompletedTask;
                });
                await nextMiddleware();
            });
            next(app);
        };
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        // Yalnız kendi havuzu: ClearAllPools süreç geneli, paralel koşan başka bir fabrikanın açmakta olduğu
        // bağlantıyı da kapatıp rastgele ObjectDisposedException üretiyordu.
        using (var own = new SqliteConnection($"Data Source={DbPath}")) SqliteConnection.ClearPool(own);
        try { Directory.Delete(DataDir, recursive: true); } catch { /* Windows'ta dosya kilidi kalabilir */ }
    }
}

public static class HttpExtensions
{
    public static async Task<JsonElement> JsonAsync(this HttpResponseMessage resp)
    {
        var text = await resp.Content.ReadAsStringAsync();
        return string.IsNullOrEmpty(text) ? default : JsonDocument.Parse(text).RootElement.Clone();
    }

    public static Task<HttpResponseMessage> PutAsJson<T>(this HttpClient c, string url, T body) => c.PutAsJsonAsync(url, body);
}
