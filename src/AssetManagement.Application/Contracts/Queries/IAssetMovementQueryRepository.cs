using System;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts.Queries
{
    public interface IAssetMovementQueryRepository
    {
        /// <param name="scopedDepartmentId">When set, only movements from or to this department (or its direct children).</param>
        /// <param name="fromUtc">Inclusive UTC lower bound.</param>
        /// <param name="toUtcExclusive">Exclusive UTC upper bound.</param>
        AssetMovementQueryResult Query(
            int organizationId,
            int? scopedDepartmentId,
            AssetMovementFilterVm filter,
            DateTime? fromUtc,
            DateTime? toUtcExclusive,
            int skip,
            int take,
            bool includeTotal);
    }
}
