using System;
using System.Data;
using System.IO;
using AssetManagement.Application.Contracts;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Infrastructure.Services
{
    public sealed class HybridFileStorageProvider : IFileStorageProvider
    {
        private const string DatabasePrefix = "db://";
        private readonly ISqlConnectionFactory _connectionFactory;
        private readonly FileSystemStorageProvider _legacyStorage;

        public HybridFileStorageProvider(ISqlConnectionFactory connectionFactory, string legacyRootPath)
        {
            _connectionFactory = connectionFactory;
            _legacyStorage = new FileSystemStorageProvider(legacyRootPath);
        }

        public string Save(Stream stream, string fileName, string contentType, string folder)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }

            using (var buffer = new MemoryStream())
            {
                stream.CopyTo(buffer);
                var key = DatabasePrefix + Guid.NewGuid().ToString("N");
                using (var connection = _connectionFactory.CreateConnection())
                {
                    connection.Open();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = @"
INSERT INTO [StoredFile] ([StorageKey], [OriginalFileName], [ContentType], [Content], [CreatedAtUtc])
VALUES (@StorageKey, @OriginalFileName, @ContentType, @Content, @CreatedAtUtc)";
                        AddParameter(command, "@StorageKey", key);
                        AddParameter(command, "@OriginalFileName", Path.GetFileName(fileName));
                        AddParameter(command, "@ContentType", contentType ?? "application/octet-stream");
                        AddParameter(command, "@Content", buffer.ToArray());
                        AddParameter(command, "@CreatedAtUtc", DateTime.UtcNow);
                        command.ExecuteNonQuery();
                    }
                }

                return key;
            }
        }

        public void Delete(string relativePath)
        {
            if (!IsDatabaseKey(relativePath))
            {
                _legacyStorage.Delete(relativePath);
                return;
            }

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM [StoredFile] WHERE [StorageKey] = @StorageKey";
                    AddParameter(command, "@StorageKey", relativePath);
                    command.ExecuteNonQuery();
                }
            }
        }

        public Stream OpenRead(string relativePath)
        {
            if (!IsDatabaseKey(relativePath))
            {
                return File.OpenRead(_legacyStorage.GetFullPath(relativePath));
            }

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT [Content] FROM [StoredFile] WHERE [StorageKey] = @StorageKey";
                    AddParameter(command, "@StorageKey", relativePath);
                    var value = command.ExecuteScalar();
                    if (value == null || value == DBNull.Value)
                    {
                        return null;
                    }

                    return new MemoryStream((byte[])value, false);
                }
            }
        }

        public bool Exists(string relativePath)
        {
            if (!IsDatabaseKey(relativePath))
            {
                return File.Exists(_legacyStorage.GetFullPath(relativePath));
            }

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT COUNT(1) FROM [StoredFile] WHERE [StorageKey] = @StorageKey";
                    AddParameter(command, "@StorageKey", relativePath);
                    return Convert.ToInt32(command.ExecuteScalar()) > 0;
                }
            }
        }

        public string GetFullPath(string relativePath)
        {
            if (IsDatabaseKey(relativePath))
            {
                throw new InvalidOperationException("Database-backed files do not have a physical path. Use OpenRead.");
            }

            return _legacyStorage.GetFullPath(relativePath);
        }

        private static bool IsDatabaseKey(string value)
        {
            return !string.IsNullOrWhiteSpace(value)
                && value.StartsWith(DatabasePrefix, StringComparison.OrdinalIgnoreCase);
        }

        private static void AddParameter(IDbCommand command, string name, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
    }
}
