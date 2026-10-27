using AssetManagement.Application.DTOs;



namespace AssetManagement.Application.Contracts

{

    public interface IAssetScanLookupRepository

    {

        bool ExistsByScanCode(string code, int organizationId, bool matchSerialNumber = true);



        /// <param name="matchSerialNumber">Review S8: false = tag, barcode and old tags only (public scan viewers).</param>
        AssetScanLookupResult FindByScanCode(string code, int organizationId, int? departmentId, bool matchSerialNumber = true);

    }

}


