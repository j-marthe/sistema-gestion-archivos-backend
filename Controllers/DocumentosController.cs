using System.Data;
using System.Data.SqlClient;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using SistemaGestionArchivosBackend.Models;
using SistemaGestionArchivosBackend.Services;



[Authorize]
[ApiController]
[Route("api/[controller]")]
public class DocumentosController : ControllerBase
 {
    private readonly IBlobStorageService _blobService;
    private readonly IConfiguration _config;

    public DocumentosController(IBlobStorageService blobService, IConfiguration config)
    {
        _blobService = blobService;
        _config = config;

    }

    [Authorize(Roles = "Administrador,Usuario Estándar")]
    [HttpPost("subir")]
    public async Task<IActionResult> SubirDocumento([FromForm] IFormFile archivo, [FromForm] SubidaArchivoDTO dto)
    {
        if (archivo == null || archivo.Length == 0)
            return BadRequest("Archivo inválido.");

        if (string.IsNullOrWhiteSpace(dto.Nombre))
            return BadRequest("El nombre del documento es obligatorio.");

        var extension = Path.GetExtension(archivo.FileName);
        var nombreInterno = $"{Guid.NewGuid()}{extension}";

        var rutaAzure = await _blobService.SubirArchivoAsync(archivo, nombreInterno);

        var usuarioId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        var archivoId = Guid.NewGuid();

        using (var conexion = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
        {
            await conexion.OpenAsync();

            using (var cmd = new SqlCommand("INSERT INTO Archivos (Id, Nombre, Extension, RutaAlmacenamiento, FechaSubida, UsuarioId, CategoriaId) VALUES (@Id, @Nombre, @Extension, @Ruta, @Fecha, @UsuarioId, @CategoriaId)", conexion))
            {
                cmd.Parameters.AddWithValue("@Id", archivoId);
                cmd.Parameters.AddWithValue("@Nombre", dto.Nombre);
                cmd.Parameters.AddWithValue("@Extension", extension);
                cmd.Parameters.AddWithValue("@Ruta", rutaAzure);
                cmd.Parameters.AddWithValue("@Fecha", DateTime.Now);
                cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
                cmd.Parameters.AddWithValue("@CategoriaId", dto.CategoriaId);
                await cmd.ExecuteNonQueryAsync();
            }

            // Etiquetas
            if (!string.IsNullOrWhiteSpace(dto.Etiquetas))
            {
                var etiquetas = dto.Etiquetas.Split(',').Select(e => e.Trim()).Distinct();
                foreach (var nombre in etiquetas)
                {
                    Guid etiquetaId;
                    using (var checkCmd = new SqlCommand("SELECT Id FROM Etiquetas WHERE Nombre = @Nombre", conexion))
                    {
                        checkCmd.Parameters.AddWithValue("@Nombre", nombre);
                        var result = await checkCmd.ExecuteScalarAsync();

                        if (result != null)
                        {
                            etiquetaId = (Guid)result;
                        }
                        else
                        {
                            etiquetaId = Guid.NewGuid();
                            using var insertCmd = new SqlCommand("INSERT INTO Etiquetas (Id, Nombre) VALUES (@Id, @Nombre)", conexion);
                            insertCmd.Parameters.AddWithValue("@Id", etiquetaId);
                            insertCmd.Parameters.AddWithValue("@Nombre", nombre);
                            await insertCmd.ExecuteNonQueryAsync();
                        }
                    }

                    using var aeCmd = new SqlCommand("INSERT INTO ArchivoEtiquetas (ArchivoId, EtiquetaId) VALUES (@ArchivoId, @EtiquetaId)", conexion);
                    aeCmd.Parameters.AddWithValue("@ArchivoId", archivoId);
                    aeCmd.Parameters.AddWithValue("@EtiquetaId", etiquetaId);
                    await aeCmd.ExecuteNonQueryAsync();
                }
            }

            // Metadatos
            if (!string.IsNullOrWhiteSpace(dto.Metadatos))
            {
                var metadatos = JsonSerializer.Deserialize<Dictionary<string, string>>(dto.Metadatos);
                foreach (var kvp in metadatos)
                {
                    using var cmdMeta = new SqlCommand("INSERT INTO Metadatos (Id, ArchivoId, Clave, Valor) VALUES (@Id, @ArchivoId, @Clave, @Valor)", conexion);
                    cmdMeta.Parameters.AddWithValue("@Id", Guid.NewGuid());
                    cmdMeta.Parameters.AddWithValue("@ArchivoId", archivoId);
                    cmdMeta.Parameters.AddWithValue("@Clave", kvp.Key);
                    cmdMeta.Parameters.AddWithValue("@Valor", kvp.Value);
                    await cmdMeta.ExecuteNonQueryAsync();
                }
            }
        }

        return Ok(new { mensaje = "Archivo subido con éxito", url = rutaAzure });
    }


    [Authorize]
    [HttpGet("descargar/{id}")]
    public async Task<IActionResult> Descargar(Guid id)
    {
        string nombreOriginal = "";
        string rutaAzure = "";

        using (var conexion = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
        {
            await conexion.OpenAsync();

            using var cmd = new SqlCommand("SELECT Nombre, Extension, RutaAlmacenamiento FROM Archivos WHERE Id = @Id", conexion);
            cmd.Parameters.AddWithValue("@Id", id);
            using var reader = await cmd.ExecuteReaderAsync();

            if (!reader.Read()) return NotFound("Archivo no encontrado");

            nombreOriginal = reader.GetString(0);
            var extension = reader.GetString(1);
            rutaAzure = reader.GetString(2);
            nombreOriginal += extension;
        }

        var stream = await _blobService.DescargarArchivoAsync(Path.GetFileName(new Uri(rutaAzure).AbsolutePath));
        return File(stream, "application/octet-stream", nombreOriginal);
    }

    [Authorize(Roles = "Administrador")]
    [HttpDelete("eliminar/{id}")]
    public async Task<IActionResult> Eliminar(Guid id)
    {
        string rutaAzure = "";

        using (var conexion = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
        {
            await conexion.OpenAsync();

            // Obtener ruta del archivo
            using var getCmd = new SqlCommand("SELECT RutaAlmacenamiento FROM Archivos WHERE Id = @Id", conexion);
            getCmd.Parameters.AddWithValue("@Id", id);
            var result = await getCmd.ExecuteScalarAsync();

            if (result == null) return NotFound("Archivo no encontrado");

            rutaAzure = (string)result;

            // Eliminar de Blob
            var nombreBlob = Path.GetFileName(new Uri(rutaAzure).AbsolutePath);
            await _blobService.EliminarArchivoAsync(nombreBlob);

            // Eliminar metadatos y relaciones
            using var delMetadatos = new SqlCommand("DELETE FROM Metadatos WHERE ArchivoId = @Id", conexion);
            delMetadatos.Parameters.AddWithValue("@Id", id);
            await delMetadatos.ExecuteNonQueryAsync();

            using var delAE = new SqlCommand("DELETE FROM ArchivoEtiquetas WHERE ArchivoId = @Id", conexion);
            delAE.Parameters.AddWithValue("@Id", id);
            await delAE.ExecuteNonQueryAsync();

            // Eliminar archivo
            using var delArchivo = new SqlCommand("DELETE FROM Archivos WHERE Id = @Id", conexion);
            delArchivo.Parameters.AddWithValue("@Id", id);
            await delArchivo.ExecuteNonQueryAsync();
        }

        return Ok("Archivo eliminado correctamente.");
    }

    // Listar todos los documentos disponibles
    [Authorize]
    [HttpGet("listar")]
    public async Task<IActionResult> Listar()
    {
        var archivos = new List<object>();

        using (var conexion = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
        {
            await conexion.OpenAsync();

            using var cmd = new SqlCommand("SELECT Archivos.Id, Archivos.Nombre, Archivos.FechaSubida, Usuarios.Nombre,Categorias.Nombre FROM Archivos INNER JOIN Usuarios ON Archivos.UsuarioId=Usuarios.Id INNER JOIN Categorias ON Archivos.CategoriaId=Categorias.Id", conexion);

            using var reader = await cmd.ExecuteReaderAsync();

            var archivoIds = new List<Guid>();
            var archivoTemp = new Dictionary<Guid, dynamic>();

            while (await reader.ReadAsync())
            {
                var id = reader.GetGuid(0);
                archivoIds.Add(id);

                archivoTemp[id] = new
                {
                    Id = id,
                    Nombre = reader.GetString(1),
                    FechaSubida = reader.GetDateTime(2),
                    Usuario = reader.GetString(3),
                    Categoria = reader.GetString(4),
                    Metadatos = new Dictionary<string, string>(),
                    Etiquetas = new List<string>()
                };
            }

            reader.Close();
            
            // Obtener Metadatos
            if (archivoIds.Count > 0)
            {
                var metadatosCmd = new SqlCommand($@"
                SELECT ArchivoId, Clave, Valor 
                FROM Metadatos 
                WHERE ArchivoId IN ({string.Join(",", archivoIds.Select((_, i) => $"@Id{i}"))})
            ", conexion);

                for (int i = 0; i < archivoIds.Count; i++)
                {
                    metadatosCmd.Parameters.AddWithValue($"@Id{i}", archivoIds[i]);
                }

                using var metaReader = await metadatosCmd.ExecuteReaderAsync();
                while (await metaReader.ReadAsync())
                {
                    var archivoId = metaReader.GetGuid(0);
                    var clave = metaReader.GetString(1);
                    var valor = metaReader.GetString(2);

                    
                    var entry = archivoTemp[archivoId];
                    ((Dictionary<string, string>)entry.Metadatos)[clave] = valor;
                }

                metaReader.Close();
            }

            // Obtener Etiquetas
            var etiquetasCmd = new SqlCommand($"SELECT AE.ArchivoId, E.Nombre FROM ArchivoEtiquetas AE INNER JOIN Etiquetas E ON AE.EtiquetaId = E.Id WHERE AE.ArchivoId IN ({string.Join(",", archivoIds.Select((_, i) => $"@IdE{i}"))})", conexion);

            for (int i = 0; i < archivoIds.Count; i++)
                etiquetasCmd.Parameters.AddWithValue($"@IdE{i}", archivoIds[i]);

            using var etiquetasReader = await etiquetasCmd.ExecuteReaderAsync();
            while (await etiquetasReader.ReadAsync())
            {
                var archivoId = etiquetasReader.GetGuid(0);
                var etiqueta = etiquetasReader.GetString(1);
                ((List<string>)archivoTemp[archivoId].Etiquetas).Add(etiqueta);
            }

            archivos = archivoTemp.Values.ToList();
        }

        return Ok(archivos);
    }

    // Ver todos los detalles de un documento concreto
    [Authorize]
    [HttpGet("detalles/{id}")]
    public async Task<IActionResult> Detalles(Guid id)
    {
        dynamic archivoDetalles = null;

        using (var conexion = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
        {
            await conexion.OpenAsync();

            // 1. Datos principales
            using (var cmd = new SqlCommand(@"
            SELECT Archivos.Id, Archivos.Nombre, Archivos.FechaSubida, 
                   Usuarios.Nombre AS Usuario, 
                   Categorias.Nombre AS Categoria 
            FROM Archivos 
            INNER JOIN Usuarios ON Archivos.UsuarioId = Usuarios.Id 
            INNER JOIN Categorias ON Archivos.CategoriaId = Categorias.Id 
            WHERE Archivos.Id = @Id", conexion))
            {
                cmd.Parameters.AddWithValue("@Id", id);
                using var reader = await cmd.ExecuteReaderAsync();

                if (!reader.Read()) return NotFound("Archivo no encontrado");

                archivoDetalles = new
                {
                    Id = reader.GetGuid(0),
                    Nombre = reader.GetString(1),
                    FechaSubida = reader.GetDateTime(2),
                    Usuario = reader.GetString(3),
                    Categoria = reader.GetString(4),
                    Metadatos = new Dictionary<string, string>(),
                    Etiquetas = new List<string>()
                };

                reader.Close();
            }

            // 2. Metadatos
            using (var cmd = new SqlCommand("SELECT Clave, Valor FROM Metadatos WHERE ArchivoId = @Id", conexion))
            {
                cmd.Parameters.AddWithValue("@Id", id);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    ((Dictionary<string, string>)archivoDetalles.Metadatos)[reader.GetString(0)] = reader.GetString(1);
                }

                reader.Close();
            }

            // 3. Etiquetas
            using (var cmd = new SqlCommand("SELECT E.Nombre FROM ArchivoEtiquetas AE INNER JOIN Etiquetas E ON AE.EtiquetaId = E.Id WHERE AE.ArchivoId = @Id", conexion))
            {
                cmd.Parameters.AddWithValue("@Id", id);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    ((List<string>)archivoDetalles.Etiquetas).Add(reader.GetString(0));
                }
            }
        }

        return Ok(archivoDetalles);
    }

}


