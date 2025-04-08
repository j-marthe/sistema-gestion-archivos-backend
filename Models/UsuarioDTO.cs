namespace SistemaGestionArchivosBackend.Models
{
    public class UsuarioDTO
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; }
        public string Email { get; set; }
        public string Contrasena { get; set; }
        public DateTime FechaRegistro { get; set; }
        public Guid RolId { get; set; }
    }
}
