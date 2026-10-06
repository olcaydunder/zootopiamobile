using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ŞANS ÇARKI (lobby): eight slices of rewards; one free spin a day, then up to five more for Kredi. The
/// wheel spins for a few seconds and slows down on the prize, which is given (boxes open right away).
/// </summary>
public class WheelScreen : MonoBehaviour
{
    private System.Action onClose;
    private RectTransform wheel;
    private Text spinLabel, infoText, coinsText;
    private Button spinButton;
    private bool spinning;

    public static WheelScreen Create(Transform canvas)
    {
        var rect = UIUtil.CreateStretch(canvas, "Wheel");
        rect.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.05f, 0.09f, 0.97f);
        var s = rect.gameObject.AddComponent<WheelScreen>();
        s.Build();
        rect.gameObject.AddComponent<PopIn>();
        rect.gameObject.SetActive(false);
        return s;
    }

    private void Build()
    {
        var t = transform;
        Text label;
        var back = UIUtil.CreateButton(t, "<", new Vector2(0f, 1f), new Vector2(80f, -70f), new Vector2(84f, 84f), Theme.Accent, false, 54, out label);
        label.color = new Color(0.1f, 0.1f, 0.1f);
        label.GetComponent<Shadow>().enabled = false;
        back.onClick.AddListener(Close);
        Icons.Create(t, "wheel", new Vector2(0f, 1f), new Vector2(190f, -70f), new Vector2(84f, 84f));
        var title = UIUtil.CreateText(t, "ŞANS ÇARKI", new Vector2(0f, 1f), new Vector2(460f, -70f), new Vector2(500f, 80f), 52, TextAnchor.MiddleLeft);
        title.fontStyle = FontStyle.Bold;
        var coins = Theme.Box(t, "Coins", new Vector2(1f, 1f), new Vector2(-230f, -70f), new Vector2(380f, 84f), Theme.Panel, false);
        Icons.Create(coins.transform, "currency", new Vector2(0f, 0.5f), new Vector2(50f, 0f), new Vector2(64f, 64f));
        coinsText = UIUtil.CreateText(coins.transform, "", new Vector2(0f, 0.5f), new Vector2(220f, 0f), new Vector2(260f, 60f), 38, TextAnchor.MiddleLeft);
        coinsText.fontStyle = FontStyle.Bold;
        coinsText.color = new Color(1f, 0.85f, 0.3f);

        var c = new Vector2(0.5f, 0.5f);
        const float size = 760f;
        var rim = UIUtil.CreateImage(t, "Rim", c, new Vector2(-260f, -40f), new Vector2(size + 40f, size + 40f), new Color(0.12f, 0.13f, 0.16f, 1f), true);
        rim.raycastTarget = false;
        wheel = UIUtil.CreateRect(t, "WheelDisc", c, new Vector2(-260f, -40f), new Vector2(size, size));
        int n = Deals.Wheel.Length;
        for (int i = 0; i < n; i++)
        {
            var sl = Deals.Wheel[i];
            // slice i covers the angles [i, i+1] * 360/n measured clockwise from the top
            var img = UIUtil.CreateImage(wheel, "Slice" + i, c, Vector2.zero, new Vector2(size, size), sl.color, true);
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Radial360;
            img.fillOrigin = 2;   // top
            img.fillClockwise = true;
            img.fillAmount = 1f / n - 0.004f;
            img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -i * 360f / n);
            img.raycastTarget = false;
            float mid = (i + 0.5f) * 2f * Mathf.PI / n;
            Vector2 at = new Vector2(Mathf.Sin(mid), Mathf.Cos(mid)) * size * 0.32f;
            var r = sl.reward;
            string icon = r.kind == RewardKind.Credits ? "currency" : r.kind == RewardKind.Crate ? "crate_" + r.id : "inv_perk";
            var ic = Icons.Create(wheel, icon, c, at, new Vector2(110f, 110f));
            ic.raycastTarget = false;
            ic.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -mid * Mathf.Rad2Deg);
            string text = r.kind == RewardKind.Credits ? r.amount.ToString() : r.kind == RewardKind.Crate ? Shop.Crate(r.id).name.Split(' ')[0] : "KART";
            var tx = UIUtil.CreateText(wheel, text, c, new Vector2(Mathf.Sin(mid), Mathf.Cos(mid)) * size * 0.43f, new Vector2(160f, 40f), 24, TextAnchor.MiddleCenter);
            tx.fontStyle = FontStyle.Bold;
            tx.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -mid * Mathf.Rad2Deg);
        }
        var hub = UIUtil.CreateImage(t, "Hub", c, new Vector2(-260f, -40f), new Vector2(120f, 120f), new Color(0.95f, 0.95f, 0.97f), true);
        hub.raycastTarget = false;
        Icons.Create(hub.transform, "wheel", c, Vector2.zero, new Vector2(80f, 80f)).raycastTarget = false;
        // the pointer at the top
        var ptr = UIUtil.CreateImage(t, "Pointer", c, new Vector2(-260f, -40f + size * 0.5f + 10f), new Vector2(46f, 70f), Theme.Accent, false);
        ptr.raycastTarget = false;
        ptr.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        ptr.rectTransform.sizeDelta = new Vector2(52f, 52f);

        spinButton = UIUtil.CreateButton(t, "", c, new Vector2(560f, -40f), new Vector2(520f, 130f), Theme.Good, false, 40, out spinLabel);
        spinLabel.color = new Color(0.05f, 0.12f, 0.05f);
        spinLabel.GetComponent<Shadow>().enabled = false;
        spinButton.onClick.AddListener(Spin);
        infoText = UIUtil.CreateText(t, "", c, new Vector2(560f, -170f), new Vector2(560f, 120f), 24, TextAnchor.UpperCenter);
        infoText.horizontalOverflow = HorizontalWrapMode.Wrap;
        infoText.color = Theme.TextDim;
        var odds = UIUtil.CreateText(t, "Ödüller: Kredi, sandıklar ve eşya kartları.\nAltın Sandık en nadir dilim.", c, new Vector2(560f, 160f), new Vector2(560f, 90f), 24, TextAnchor.MiddleCenter);
        odds.horizontalOverflow = HorizontalWrapMode.Wrap;
        // every slice's chance, before spinning (Google Play's rule for random items)
        var oddsButton = UIUtil.CreateButton(t, "OLASILIKLAR", c, new Vector2(560f, 85f), new Vector2(300f, 56f), Theme.PanelLight, false, 24, out label);
        oddsButton.onClick.AddListener(() => OddsPanel.Show(transform, "ŞANS ÇARKI – OLASILIKLAR", Deals.WheelOdds()));
    }

    public void Open(System.Action closed)
    {
        onClose = closed;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Refresh();
    }

    public void Hide() { gameObject.SetActive(false); }

    private void Close()
    {
        if (spinning)
            return;
        Hide();
        if (onClose != null)
            onClose();
    }

    private void Refresh()
    {
        var p = GameManager.Instance.profile;
        coinsText.text = p.coins.ToString("N0");
        if (Deals.FreeSpinReady)
        {
            spinLabel.text = "ÇEVİR  •  ÜCRETSİZ";
            spinButton.GetComponent<Image>().color = Theme.Good;
            infoText.text = "Bugünkü ücretsiz çevirişin hazır!";
        }
        else
        {
            int left = Deals.MaxPaidSpins - Deals.PaidSpinsToday;
            spinLabel.text = left > 0 ? "ÇEVİR  •  " + Deals.SpinPrice + " Kredi" : "YARIN YENİDEN";
            spinButton.GetComponent<Image>().color = left > 0 ? Theme.Accent : Theme.PanelLight;
            infoText.text = left > 0 ? "Bugün " + left + " çeviriş daha alabilirsin.\nÜcretsiz çeviriş yarın yenilenir (" + Deals.TimeLeft + ")."
                                     : "Bugünlük çevirişler bitti. Yarın ücretsiz çeviriş yenilenir (" + Deals.TimeLeft + ").";
        }
    }

    private void Spin()
    {
        if (spinning)
            return;
        var p = GameManager.Instance.profile;
        GrantedReward given;
        int index = Deals.Spin(p, out given);
        if (index < 0)
        {
            infoText.text = Deals.PaidSpinsToday >= Deals.MaxPaidSpins ? "Bugünlük çevirişler bitti." : "Yetersiz Kredi: " + Deals.SpinPrice + " gerekli.";
            return;
        }
        StartCoroutine(SpinRoutine(index, given));
    }

    private System.Collections.IEnumerator SpinRoutine(int index, GrantedReward given)
    {
        spinning = true;
        UiSound.Click();
        int n = Deals.Wheel.Length;
        float slice = 360f / n;
        // The disc turns clockwise (negative z); slice i's middle is at -(i + 0.5) * slice from the top.
        float start = wheel.localEulerAngles.z;
        float target = (index + 0.5f) * slice + Random.Range(-slice * 0.3f, slice * 0.3f);   // rotation that brings it to the top
        float total = 360f * 5f + Mathf.Repeat(target - start, 360f);
        const float duration = 4.2f;
        float t = 0f, lastTick = start;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            float e = 1f - Mathf.Pow(1f - k, 3f);
            float z = start + total * e;
            wheel.localRotation = Quaternion.Euler(0f, 0f, z);
            if (Mathf.Abs(z - lastTick) >= slice)
            {
                lastTick = z;
                Sfx.Play(SoundBank.Hit, 0.25f, 1.6f);
                Haptics.Tap(8);
            }
            yield return null;
        }
        wheel.localRotation = Quaternion.Euler(0f, 0f, start + total);
        spinning = false;
        UiSound.Confirm();
        var p = GameManager.Instance.profile;
        if (given.reward.kind == RewardKind.Crate)
        {
            var list = Shop.OpenCrate(p, given.reward.id);
            CrateOpening.Show(given.reward.id, list, "ŞANS ÇARKI: " + Shop.Crate(given.reward.id).name, Refresh);
        }
        else
            CrateOpening.ShowQuick("wood", new System.Collections.Generic.List<GrantedReward> { given }, "ŞANS ÇARKI", Refresh);
        Refresh();
    }
}
