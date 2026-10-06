/// <summary>
/// Everything Google Play needs to know about this game in one place: the AdMob ids (Google's TEST ids until the real
/// ones are put here; the app id also goes into the build through ZootopiaBuild.ConfigureAds), the Kredi packs sold
/// through Google Play Billing (the same product ids must be created in Play Console as "in-app products"), and the
/// addresses of the privacy policy and account-deletion pages.
/// </summary>
public static class PlayConfig
{
    // ----- AdMob (AdMob > Apps > the app > App settings / Ad units) -----
    public const string AdMobAndroidAppId = "ca-app-pub-3940256099942544~3347511713";   // TEST
    public const string RewardedAdUnitId = "ca-app-pub-3940256099942544/5224354917";    // TEST rewarded

    /// <summary>Still Google's sample ids: ads are test ads (no income).</summary>
    public static bool TestAds { get { return RewardedAdUnitId.StartsWith("ca-app-pub-3940256099942544"); } }

    /// <summary>Kredi given for watching one rewarded ad, and how many a day.</summary>
    public const int AdReward = 100;
    public const int AdsPerDay = 5;

    // ----- Google Play in-app products (consumable) -----
    public class CreditPack
    {
        public string id;      // product id in Play Console
        public int credits;    // Kredi given (bonus included)
        public int bonus;      // the part shown as "+bonus"
        public string tag;     // small label on the card ("" none)
    }

    public static readonly CreditPack[] CreditPacks =
    {
        new CreditPack { id = "kredi_1000", credits = 1000, bonus = 0, tag = "" },
        new CreditPack { id = "kredi_2750", credits = 2750, bonus = 250, tag = "" },
        new CreditPack { id = "kredi_6000", credits = 6000, bonus = 1000, tag = "POPÜLER" },
        new CreditPack { id = "kredi_13000", credits = 13000, bonus = 3000, tag = "" },
        new CreditPack { id = "kredi_30000", credits = 30000, bonus = 10000, tag = "EN İYİ DEĞER" },
    };

    public static CreditPack Pack(string id)
    {
        foreach (var p in CreditPacks)
            if (p.id == id)
                return p;
        return null;
    }

    // ----- Web pages -----
    // The game server serves the privacy policy and the account-deletion page (Server/web/). When they are also put
    // on the developer website, write those addresses here (they then open instead).
    public const string WebsitePrivacyUrl = "";
    public const string WebsiteDeletionUrl = "";
    public static string PrivacyPolicyUrl { get { return WebsitePrivacyUrl.Length > 0 ? WebsitePrivacyUrl : OnlineService.WebPage("/gizlilik"); } }
    public static string AccountDeletionUrl { get { return WebsiteDeletionUrl.Length > 0 ? WebsiteDeletionUrl : OnlineService.WebPage("/hesap-silme"); } }
    public const string SupportEmail = "zootopiayazilim@gmail.com";
}
