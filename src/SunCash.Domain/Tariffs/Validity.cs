namespace SunCash.Domain.Tariffs;

/// <summary>
/// L'unica risposta a «quale riga vale in questa data?» per i dati con data di inizio validità
/// (offerte, parte regolata): vale l'ultima riga iniziata entro quella data.
/// </summary>
public static class Validity
{
    /// <summary>La riga valida nella data, o <c>null</c> se nessuna è ancora iniziata.</summary>
    public static T? At<T>(IEnumerable<T> rows, Func<T, DateOnly> validFrom, DateOnly date) where T : class =>
        rows.Where(r => validFrom(r) <= date).MaxBy(validFrom);
}
