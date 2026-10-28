using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace SunCash.Infrastructure.Persistence;

/// <summary>
/// Upsert in blocco (<c>INSERT … ON CONFLICT DO UPDATE</c>) in una transazione: gli import sono
/// idempotenti, rilanciarli sullo stesso intervallo non crea duplicati. EF Core non lo fa da solo.
/// </summary>
internal static class SqliteUpsert
{
    /// <summary>Esegue un upsert per ogni elemento, con un comando preparato una volta sola.</summary>
    /// <param name="sql">Comando con parametri <c>$p0</c>, <c>$p1</c>, …</param>
    /// <param name="values">Per ogni elemento, i valori dei parametri nell'ordine.</param>
    public static async Task<int> RunAsync<T>(
        SunCashDbContext db, string sql, IEnumerable<T> items, Func<T, object?[]> values, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var transaction = await connection.BeginTransactionAsync(ct);
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;

            var count = 0;
            DbParameter[]? parameters = null;
            foreach (var item in items)
            {
                var v = values(item);
                if (parameters is null)
                {
                    parameters = new DbParameter[v.Length];
                    for (var i = 0; i < v.Length; i++)
                    {
                        parameters[i] = command.CreateParameter();
                        parameters[i].ParameterName = "$p" + i;
                        command.Parameters.Add(parameters[i]);
                    }
                    await command.PrepareAsync(ct);
                }

                for (var i = 0; i < v.Length; i++)
                    parameters[i].Value = v[i] ?? DBNull.Value;
                count += await command.ExecuteNonQueryAsync(ct);
            }

            await transaction.CommitAsync(ct);
            return count;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}
