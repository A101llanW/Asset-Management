using System;
using System.Collections.Generic;
using System.Reflection;

namespace AssetManagement.Infrastructure.Persistence
{
    public sealed class EntityMap
    {
        public Type EntityType { get; set; }

        public string TableName { get; set; }

        public string PrimaryKey { get; set; }

        public bool PrimaryKeyIsIdentity { get; set; }

        public IList<PropertyInfo> ScalarProperties { get; set; }

        public IList<NavigationBinding> Navigations { get; set; }

        /// <summary>Table added by a migration that may not be applied yet: inserts are skipped while it is missing.</summary>
        public bool SkipWritesWhenTableMissing { get; set; }
    }

    public sealed class NavigationBinding
    {
        public PropertyInfo NavigationProperty { get; set; }

        public string ForeignKeyProperty { get; set; }

        public Type RelatedEntityType { get; set; }
    }
}
