using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Script.Serialization;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Helpers
{
    public class LabelLayoutTemplate
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public int WidthMm { get; set; }

        public int HeightMm { get; set; }

        public decimal QrMagnification { get; set; }

        public string LayoutDesignJson { get; set; }

        public string UpdatedAtUtc { get; set; }
    }

    public class LabelLayoutTemplateCollection
    {
        public IList<LabelLayoutTemplate> Templates { get; set; } = new List<LabelLayoutTemplate>();
    }

    public static class LabelLayoutTemplateHelper
    {
        public const string TemplatesKey = "Label.Layout.TemplatesJson";
        public const int MaxTemplates = 20;
        public const int MaxNameLength = 80;

        public static IList<LabelLayoutTemplate> ListTemplates(IDictionary<string, Domain.Entities.SystemSetting> settings)
        {
            var collection = Deserialize(settings);
            return collection.Templates
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Id))
                .OrderBy(x => x.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static string SerializeForClient(IEnumerable<LabelLayoutTemplate> templates)
        {
            var collection = new LabelLayoutTemplateCollection
            {
                Templates = (templates ?? Enumerable.Empty<LabelLayoutTemplate>()).ToList()
            };

            return new JavaScriptSerializer().Serialize(collection);
        }

        public static LabelLayoutTemplate SaveTemplate(
            LabelLayoutTemplateCollection collection,
            string name,
            string layoutDesignJson,
            int widthMm,
            int heightMm,
            decimal qrMagnification,
            string templateId = null)
        {
            collection = collection ?? new LabelLayoutTemplateCollection();
            if (collection.Templates == null)
            {
                collection.Templates = new List<LabelLayoutTemplate>();
            }

            var normalizedName = NormalizeName(name);
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                throw new ArgumentException("Template name is required.");
            }

            var design = LabelLayoutDesignHelper.Deserialize(layoutDesignJson, widthMm, heightMm);
            var serializedDesign = LabelLayoutDesignHelper.Serialize(design, widthMm, heightMm);
            var now = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

            LabelLayoutTemplate existing = null;
            if (!string.IsNullOrWhiteSpace(templateId))
            {
                existing = collection.Templates.FirstOrDefault(x =>
                    string.Equals(x.Id, templateId.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (existing == null)
            {
                existing = collection.Templates.FirstOrDefault(x =>
                    string.Equals(NormalizeName(x.Name), normalizedName, StringComparison.OrdinalIgnoreCase));
            }

            if (existing != null)
            {
                existing.Name = normalizedName;
                existing.WidthMm = widthMm;
                existing.HeightMm = heightMm;
                existing.QrMagnification = qrMagnification;
                existing.LayoutDesignJson = serializedDesign;
                existing.UpdatedAtUtc = now;
                return existing;
            }

            if (collection.Templates.Count >= MaxTemplates)
            {
                throw new InvalidOperationException("Maximum of " + MaxTemplates + " layout templates reached. Delete one before saving another.");
            }

            var template = new LabelLayoutTemplate
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = normalizedName,
                WidthMm = widthMm,
                HeightMm = heightMm,
                QrMagnification = qrMagnification,
                LayoutDesignJson = serializedDesign,
                UpdatedAtUtc = now
            };

            collection.Templates.Add(template);
            return template;
        }

        public static bool DeleteTemplate(LabelLayoutTemplateCollection collection, string templateId)
        {
            if (collection == null || collection.Templates == null || string.IsNullOrWhiteSpace(templateId))
            {
                return false;
            }

            var removed = 0;
            for (var i = collection.Templates.Count - 1; i >= 0; i--)
            {
                if (string.Equals(collection.Templates[i].Id, templateId.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    collection.Templates.RemoveAt(i);
                    removed++;
                }
            }

            return removed > 0;
        }

        public static LabelLayoutTemplateCollection Deserialize(IDictionary<string, Domain.Entities.SystemSetting> settings)
        {
            settings = settings ?? new Dictionary<string, Domain.Entities.SystemSetting>();
            Domain.Entities.SystemSetting setting;
            if (!settings.TryGetValue(TemplatesKey, out setting) || string.IsNullOrWhiteSpace(setting.SettingValue))
            {
                return new LabelLayoutTemplateCollection();
            }

            try
            {
                var collection = new JavaScriptSerializer().Deserialize<LabelLayoutTemplateCollection>(setting.SettingValue);
                if (collection == null || collection.Templates == null)
                {
                    return new LabelLayoutTemplateCollection();
                }

                return collection;
            }
            catch (ArgumentException)
            {
                return new LabelLayoutTemplateCollection();
            }
            catch (InvalidOperationException)
            {
                return new LabelLayoutTemplateCollection();
            }
        }

        public static string SerializeCollection(LabelLayoutTemplateCollection collection)
        {
            collection = collection ?? new LabelLayoutTemplateCollection();
            collection.Templates = (collection.Templates ?? new List<LabelLayoutTemplate>())
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Id))
                .OrderBy(x => x.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new JavaScriptSerializer().Serialize(collection);
        }

        public static string NormalizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            var trimmed = name.Trim();
            if (trimmed.Length > MaxNameLength)
            {
                trimmed = trimmed.Substring(0, MaxNameLength);
            }

            return trimmed;
        }
    }
}
