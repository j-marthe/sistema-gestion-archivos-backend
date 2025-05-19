using System;
using System.Threading.Tasks;

public static class RetryHelper
{
    // Método para ejecutar una acción con reintentos
    public static async Task<T> ExecuteAsync<T>(Func<Task<T>> action, int maxAttempts = 6, int delayMilliseconds = 2000)
    {
        int attempts = 0; // Contador de intentos
        while (true)
        {
            try
            {
                attempts++;  // Incrementamos el contador de intentos
                return await action();  // Intentamos ejecutar la acción
            }
            catch (Exception ex)  // Si hay un error, lo capturamos
            {
                Console.WriteLine($"Intento {attempts} fallido: {ex.Message}");

                if (attempts >= maxAttempts)
                {
                    Console.WriteLine("Todos los intentos fallaron, lanzando excepción final.");
                    throw;  // Lanza la excepción final si se alcanzaron los intentos máximos
                }

                // Si no hemos alcanzado los intentos máximos, esperamos un poco y reintentamos
                await Task.Delay(delayMilliseconds);  // Espera 2 segundos (ajustable)
            }
        }
    }
}
