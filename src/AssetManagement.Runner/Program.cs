using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Organizations;
using AssetManagement.Infrastructure.Persistence;
using AssetManagement.Infrastructure.Services;

namespace AssetManagement.Runner
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            if (args.Length > 0 && string.Equals(args[0], "school-org", StringComparison.OrdinalIgnoreCase))
            {
                RunSchoolOrganizationBootstrap(args);
                return;
            }

            if (args.Length > 0 && string.Equals(args[0], "migrate", StringComparison.OrdinalIgnoreCase))
            {
                RunMigrations(args);
                return;
            }

            if (args.Length > 0 && string.Equals(args[0], "export-import-template", StringComparison.OrdinalIgnoreCase))
            {
                ExportImportTemplate(args);
                return;
            }

            if (args.Length > 0 && string.Equals(args[0], "outbox-worker", StringComparison.OrdinalIgnoreCase))
            {
                RunOutboxWorker(args);
                return;
            }

            RunWebHost(args);
        }

        private static void RunOutboxWorker(string[] args)
        {
            var dispatcher = BootstrapServiceFactory.CreateOutboxDispatcher();
            PurgeStaleSecurityRecords(new SqlConnectionFactory("AssetManagementConnection"));
            var intervalSeconds = 5;
            for (;;)
            {
                dispatcher.ProcessPending(100);
                System.Threading.Thread.Sleep(intervalSeconds * 1000);
            }
        }

        private static void PurgeStaleSecurityRecords(ISqlConnectionFactory connectionFactory)
        {
            try
            {
                new AssetManagement.Infrastructure.Security.LoginAttemptRepository(connectionFactory)
                    .PurgeOlderThan(System.DateTime.UtcNow.AddDays(-30));
                new AssetManagement.Infrastructure.Identity.PasswordResetRepository(connectionFactory)
                    .PurgeStale(System.DateTime.UtcNow.AddDays(-7));
            }
            catch (System.Exception ex)
            {
                Console.Error.WriteLine("Security cleanup failed: " + ex.Message);
            }
        }

        private static void RunMigrations(string[] args)
        {
            var scriptsPath = ResolveScriptsPath(args);
            if (!string.IsNullOrWhiteSpace(scriptsPath))
            {
                ConfigurationManager.AppSettings["DatabaseScriptsPath"] = scriptsPath;
            }

            var connectionString = ConfigurationManager.ConnectionStrings["AssetManagementConnection"].ConnectionString;
            var builder = new System.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
            Console.WriteLine("Applying migrations via ASP.NET SqlDatabaseInitializer...");
            Console.WriteLine("  Server:   " + builder.DataSource);
            Console.WriteLine("  Database: " + builder.InitialCatalog);
            Console.WriteLine("  Scripts:  " + (string.IsNullOrWhiteSpace(scriptsPath) ? "auto" : scriptsPath));

            SqlDatabaseInitializer.ApplyMigrations(
                "AssetManagementConnection",
                SqlDatabaseInitializer.ResolveMigrationContinueOnError());
            Console.WriteLine("Migrations complete.");
        }

        private static void ExportImportTemplate(string[] args)
        {
            var outputPath = (string)null;
            for (var i = 1; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--output", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    outputPath = Path.GetFullPath(args[++i]);
                }
            }

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                Console.Error.WriteLine("Usage: export-import-template --output <path.xlsx>");
                Environment.Exit(1);
                return;
            }

            var importService = BootstrapServiceFactory.CreateAssetImportService();
            if (importService == null)
            {
                Console.Error.WriteLine("Asset import service is unavailable.");
                Environment.Exit(1);
                return;
            }

            var bytes = importService.GetImportTemplate();
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(outputPath, bytes);
            Console.WriteLine("Wrote import template: " + outputPath);
        }

        private static void RunSchoolOrganizationBootstrap(string[] args)
        {
            var name = "NIS";
            var slug = "nis";
            var templatePath = ResolveDefaultTemplatePath();
            var roleTemplateSlug = "nanosoft";
            var adminEmail = (string)null;
            var force = false;

            for (var i = 1; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--name", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    name = args[++i];
                }
                else if (string.Equals(args[i], "--slug", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    slug = args[++i];
                }
                else if (string.Equals(args[i], "--template", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    templatePath = Path.GetFullPath(args[++i]);
                }
                else if (string.Equals(args[i], "--role-template", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    roleTemplateSlug = args[++i];
                }
                else if (string.Equals(args[i], "--admin-email", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    adminEmail = args[++i];
                }
                else if (string.Equals(args[i], "--force", StringComparison.OrdinalIgnoreCase))
                {
                    force = true;
                }
            }

            if (string.IsNullOrWhiteSpace(templatePath) || !File.Exists(templatePath))
            {
                Console.Error.WriteLine("Template file not found: " + (templatePath ?? "(null)"));
                Environment.Exit(1);
                return;
            }

            if (force)
            {
                PurgeOrganizationBySlug(slug);
            }

            var bootstrap = BootstrapServiceFactory.CreateSchoolBootstrap();
            var result = bootstrap.Bootstrap(new SchoolOrganizationBootstrapRequest
            {
                Name = name,
                Slug = slug,
                AdminEmail = adminEmail,
                RoleTemplateOrganizationSlug = roleTemplateSlug,
                TemplatePath = templatePath
            });

            if (!result.Succeeded)
            {
                Console.Error.WriteLine("School organization bootstrap failed: " + result.Message);
                Environment.Exit(1);
                return;
            }

            Console.WriteLine("School organization bootstrap complete.");
            Console.WriteLine("  Organization: " + name + " (id " + result.OrganizationId + ", slug " + result.Slug + ")");
            Console.WriteLine("  Admin email:  " + result.AdminEmail);
            if (!string.IsNullOrWhiteSpace(result.ProvisionalPassword))
            {
                Console.WriteLine("  Password:     " + result.ProvisionalPassword);
            }

            Console.WriteLine("  Imported:     " + result.ImportedCount + " assets");
            if (result.SkippedCount > 0)
            {
                Console.WriteLine("  Skipped:      " + result.SkippedCount + " rows");
                if (result.ImportMessages != null)
                {
                    var maxMessages = Math.Min(result.ImportMessages.Count, 10);
                    for (var i = 0; i < maxMessages; i++)
                    {
                        Console.WriteLine("    " + result.ImportMessages[i]);
                    }

                    if (result.ImportMessages.Count > maxMessages)
                    {
                        Console.WriteLine("    ... and " + (result.ImportMessages.Count - maxMessages) + " more");
                    }
                }
            }

            Console.WriteLine("  Portal:       /" + result.Slug + "/Account/Login");
        }

        private static void PurgeOrganizationBySlug(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return;
            }

            var connectionFactory = new SqlConnectionFactory("AssetManagementConnection");
            using (var connection = connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT [Id] FROM [Organization] WHERE [Slug] = @Slug";
                    command.Parameters.Add(new System.Data.SqlClient.SqlParameter("@Slug", slug.Trim().ToLowerInvariant()));
                    var orgIdObj = command.ExecuteScalar();
                    if (orgIdObj == null || orgIdObj == DBNull.Value)
                    {
                        return;
                    }

                    var purge = new OrganizationPurgeService(connectionFactory);
                    var purgeResult = purge.DeleteOrganizationAndData(Convert.ToInt32(orgIdObj));
                    if (purgeResult.Succeeded)
                    {
                        Console.WriteLine("Removed existing organization '" + slug + "' before bootstrap.");
                    }
                    else
                    {
                        Console.Error.WriteLine("Could not remove existing organization: " + purgeResult.Message);
                        Environment.Exit(1);
                    }
                }
            }
        }

        private static string ResolveDefaultTemplatePath()
        {
            var repoRoot = ResolveRepoRoot();
            var fixturePath = Path.Combine(repoRoot, "database", "fixtures", "nis-school-opening-balance.xlsx");
            if (File.Exists(fixturePath))
            {
                return fixturePath;
            }

            var e2eFixture = Path.Combine(repoRoot, "e2e", "fixtures", "school-import-template.xlsx");
            return File.Exists(e2eFixture) ? e2eFixture : fixturePath;
        }

        private static string ResolveRepoRoot()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "database", "scripts"))
                    && Directory.Exists(Path.Combine(dir.FullName, "src")))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", ".."));
        }

        private static string ResolveScriptsPath(string[] args)
        {
            for (var i = 1; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--scripts", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    return Path.GetFullPath(args[i + 1]);
                }
            }

            var repoScripts = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "database", "scripts"));
            return Directory.Exists(repoScripts) ? repoScripts : null;
        }

        private static void RunWebHost(string[] args)
        {
            var port = args.Length > 0 ? args[0] : "51901";
            var repoRoot = ResolveRepoRoot();
            var webPath = Path.GetFullPath(Path.Combine(repoRoot, "src", "AssetManagement.Web"));
            var configPath = Path.Combine(repoRoot, ".build", "iis-remote", "applicationhost.config");
            var iisExpressPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "IIS Express", "iisexpress.exe");
            if (!File.Exists(iisExpressPath))
            {
                Console.Error.WriteLine("IIS Express not found. Install IIS Express to run the web module.");
                Environment.Exit(1);
                return;
            }

            if (!Directory.Exists(webPath))
            {
                Console.Error.WriteLine("Web project path not found: " + webPath);
                Environment.Exit(1);
                return;
            }

            if (!File.Exists(configPath))
            {
                Console.Error.WriteLine("IIS Express config not found: " + configPath);
                Environment.Exit(1);
                return;
            }

            EnsureIisConfig(webPath, port, configPath, bindAllInterfaces: false);
            ClearStaleIisExpressTempConfigs();

            var startInfo = new ProcessStartInfo
            {
                FileName = iisExpressPath,
                Arguments = "/config:\"" + configPath + "\" /site:AssetManagementRemote",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = false
            };

            var process = Process.Start(startInfo);
            if (process == null)
            {
                Console.Error.WriteLine("Failed to start IIS Express.");
                Environment.Exit(1);
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "http://localhost:" + port + "/",
                UseShellExecute = true
            });

            process.OutputDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) Console.WriteLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) Console.Error.WriteLine(e.Data); };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            Console.WriteLine("AssetManagement web app running on http://localhost:" + port + "/");
            Console.WriteLine("MFA: codes are emailed via SMTP (MfaAllowAnyCode=false). Configure Platform Settings → Email or Web.config SMTP keys.");
            Console.WriteLine("Press ENTER to stop.");
            Console.ReadLine();

            if (!process.HasExited)
            {
                process.Kill();
                process.WaitForExit();
            }
        }

        private static void EnsureIisConfig(string webPath, string port, string configPath, bool bindAllInterfaces)
        {
            var doc = XDocument.Load(configPath);
            var site = doc.Root
                .Element("system.applicationHost")
                .Element("sites")
                .Elements("site")
                .FirstOrDefault(x => (string)x.Attribute("name") == "AssetManagementRemote");

            if (site == null)
            {
                throw new InvalidOperationException("Site 'AssetManagementRemote' was not found in " + configPath);
            }

            var bindingHost = bindAllInterfaces ? "*" : "127.0.0.1";
            site.Element("application").Element("virtualDirectory").SetAttributeValue("physicalPath", webPath);
            site.Element("bindings").Element("binding").SetAttributeValue("bindingInformation", bindingHost + ":" + port + ":");
            doc.Save(configPath);
        }

        private static void ClearStaleIisExpressTempConfigs()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "iisexpress");
            if (!Directory.Exists(tempDir))
            {
                return;
            }

            foreach (var file in Directory.GetFiles(tempDir, "applicationhost*.config"))
            {
                try
                {
                    File.Delete(file);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

    }
}
