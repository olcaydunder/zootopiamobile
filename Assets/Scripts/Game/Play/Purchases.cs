#if UNITY_ANDROID
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;

/// <summary>
/// Kredi packs bought with real money through Google Play Billing (Unity IAP 5). Connects once at start-up (phones
/// only, never on the game server), fetches the packs' prices from Play, and gives the Kredi when Play reports a
/// purchase, then confirms (consumes) it so the pack can be bought again. Purchases that were paid but not yet
/// given (the game closed in between) are given on the next start. Each order is given only once (its transaction
/// id is remembered), and only when Google Play's signature on it is right (ReceiptCheck).
/// </summary>
public class Purchases : MonoBehaviour
{
    private static Purchases instance;
    private StoreController store;
    private bool connected, productsReady, busy;

    /// <summary>Raised on the main thread when prices arrive or a purchase finishes (the store redraws).</summary>
    public static event System.Action Changed;
    /// <summary>Last thing to tell the player (purchase done / failed), "" none.</summary>
    public static string Message = "";

    public static bool Available { get { return instance != null && instance.productsReady; } }
    public static bool Busy { get { return instance != null && instance.busy; } }

    public static void Init()
    {
        if (instance != null || NetGame.IsServer || Application.platform != RuntimePlatform.Android)
            return;
        var go = new GameObject("Purchases");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<Purchases>();
        instance.Connect();
    }

    private async void Connect()
    {
        try
        {
            try
            {
                await Unity.Services.Core.UnityServices.InitializeAsync();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Purchases] Unity Services: " + e.Message);
            }
            store = UnityIAPServices.StoreController();
            store.OnProductsFetched += OnProductsFetched;
            store.OnProductsFetchFailed += f => Debug.LogWarning("[Purchases] products not fetched: " + f);
            store.OnPurchasePending += OnPurchasePending;
            store.OnPurchaseFailed += OnPurchaseFailed;
            store.OnPurchasesFetched += OnPurchasesFetched;
            store.OnStoreDisconnected += d =>
            {
                connected = false;
                Debug.LogWarning("[Purchases] store disconnected: " + d);
            };
            await store.Connect();
            connected = true;
            var defs = new List<ProductDefinition>();
            foreach (var p in PlayConfig.CreditPacks)
                defs.Add(new ProductDefinition(p.id, ProductType.Consumable));
            store.FetchProducts(defs);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Purchases] could not connect to Google Play: " + e.Message);
        }
    }

    private void OnProductsFetched(List<Product> products)
    {
        productsReady = true;
        Debug.Log("[Purchases] " + products.Count + " products");
        store.FetchPurchases();   // paid earlier but not given yet
        Notify();
    }

    private void OnPurchasesFetched(Orders orders)
    {
        foreach (var order in orders.PendingOrders)
            OnPurchasePending(order);
    }

    /// <summary>Localised price of a pack from Google Play ("" until it is known).</summary>
    public static string Price(string productId)
    {
        if (!Available)
            return "";
        var p = instance.store.GetProductById(productId);
        return p != null && p.availableToPurchase && p.metadata != null ? p.metadata.localizedPriceString : "";
    }

    /// <summary>Opens Google Play's purchase sheet for a pack.</summary>
    public static bool Buy(string productId)
    {
        if (!Available || instance.busy)
            return false;
        var p = instance.store.GetProductById(productId);
        if (p == null || !p.availableToPurchase)
            return false;
        instance.busy = true;
        Message = "";
        instance.store.PurchaseProduct(p);
        return true;
    }

    private void OnPurchasePending(PendingOrder order)
    {
        busy = false;
        var info = order.Info;
        string tx = info != null ? info.TransactionID : "";
        string receipt = info != null ? info.Receipt : "";
        int given = 0;
        bool rejected = false;
        if (!AlreadyGiven(tx) && info != null && info.PurchasedProductInfo != null)
        {
            foreach (var item in info.PurchasedProductInfo)
            {
                var pack = PlayConfig.Pack(item.productId);
                if (pack == null)
                    continue;
                // Signed by Google Play for this game and this pack? (made-up purchases are not)
                string why;
                var check = ReceiptCheck.Check(receipt, PlayConfig.GooglePlayPublicKey, Application.identifier, item.productId, out why);
                if (check == ReceiptCheck.Result.Invalid)
                {
                    rejected = true;
                    Debug.LogWarning("[Purchases] " + item.productId + " not given, receipt rejected: " + why);
                    continue;
                }
                if (check == ReceiptCheck.Result.Unknown)
                    Debug.LogWarning("[Purchases] receipt not checked: " + why);
                given += pack.credits;
            }
            if (given > 0)
            {
                AddCredits(given);
                Remember(tx);
                Message = given.ToString("N0") + " Kredi hesabına eklendi. Teşekkürler!";
                UiSound.Confirm();
            }
        }
        if (rejected && given == 0)
        {
            // Not confirmed: if money was really taken, Google Play refunds an unconfirmed purchase within 3 days.
            Message = "Satın alma Google Play'den doğrulanamadı, Kredi verilmedi. Ücret alındıysa Google Play 3 gün içinde iade eder.";
            Notify();
            return;
        }
        store.ConfirmPurchase(order);   // consumed: the pack can be bought again
        Notify();
    }

    private void OnPurchaseFailed(FailedOrder order)
    {
        busy = false;
        string reason = order != null ? order.FailureReason.ToString() : "";
        Message = reason.Contains("UserCancelled") ? "Satın alma iptal edildi." : "Satın alma tamamlanamadı. Ücret alınmadı.";
        Debug.LogWarning("[Purchases] failed: " + reason);
        Notify();
    }

    private static void AddCredits(int amount)
    {
        var gm = GameManager.Instance;
        if (gm != null && gm.profile != null)
        {
            gm.profile.coins += amount;
            gm.profile.Save();
        }
        else
        {
            PlayerPrefs.SetInt("zm_coins", PlayerPrefs.GetInt("zm_coins", 0) + amount);
        }
        PlayerPrefs.Save();
    }

    // The last orders given (transaction ids), so a repeated report never gives twice.
    private static bool AlreadyGiven(string tx)
    {
        if (string.IsNullOrEmpty(tx))
            return false;
        return ("|" + PlayerPrefs.GetString("zm_iap_given", "") + "|").Contains("|" + tx + "|");
    }

    private static void Remember(string tx)
    {
        if (string.IsNullOrEmpty(tx))
            return;
        var list = new List<string>(PlayerPrefs.GetString("zm_iap_given", "").Split(new[] { '|' }, System.StringSplitOptions.RemoveEmptyEntries));
        list.Add(tx);
        while (list.Count > 60)
            list.RemoveAt(0);
        PlayerPrefs.SetString("zm_iap_given", string.Join("|", list.ToArray()));
        PlayerPrefs.Save();
    }

    private static void Notify()
    {
        if (Changed != null)
            Changed();
    }
}
#else
/// <summary>Other platforms (the Linux game server, desktop tests): no Google Play purchases.</summary>
public class Purchases : UnityEngine.MonoBehaviour
{
    public static event System.Action Changed;
    public static string Message = "";
    public static bool Available { get { return false; } }
    public static bool Busy { get { return false; } }
    public static void Init() { if (Changed != null) Changed = null; }
    public static string Price(string productId) { return ""; }
    public static bool Buy(string productId) { return false; }
}
#endif
