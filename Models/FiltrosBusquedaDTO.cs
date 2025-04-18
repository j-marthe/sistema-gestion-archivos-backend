public class FiltrosBusquedaDTO
{
    public string? Nombre { get; set; }
    public string? Usuario { get; set; }
    public Guid? CategoriaId { get; set; }
    public List<string>? Etiquetas { get; set; }
    public DateTime? FechaDesde { get; set; }
    public DateTime? FechaHasta { get; set; }
    public string? ValorMetadato { get; set; }
}
