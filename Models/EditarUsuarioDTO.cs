namespace SistemaGestionArchivosBackend.Models
{
    public class EditarUsuarioDTO
    {
        public string Nombre { get; set; }
        public string Email { get; set; }
        public string Contrasena { get; set; } 
        public Guid RolId { get; set; }
    }
}
