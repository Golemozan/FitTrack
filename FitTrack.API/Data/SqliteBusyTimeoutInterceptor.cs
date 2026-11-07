using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FitTrack.API.Data;

/// <summary>
/// Telegram sohbetleri artık paralel işleniyor, web istekleri zaten paraleldi. SQLite'ta yazan, okuyan bitene
/// kadar commit edemez; Microsoft.Data.Sqlite'ın kendi yeniden denemesi <c>RETURNING</c>'li yazının commit
/// adımını kapsamıyor ve istek "database is locked" ile düşüyordu. Her bağlantıda SQLite'ın kendi bekleme
/// süresi açılır: yazı hata vermek yerine okuyanın bitmesini bekler.
/// </summary>
public sealed class SqliteBusyTimeoutInterceptor : DbConnectionInterceptor
{
    private const string Pragma = "PRAGMA busy_timeout = 5000;";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var command = connection.CreateCommand();
        command.CommandText = Pragma;
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = Pragma;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
