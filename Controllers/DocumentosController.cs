using System.Data;
using System.Data.SqlClient;
using System.Security.Claims;
using System.Text;
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
    private readonly IAuditoriaService _auditoriaService;

    public DocumentosController(IBlobStorageService blobService, IConfiguration config, IAuditoriaService auditoriaService)
    {
        _blobService = blobService;
        _config = config;
        _auditoriaService = auditoriaService;

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

            // Registrar la primera versión en VersionesArchivos
            using (var cmdVersion = new SqlCommand("INSERT INTO VersionesArchivos (Id, ArchivoId, NumeroVersion, RutaAlmacenamiento, FechaCreacion, UsuarioId) VALUES (@Id, @ArchivoId, @NumeroVersion, @Ruta, @Fecha, @UsuarioId)", conexion))
            {
                cmdVersion.Parameters.AddWithValue("@Id", Guid.NewGuid());
                cmdVersion.Parameters.AddWithValue("@ArchivoId", archivoId);
                cmdVersion.Parameters.AddWithValue("@NumeroVersion", 1);
                cmdVersion.Parameters.AddWithValue("@Ruta", rutaAzure);
                cmdVersion.Parameters.AddWithValue("@Fecha", DateTime.Now);
                cmdVersion.Parameters.AddWithValue("@UsuarioId", usuarioId);
                await cmdVersion.ExecuteNonQueryAsync();
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

        // Registrar acción en auditoría
        await _auditoriaService.RegistrarAsync(
            usuarioId,
            archivoId,
            "Subida",
            $"El usuario subió el archivo '{dto.Nombre}' con el identificador '{archivoId}'"
        );

        return Ok(new { mensaje = "Archivo subido con éxito", url = rutaAzure });
    }


    [Authorize]
    [HttpGet("descargar/{id}")]
    public async Task<IActionResult> Descargar(Guid id)
    {
        string nombreOriginal = "";
        string rutaAzure = "";
        var usuarioId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

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

        // Registrar acción en auditoría
        await _auditoriaService.RegistrarAsync(
            usuarioId,
            id,
            "Descarga",
            $"El usuario descargó el archivo con ID '{id}'"
        );

        var stream = await _blobService.DescargarArchivoAsync(Path.GetFileName(new Uri(rutaAzure).AbsolutePath));
        return File(stream, "application/octet-stream", nombreOriginal);
    }

    [Authorize(Roles = "Administrador")]
    [HttpDelete("eliminar/{id}")]
    public async Task<IActionResult> Eliminar(Guid id)
    {
        string rutaAzure = "";
        Guid usuarioId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

        using (var conexion = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
        {
            await conexion.OpenAsync();

            // Obtener ruta del archivo
            using var getCmd = new SqlCommand("SELECT RutaAlmacenamiento FROM Archivos WHERE Id = @Id", conexion);
            getCmd.Parameters.AddWithValue("@Id", id);
            var result = await getCmd.ExecuteScalarAsync();

            if (result == null) return NotFound("Archivo no encontrado");

            rutaAzure = (string)result;

            // Registrar acción en auditoría ANTES de eliminar el archivo
            await _auditoriaService.RegistrarAsync(
                usuarioId,
                id,
                "Eliminación",
                $"El usuario eliminó el archivo '{id}'"
            );

            // Eliminar las versiones del archivo de la tabla 'VersionesArchivos'
            using var delVersiones = new SqlCommand("SELECT RutaAlmacenamiento FROM VersionesArchivos WHERE ArchivoId = @ArchivoId", conexion);
            delVersiones.Parameters.AddWithValue("@ArchivoId", id);
            using var reader = await delVersiones.ExecuteReaderAsync();  // Usamos ExecuteReaderAsync para leer las versiones

            // Eliminar cada versión del blob en Azure
            while (await reader.ReadAsync())
            {
                string rutaVersionBlob = reader.GetString(0);
                var nombreVersionBlob = Path.GetFileName(new Uri(rutaVersionBlob).AbsolutePath);
                // Eliminar cada versión del archivo en Blob Storage
                await _blobService.EliminarArchivoAsync(nombreVersionBlob);
            }

            // Cerrar el DataReader antes de ejecutar otro comando
            await reader.DisposeAsync();

            // Eliminar las versiones del archivo de la base de datos
            using var delVersionesCmd = new SqlCommand("DELETE FROM VersionesArchivos WHERE ArchivoId = @ArchivoId", conexion);
            delVersionesCmd.Parameters.AddWithValue("@ArchivoId", id);
            await delVersionesCmd.ExecuteNonQueryAsync();

            // Eliminar el archivo principal de Blob
            var nombreBlob = Path.GetFileName(new Uri(rutaAzure).AbsolutePath);
            await _blobService.EliminarArchivoAsync(nombreBlob);

            // Eliminar metadatos asociados al archivo
            using var delMetadatos = new SqlCommand("DELETE FROM Metadatos WHERE ArchivoId = @Id", conexion);
            delMetadatos.Parameters.AddWithValue("@Id", id);
            await delMetadatos.ExecuteNonQueryAsync();

            // Eliminar relaciones de etiquetas con el archivo
            using var delAE = new SqlCommand("DELETE FROM ArchivoEtiquetas WHERE ArchivoId = @Id", conexion);
            delAE.Parameters.AddWithValue("@Id", id);
            await delAE.ExecuteNonQueryAsync();

            // Finalmente, eliminar el archivo de la tabla 'Archivos'
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

            // Obtener los datos principales
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

            // Obtener Metadatos
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

            // Obtener Etiquetas
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

    // Editar los metadatos de un archivo
    [Authorize]
    [HttpPut("editar-metadatos/{id}")]
    public async Task<IActionResult> EditarMetadatos(Guid id, [FromBody] Dictionary<string, string> metadatos)
    {
        if (metadatos == null || metadatos.Count == 0)
        {
            return BadRequest("Los metadatos no pueden estar vacíos.");
        }

        var usuarioId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

        using (var conexion = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
        {
            await conexion.OpenAsync();

            //Verificar si existe el archivo
            using var checkCmd = new SqlCommand("SELECT COUNT(1) FROM Archivos WHERE Id = @Id", conexion);
            checkCmd.Parameters.AddWithValue("@Id", id);
            var existe = (int)await checkCmd.ExecuteScalarAsync();

            if (existe == 0) return NotFound("Archivo no encontrado");

            //Eliminar los metadatos que hubiese
            using var deleteCmd = new SqlCommand("DELETE FROM Metadatos WHERE ArchivoId = @Id", conexion);
            deleteCmd.Parameters.AddWithValue("@Id", id);
            await deleteCmd.ExecuteNonQueryAsync();

            //Insertar los nuevos metadatos
            foreach (var metadato in metadatos)
            {
                using var insertCmd = new SqlCommand("INSERT INTO Metadatos (Id, ArchivoId, Clave, Valor) VALUES (@Id, @ArchivoId, @Clave, @Valor)", conexion);
                insertCmd.Parameters.AddWithValue("@Id", Guid.NewGuid());
                insertCmd.Parameters.AddWithValue("@ArchivoId", id);
                insertCmd.Parameters.AddWithValue("@Clave", metadato.Key);
                insertCmd.Parameters.AddWithValue("@Valor", metadato.Value);
                await insertCmd.ExecuteNonQueryAsync();
            }
        }

        // Registrar acción en auditoría
        await _auditoriaService.RegistrarAsync(
            usuarioId,
            id,
            "Edición de metadatos",
            $"El usuario editó los metadatos del archivo con ID '{id}'"
        );

        return Ok("Metadatos actualizados correctamente.");
    }

    // Búsqueda Avanzada
    [Authorize]
    [HttpPost("buscar")]
    public async Task<IActionResult> Buscar([FromBody] FiltrosBusquedaDTO filtros)
    {
        var resultados = new List<object>();

        using var conexion = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
        await conexion.OpenAsync();

        var consulta = new StringBuilder(@"
        SELECT DISTINCT A.Id, A.Nombre, A.FechaSubida, U.Nombre AS Usuario, C.Nombre AS Categoria
        FROM Archivos A
        INNER JOIN Usuarios U ON A.UsuarioId = U.Id
        INNER JOIN Categorias C ON A.CategoriaId = C.Id
        LEFT JOIN ArchivoEtiquetas AE ON A.Id = AE.ArchivoId
        LEFT JOIN Etiquetas E ON AE.EtiquetaId = E.Id
        LEFT JOIN Metadatos M ON A.Id = M.ArchivoId
        WHERE 1=1
    ");

        var cmd = new SqlCommand();
        cmd.Connection = conexion;

        if (!string.IsNullOrWhiteSpace(filtros.Nombre))
        {
            consulta.Append(" AND A.Nombre LIKE @Nombre");
            cmd.Parameters.AddWithValue("@Nombre", $"%{filtros.Nombre}%");
        }

        if (!string.IsNullOrWhiteSpace(filtros.Usuario))
        {
            consulta.Append(" AND U.Nombre LIKE @Usuario");
            cmd.Parameters.AddWithValue("@Usuario", $"%{filtros.Usuario}%");
        }

        if (filtros.CategoriaId.HasValue)
        {
            consulta.Append(" AND A.CategoriaId = @CategoriaId");
            cmd.Parameters.AddWithValue("@CategoriaId", filtros.CategoriaId);
        }

        if (filtros.FechaDesde.HasValue)
        {
            consulta.Append(" AND A.FechaSubida >= @Desde");
            cmd.Parameters.AddWithValue("@Desde", filtros.FechaDesde.Value);
        }

        if (filtros.FechaHasta.HasValue)
        {
            consulta.Append(" AND A.FechaSubida <= @Hasta");
            cmd.Parameters.AddWithValue("@Hasta", filtros.FechaHasta.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtros.ValorMetadato))
        {
            consulta.Append(" AND M.Valor LIKE @ValorMeta");
            cmd.Parameters.AddWithValue("@ValorMeta", $"%{filtros.ValorMetadato}%");
        }

        if (filtros.Etiquetas != null && filtros.Etiquetas.Any())
        {
            var etiquetasParams = filtros.Etiquetas.Select((etq, i) => $"@Etiqueta{i}").ToList();
            consulta.Append($" AND E.Nombre IN ({string.Join(",", etiquetasParams)})");
            for (int i = 0; i < filtros.Etiquetas.Count; i++)
            {
                cmd.Parameters.AddWithValue($"@Etiqueta{i}", filtros.Etiquetas[i]);
            }
        }

        cmd.CommandText = consulta.ToString();

        // Primero se obtiene los datos de los archivos
        var archivos = new List<(Guid Id, string Nombre, DateTime Fecha, string Usuario, string Categoria)>();

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            archivos.Add((
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetDateTime(2),
                reader.GetString(3),
                reader.GetString(4)
            ));
        }
        reader.Close();

        // Luego se obtiene las etiquetas y los metadatos
        foreach (var archivo in archivos)
        {
            var etiquetasCmd = new SqlCommand("SELECT E.Nombre FROM ArchivoEtiquetas AE INNER JOIN Etiquetas E ON AE.EtiquetaId = E.Id WHERE AE.ArchivoId = @ArchivoId", conexion);
            etiquetasCmd.Parameters.AddWithValue("@ArchivoId", archivo.Id);
            var etiquetas = new List<string>();
            using var etiquetasReader = await etiquetasCmd.ExecuteReaderAsync();
            while (await etiquetasReader.ReadAsync())
                etiquetas.Add(etiquetasReader.GetString(0));
            etiquetasReader.Close();

            var metadatosCmd = new SqlCommand("SELECT Clave, Valor FROM Metadatos WHERE ArchivoId = @ArchivoId", conexion);
            metadatosCmd.Parameters.AddWithValue("@ArchivoId", archivo.Id);
            var metadatos = new Dictionary<string, string>();
            using var metadatosReader = await metadatosCmd.ExecuteReaderAsync();
            while (await metadatosReader.ReadAsync())
                metadatos[metadatosReader.GetString(0)] = metadatosReader.GetString(1);
            metadatosReader.Close();

            resultados.Add(new
            {
                id = archivo.Id,
                nombre = archivo.Nombre,
                fechaSubida = archivo.Fecha,
                usuario = archivo.Usuario,
                categoria = archivo.Categoria,
                metadatos = metadatos,
                etiquetas = etiquetas
            });
        }

        return Ok(resultados);
    }

    // Listar todas las categorías
    [Authorize]
    [HttpGet("categorias/listar")]
    public async Task<IActionResult> ListarCategorias()
    {
        var categorias = new List<object>();

        using var conexion = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
        await conexion.OpenAsync();

        using var cmd = new SqlCommand("SELECT Id, Nombre FROM Categorias ORDER BY Nombre", conexion);
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            categorias.Add(new
            {
                Id = reader.GetGuid(0),
                Nombre = reader.GetString(1)
            });
        }

        return Ok(categorias);
    }

    // Crear una nueva categoría
    [Authorize(Roles = "Administrador,Usuario Estándar")]
    [HttpPost("categorias/crear")]
    public async Task<IActionResult> CrearCategorias([FromBody] string nombreCategoria)
    {
        if (string.IsNullOrWhiteSpace(nombreCategoria))
            return BadRequest("El nombre de la categoría es obligatorio.");

        using var conexion = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
        await conexion.OpenAsync();

        // Verificar si ya existe
        using var checkCmd = new SqlCommand("SELECT COUNT(*) FROM Categorias WHERE Nombre = @Nombre", conexion);
        checkCmd.Parameters.AddWithValue("@Nombre", nombreCategoria);
        var existe = (int)await checkCmd.ExecuteScalarAsync();

        if (existe > 0)
            return Conflict("Ya existe una categoría con ese nombre.");

        // Insertar nueva categoría
        using var insertCmd = new SqlCommand("INSERT INTO Categorias (Id, Nombre) VALUES (@Id, @Nombre)", conexion);
        insertCmd.Parameters.AddWithValue("@Id", Guid.NewGuid());
        insertCmd.Parameters.AddWithValue("@Nombre", nombreCategoria);
        await insertCmd.ExecuteNonQueryAsync();

        return Ok("Categoría creada exitosamente.");
    }

    // Control de versiones

    [Authorize(Roles = "Administrador,Usuario Estándar")]
    [HttpPost("subir-version/{id}")]
    public async Task<IActionResult> SubirNuevaVersion(Guid id, [FromForm] IFormFile archivo)
    {
        if (archivo == null || archivo.Length == 0)
            return BadRequest("Archivo inválido.");

        var extension = Path.GetExtension(archivo.FileName);
        var nombreInterno = $"{Guid.NewGuid()}{extension}";

        var rutaAzure = await _blobService.SubirArchivoAsync(archivo, nombreInterno);
        var usuarioId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

        using (var conexion = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
        {
            await conexion.OpenAsync();

            // Obtener la última versión del archivo
            int numeroVersion;
            using (var cmdVersion = new SqlCommand("SELECT MAX(NumeroVersion) FROM VersionesArchivos WHERE ArchivoId = @ArchivoId", conexion))
            {
                cmdVersion.Parameters.AddWithValue("@ArchivoId", id);
                numeroVersion = (int)(await cmdVersion.ExecuteScalarAsync() ?? 0) + 1;
            }

            // Actualizar la ruta en la tabla Archivos
            using (var cmdUpdate = new SqlCommand("UPDATE Archivos SET RutaAlmacenamiento = @Ruta WHERE Id = @Id", conexion))
            {
                cmdUpdate.Parameters.AddWithValue("@Ruta", rutaAzure);
                cmdUpdate.Parameters.AddWithValue("@Id", id);
                await cmdUpdate.ExecuteNonQueryAsync();
            }

            // Insertar la nueva versión en la tabla VersionesArchivos
            using (var cmdInsertVersion = new SqlCommand("INSERT INTO VersionesArchivos (Id, ArchivoId, NumeroVersion, RutaAlmacenamiento, FechaCreacion, UsuarioId) VALUES (@Id, @ArchivoId, @NumeroVersion, @Ruta, @Fecha, @UsuarioId)", conexion))
            {
                cmdInsertVersion.Parameters.AddWithValue("@Id", Guid.NewGuid());
                cmdInsertVersion.Parameters.AddWithValue("@ArchivoId", id);
                cmdInsertVersion.Parameters.AddWithValue("@NumeroVersion", numeroVersion);
                cmdInsertVersion.Parameters.AddWithValue("@Ruta", rutaAzure);
                cmdInsertVersion.Parameters.AddWithValue("@Fecha", DateTime.Now);
                cmdInsertVersion.Parameters.AddWithValue("@UsuarioId", usuarioId);
                await cmdInsertVersion.ExecuteNonQueryAsync();
            }
        }

        // Registrar acción en auditoría
        await _auditoriaService.RegistrarAsync(usuarioId, id, "Subida Versión", $"El usuario subió una nueva versión del archivo '{id}'");

        return Ok(new { mensaje = "Nueva versión subida con éxito", url = rutaAzure });
    }

    [Authorize(Roles = "Administrador,Usuario Estándar")]
    [HttpPost("restaurar-version/{id}/{version}")]
    public async Task<IActionResult> RestaurarVersion(Guid id, int version)
    {
        string rutaAzure = "";
        Guid usuarioId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

        using (var conexion = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
        {
            await conexion.OpenAsync();

            // Obtener la ruta de la versión solicitada
            using var getCmd = new SqlCommand("SELECT RutaAlmacenamiento FROM VersionesArchivos WHERE ArchivoId = @ArchivoId AND NumeroVersion = @Version", conexion);
            getCmd.Parameters.AddWithValue("@ArchivoId", id);
            getCmd.Parameters.AddWithValue("@Version", version);
            var result = await getCmd.ExecuteScalarAsync();

            if (result == null) return NotFound("Versión no encontrada");

            rutaAzure = (string)result;

            // Actualizar la ruta en la tabla Archivos
            using var cmdUpdate = new SqlCommand("UPDATE Archivos SET RutaAlmacenamiento = @Ruta WHERE Id = @Id", conexion);
            cmdUpdate.Parameters.AddWithValue("@Ruta", rutaAzure);
            cmdUpdate.Parameters.AddWithValue("@Id", id);
            await cmdUpdate.ExecuteNonQueryAsync();
        }

        // Registrar acción en auditoría
        await _auditoriaService.RegistrarAsync(usuarioId, id, "Restaurar Versión", $"El usuario restauró la versión {version} del archivo '{id}'");

        return Ok(new { mensaje = "Versión restaurada con éxito", url = rutaAzure });
    }


}


