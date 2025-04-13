namespace SistemaGestionArchivosBackend.Models
{
    public class SubidaArchivoDTO
    {
        public string Nombre { get; set; }
        public Guid CategoriaId { get; set; }
        public string? Etiquetas { get; set; }
        public string? Metadatos { get; set; }
    }
}
