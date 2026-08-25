using System;
using System.Web.Mvc;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Web.Helpers;
using AssetManagement.Web.Security;
using AssetManagement.Web.Services;

namespace AssetManagement.Web.Controllers
{
    public class CaptchaController : Controller
    {
        private readonly RealisticCaptchaService _captchaService = new RealisticCaptchaService();
        private readonly IDistributedRateLimiter _rateLimiter;

        public CaptchaController()
        {
            _rateLimiter = DependencyResolver.Current.GetService<IDistributedRateLimiter>();
        }

        [HttpGet]
        public JsonResult Generate()
        {
            if (!CaptchaRateLimiter.TryAcquire(HttpContext, "generate", _rateLimiter))
            {
                return RateLimitedJson();
            }

            return Json(CreateCaptchaPayload(), JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult Refresh()
        {
            if (!CaptchaRateLimiter.TryAcquire(HttpContext, "refresh", _rateLimiter))
            {
                return RateLimitedJson();
            }

            CaptchaSessionHelper.Clear(Session);
            return Json(CreateCaptchaPayload(), JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult Validate(string captchaId, string userInput)
        {
            if (!CaptchaRateLimiter.TryAcquire(HttpContext, "validate", _rateLimiter))
            {
                return RateLimitedJson();
            }

            var sessionId = Session[CaptchaSessionHelper.CaptchaIdKey] as string;
            if (!string.Equals(sessionId, captchaId, StringComparison.Ordinal))
            {
                return Json(new { success = false, message = "Invalid CAPTCHA ID" });
            }

            var error = CaptchaSessionHelper.ValidateSubmittedCode(Session, userInput, clearOnSuccess: false);
            if (error != null)
            {
                return Json(new { success = false, message = error });
            }

            return Json(new { success = true });
        }

        private JsonResult RateLimitedJson()
        {
            Response.StatusCode = 429;
            Response.TrySkipIisCustomErrors = true;
            return Json(new { success = false, message = "Too many CAPTCHA requests. Please wait and try again." }, JsonRequestBehavior.AllowGet);
        }

        private object CreateCaptchaPayload()
        {
            try
            {
                var captcha = _captchaService.GenerateCaptcha();
                CaptchaSessionHelper.Store(Session, captcha.CaptchaText, captcha.ExpiresAt, captcha.CaptchaId);
                return new
                {
                    success = true,
                    captchaId = captcha.CaptchaId,
                    captchaImage = captcha.CaptchaBase64,
                    expiresAt = captcha.ExpiresAt
                };
            }
            catch (Exception ex)
            {
                return new { success = false, message = ex.Message };
            }
        }
    }
}
