using FitTrack.API.Controllers;
using FitTrack.API.Data;
using FitTrack.API.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.API.Security;

/// <summary>
/// E-posta ile sıfırlama yok. Parolası unutulan hesap, sunucu sahibinin ortam değişkeniyle açılır:
/// <c>FITTRACK_RESET_EMAIL</c> + <c>FITTRACK_RESET_PASSWORD</c> ayarlanıp servis yeniden başlatılır.
/// Değişkenlere yalnız Railway projesinin sahibi erişebilir. Parola zaten o değerse hiçbir şey
/// yapılmaz — değişkenler unutulup kalırsa her yeniden başlatmada oturumlar ve Telegram bağlantısı düşmesin diye.
/// </summary>
public static class PasswordReset
{
    public static void Apply(AppDbContext db, IPasswordHasher<User> hasher, ILogger log, string? rawEmail, string? password)
    {
        if (string.IsNullOrWhiteSpace(rawEmail) && string.IsNullOrEmpty(password)) return;

        var email = AuthController.NormalizeEmail(rawEmail);
        if (email is null || string.IsNullOrEmpty(password))
        {
            log.LogWarning("Parola sıfırlama atlandı: FITTRACK_RESET_EMAIL ve FITTRACK_RESET_PASSWORD birlikte ve geçerli olmalı.");
            return;
        }

        var error = AuthController.ValidatePassword(password, email);
        if (error is not null)
        {
            log.LogWarning("Parola sıfırlama atlandı: {Error}", error);
            return;
        }

        var user = db.Users.FirstOrDefault(u => u.Email == email);
        if (user is null)
        {
            log.LogWarning("Parola sıfırlama atlandı: bu e-postayla hesap yok.");
            return;
        }

        if (hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed)
        {
            log.LogInformation("Parola sıfırlama: parola zaten güncel, değişiklik yok. Değişkenleri silebilirsin.");
            return;
        }

        user.PasswordHash = hasher.HashPassword(user, password);
        user.FailedLoginCount = 0;
        user.LastFailedLoginAt = null;
        user.LockoutUntil = null;
        user.SessionVersion++; // eski oturumlar düşsün
        user.DropTelegramLink(); // parolayla birlikte Telegram erişimi de düşer
        db.SaveChanges();
        log.LogWarning("Parola sıfırlandı: {UserId}. FITTRACK_RESET_* değişkenlerini şimdi sil.", user.Id);
    }
}
