using System.Web;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.ViewModels;
using AssetManagement.Web.ViewModels;

namespace AssetManagement.Web.Helpers
{
    public static class AssetLabelPrintHelper
    {
        public static AssetLabelPrintVm CreateModel(IAssetService assetService, HttpRequestBase request, UrlHelper urlHelper, int assetId)
        {
            if (assetService == null || request == null || urlHelper == null)
            {
                return null;
            }

            var asset = assetService.GetById(assetId);
            if (asset == null)
            {
                return null;
            }

            return CreateModel(request, urlHelper, asset);
        }

        public static AssetLabelPrintVm CreateModel(HttpRequestBase request, UrlHelper urlHelper, AssetDetailsVm asset)
        {
            if (request == null || urlHelper == null || asset == null)
            {
                return null;
            }

            return new AssetLabelPrintVm
            {
                AssetId = asset.Id,
                AssetTag = asset.AssetTag,
                AssetName = asset.AssetName,
                DepartmentName = asset.DepartmentName,
                SerialNumber = asset.SerialNumber,
                CurrentStatus = asset.CurrentStatus.ToString(),
                ScanUrl = AssetQrUrlHelper.BuildScanUrl(request, urlHelper, asset.AssetTag)
            };
        }
    }
}
