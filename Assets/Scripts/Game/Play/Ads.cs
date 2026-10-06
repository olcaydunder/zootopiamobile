using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

/// <summary>
/// Optional rewarded ads (AdMob): the player chooses to watch one for Kredi; the game never shows an ad by itself.
/// At start-up the consent form (Google UMP: GDPR/EEA, UK and other places that need it) is shown when required;
/// ads are requested only once consent allows it. Ads are rated for teens. Phones only, never on the game server.
/// Until PlayConfig has the real AdMob ids these are Google's test ads.
/// </summary>
public class Ads : MonoBehaviour
{
    private static Ads instance;
    private RewardedAd rewarded;
    private bool started, loading;
    private float retryAt = -1f;
    private int failures;

    public static void Init()
    {
        if (instance != null || NetGame.IsServer || Application.platform != RuntimePlatform.Android)
            return;
        var go = new GameObject("Ads");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<Ads>();
        instance.AskConsent();
    }

    private void AskConsent()
    {
        MobileAds.RaiseAdEventsOnUnityMainThread = true;
        try
        {
            ConsentInformation.Update(new ConsentRequestParameters(), updateError =>
            {
                if (updateError != null)
                    Debug.LogWarning("[Ads] consent info: " + updateError.Message);
                ConsentForm.LoadAndShowConsentFormIfRequired(formError =>
                {
                    if (formError != null)
                        Debug.LogWarning("[Ads] consent form: " + formError.Message);
                    if (ConsentInformation.CanRequestAds())
                        StartAds();
                });
            });
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Ads] consent: " + e.Message);
        }
        // consent given in an earlier session: no need to wait for the update
        if (ConsentInformation.CanRequestAds())
            StartAds();
    }

    private void StartAds()
    {
        if (started)
            return;
        started = true;
        MobileAds.SetRequestConfiguration(new RequestConfiguration { MaxAdContentRating = MaxAdContentRating.T });
        MobileAds.Initialize(status => Load());
    }

    private void Load()
    {
        if (loading || rewarded != null)
            return;
        loading = true;
        RewardedAd.Load(PlayConfig.RewardedAdUnitId, new AdRequest(), (ad, error) =>
        {
            loading = false;
            if (error != null || ad == null)
            {
                failures++;
                retryAt = Time.realtimeSinceStartup + Mathf.Min(300f, 20f * failures);
                Debug.LogWarning("[Ads] rewarded not loaded: " + (error != null ? error.GetMessage() : "?"));
                return;
            }
            failures = 0;
            rewarded = ad;
            ad.OnAdFullScreenContentClosed += Reload;
            ad.OnAdFullScreenContentFailed += e => Reload();
        });
    }

    private void Reload()
    {
        if (rewarded != null)
            rewarded.Destroy();
        rewarded = null;
        AudioListener.pause = false;
        Load();
    }

    private void Update()
    {
        if (started && rewarded == null && !loading && retryAt > 0f && Time.realtimeSinceStartup >= retryAt)
        {
            retryAt = -1f;
            Load();
        }
    }

    // ----- for the store -----

    private static string DayKey { get { return "zm_ads_" + System.DateTime.Now.ToString("yyyyMMdd"); } }
    public static int WatchedToday { get { return PlayerPrefs.GetInt(DayKey, 0); } }
    public static int LeftToday { get { return Mathf.Max(0, PlayConfig.AdsPerDay - WatchedToday); } }

    /// <summary>A rewarded ad is loaded and the daily limit is not reached.</summary>
    public static bool RewardReady { get { return instance != null && instance.rewarded != null && instance.rewarded.CanShowAd() && LeftToday > 0; } }

    /// <summary>Shows the rewarded ad; <paramref name="rewarded"/> runs (main thread) when it was watched to the end.</summary>
    public static bool ShowRewarded(System.Action rewarded)
    {
        if (!RewardReady)
            return false;
        AudioListener.pause = true;
        instance.rewarded.Show(reward =>
        {
            PlayerPrefs.SetInt(DayKey, WatchedToday + 1);
            PlayerPrefs.Save();
            if (rewarded != null)
                rewarded();
        });
        return true;
    }

    /// <summary>Where the player can change their ad consent (shown in the settings when the region requires it).</summary>
    public static bool PrivacyOptionsRequired
    {
        get
        {
            try { return instance != null && ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required; }
            catch (System.Exception) { return false; }
        }
    }

    public static void ShowPrivacyOptions()
    {
        try
        {
            ConsentForm.ShowPrivacyOptionsForm(error =>
            {
                if (error != null)
                    Debug.LogWarning("[Ads] privacy options: " + error.Message);
            });
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Ads] privacy options: " + e.Message);
        }
    }
}
