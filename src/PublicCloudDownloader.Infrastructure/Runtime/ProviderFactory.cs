using PublicCloudDownloader.Core.Models;
using PublicCloudDownloader.Core.Providers;
using PublicCloudDownloader.Core.Workflow;
using PublicCloudDownloader.Providers.GoogleDrive;
using PublicCloudDownloader.Providers.OneDrivePersonal;

namespace PublicCloudDownloader.Infrastructure.Runtime;

public sealed class ProviderFactory : IProviderFactory
{
    public IPublicCloudProvider Create(ProviderKind kind) => kind switch
    {
        ProviderKind.GoogleDrive => new GoogleDriveProvider(),
        ProviderKind.OneDrivePersonal => new OneDrivePersonalProvider(),
        _ => throw new NotSupportedException("The cloud provider is not supported.")
    };
}
