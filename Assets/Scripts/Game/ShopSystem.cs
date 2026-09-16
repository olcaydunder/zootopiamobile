using UnityEngine;

public class ShopSystem : MonoBehaviour
{
    public string[] cosmeticSkins = { "Default", "Crimson", "Violet", "Silver" };

    public void PurchaseSkin(ProfileData profile, string skinName, int cost)
    {
        if (profile == null)
            return;

        profile.UnlockSkin(skinName, cost);
    }
}
