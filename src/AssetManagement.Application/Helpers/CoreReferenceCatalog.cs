using System;
using System.Collections.Generic;
using System.Linq;

namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// Company-agnostic starter departments, categories, and asset types.
    /// Used by the import template pick-lists and organization provisioning.
    /// </summary>
    public static class CoreReferenceCatalog
    {
        public sealed class DepartmentSeed
        {
            public string Name { get; set; }
            public string Code { get; set; }
            public string Description { get; set; }
        }

        public sealed class CategorySeed
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public int? DefaultDepreciationLifeMonths { get; set; }
            public decimal? DefaultDepreciationRatePercent { get; set; }
        }

        public sealed class AssetTypeSeed
        {
            public string CategoryName { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
        }

        public static readonly DepartmentSeed[] Departments =
        {
            new DepartmentSeed { Name = "Information Technology", Code = "IT", Description = "IT department" },
            new DepartmentSeed { Name = "Finance", Code = "FIN", Description = "Finance department" },
            new DepartmentSeed { Name = "Human Resources", Code = "HR", Description = "HR department" },
            new DepartmentSeed { Name = "Operations", Code = "OPS", Description = "Operations department" },
            new DepartmentSeed { Name = "Administration", Code = "ADMIN", Description = "Administration department" }
        };

        public static readonly CategorySeed[] Categories =
        {
            new CategorySeed { Name = "IT Equipment", Description = "Computing and peripheral assets", DefaultDepreciationLifeMonths = 48, DefaultDepreciationRatePercent = 25m },
            new CategorySeed { Name = "Office Equipment", Description = "Printers, projectors, and general office devices", DefaultDepreciationLifeMonths = 60, DefaultDepreciationRatePercent = 20m },
            new CategorySeed { Name = "Furniture", Description = "Office furniture assets", DefaultDepreciationLifeMonths = 84, DefaultDepreciationRatePercent = 14.29m },
            new CategorySeed { Name = "Networking", Description = "Network and communication assets", DefaultDepreciationLifeMonths = 60, DefaultDepreciationRatePercent = 20m },
            new CategorySeed { Name = "Medical/Lab Equipment", Description = "Healthcare and laboratory assets", DefaultDepreciationLifeMonths = 84, DefaultDepreciationRatePercent = 14.29m },
            new CategorySeed { Name = "Vehicles", Description = "Fleet and transport assets", DefaultDepreciationLifeMonths = 60, DefaultDepreciationRatePercent = 20m }
        };

        public static readonly AssetTypeSeed[] AssetTypes =
        {
            new AssetTypeSeed { CategoryName = "IT Equipment", Name = "Laptop", Description = "Portable computer" },
            new AssetTypeSeed { CategoryName = "IT Equipment", Name = "Desktop", Description = "Desktop computer" },
            new AssetTypeSeed { CategoryName = "Networking", Name = "Router", Description = "Router and gateway" },
            new AssetTypeSeed { CategoryName = "Furniture", Name = "Office Chair", Description = "Ergonomic chair" },
            new AssetTypeSeed { CategoryName = "Furniture", Name = "Office Desk", Description = "Office desk or workstation" },
            new AssetTypeSeed { CategoryName = "Medical/Lab Equipment", Name = "Lab Microscope", Description = "Microscope device" },
            new AssetTypeSeed { CategoryName = "Medical/Lab Equipment", Name = "Lab Centrifuge", Description = "Benchtop laboratory centrifuge" },
            new AssetTypeSeed { CategoryName = "Office Equipment", Name = "Printer", Description = "Office printer or MFP" },
            new AssetTypeSeed { CategoryName = "Office Equipment", Name = "Projector", Description = "Conference room projector" },
            new AssetTypeSeed { CategoryName = "Vehicles", Name = "Vehicle", Description = "Company fleet vehicle" }
        };

        public static string[] DepartmentNames
        {
            get { return Departments.Select(x => x.Name).ToArray(); }
        }

        public static string[] CategoryNames
        {
            get { return Categories.Select(x => x.Name).ToArray(); }
        }

        public static string[] AssetTypeNames
        {
            get { return AssetTypes.Select(x => x.Name).ToArray(); }
        }
    }
}
