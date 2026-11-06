using System.Net;
using SunCash.Infrastructure.Sync;

namespace SunCash.Infrastructure.Tests;

public class SyncJobRunnerTests
{
    [Fact]
    public void Un_errore_HTTP_si_legge_in_italiano_con_il_codice()
    {
        var ex = new HttpRequestException("Response status code does not indicate success: 401 (Unauthorized).", null, HttpStatusCode.Unauthorized);

        Assert.Equal("Il servizio esterno ha risposto con l'errore HTTP 401.", SyncJobRunner.FailureMessage(ex));
    }

    [Fact]
    public void Gli_altri_errori_restano_come_sono()
    {
        Assert.Equal("Login GME rifiutato: Password scaduta.",
            SyncJobRunner.FailureMessage(new InvalidOperationException("Login GME rifiutato: Password scaduta.")));
    }
}
