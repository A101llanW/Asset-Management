using AssetManagement.Application.DTOs;



namespace AssetManagement.Application.Contracts

{

    public interface IAssetScanLookupRepository

    {

        bool ExistsByScanCode(string code, int organizationId);



        AssetScanLookupResult FindByScanCode(string code, int organizationId, int? departmentId);

    }

}


