using System.ComponentModel.DataAnnotations;
using CatalogoPedidos.Application.Empresas;
using CatalogoPedidos.Domain.Entities;

namespace CatalogoPedidos.Application.Sedes;

public class SedeService(ISedeRepository sedes, IEmpresaRepository empresas) : ISedeService
{
    private static readonly EmailAddressAttribute EmailValidator = new();

    private static void Validar(CrearSedeDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            throw new InvalidOperationException("El nombre de la sede es obligatorio.");
        if (string.IsNullOrWhiteSpace(dto.Pais))
            throw new InvalidOperationException("El país de la sede es obligatorio.");
        if (string.IsNullOrWhiteSpace(dto.Ciudad))
            throw new InvalidOperationException("La ciudad de la sede es obligatoria.");
        if (!string.IsNullOrWhiteSpace(dto.Email) && !EmailValidator.IsValid(dto.Email))
            throw new InvalidOperationException("El email no tiene un formato válido.");
    }

    public Task<List<Sede>> ObtenerTodasAsync(int? empresaId = null, CancellationToken ct = default)
        => sedes.ObtenerTodasAsync(empresaId, ct);

    public Task<Sede?> ObtenerPorIdAsync(int id, CancellationToken ct = default)
        => sedes.ObtenerPorIdAsync(id, ct);

    public async Task<Sede> CrearAsync(CrearSedeDto dto, CancellationToken ct = default)
    {
        Validar(dto);

        _ = await empresas.ObtenerPorIdAsync(dto.EmpresaId, ct)
            ?? throw new InvalidOperationException("La empresa seleccionada no existe.");

        var sede = new Sede
        {
            EmpresaId = dto.EmpresaId,
            Nombre = dto.Nombre.Trim(),
            Pais = dto.Pais.Trim(),
            Ciudad = dto.Ciudad.Trim(),
            Direccion = dto.Direccion,
            Contacto = dto.Contacto,
            Telefono = dto.Telefono,
            Email = dto.Email
        };

        return await sedes.CrearAsync(sede, ct);
    }

    public async Task ActualizarAsync(int id, CrearSedeDto dto, CancellationToken ct = default)
    {
        Validar(dto);

        var sede = await sedes.ObtenerPorIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Sede {id} no encontrada.");

        if (sede.EmpresaId != dto.EmpresaId)
        {
            _ = await empresas.ObtenerPorIdAsync(dto.EmpresaId, ct)
                ?? throw new InvalidOperationException("La empresa seleccionada no existe.");
        }

        sede.EmpresaId = dto.EmpresaId;
        sede.Nombre = dto.Nombre.Trim();
        sede.Pais = dto.Pais.Trim();
        sede.Ciudad = dto.Ciudad.Trim();
        sede.Direccion = dto.Direccion;
        sede.Contacto = dto.Contacto;
        sede.Telefono = dto.Telefono;
        sede.Email = dto.Email;

        await sedes.ActualizarAsync(sede, ct);
    }

    public async Task DesactivarAsync(int id, CancellationToken ct = default)
    {
        var sede = await sedes.ObtenerPorIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Sede {id} no encontrada.");

        sede.Activo = false;
        await sedes.ActualizarAsync(sede, ct);
    }
}
