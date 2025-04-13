using System.Data.SqlClient;
using System;
using Microsoft.Extensions.Configuration;
using SistemaGestionArchivosBackend.Models;



public class AuthService
{

    private readonly string _connectionString;
    private readonly IConfiguration _configuration;

    public AuthService(IConfiguration configuration)
    {
        _configuration = configuration;
        _connectionString = _configuration.GetConnectionString("DefaultConnection");
    }

    public bool UsuarioExiste(string email)
    {
        using var conexion = new SqlConnection(_connectionString);
        conexion.Open();

        var query = "SELECT COUNT(*) FROM Usuarios WHERE Email = @Email";
        using var cmd = new SqlCommand(query, conexion);
        cmd.Parameters.AddWithValue("@Email", email);

        int count = (int)cmd.ExecuteScalar();
        return count > 0;
    }

    public void RegistrarUsuario(Usuario usuario)
    {

        using var conexion = new SqlConnection(_connectionString);
        conexion.Open();

        var query = @"INSERT INTO Usuarios (Id, Nombre, Email, ContrasenaHash, FechaRegistro, Rol_Id)
                      VALUES (@Id, @Nombre, @Email, @ContrasenaHash, @FechaRegistro, @Rol_Id)";

        using var cmd = new SqlCommand(query, conexion);
        cmd.Parameters.AddWithValue("@Id", usuario.Id);
        cmd.Parameters.AddWithValue("@Nombre", usuario.Nombre);
        cmd.Parameters.AddWithValue("@Email", usuario.Email);
        cmd.Parameters.AddWithValue("@ContrasenaHash", usuario.ContrasenaHash);
        cmd.Parameters.AddWithValue("@FechaRegistro", usuario.FechaRegistro);
        cmd.Parameters.AddWithValue("@Rol_Id", usuario.RolId);

        cmd.ExecuteNonQuery();
    }

    public Usuario? ObtenerUsuarioPorEmail(string email)
    {

        using var conexion = new SqlConnection(_connectionString);
        conexion.Open();

        var query = "SELECT * FROM Usuarios WHERE Email = @Email";
        using var cmd = new SqlCommand(query, conexion);
        cmd.Parameters.AddWithValue("@Email", email);

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return new Usuario
            {
                Id = reader.GetGuid(0),
                Nombre = reader.GetString(1),
                Email = reader.GetString(2),
                ContrasenaHash = reader.GetString(3),
                FechaRegistro = reader.GetDateTime(4),
                RolId = reader.GetGuid(5)
            };
        }
        return null;

    }

    public Usuario? ObtenerUsuarioPorId(Guid id) 
    {
        using var conexion = new SqlConnection(_connectionString);
        conexion.Open();

        var query = "SELECT * FROM Usuarios WHERE Id = @Id";
        using var cmd = new SqlCommand(query, conexion);
        cmd.Parameters.AddWithValue("@Id", id);

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return new Usuario
            {
                Id = reader.GetGuid(0),
                Nombre = reader.GetString(1),
                Email = reader.GetString(2),
                ContrasenaHash = reader.GetString(3),
                FechaRegistro = reader.GetDateTime(4),
                RolId = reader.GetGuid(5)
            };
        }
        return null;

    }

    public List<UsuarioDTO> ObtenerTodosLosUsuarios()
    {
        var usuarios = new List<UsuarioDTO>();
        using var conexion = new SqlConnection(_connectionString);
        conexion.Open();

        var query = @"SELECT u.Id, u.Nombre, u.Email, u.FechaRegistro, r.Nombre AS Rol
                  FROM Usuarios u
                  JOIN Roles r ON u.Rol_Id = r.Id";

        using var cmd = new SqlCommand(query, conexion);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            usuarios.Add(new UsuarioDTO
            {
                Id = reader.GetGuid(0),
                Nombre = reader.GetString(1),
                Email = reader.GetString(2),
                FechaRegistro = reader.GetDateTime(4),
                RolId = reader.GetGuid(5)
            });
        }

        return usuarios;
    }

    public bool EliminarUsuario(Guid id)
    {
        using var conexion = new SqlConnection(_connectionString);
        conexion.Open();

        var cmd = new SqlCommand("DELETE FROM Usuarios WHERE Id = @Id", conexion);
        cmd.Parameters.AddWithValue("@Id", id);

        return cmd.ExecuteNonQuery() > 0;
    }

    public bool EditarUsuario(Usuario usuario) 
    {
        using var conexion = new SqlConnection(_connectionString);
        conexion.Open();

        var query = @"UPDATE Usuarios 
                  SET Nombre = @Nombre, Email = @Email, Rol_Id = @Rol_Id 
                  WHERE Id = @Id";

        using var cmd = new SqlCommand(query, conexion);
        cmd.Parameters.AddWithValue("@Id", usuario.Id);
        cmd.Parameters.AddWithValue("@Nombre", usuario.Nombre);
        cmd.Parameters.AddWithValue("@Email", usuario.Email);
        cmd.Parameters.AddWithValue("@Rol_Id", usuario.RolId);

        return cmd.ExecuteNonQuery() > 0;
    }

}