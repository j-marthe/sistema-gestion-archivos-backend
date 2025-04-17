

using System.Text.RegularExpressions;
using Azure.Storage.Blobs;
using System.Text.RegularExpressions;

namespace SistemaGestionArchivosBackend.Services
{
    public interface IBlobStorageService
    {
        Task<string> SubirArchivoAsync(IFormFile archivo, string nombre);
        Task<Stream> DescargarArchivoAsync(string nombre);
        Task<bool> EliminarArchivoAsync(string nombre);
    }

    public class BlobStorageService: IBlobStorageService
    {
        private readonly string _connectionString;
        private readonly string _containerName;

        public BlobStorageService(IConfiguration config)
        {
            _connectionString = config["AzureBlobStorage:ConnectionString"];
            _containerName = config["AzureBlobStorage:ContainerName"];
        }

        public async Task<string> SubirArchivoAsync(IFormFile archivo, string nombre)
        {
            var blobServiceClient = new BlobServiceClient(_connectionString);
            var container = blobServiceClient.GetBlobContainerClient(_containerName);
            await container.CreateIfNotExistsAsync();

            // Limpieza del nombre (sin espacios ni caracteres conflictivos)
            string nombreLimpio = nombre.Replace(" ", "_");
            nombreLimpio = Regex.Replace(nombreLimpio, @"[^a-zA-Z0-9_\.-]", "");

            var blobClient = container.GetBlobClient(nombreLimpio); // 👈 NO agregar extensión

            using var stream = archivo.OpenReadStream();
            await blobClient.UploadAsync(stream, overwrite: true);

            return blobClient.Uri.ToString();
        }

        public async Task<Stream> DescargarArchivoAsync(string nombre)
        {
            var blobServiceClient = new BlobServiceClient(_connectionString);
            var container = blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = container.GetBlobClient(nombre);
            var response = await blobClient.DownloadAsync();
            return response.Value.Content;
        }

        public async Task<bool> EliminarArchivoAsync(string nombre)
        {
            var blobServiceClient = new BlobServiceClient(_connectionString);
            var container = blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = container.GetBlobClient(nombre);
            return await blobClient.DeleteIfExistsAsync();
        }
    }
}
