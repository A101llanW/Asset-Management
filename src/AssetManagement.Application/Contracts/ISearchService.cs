using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface ISearchService
    {
        GlobalSearchResultVm Search(string query, int maxResults);
    }
}
