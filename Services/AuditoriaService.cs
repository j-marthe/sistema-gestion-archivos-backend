using System.Data.SqlClient;
using System;
using Microsoft.Extensions.Configuration;
using SistemaGestionArchivosBackend.Models;
using System.Data;

public interface IAuditoriaService
{
    Task RegistrarAsync(Guid usuarioId, Guid archivoId, string accion, string? detalle = null);
    Task<IEnumerable<AuditoriaDTO>> ListarAsync(DateTime? desde, DateTime? hasta);
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

    public async Task<IEnumerable<AuditoriaDTO>> ListarAsync(DateTime? desde, DateTime? hasta)
    {
        var auditorias = new List<AuditoriaDTO>();

        using var conexion = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
        await conexion.OpenAsync();

        var consulta = @"
                SELECT A.Id, U.Nombre AS Usuario, AR.Nombre AS Archivo, A.Accion, A.Detalle, A.Fecha
                FROM Auditoria A
                INNER JOIN Usuarios U ON A.UsuarioId = U.Id
                LEFT JOIN Archivos AR ON A.ArchivoId = AR.Id
                WHERE (@Desde IS NULL OR A.Fecha >= @Desde)
                  AND (@Hasta IS NULL OR A.Fecha <= @Hasta)
                ORDER BY A.Fecha DESC";

        using var cmd = new SqlCommand(consulta, conexion);
        cmd.Parameters.Add("@Desde", SqlDbType.DateTime).Value = (object?)desde ?? DBNull.Value;
        cmd.Parameters.Add("@Hasta", SqlDbType.DateTime).Value = (object?)hasta ?? DBNull.Value;

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            auditorias.Add(new AuditoriaDTO
            {
                Id = reader.GetGuid(0),
                Usuario = reader.GetString(1),
                Archivo = reader.IsDBNull(2) ? "[Eliminado]" : reader.GetString(2),
                Accion = reader.GetString(3),
                Detalle = reader.IsDBNull(4) ? null : reader.GetString(4),
                Fecha = reader.GetDateTime(5)
            });
        }

        return auditorias;
    }
}
