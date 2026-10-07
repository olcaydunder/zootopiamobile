/// <summary>
/// Everything Google Play needs to know about this game in one place: the AdMob ids (Google's TEST ids until the real
/// ones are put here; the app id also goes into the build through ZootopiaBuild.ConfigureAds), the Kredi packs sold
/// through Google Play Billing (the same product ids must be created in Play Console as "in-app products"), the
/// public licence key purchases are checked with, and the addresses of the privacy policy and account-deletion pages.
/// </summary>
public static class PlayConfig
{
    // ----- AdMob (AdMob > Apps > the app > App settings / Ad units) -----
    public const string AdMobAndroidAppId = "ca-app-pub-6275447087051506~7092903756";   // Rise of Davraz
    public const string RewardedAdUnitId = "ca-app-pub-3940256099942544/5224354917";    // TEST rewarded (Google's sample unit works with any app id)

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

    /// <summary>
    /// The game's public licence key (Play Console > Monetisation setup > Licensing; base64 RSA public key). Google Play
    /// signs every purchase with the matching private key, which only Google has; ReceiptCheck uses this to refuse
    /// made-up purchases. It is a PUBLIC key: not a secret, safe to keep in the code.
    /// </summary>
    public const string GooglePlayPublicKey =
        "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAtPIFKmzs4dUK3j9avq6lp6no5coJKMQi7T3/HiMEtPQ8UxHFlohE8cAUaZabiochTDmfz8" +
        "zV6i7BsmzB8+DqJve5e2EikEuTytMfYF26n5AIzJRX4TvJi1k4fSrRZawcbkc40X7IS8wr12FoCpOBYI3tyni9XKSv4vCj/YaEQ/6HxiIjrsuRwe7W" +
        "8a35dRLVreP4AtZebA7bi2H1KK/6PXMshZOFqpTgXb/BTZRhhjZchrDavyR1/6jn5EiMT07055GGM4HUYx9QHkIEr437Nq4eO8JIg7U4aUm2QyERTg" +
        "K+jG1F5tvRIlRriOQUua3WWPDP6Y3fbSkLjIq62Tf2owIDAQAB";

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
