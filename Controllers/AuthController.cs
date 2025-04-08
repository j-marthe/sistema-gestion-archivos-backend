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

    [HttpPost("registro")]
    public IActionResult Registrar(UsuarioDTO usuarioDto)
    {
        try
        {
            if (_authService.UsuarioExiste(usuarioDto.Email))
                return BadRequest("El email ya está registrado.");

            var usuario = new Usuario
            {
                Id = Guid.NewGuid(),
                Nombre = usuarioDto.Nombre,
                Email = usuarioDto.Email,
                ContrasenaHash = BCrypt.Net.BCrypt.HashPassword(usuarioDto.Contrasena),
                FechaRegistro = DateTime.Now,
                RolId = usuarioDto.RolId
            };

            _authService.RegistrarUsuario(usuario);
            return Ok("Usuario registrado correctamente.");
        }
        catch (Exception ex)
        {
            // Devuelve el error real al cliente (temporalmente para debug)
            return StatusCode(500, $"Error interno: {ex.Message}");
        }
    }

    [HttpPost("login")]
    public IActionResult Login(LoginDTO login)
    {
        var usuario = _authService.ObtenerUsuarioPorEmail(login.Email);
        Guid rolId = usuario.RolId;

        if (usuario == null || !BCrypt.Net.BCrypt.Verify(login.Contrasena, usuario.ContrasenaHash))
            return Unauthorized("Credenciales inválidas.");

        string rolNombre = usuario.RolId switch
        {
            Guid id when id == Guid.Parse("C3C1D079-5736-4D73-A522-3ED165DB693B") => "Administrador",
            Guid id when id == Guid.Parse("07A3CE69-025D-41F4-8C76-F3DB00E60BD8") => "Usuario Estándar",
            Guid id when id == Guid.Parse("6B5F923B-DDA5-4096-AE0A-0E5617694F1E") => "Lector",
            _ => "Desconocido" // Se añade una opción por si no coincide con ningún rol conocido
        };

        var token = TokenUtils.GenerarTokenJWT(usuario, rolNombre, _config);
        return Ok(new { Token = token });
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
    [HttpGet("usuarios/{id}")]
    public IActionResult EliminarUsuario(Guid id)
    {
        var eliminado = _authService.EliminarUsuario(id);
        return eliminado ? Ok("Usuario eliminado") : NotFound("Usuario no encontrado");
    }
}

// Mejor método para proteger rutas por roles en un futuro
/*[Authorize(Roles = "Administrador")]
[HttpGet("admin-only")]
public IActionResult SoloAdmin()
{
    return Ok("Solo los admins pueden ver esto");
}
*/
