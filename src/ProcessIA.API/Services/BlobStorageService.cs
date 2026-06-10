using Azure.Storage.Blobs;
using Azure.Storage.Sas;

namespace ProcessIA.API.Services;

public class BlobStorageService(IConfiguration config)
{
    private readonly BlobContainerClient _container = new(
        config["Azure:BlobStorage:ConnectionString"],
        config["Azure:BlobStorage:ContainerName"]
    );

    public async Task<(string blobUrl, string blobName)> UploadAsync(Stream stream, string fileName)
    {
        var blobName = $"{Guid.NewGuid()}/{fileName}";
        var client = _container.GetBlobClient(blobName);

        await client.UploadAsync(stream, overwrite: false);

        return (client.Uri.ToString(), blobName);
    }

    public Uri GetSignedUrl(string blobName, TimeSpan expiry)
    {
        var client = _container.GetBlobClient(blobName);

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = _container.Name,
            BlobName = blobName,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.Add(expiry)
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        return client.GenerateSasUri(sasBuilder);
    }

    public async Task DeleteAsync(string blobName)
    {
        var client = _container.GetBlobClient(blobName);
        await client.DeleteIfExistsAsync();
    }
}
