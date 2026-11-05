# GA4 anonymous usage analytics (NanoAssetSuite)

Commits: 78cade7 (feature) + Web.Release.config / docs follow-up on `feature/ga4-anon-analytics`. LOCAL ONLY until Allan says otherwise.

## Config keys (appSettings)

| Key | Default when missing/empty | Meaning |
|---|---|---|
| `Ga4MeasurementId` | empty = **GA OFF** (no script, no banner, nothing rendered) | GA4 measurement ID. Production value: `G-B7FDYNVT7G` (NanoAssetSuite property; public, not a secret). Must match `G-[A-Z0-9]{4,20}` or it is treated as empty. |
| `Ga4AllowNonProductionHost` | `false` | Local verification only. `true` lets GA render on hosts other than nanosoft.africa / www.nanosoft.africa. Never set it on live. |

Even with an ID set, GA renders only when the request host is `nanosoft.africa` or `www.nanosoft.africa` (server check in `Ga4AnalyticsRules.ShouldRender`, repeated client-side in `ga4-consent.js`). localhost, LAN IPs, alLAN, Test-WF and NIS QA stay off.

## Where the production ID lives

1. `src/AssetManagement.Web/Web.Release.config`: `<add key="Ga4MeasurementId" value="G-B7FDYNVT7G" xdt:Transform="InsertIfMissing" xdt:Locator="Match(key)" />`. This only reaches a server through a Release-transformed Web.config. Our live kits and IIS publishes strip the root Web.config, so this is the recorded source of truth, not the delivery mechanism.
2. The repo `Web.config` (dev/local) has **no** `Ga4MeasurementId` key, so local and dev stay off. The code default when the key is missing is off.
3. **Live deploy step (one-time, manual, by whoever deploys live, after Allan's go):** back up the live `Web.config`, then add this one line inside the existing `<appSettings>` and save (the app pool recycles):

   ```xml
   <add key="Ga4MeasurementId" value="G-B7FDYNVT7G" />
   ```

   Do not add `Ga4AllowNonProductionHost` on live. Rollback: remove the line (or set `value=""`).

## Behaviour

- Consent Mode v2: `gtag('consent','default',{ad_storage, ad_user_data, ad_personalization, analytics_storage: 'denied'})` runs inline before anything else.
- A small dismissible prompt (bottom-left, per browser, localStorage key `amGa4Consent`) asks for consent. Nothing is sent and gtag.js is not loaded until **Accept**, which runs `gtag('consent','update',{analytics_storage:'granted'})` only; the ad keys stay denied. Decline or close stores `denied` and nothing loads. To ask again, clear `amGa4Consent` in the browser's localStorage.
- `gtag('config', id, { send_page_view:false, allow_google_signals:false, allow_ad_personalization_signals:false, page_location, page_path, page_title, page_referrer })`, then a manual `page_view` with the same sanitized values. There is no user_id and there are no user properties.
- Paths are rebuilt from route names only: `/NanoAssetSuite/{org}/Assets/Details/{id}` (`{id}` numeric, `{guid}` GUID, `{tag}` anything else, `{org}` tenant slug). No query string or hash is sent. The title is generic ("Assets Details"). A same-host referrer is sanitized the same way; other referrers are reduced to their origin. The hidden DatabaseConsole is never tracked.
- Name-only feature events via `data-ga-event`: `asset_create` (Assets/Create form), `requisition_create` (PurchaseRequests/Create form), `report_export_csv` and `report_export_pdf` (Reports). There are no print or label events.

## GA4 property settings (Allan, in the GA4 admin UI)

- Data stream > Enhanced measurement: turn **off** "Page changes based on browser history events", "Form interactions", "Site search", "Outbound clicks", "File downloads" and "Video engagement". These auto-events can carry raw URLs and form ids; the app does not need them.
- Google signals off. Ads links: none. Data retention: as you prefer.
