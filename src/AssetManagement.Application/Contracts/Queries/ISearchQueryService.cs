using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts.Queries
{
    public interface ISearchQueryService
    {
        GlobalSearchResultVm GlobalSearch(string term, int maxResults);
    }
}
