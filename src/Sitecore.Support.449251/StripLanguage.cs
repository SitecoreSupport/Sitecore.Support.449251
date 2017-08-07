namespace Sitecore.Support.Pipelines.PreprocessRequest
{
    using Collections;
    using Data.Managers;
    using Sitecore;
    using Sitecore.Configuration;
    using Sitecore.Diagnostics;
    using Sitecore.Globalization;
    using Sitecore.Pipelines.PreprocessRequest;
    using Sitecore.Text;
    using Sitecore.Web;
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Web;

    public class StripLanguage : PreprocessRequestProcessor
    {
        private static CultureInfo[] customCultures = CultureInfo.GetCultures(CultureTypes.UserCustomCulture);
        private static Language ExtractLanguage(HttpRequest request)
        {
            Assert.ArgumentNotNull(request, "request");
            string str = WebUtil.ExtractLanguageName(request.FilePath);
            if (!string.IsNullOrEmpty(str))
            {
                Language language;
                if (!IsValidCultureInfo(str))
                {
                    return null;
                }
                if (!Language.TryParse(str, out language))
                {
                    return null;
                }

                if ((language.CultureInfo.LCID == 0x1000) || language.CultureInfo.CultureTypes.HasFlag(CultureTypes.UserCustomCulture))
                {
                    return customCultures.Contains(language.CultureInfo) ? language : null;
                }
                return language;                
            }
            return null;
        }

        private static bool IsValidCultureInfo(string languageName)
        {
            string[] strArray = StringUtil.Divide(languageName, '-', true);
            if (strArray.Length < 1)
            {
                return false;
            }
            int langLen = strArray[0].Trim().Length;

            return (langLen == 1 || langLen == 2 || langLen == 3);
        }

        public override void Process(PreprocessRequestArgs args)
        {
            Assert.ArgumentNotNull(args, "args");
            if (Settings.Languages.AlwaysStripLanguage)
            {
                Language embeddedLanguage = ExtractLanguage(args.Context.Request);
                if (embeddedLanguage != null)
                {
                    Context.Language = embeddedLanguage;
                    Context.Data.FilePathLanguage = embeddedLanguage;
                    RewriteUrl(args.Context, embeddedLanguage);
                    Tracer.Info(string.Format("Language changed to \"{0}\" as request url contains language embedded in the file path.", embeddedLanguage.Name));
                }
            }
        }

        private static void RewriteUrl(HttpContext context, Language embeddedLanguage)
        {
            Assert.ArgumentNotNull(context, "context");
            Assert.ArgumentNotNull(embeddedLanguage, "embeddedLanguage");
            HttpRequest request = context.Request;
            string str = request.FilePath.Substring(embeddedLanguage.Name.Length + 1);
            if (!string.IsNullOrEmpty(str) && str.StartsWith(".", StringComparison.InvariantCulture))
            {
                str = string.Empty;
            }
            if (string.IsNullOrEmpty(str))
            {
                str = "/";
            }
            if (!UseRedirect(str))
            {
                context.RewritePath(str, request.PathInfo, StringUtil.RemovePrefix('?', request.Url.Query));
            }
            else
            {
                UrlString str2 = new UrlString(str + request.Url.Query);
                str2["sc_lang"] = embeddedLanguage.Name;
                context.Response.Redirect(str2.ToString(), true);
            }
        }

        private static bool UseRedirect(string filePath)
        {
            Assert.IsNotNullOrEmpty(filePath, "filePath");
            return Settings.RedirectUrlPrefixes.Any<string>(path => filePath.StartsWith(path, StringComparison.InvariantCulture));
        }
    }
}
