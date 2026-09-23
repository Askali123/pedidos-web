using CatalogoPedidos.Application.Sedes;
using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Application.Empresas;

public class EmpresaService(IEmpresaRepository empresas, ISedeRepository sedes) : IEmpresaService
{
    public Task<List<Empresa>> ObtenerTodasAsync(CancellationToken ct = default)
        => empresas.ObtenerTodasAsync(ct);

    public Task<Empresa?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
        => empresas.ObtenerPorIdAsync(id, ct);

    public Task<Empresa> CrearAsync(CrearEmpresaDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            throw new InvalidOperationException("El nombre de la empresa es obligatorio.");

        var empresa = new Empresa
        {
            Nombre = dto.Nombre.Trim(),
            Nit = dto.Nit
        };

        return empresas.CrearAsync(empresa, ct);
    }

    public async Task ActualizarAsync(int id, CrearEmpresaDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            throw new InvalidOperationException("El nombre de la empresa es obligatorio.");

        var empresa = await empresas.ObtenerPorIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Empresa {id} no encontrada.");

        empresa.Nombre = dto.Nombre.Trim();
        empresa.Nit = dto.Nit;

        await empresas.ActualizarAsync(empresa, ct);
    }

    public async Task DesactivarAsync(int id, CancellationToken ct = default)
    {
        var empresa = await empresas.ObtenerPorIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Empresa {id} no encontrada.");

        empresa.Activo = false;
        await empresas.ActualizarAsync(empresa, ct);

        // Cascada: una Sede no puede seguir "activa" con su Empresa inactiva — mismo
        // criterio de baja lógica que el resto del dominio (no borra, no toca usuarios ni
        // histórico, solo deja de ofrecerse para lo nuevo). Ver docs/PLAN_EMPRESAS_FILIALES.md, Etapa 9.
        var sedesActivas = await sedes.ObtenerTodasAsync(id, ct);
        foreach (var sede in sedesActivas)
        {
            sede.Activo = false;
            await sedes.ActualizarAsync(sede, ct);
        }
    }
}
