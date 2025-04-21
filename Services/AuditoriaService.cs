using System.Data.SqlClient;
using System;
using Microsoft.Extensions.Configuration;
using SistemaGestionArchivosBackend.Models;

public interface IAuditoriaService
{
    Task RegistrarAsync(Guid usuarioId, Guid archivoId, string accion, string? detalle = null);
    Task<IEnumerable<AuditoriaDTO>> ListarAsync(DateTime? desde, DateTime? hasta, int pagina, int tamanoPagina);
}

public class AuditoriaService : IAuditoriaService
{
    private readonly IConfiguration _config;

    public AuditoriaService(IConfiguration config)
    {
        _config = config;
    }

    public async Task RegistrarAsync(Guid usuarioId, Guid archivoId, string accion, string? detalle = null)
    {
        using var conexion = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
        await conexion.OpenAsync();

        var cmd = new SqlCommand(@"INSERT INTO Auditoria (UsuarioId, ArchivoId, Accion, Detalle)
                                   VALUES (@UsuarioId, @ArchivoId, @Accion, @Detalle)", conexion);
        cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
        cmd.Parameters.AddWithValue("@ArchivoId", archivoId);
        cmd.Parameters.AddWithValue("@Accion", accion);
        cmd.Parameters.AddWithValue("@Detalle", (object?)detalle ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<IEnumerable<AuditoriaDTO>> ListarAsync(DateTime? desde, DateTime? hasta, int pagina, int tamanoPagina)
    {
        var auditorias = new List<AuditoriaDTO>();

        using var conexion = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
        await conexion.OpenAsync();

        var offset = (pagina - 1) * tamanoPagina;

        var consulta = @"
            SELECT A.Id, U.Nombre AS Usuario, AR.Nombre AS Archivo, A.Accion, A.Detalle, A.Fecha
            FROM Auditoria A
            INNER JOIN Usuarios U ON A.UsuarioId = U.Id
            INNER JOIN Archivos AR ON A.ArchivoId = AR.Id
            WHERE (@Desde IS NULL OR A.Fecha >= @Desde)
              AND (@Hasta IS NULL OR A.Fecha <= @Hasta)
            ORDER BY A.Fecha DESC
            OFFSET @Offset ROWS FETCH NEXT @Tamano ROWS ONLY";

        using var cmd = new SqlCommand(consulta, conexion);
        cmd.Parameters.AddWithValue("@Desde", (object?)desde ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Hasta", (object?)hasta ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Offset", offset);
        cmd.Parameters.AddWithValue("@Tamano", tamanoPagina);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            auditorias.Add(new AuditoriaDTO
            {
                Id = reader.GetGuid(0),
                Usuario = reader.GetString(1),
                Archivo = reader.GetString(2),
                Accion = reader.GetString(3),
                Detalle = reader.IsDBNull(4) ? null : reader.GetString(4),
                Fecha = reader.GetDateTime(5)
            });
        }

        return auditorias;
    }
}
