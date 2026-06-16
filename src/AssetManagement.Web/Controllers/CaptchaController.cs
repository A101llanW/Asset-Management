using System;
using System.Web.Mvc;
using AssetManagement.Web.Helpers;
using AssetManagement.Web.Services;

namespace AssetManagement.Web.Controllers
{
    public class CaptchaController : Controller
    {
        private readonly RealisticCaptchaService _captchaService = new RealisticCaptchaService();

        [HttpGet]
        public JsonResult Generate()
        {
            return Json(CreateCaptchaPayload(), JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult Refresh()
        {
            CaptchaSessionHelper.Clear(Session);
            return Json(CreateCaptchaPayload(), JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult Validate(string captchaId, string userInput)
        {
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
