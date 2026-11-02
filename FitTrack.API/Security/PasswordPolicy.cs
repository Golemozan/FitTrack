namespace FitTrack.API.Security;

/// <summary>
/// Uzunluk tek başına yetmiyor: "1234567890", "password12", "qwertyuiop" 10 karakter ama ilk denenen
/// parolalar. Liste dışarıdan indirilmiyor — en yaygın kökler + kalıp kuralları, sızıntı listelerinin
/// tepesini kapatıyor. "Passw0rd!!" gibi harf-rakam takası da köke indirgenip yakalanıyor.
/// </summary>
public static class PasswordPolicy
{
    private static readonly string[] CommonRoots =
    {
        "password", "passwort", "parola", "sifre", "qwerty", "asdf", "zxcv", "letmein", "welcome", "iloveyou",
        "admin", "login", "master", "monkey", "dragon", "football", "futbol", "baseball", "superman", "batman",
        "sunshine", "princess", "shadow", "michael", "trustno", "starwars", "whatever", "freedom", "hello",
        "galatasaray", "fenerbahce", "besiktas", "trabzonspor", "istanbul", "ankara", "turkiye", "seniseviyorum",
        "fittrack", "fitness", "gym", "abc", "test", "user", "secret", "changeme",
    };

    private static readonly string[] Sequences =
    {
        "qwertyuiopasdfghjklzxcvbnm", "1234567890", "0987654321", "abcdefghijklmnopqrstuvwxyz", "zyxwvutsrqponmlkjihgfedcba",
        "qazwsxedcrfvtgbyhnujmikolp", "1qaz2wsx3edc4rfv",
    };

    public static string? Check(string password, string email)
    {
        var lower = password.ToLowerInvariant();
        if (lower.All(char.IsDigit)) return "Parola yalnız rakamdan oluşamaz.";

        var normalized = Normalize(lower);
        var letters = new string(normalized.Where(char.IsLetterOrDigit).ToArray());

        // Kök + birkaç karakter ("password12", "Galatasaray1905") — kök çıkınca geriye anlamlı bir şey kalmıyor.
        foreach (var root in CommonRoots)
            if (letters.Contains(root) && letters.Length - root.Length <= 5)
                return "Bu parola çok yaygın. Tahmin edilmesi zor bir şey seç.";

        // Klavye ya da alfabe dizisi: parolanın çoğu tek bir dizinin parçasıysa. Ham hâli üzerinde —
        // rakamı harfe çevirmek "1qaz2wsx" gibi dizileri bozardı.
        var raw = new string(lower.Where(char.IsLetterOrDigit).ToArray());
        if (raw.Length > 0 && LongestSequenceRun(raw) >= Math.Max(6, raw.Length - 3))
            return "Klavye ya da sayı dizisi parola olamaz.";

        var local = email.Split('@')[0];
        if (local.Length >= 4 && letters.Contains(Normalize(local)) && letters.Length - local.Length <= 5)
            return "Parola e-posta adresine çok benziyor.";

        return null;
    }

    private static string Normalize(string s) => new(s.Select(c => c switch
    {
        '0' => 'o', '1' => 'i', '3' => 'e', '4' => 'a', '5' => 's', '7' => 't', '@' => 'a', '$' => 's', '!' => 'i',
        'ı' => 'i', 'ş' => 's', 'ğ' => 'g', 'ü' => 'u', 'ö' => 'o', 'ç' => 'c',
        _ => c,
    }).ToArray());

    /// <summary>Parolanın, bilinen dizilerden birinin ardışık parçası olan en uzun bölümü.</summary>
    private static int LongestSequenceRun(string s)
    {
        var best = 0;
        for (var i = 0; i < s.Length; i++)
            for (var len = s.Length - i; len > best; len--)
            {
                var part = s.Substring(i, len);
                if (Sequences.Any(seq => seq.Contains(part, StringComparison.Ordinal))) { best = len; break; }
            }
        return best;
    }
}
