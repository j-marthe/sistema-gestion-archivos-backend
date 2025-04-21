public class Auditoria
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public Guid ArchivoId { get; set; }
    public string Accion { get; set; }
    public string? Detalle { get; set; }
    public DateTime Fecha { get; set; }
}
