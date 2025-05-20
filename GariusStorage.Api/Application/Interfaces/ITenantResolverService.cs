namespace GariusStorage.Api.Application.Interfaces
{
    public interface ITenantResolverService
    {
        Guid? GetCurrentCompanyId();
    }
}
