using System;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Services
{
    public class SearchService : ISearchService
    {
        private readonly ISearchQueryService _searchQueryService;

        public SearchService(ISearchQueryService searchQueryService)
        {
            _searchQueryService = searchQueryService;
        }

        public GlobalSearchResultVm Search(string query, int maxResults)
        {
            return _searchQueryService.GlobalSearch(query, maxResults);
        }
    }
}
