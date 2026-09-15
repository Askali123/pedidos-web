using CatalogoPedidos.Application.Solicitudes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CatalogoPedidos.Infrastructure.BackgroundJobs;

/// <summary>
/// Revisa periódicamente los pedidos pendientes hace más de 48h y le avisa al gestor
/// (ISolicitudService.EnviarRecordatoriosPendientesAsync), sin depender de que alguien
/// tenga la Bandeja abierta. Corre una vez al iniciar la app y luego según <see cref="Intervalo"/>.
/// </summary>
public class RecordatorioPendientesHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<RecordatorioPendientesHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Intervalo);

        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var solicitudes = scope.ServiceProvider.GetRequiredService<ISolicitudService>();
                await solicitudes.EnviarRecordatoriosPendientesAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error enviando recordatorios de pedidos pendientes.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
