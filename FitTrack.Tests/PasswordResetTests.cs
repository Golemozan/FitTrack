using System.Net;
using System.Net.Http.Json;
using FitTrack.API.Data;
using FitTrack.API.Models;
using FitTrack.API.Security;
using FitTrack.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace FitTrack.Tests;

public class PasswordResetTests : IClassFixture<TestApp>
{
    private readonly TestApp _app;
    public PasswordResetTests(TestApp app) => _app = app;

    private void Reset(string? email, string? password)
    {
        using var scope = _app.Services.CreateScope();
        PasswordReset.Apply(scope.ServiceProvider.GetRequiredService<AppDbContext>(),
            scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>(), NullLogger.Instance, email, password);
    }

    private Task<HttpResponseMessage> Login(string email, string password) =>
        _app.Browser().PostAsJsonAsync("/api/auth/login", new { email, password });

    [Fact]
    public async Task Reset_replaces_password_and_drops_old_sessions()
    {
        var (oldSession, _, email, oldPassword) = await _app.RegisterAsync("Reset");
        const string newPassword = "yepyeni-parola-42";

        Reset(email.ToUpperInvariant(), newPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(email, oldPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Login(email, newPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await oldSession.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Reset_clears_lockout()
    {
        var (_, _, email, password) = await _app.RegisterAsync("Locked");
        // Hesap geneli kilit: her deneme farklı adresten (kaynak kilidine takılmadan) 30 hata.
        for (var i = 0; i < 30; i++)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login") { Content = JsonContent.Create(new { email, password = "yanlis-parola-000" }) };
            req.Headers.Add("X-Forwarded-For", $"198.51.100.{i + 1}");
            await _app.Browser().SendAsync(req);
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(email, password)).StatusCode);

        Reset(email, "kilit-acan-parola-7");
        Assert.Equal(HttpStatusCode.OK, (await Login(email, "kilit-acan-parola-7")).StatusCode);
    }

    [Fact]
    public async Task Same_password_again_keeps_sessions()
    {
        var (session, _, email, _) = await _app.RegisterAsync("Idem");
        Reset(email, "ayni-kalan-parola-1");
        var fresh = _app.Browser();
        Assert.Equal(HttpStatusCode.OK, (await fresh.PostAsJsonAsync("/api/auth/login", new { email, password = "ayni-kalan-parola-1" })).StatusCode);

        Reset(email, "ayni-kalan-parola-1"); // değişken unutuldu, servis yeniden başladı
        Assert.Equal(HttpStatusCode.OK, (await fresh.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Invalid_input_changes_nothing()
    {
        var (_, _, email, password) = await _app.RegisterAsync("Weak");
        Reset(email, "kisa");
        Reset("yok-boyle-biri@test.com", "gecerli-bir-parola-9");
        Reset(email, null);
        Assert.Equal(HttpStatusCode.OK, (await Login(email, password)).StatusCode);
    }
}
