public class AuditoriaDTO
{
    public Guid Id { get; set; }
    public string Usuario { get; set; }
    public string Archivo { get; set; }
    public string Accion { get; set; }
    public string? Detalle { get; set; }
    public DateTime Fecha { get; set; }
}
