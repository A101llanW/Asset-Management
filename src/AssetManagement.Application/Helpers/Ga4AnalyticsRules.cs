using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// GA4 (anonymous usage analytics) rules, kept free of System.Web so they can be unit tested.
    /// Off by default: nothing is rendered unless the appSetting <see cref="MeasurementIdKey"/> holds a valid
    /// GA4 measurement ID AND the request host is the production host (or the local-only test flag
    /// <see cref="AllowNonProductionHostKey"/> is "true").
    /// Privacy: page paths are rebuilt from route names only (record ids, GUIDs, tags and the tenant slug become
    /// placeholders); query strings and fragments are never sent; titles are generic controller/action names.
    /// </summary>
    public static class Ga4AnalyticsRules
    {
        /// <summary>appSettings key for the GA4 measurement ID (e.g. G-XXXXXXXXXX). Missing or empty = analytics off.</summary>
        public const string MeasurementIdKey = "Ga4MeasurementId";

        /// <summary>appSettings key, local verification only: "true" lets GA render on non-production hosts. Default false.</summary>
        public const string AllowNonProductionHostKey = "Ga4AllowNonProductionHost";

        public const string IdPlaceholder = "{id}";
        public const string GuidPlaceholder = "{guid}";
        public const string TagPlaceholder = "{tag}";
        public const string OrgPlaceholder = "{org}";
        public const string ValuePlaceholder = "{value}";

        private static readonly string[] ProductionHostNames = { "nanosoft.africa", "www.nanosoft.africa" };

        private static readonly Regex MeasurementIdPattern = new Regex(@"^G-[A-Z0-9]{4,20}$", RegexOptions.CultureInvariant);
        private static readonly Regex NumericPattern = new Regex(@"^\d+$", RegexOptions.CultureInvariant);
        private static readonly Regex GuidPattern = new Regex(
            @"^\{?[0-9a-fA-F]{8}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{4}-?[0-9a-fA-F]{12}\}?$",
            RegexOptions.CultureInvariant);
        private static readonly Regex RouteNamePattern = new Regex(@"^[A-Za-z][A-Za-z_]{0,59}$", RegexOptions.CultureInvariant);
        private static readonly Regex EventNamePattern = new Regex(@"^[a-z][a-z0-9_]{0,39}$", RegexOptions.CultureInvariant);

        public static IList<string> ProductionHosts
        {
            get { return Array.AsReadOnly(ProductionHostNames); }
        }

        /// <summary>Returns the trimmed, upper-cased ID when it looks like a GA4 measurement ID; otherwise null (= off).</summary>
        public static string NormalizeMeasurementId(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var id = raw.Trim().ToUpperInvariant();
            return MeasurementIdPattern.IsMatch(id) ? id : null;
        }

        /// <summary>Only "true" or "1" enable the flag; anything else (missing, empty, "false", junk) is false.</summary>
        public static bool ParseFlag(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            var v = raw.Trim();
            return string.Equals(v, "true", StringComparison.OrdinalIgnoreCase) || v == "1";
        }

        public static bool IsProductionHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                return false;
            }

            var h = host.Trim().TrimEnd('.');
            var colon = h.IndexOf(':');
            if (colon >= 0)
            {
                h = h.Substring(0, colon);
            }

            return ProductionHostNames.Any(p => string.Equals(p, h, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>GA renders only with a valid ID and either the production host or the local test flag.</summary>
        public static bool ShouldRender(string rawMeasurementId, string host, bool allowNonProductionHost)
        {
            if (NormalizeMeasurementId(rawMeasurementId) == null)
            {
                return false;
            }

            return allowNonProductionHost || IsProductionHost(host);
        }

        public static bool IsValidEventName(string name)
        {
            return !string.IsNullOrEmpty(name) && EventNamePattern.IsMatch(name);
        }

        /// <summary>Placeholder for a single variable segment: {id} numeric, {guid} GUID, {tag} anything else.</summary>
        public static string ClassifyValueSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var v = value.Trim();
            if (NumericPattern.IsMatch(v))
            {
                return IdPlaceholder;
            }

            if (GuidPattern.IsMatch(v))
            {
                return GuidPlaceholder;
            }

            return TagPlaceholder;
        }

        /// <summary>
        /// Builds the page path from route names only, e.g. "/NanoAssetSuite/{org}/Assets/Details/{id}".
        /// Controller, action and area must be plain identifiers; otherwise they are replaced by {value}.
        /// </summary>
        public static string BuildPagePath(string applicationPath, bool hasTenant, string area, string controller, string action, string idValue)
        {
            var sb = new StringBuilder(NormalizeApplicationPath(applicationPath));
            if (hasTenant)
            {
                sb.Append('/').Append(OrgPlaceholder);
            }

            if (!string.IsNullOrWhiteSpace(area))
            {
                sb.Append('/').Append(SafeRouteName(area));
            }

            sb.Append('/').Append(SafeRouteName(string.IsNullOrWhiteSpace(controller) ? "Home" : controller));
            sb.Append('/').Append(SafeRouteName(string.IsNullOrWhiteSpace(action) ? "Index" : action));

            var idPart = ClassifyValueSegment(idValue);
            if (idPart != null)
            {
                sb.Append('/').Append(idPart);
            }

            return sb.ToString();
        }

        /// <summary>Generic title such as "Assets Details" or "Platform Organizations Index"; never a record name.</summary>
        public static string BuildPageTitle(string area, string controller, string action)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(area))
            {
                parts.Add(SafeRouteName(area));
            }

            parts.Add(SafeRouteName(string.IsNullOrWhiteSpace(controller) ? "Home" : controller));
            parts.Add(SafeRouteName(string.IsNullOrWhiteSpace(action) ? "Index" : action));
            return string.Join(" ", parts.ToArray());
        }

        /// <summary>
        /// Rule-based sanitizer for a path or URL we have no route data for (the referrer). Drops scheme, host,
        /// query and fragment; strips the application path; the first segment becomes {org} unless it is a known
        /// controller or area name; after that at most two plain route names are kept (three after an area) and every
        /// other segment becomes {id}, {guid} or {tag}. Returns the application path plus the sanitized segments.
        /// </summary>
        public static string SanitizePath(string pathOrUrl, string applicationPath, ICollection<string> knownControllers, ICollection<string> knownAreas)
        {
            var appPath = NormalizeApplicationPath(applicationPath);
            var path = StripToPath(pathOrUrl);
            if (appPath.Length > 0
                && (path.Equals(appPath, StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith(appPath + "/", StringComparison.OrdinalIgnoreCase)))
            {
                path = path.Substring(appPath.Length);
            }

            var segments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            var output = new List<string>();
            var i = 0;
            if (segments.Length > 0)
            {
                var first = DecodeSegment(segments[0]);
                if (!IsKnown(first, knownControllers) && !IsKnown(first, knownAreas))
                {
                    output.Add(OrgPlaceholder);
                    i = 1;
                }
            }

            var namesAllowed = 2;
            if (i < segments.Length && IsKnown(DecodeSegment(segments[i]), knownAreas))
            {
                namesAllowed = 3;
            }

            for (; i < segments.Length; i++)
            {
                var seg = DecodeSegment(segments[i]);
                if (namesAllowed > 0 && RouteNamePattern.IsMatch(seg))
                {
                    output.Add(seg);
                    namesAllowed--;
                    continue;
                }

                namesAllowed = 0;
                output.Add(ClassifyValueSegment(seg) ?? TagPlaceholder);
            }

            return appPath + "/" + string.Join("/", output.ToArray());
        }

        /// <summary>
        /// Referrer to send: same-host referrers are sanitized like page paths; other hosts are reduced to their origin;
        /// unparsable or empty referrers return an empty string.
        /// </summary>
        public static string SanitizeReferrer(Uri referrer, string currentHost, string applicationPath, ICollection<string> knownControllers, ICollection<string> knownAreas)
        {
            if (referrer == null || !referrer.IsAbsoluteUri
                || (referrer.Scheme != Uri.UriSchemeHttp && referrer.Scheme != Uri.UriSchemeHttps))
            {
                return string.Empty;
            }

            var origin = referrer.GetLeftPart(UriPartial.Authority);
            if (!string.Equals(referrer.Host, currentHost ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                return origin + "/";
            }

            return origin + SanitizePath(referrer.AbsolutePath, applicationPath, knownControllers, knownAreas);
        }

        public static string NormalizeApplicationPath(string applicationPath)
        {
            if (string.IsNullOrWhiteSpace(applicationPath))
            {
                return string.Empty;
            }

            var p = applicationPath.Trim().TrimEnd('/');
            if (p.Length == 0)
            {
                return string.Empty;
            }

            if (!p.StartsWith("/", StringComparison.Ordinal))
            {
                p = "/" + p;
            }

            // Only keep a plain virtual directory name (e.g. /NanoAssetSuite); anything else is dropped.
            return Regex.IsMatch(p, @"^(/[A-Za-z0-9_.-]{1,60})+$") ? p : string.Empty;
        }

        private static string StripToPath(string pathOrUrl)
        {
            if (string.IsNullOrWhiteSpace(pathOrUrl))
            {
                return string.Empty;
            }

            var s = pathOrUrl.Trim();
            var cut = s.IndexOfAny(new[] { '?', '#' });
            if (cut >= 0)
            {
                s = s.Substring(0, cut);
            }

            Uri abs;
            if (Uri.TryCreate(s, UriKind.Absolute, out abs)
                && (abs.Scheme == Uri.UriSchemeHttp || abs.Scheme == Uri.UriSchemeHttps))
            {
                s = abs.AbsolutePath;
            }

            return s;
        }

        private static string DecodeSegment(string segment)
        {
            try
            {
                return Uri.UnescapeDataString(segment);
            }
            catch (UriFormatException)
            {
                return segment;
            }
        }

        private static bool IsKnown(string segment, ICollection<string> names)
        {
            return names != null && names.Any(n => string.Equals(n, segment, StringComparison.OrdinalIgnoreCase));
        }

        private static string SafeRouteName(string name)
        {
            var n = (name ?? string.Empty).Trim();
            return RouteNamePattern.IsMatch(n) ? n : ValuePlaceholder;
        }
    }
}
