using System;
using System.IO;
using System.Web;
using System.Web.Mvc;

namespace AssetManagement.Web.Helpers
{
    public static class OrganizationLogoHelper
    {
        public static string GetLogoUrl(UrlHelper urlHelper, string logoPath)
        {
            if (urlHelper == null || string.IsNullOrWhiteSpace(logoPath))
            {
                return null;
            }

            return urlHelper.Content(logoPath.TrimStart('~'));
        }

        private const int MaxLogoBytes = 512 * 1024;

        public static string SaveLogo(HttpPostedFileBase file, int organizationId)
        {
            if (file == null || file.ContentLength <= 0 || file.ContentLength > MaxLogoBytes)
            {
                return null;
            }

            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".png";
            }

            extension = extension.ToLowerInvariant();
            if (extension != ".png" && extension != ".jpg" && extension != ".jpeg" && extension != ".gif" && extension != ".webp")
            {
                return null;
            }

            var relativeDirectory = "Content/OrganizationLogos/" + organizationId;
            var absoluteDirectory = HttpContext.Current.Server.MapPath("~/" + relativeDirectory);
            Directory.CreateDirectory(absoluteDirectory);

            var fileName = Guid.NewGuid().ToString("N") + extension;
            var absolutePath = Path.Combine(absoluteDirectory, fileName);
            file.SaveAs(absolutePath);
            return "/" + relativeDirectory + "/" + fileName;
        }

        public static void DeleteLogo(string logoPath)
        {
            if (string.IsNullOrWhiteSpace(logoPath) || HttpContext.Current == null)
            {
                return;
            }

            var absolutePath = HttpContext.Current.Server.MapPath("~" + logoPath.TrimStart('/'));
            if (File.Exists(absolutePath))
            {
                File.Delete(absolutePath);
            }
        }
    }
}
