using System.Data.SqlClient;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using SistemaGestionArchivosBackend.Models;



[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly IConfiguration _config;

    public AuthController(AuthService authService, IConfiguration config)
    {
        _authService = authService;
        _config = config;
    }

    [Authorize]
    [HttpPost("registro")]
    public async Task<IActionResult> Registrar(UsuarioDTO usuarioDto)
    {
        try
        {
            if (_authService.UsuarioExiste(usuarioDto.Email))
                return BadRequest("El email ya está registrado.");

            var rolPrestablecido = Guid.Parse("6B5F923B-DDA5-4096-AE0A-0E5617694F1E");

            var usuario = new Usuario
            {
                Id = Guid.NewGuid(),
                Nombre = usuarioDto.Nombre,
                Email = usuarioDto.Email,
                ContrasenaHash = BCrypt.Net.BCrypt.HashPassword(usuarioDto.Contrasena),
                FechaRegistro = DateTime.Now,
                RolId = rolPrestablecido
            };

            // Usamos RetryHelper para registrar el usuario
            await RetryHelper.ExecuteAsync(async () =>
            {
                _authService.RegistrarUsuario(usuario);
                return true; // Regresa un valor para que RetryHelper funcione
            });

            return Ok("Usuario registrado correctamente.");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error interno: {ex.Message}");
        }
    }


    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDTO login)
    {
        try
        {
            // Usamos RetryHelper para el acceso al usuario
            var usuario = await RetryHelper.ExecuteAsync(async () =>
            {
                return _authService.ObtenerUsuarioPorEmail(login.Email);
            });

            if (usuario == null || !BCrypt.Net.BCrypt.Verify(login.Contrasena, usuario.ContrasenaHash))
                return Unauthorized("Credenciales inválidas.");

            string rolNombre = usuario.RolId switch
            {
                Guid id when id == Guid.Parse("C3C1D079-5736-4D73-A522-3ED165DB693B") => "Administrador",
                Guid id when id == Guid.Parse("07A3CE69-025D-41F4-8C76-F3DB00E60BD8") => "Usuario Estándar",
                Guid id when id == Guid.Parse("6B5F923B-DDA5-4096-AE0A-0E5617694F1E") => "Lector",
                _ => "Desconocido"
            };

            var token = TokenUtils.GenerarTokenJWT(usuario, rolNombre, _config);
            return Ok(new { Token = token });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error interno: {ex.Message}");
        }
    }


    [Authorize]
    [HttpGet("perfil")]
    public IActionResult ObtenerPerfil()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var usuario = _authService.ObtenerUsuarioPorId(Guid.Parse(userId));

        if (usuario == null) return NotFound("Usuario no encontrado");

        return Ok(new { 
            usuario.Id,
            usuario.Nombre,
            usuario.Email,
            usuario.FechaRegistro,
            usuario.RolId
        });
    }

    [Authorize(Roles = "Administrador")]
    [HttpGet("usuarios")]
    public IActionResult ObtenerUsuarios()
    {
        var usuarios = _authService.ObtenerTodosLosUsuarios();
        return Ok(usuarios);
    }

    [Authorize(Roles = "Administrador")]
    [HttpDelete("usuarios/eliminar/{id}")]
    public IActionResult EliminarUsuario(Guid id)
    {
        var eliminado = _authService.EliminarUsuario(id);
        return eliminado ? Ok("Usuario eliminado") : NotFound("Usuario no encontrado");
    }

    [Authorize]
    [HttpPut("usuarios/editar/{id}")]
    public IActionResult EditarUsuario(Guid id, [FromBody] EditarUsuarioDTO dto)
    {
        
        var userIdFromToken = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        
        var esAdmin = User.IsInRole("Administrador");

        
        if (!esAdmin && userIdFromToken != id.ToString())
        {
            return Forbid("No tienes permisos para editar este perfil.");
        }

        var usuario = new Usuario
        {
            Id = id,
            Nombre = dto.Nombre,
            Email = dto.Email,
            ContrasenaHash = string.IsNullOrWhiteSpace(dto.Contrasena)
                             ? null
                             : BCrypt.Net.BCrypt.HashPassword(dto.Contrasena)
        };

        bool actualizado = _authService.EditarUsuario(usuario);

        return actualizado
            ? Ok("Usuario actualizado correctamente")
            : NotFound("Usuario no encontrado");
    }


    [Authorize]
    [HttpGet("usuarios/{id}/documentos")]
    public IActionResult ObtenerDocumentosPorUsuario(Guid id)
    {
        var userIdFromToken = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var esAdmin = User.IsInRole("Administrador");

        if (!esAdmin && userIdFromToken != id.ToString())
        {
            return Forbid("No puedes ver documentos de otros usuarios.");
        }

        var documentos = _authService.ObtenerDocumentosPorUsuario(id);

        return documentos == null || !documentos.Any()
            ? NotFound("El usuario no ha subido documentos.")
            : Ok(documentos);
    }



    [Authorize(Roles = "Administrador")]
    [HttpGet("roles")]
    public async Task<IActionResult> ObtenerRoles()
    {
        List<object> roles = new List<object>();

        using (var conexion = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
        {
            try
            {
                await conexion.OpenAsync();

                using (var command = new SqlCommand("SELECT Id, Nombre FROM Roles", conexion))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            roles.Add(new
                            {
                                Id = reader["Id"],
                                Nombre = reader["Nombre"]
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al obtener los roles: {ex.Message}");
            }
        }
        return Ok(roles);
    }

    [Authorize(Roles = "Administrador")]
    [HttpPut("roles/modificar-rol/{usuarioId}")]
    public async Task<IActionResult> ModificarRolUsuario(Guid usuarioId, [FromBody] ModificarRolDTO dto)
    {
        if (usuarioId == Guid.Empty || dto == null || dto.NuevoRolId == Guid.Empty)
            return BadRequest("Datos inválidos");

        using (var conexion = new SqlConnection(_config.GetConnectionString("DefaultConnection")))
        {
            await conexion.OpenAsync();

            // Verifica que el usuario exista
            using (var checkCmd = new SqlCommand("SELECT COUNT(*) FROM Usuarios WHERE Id = @UsuarioId", conexion))
            {
                checkCmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
                var existe = (int)await checkCmd.ExecuteScalarAsync();
                if (existe == 0)
                    return NotFound("Usuario no encontrado");
            }

            // Actualiza el rol
            using (var updateCmd = new SqlCommand("UPDATE Usuarios SET Rol_Id = @NuevoRolId WHERE Id = @UsuarioId", conexion))
            {
                updateCmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
                updateCmd.Parameters.AddWithValue("@NuevoRolId", dto.NuevoRolId);

                await updateCmd.ExecuteNonQueryAsync();
            }
        }

        return Ok("Rol del usuario actualizado correctamente.");
    }


}


