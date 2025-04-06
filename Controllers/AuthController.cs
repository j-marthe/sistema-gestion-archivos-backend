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

    [HttpPost("login")]
    public IActionResult Login(LoginDTO login)
    {
        var usuario = _authService.ObtenerUsuarioPorEmail(login.Email);
        if (usuario == null || !BCrypt.Net.BCrypt.Verify(login.Contrasena, usuario.ContrasenaHash))
            return Unauthorized("Credenciales inválidas.");

        var token = TokenUtils.GenerarTokenJWT(usuario, _config);
        return Ok(new { Token = token });
    }
}
