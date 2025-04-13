

using Azure.Storage.Blobs;

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

            var blobClient = container.GetBlobClient(nombre);
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
