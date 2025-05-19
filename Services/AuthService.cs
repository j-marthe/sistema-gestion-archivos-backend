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

        var query = @"SELECT Id, Nombre, Email, FechaRegistro, Rol_Id FROM Usuarios";

        using var cmd = new SqlCommand(query, conexion);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            usuarios.Add(new UsuarioDTO
            {
                Id = reader.GetGuid(0),             
                Nombre = reader.GetString(1),       
                Email = reader.GetString(2),        
                FechaRegistro = reader.GetDateTime(3), 
                RolId = reader.GetGuid(4)           
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

        // Si se incluye una contraseña nueva, también se actualiza
        var query = usuario.ContrasenaHash != null
            ? @"UPDATE Usuarios 
           SET Nombre = @Nombre, Email = @Email, ContrasenaHash = @ContrasenaHash
           WHERE Id = @Id"
            : @"UPDATE Usuarios 
           SET Nombre = @Nombre, Email = @Email
           WHERE Id = @Id";

        using var cmd = new SqlCommand(query, conexion);
        cmd.Parameters.AddWithValue("@Id", usuario.Id);
        cmd.Parameters.AddWithValue("@Nombre", usuario.Nombre);
        cmd.Parameters.AddWithValue("@Email", usuario.Email);

        if (usuario.ContrasenaHash != null)
            cmd.Parameters.AddWithValue("@ContrasenaHash", usuario.ContrasenaHash);

        return cmd.ExecuteNonQuery() > 0;
    }


    public List<DocumentoUsuarioDTO> ObtenerDocumentosPorUsuario(Guid usuarioId)
    {
        var documentos = new List<DocumentoUsuarioDTO>();

        using var conexion = new SqlConnection(_connectionString);
        conexion.Open();
        var query = @"SELECT Archivos.Nombre, Archivos.FechaSubida, Categorias.Nombre FROM Archivos INNER JOIN Categorias ON Archivos.CategoriaId = Categorias.Id  WHERE Archivos.UsuarioId = @usuarioId";

        using var cmd = new SqlCommand(query, conexion);
        cmd.Parameters.AddWithValue("@usuarioId", usuarioId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            documentos.Add(new DocumentoUsuarioDTO
            {
                Nombre = reader.GetString(0),
                FechaRegistro = reader.GetDateTime(1),
                Categoria = reader.GetString(2),
            });
        }

        return documentos;
    }


}