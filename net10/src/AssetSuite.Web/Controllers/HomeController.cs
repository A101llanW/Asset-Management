using AssetManagement.Application.Security;
using AssetSuite.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetSuite.Web.Controllers;

[Route("[controller]/[action]")]
[Route("{tenant}/[controller]/[action]")]
public class HomeController : Controller
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Privacy(string? tenant)
    {
        ViewBag.HideNavbar = false;
        ViewBag.LegalDocTenant = tenant ?? RouteData.Values["tenant"] as string;
        ViewBag.LegalDocPage = "privacy";
        ViewBag.CompanyName = LegalPolicyDefaults.CompanyName;
        ViewBag.ContactEmail = LegalPolicyDefaults.ContactEmail;
        ViewBag.ContactAddress = LegalPolicyDefaults.ContactAddress;
        ViewBag.LastUpdated = LegalPolicyDefaults.LastUpdated;
        ViewBag.PrivacyVersionLabel = LegalPolicyDefaults.GetPrivacyVersion(LegalRelationshipKind.TenantUser);
        ViewData["Title"] = "Privacy";
        return View();
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Terms(string? tenant)
    {
        ViewBag.HideNavbar = false;
        ViewBag.LegalDocTenant = tenant ?? RouteData.Values["tenant"] as string;
        ViewBag.LegalDocPage = "terms";
        ViewBag.CompanyName = LegalPolicyDefaults.CompanyName;
        ViewBag.ContactEmail = LegalPolicyDefaults.ContactEmail;
        ViewBag.LastUpdated = LegalPolicyDefaults.LastUpdated;
        ViewBag.TermsVersionLabel = LegalPolicyDefaults.GetTermsVersion(LegalRelationshipKind.TenantUser);
        ViewData["Title"] = "Terms";
        return View("Terms");
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Error()
    {
        return View();
    }
}
