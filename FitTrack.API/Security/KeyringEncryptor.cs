using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;

namespace FitTrack.API.Security;

/// <summary>
/// Oturum çerezlerini imzalayan anahtar halkası veritabanıyla aynı diskte duruyor. Şifresiz kalırsa
/// volume ya da yedeği sızan biri her kullanıcı için geçerli çerez üretebilir. Halka, API anahtarlarını
/// koruyan ana anahtarla (<c>FITTRACK_ENCRYPTION_KEY</c>, ortam değişkeni — diskte değil) şifrelenir.
/// Ana anahtar değişirken eskisi <c>FITTRACK_ENCRYPTION_KEY_PREVIOUS</c>'ta durduğu sürece halka okunur.
/// </summary>
public sealed class KeyringEncryptor(KeyProtector protector) : IXmlEncryptor
{
    /// <summary>Şifrelemeye bağlanan sabit bağlam: kullanıcı anahtarlarıyla karışmasın.</summary>
    internal static readonly Guid Context = new("6f1d2c3b-9a8e-4b7d-8c6f-5e4d3c2b1a09");

    public EncryptedXmlInfo Encrypt(XElement plaintextElement)
    {
        var blob = protector.Protect(plaintextElement.ToString(SaveOptions.DisableFormatting), Context);
        return new EncryptedXmlInfo(new XElement("encryptedKey", blob), typeof(KeyringDecryptor));
    }
}

public sealed class KeyringDecryptor(IServiceProvider services) : IXmlDecryptor
{
    public XElement Decrypt(XElement encryptedElement)
    {
        var protector = services.GetRequiredService<KeyProtector>();
        if (!protector.TryUnprotect(encryptedElement.Value, KeyringEncryptor.Context, out var xml, out _))
            throw new InvalidOperationException("Oturum anahtar halkası çözülemedi: FITTRACK_ENCRYPTION_KEY değişmiş olabilir.");
        return XElement.Parse(xml);
    }
}
