using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Application.Empresas;

public class EmpresaService(IEmpresaRepository empresas) : IEmpresaService
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
    }
}
