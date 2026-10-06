using System.Collections;
using System.IO;
using UnityEngine;

/// <summary>
/// Screen test (development tool): started with "-uishots &lt;folder&gt;" on a desktop build, it lets the game start
/// normally, then photographs the title, the lobby, the store (Kredi, boxes, odds), the settings (privacy, credits)
/// and the odds panels of the wheel and the weekly draw into PPM files, and quits.
/// </summary>
public class UiShots : MonoBehaviour
{
    private string dir;
    private int index;

    public static void TryStart()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] != "-uishots")
                continue;
            var go = new GameObject("UiShots");
            DontDestroyOnLoad(go);
            go.AddComponent<UiShots>().dir = args[i + 1];
            return;
        }
    }

    private IEnumerator Start()
    {
        Directory.CreateDirectory(dir);
        yield return new WaitForSecondsRealtime(4f);
        // wait for the title screen
        float until = Time.realtimeSinceStartup + 120f;
        while (FindObjectOfType<TitleScreen>() == null && Time.realtimeSinceStartup < until)
            yield return null;
        yield return new WaitForSecondsRealtime(6f);
        yield return Shot("title");
        var title = FindObjectOfType<TitleScreen>();
        var gm = GameManager.Instance;
        if (title != null && gm != null)
            title.Dismiss(gm.player);
        yield return new WaitForSecondsRealtime(3f);
        yield return Shot("lobby");
        var ui = gm != null ? gm.uiManager : null;
        if (ui != null)
        {
            ui.OpenStore(StoreScreen.Tab.Credits);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("store_kredi");
            ui.OpenStore(StoreScreen.Tab.Boxes);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("store_kutular");
            var store = FindObjectOfType<StoreScreen>();
            OddsPanel.Show(store.transform, Shop.Crates[3].name + " – OLASILIKLAR", Shop.OddsLines(Shop.Crates[3]));
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("odds_kutu");
            CloseOdds();
            OddsPanel.Show(store.transform, "ŞANS ÇARKI – OLASILIKLAR", Deals.WheelOdds());
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("odds_cark");
            CloseOdds();
            OddsPanel.Show(store.transform, "ŞANS ÇEKİLİŞİ – OLASILIKLAR", LuckyDraw.OddsLines());
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("odds_cekilis");
            CloseOdds();
            store.Hide();
            ui.ShowLobby();
            ui.OpenSettings(false);
            var settings = FindObjectOfType<SettingsScreen>();
            if (settings != null)
            {
                settings.OpenPage(4);
                yield return new WaitForSecondsRealtime(1f);
                yield return Shot("ayarlar_gizlilik");
                settings.OpenPage(5);
                yield return new WaitForSecondsRealtime(1f);
                yield return Shot("ayarlar_kunye");
            }
        }
        File.WriteAllText(Path.Combine(dir, "done.txt"), "ok " + index);
        Application.Quit();
    }

    private static void CloseOdds()
    {
        var go = GameObject.Find("Odds");
        if (go != null)
            Destroy(go);
    }

    private IEnumerator Shot(string name)
    {
        yield return new WaitForEndOfFrame();
        var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        tex.Apply();
        Color32[] px = tex.GetPixels32();
        byte[] head = System.Text.Encoding.ASCII.GetBytes("P6\n" + tex.width + " " + tex.height + "\n255\n");
        var bytes = new byte[head.Length + tex.width * tex.height * 3];
        System.Array.Copy(head, bytes, head.Length);
        int o = head.Length;
        for (int y = tex.height - 1; y >= 0; y--)
            for (int x = 0; x < tex.width; x++)
            {
                Color32 c = px[y * tex.width + x];
                bytes[o++] = c.r;
                bytes[o++] = c.g;
                bytes[o++] = c.b;
            }
        File.WriteAllBytes(Path.Combine(dir, string.Format("{0:00}_{1}.ppm", index++, name)), bytes);
        Destroy(tex);
    }
}
