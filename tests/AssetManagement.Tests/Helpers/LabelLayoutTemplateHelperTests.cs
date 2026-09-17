using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Helpers
{
    [TestFixture]
    public class LabelLayoutTemplateHelperTests
    {
        [Test]
        public void SaveTemplate_CreatesNewTemplate()
        {
            var collection = new LabelLayoutTemplateCollection();
            var designJson = LabelLayoutDesignHelper.Serialize(LabelLayoutDesignHelper.CreateDefault(76, 37), 76, 37);

            var template = LabelLayoutTemplateHelper.SaveTemplate(
                collection,
                "Standard tag",
                designJson,
                76,
                37,
                3m);

            Assert.IsFalse(string.IsNullOrWhiteSpace(template.Id));
            Assert.AreEqual("Standard tag", template.Name);
            Assert.AreEqual(1, collection.Templates.Count);
        }

        [Test]
        public void SaveTemplate_UpdatesExistingName()
        {
            var collection = new LabelLayoutTemplateCollection();
            var designJson = LabelLayoutDesignHelper.Serialize(LabelLayoutDesignHelper.CreateDefault(76, 37), 76, 37);

            var first = LabelLayoutTemplateHelper.SaveTemplate(collection, "Standard tag", designJson, 76, 37, 3m);
            var updated = LabelLayoutTemplateHelper.SaveTemplate(collection, "standard tag", designJson, 80, 40, 4m);

            Assert.AreEqual(first.Id, updated.Id);
            Assert.AreEqual(1, collection.Templates.Count);
            Assert.AreEqual(80, updated.WidthMm);
            Assert.AreEqual(40, updated.HeightMm);
        }

        [Test]
        public void SaveTemplate_EnforcesMaxTemplates()
        {
            var collection = new LabelLayoutTemplateCollection();
            var designJson = LabelLayoutDesignHelper.Serialize(LabelLayoutDesignHelper.CreateDefault(76, 37), 76, 37);

            for (var i = 0; i < LabelLayoutTemplateHelper.MaxTemplates; i++)
            {
                LabelLayoutTemplateHelper.SaveTemplate(collection, "Template " + i, designJson, 76, 37, 3m);
            }

            Assert.Throws<InvalidOperationException>(() =>
                LabelLayoutTemplateHelper.SaveTemplate(collection, "Overflow", designJson, 76, 37, 3m));
        }

        [Test]
        public void DeleteTemplate_RemovesById()
        {
            var collection = new LabelLayoutTemplateCollection();
            var designJson = LabelLayoutDesignHelper.Serialize(LabelLayoutDesignHelper.CreateDefault(76, 37), 76, 37);
            var template = LabelLayoutTemplateHelper.SaveTemplate(collection, "Temp", designJson, 76, 37, 3m);

            var removed = LabelLayoutTemplateHelper.DeleteTemplate(collection, template.Id);

            Assert.IsTrue(removed);
            Assert.AreEqual(0, collection.Templates.Count);
        }

        [Test]
        public void Deserialize_EmptySetting_ReturnsEmptyCollection()
        {
            var settings = new Dictionary<string, AssetManagement.Domain.Entities.SystemSetting>();
            var templates = LabelLayoutTemplateHelper.ListTemplates(settings);

            Assert.AreEqual(0, templates.Count);
        }
    }
}
