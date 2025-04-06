using System;

public class Usuario
{
    public Guid Id {get; set; }
    public string Nombre { get; set; }
    public string Email { get; set; }
    public string ContrasenaHash { get; set; }
    public DateTime FechaRegistro { get; set; }
    public Guid RolId { get; set; } 
}