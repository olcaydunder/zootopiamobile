using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Online menus: finding a quick match, creating a private room (with its 6-digit code to share),
/// joining one with the number pad, the waiting room (players, countdown, BAŞLAT for the room
/// leader) and the player-name editor. The match itself starts when the server says so.
/// </summary>
public class NetLobbyScreen : MonoBehaviour
{
    private enum Page { Status, Join, Rename }

    private Page page;
    private System.Action onClose;
    private Text title, status, hint, codeLabel, codeText, playersTitle, joinDigits, joinError;
    private readonly List<Text> playerTexts = new List<Text>();
    private GameObject statusGroup, joinGroup, renameGroup, codeGroup, listGroup;
    private GameObject startButton, okButton, leaveButton, voiceGroup, inviteButton;
    private Text micLabel, speakerLabel, talkingText;
    private InputField nameField;
    private string code = "";
    private bool waitingHttp;
    private bool showingError;
    private int shownRevision = -1;
    private int shownSeconds = -1;
    private MatchMode mode;

    private static readonly string[] AnimalNames =
    {
        "Tilki", "Aslan", "Kurt", "Kaplan", "Şahin", "Panda", "Vaşak", "Kartal", "Ayı", "Jaguar",
        "Puma", "Leopar", "Baykuş", "Gelincik", "Porsuk", "Sincap", "Kunduz", "Zürafa", "Zebra", "Tavşan"
    };

    public static NetLobbyScreen Create(Transform canvas)
    {
        var rect = UIUtil.CreateStretch(canvas, "NetLobby");
        var img = rect.gameObject.AddComponent<Image>();
        img.color = new Color(0.03f, 0.06f, 0.1f, 0.93f);
        var s = rect.gameObject.AddComponent<NetLobbyScreen>();
        s.Build();
        rect.gameObject.AddComponent<PopIn>();
        rect.gameObject.SetActive(false);
        return s;
    }

    private void Build()
    {
        var t = transform;
        Text label;
        var c = new Vector2(0.5f, 0.5f);

        var back = UIUtil.CreateButton(t, "<", new Vector2(0f, 1f), new Vector2(80f, -70f), new Vector2(84f, 84f), Theme.Accent, false, 54, out label);
        label.color = new Color(0.1f, 0.1f, 0.1f);
        label.GetComponent<Shadow>().enabled = false;
        back.onClick.AddListener(Close);
        title = UIUtil.CreateText(t, "ÇEVRİMİÇİ", new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(1200f, 80f), 50, TextAnchor.MiddleCenter);
        title.fontStyle = FontStyle.Bold;
        title.color = Theme.Accent;

        // ---- Status / waiting room
        statusGroup = UIUtil.CreateStretch(t, "Status").gameObject;
        var sg = statusGroup.transform;
        status = UIUtil.CreateText(sg, "", c, new Vector2(0f, 330f), new Vector2(1600f, 70f), 44, TextAnchor.MiddleCenter);
        status.fontStyle = FontStyle.Bold;
        hint = UIUtil.CreateText(sg, "", c, new Vector2(0f, 270f), new Vector2(1600f, 40f), 24, TextAnchor.MiddleCenter);
        hint.color = Theme.TextDim;

        codeGroup = Theme.Box(sg, "Code", c, new Vector2(-560f, 20f), new Vector2(460f, 300f), Theme.Panel, true).gameObject;
        codeLabel = UIUtil.CreateText(codeGroup.transform, "ODA KODU", c, new Vector2(0f, 95f), new Vector2(420f, 40f), 28, TextAnchor.MiddleCenter);
        codeLabel.color = Theme.TextDim;
        codeText = UIUtil.CreateText(codeGroup.transform, "", c, new Vector2(0f, 10f), new Vector2(440f, 110f), 84, TextAnchor.MiddleCenter);
        codeText.fontStyle = FontStyle.Bold;
        codeText.color = Theme.Accent;
        var share = UIUtil.CreateText(codeGroup.transform, "Arkadaşların ODAYA KATIL'a\nbu kodu yazsın", c, new Vector2(0f, -95f), new Vector2(420f, 70f), 22, TextAnchor.MiddleCenter);
        share.color = Theme.TextDim;

        listGroup = Theme.Box(sg, "Players", c, new Vector2(170f, 20f), new Vector2(900f, 470f), Theme.Panel, false).gameObject;
        playersTitle = UIUtil.CreateText(listGroup.transform, "OYUNCULAR", new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(840f, 44f), 30, TextAnchor.MiddleLeft);
        playersTitle.fontStyle = FontStyle.Bold;
        for (int i = 0; i < NetProtocol.MaxHumans; i++)
        {
            float x = i < 8 ? -215f : 225f;
            float y = 150f - (i % 8) * 46f;
            var pt = UIUtil.CreateText(listGroup.transform, "", c, new Vector2(x, y), new Vector2(420f, 42f), 26, TextAnchor.MiddleLeft);
            playerTexts.Add(pt);
        }

        var leave = UIUtil.CreateButton(sg, "AYRIL", new Vector2(0.5f, 0f), new Vector2(-260f, 110f), new Vector2(420f, 110f), Theme.Red, false, 40, out label);
        leave.onClick.AddListener(Close);
        leaveButton = leave.gameObject;
        var start = UIUtil.CreateButton(sg, "BAŞLAT", new Vector2(0.5f, 0f), new Vector2(260f, 110f), new Vector2(420f, 110f), Theme.Accent, false, 44, out label);
        label.color = new Color(0.1f, 0.08f, 0.02f);
        label.GetComponent<Shadow>().enabled = false;
        start.onClick.AddListener(() =>
        {
            if (NetClient.Instance != null)
                NetClient.Instance.RequestStart();
            UiSound.Confirm();
        });
        startButton = start.gameObject;
        // Voice in a private room's waiting room, and inviting friends.
        voiceGroup = UIUtil.CreateRect(sg, "Voice", new Vector2(0f, 0f), new Vector2(330f, 110f), new Vector2(560f, 110f)).gameObject;
        var mic = UIUtil.CreateButton(voiceGroup.transform, "MİK", new Vector2(0f, 0.5f), new Vector2(70f, 0f), new Vector2(130f, 100f), Theme.Panel, false, 24, out micLabel);
        mic.onClick.AddListener(VoiceChat.ToggleMic);
        var spk = UIUtil.CreateButton(voiceGroup.transform, "SES", new Vector2(0f, 0.5f), new Vector2(210f, 0f), new Vector2(130f, 100f), Theme.Panel, false, 24, out speakerLabel);
        spk.onClick.AddListener(() => VoiceChat.SpeakerOn = !VoiceChat.SpeakerOn);
        talkingText = UIUtil.CreateText(voiceGroup.transform, "", new Vector2(0f, 0.5f), new Vector2(430f, 0f), new Vector2(260f, 60f), 24, TextAnchor.MiddleLeft);
        talkingText.color = new Color(0.5f, 1f, 0.6f);
        talkingText.horizontalOverflow = HorizontalWrapMode.Wrap;
        var invite = UIUtil.CreateButton(sg, "ARKADAŞ ÇAĞIR", new Vector2(0.5f, 0.5f), new Vector2(-560f, -205f), new Vector2(460f, 90f), new Color(0.16f, 0.45f, 0.95f, 0.95f), false, 30, out label);
        invite.onClick.AddListener(() => GameManager.Instance.uiManager.OpenSocialFromRoom(gameObject));
        inviteButton = invite.gameObject;

        var ok = UIUtil.CreateButton(sg, "TAMAM", new Vector2(0.5f, 0f), new Vector2(260f, 110f), new Vector2(420f, 110f), new Color(0.2f, 0.5f, 1f, 0.95f), false, 40, out label);
        ok.onClick.AddListener(Close);
        okButton = ok.gameObject;

        // ---- Join with a code: number pad
        joinGroup = UIUtil.CreateStretch(t, "Join").gameObject;
        var jg = joinGroup.transform;
        var jt = UIUtil.CreateText(jg, "Arkadaşının verdiği 6 haneli oda kodunu yaz", c, new Vector2(0f, 330f), new Vector2(1400f, 50f), 32, TextAnchor.MiddleCenter);
        jt.color = Theme.TextDim;
        var digitsBox = Theme.Box(jg, "Digits", c, new Vector2(0f, 220f), new Vector2(620f, 130f), Theme.Panel, true);
        joinDigits = UIUtil.CreateText(digitsBox.transform, "", c, Vector2.zero, new Vector2(600f, 120f), 86, TextAnchor.MiddleCenter);
        joinDigits.fontStyle = FontStyle.Bold;
        joinDigits.color = Theme.Accent;
        joinError = UIUtil.CreateText(jg, "", c, new Vector2(0f, 128f), new Vector2(1200f, 40f), 26, TextAnchor.MiddleCenter);
        joinError.color = Theme.Bad;
        string[] keys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "SİL", "0", "KATIL" };
        for (int i = 0; i < keys.Length; i++)
        {
            string k = keys[i];
            float x = (i % 3 - 1) * 200f;
            float y = 30f - (i / 3) * 112f;
            Color col = k == "KATIL" ? Theme.Accent : k == "SİL" ? Theme.PanelLight : new Color(0.16f, 0.2f, 0.27f, 0.95f);
            var b = UIUtil.CreateButton(jg, k, c, new Vector2(x, y), new Vector2(184f, 100f), col, false, k.Length > 1 ? 32 : 50, out label);
            if (k == "KATIL")
            {
                label.color = new Color(0.1f, 0.08f, 0.02f);
                label.GetComponent<Shadow>().enabled = false;
            }
            b.onClick.AddListener(() => PressKey(k));
        }

        // ---- Player name
        renameGroup = UIUtil.CreateStretch(t, "Rename").gameObject;
        var rg = renameGroup.transform;
        var rt = UIUtil.CreateText(rg, "Çevrimiçi maçlarda diğer oyuncular bu ismi görür", c, new Vector2(0f, 200f), new Vector2(1400f, 50f), 30, TextAnchor.MiddleCenter);
        rt.color = Theme.TextDim;
        var fieldBg = UIUtil.CreateImage(rg, "NameField", c, new Vector2(0f, 90f), new Vector2(720f, 120f), new Color(1f, 1f, 1f, 0.12f), false);
        var fieldText = UIUtil.CreateText(fieldBg.transform, "", c, Vector2.zero, new Vector2(680f, 110f), 56, TextAnchor.MiddleCenter);
        fieldText.supportRichText = false;
        var placeholder = UIUtil.CreateText(fieldBg.transform, "İsmini yaz", c, Vector2.zero, new Vector2(680f, 110f), 52, TextAnchor.MiddleCenter);
        placeholder.color = new Color(1f, 1f, 1f, 0.35f);
        nameField = fieldBg.gameObject.AddComponent<InputField>();
        nameField.textComponent = fieldText;
        nameField.placeholder = placeholder;
        nameField.characterLimit = 16;
        nameField.lineType = InputField.LineType.SingleLine;
        var save = UIUtil.CreateButton(rg, "KAYDET", c, new Vector2(0f, -70f), new Vector2(420f, 110f), Theme.Accent, false, 44, out label);
        label.color = new Color(0.1f, 0.08f, 0.02f);
        label.GetComponent<Shadow>().enabled = false;
        save.onClick.AddListener(SaveName);
    }

    // ----- Opening -----

    private void Show(Page p, string heading)
    {
        page = p;
        title.text = heading;
        statusGroup.SetActive(p == Page.Status);
        joinGroup.SetActive(p == Page.Join);
        renameGroup.SetActive(p == Page.Rename);
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        shownRevision = -1;
        shownSeconds = -1;
    }

    public void OpenQuick(MatchMode matchMode, System.Action closed)
    {
        onClose = closed;
        mode = matchMode;
        EnsureName();
        Show(Page.Status, "ÇEVRİMİÇİ  •  " + ModeLabel(matchMode) + "  •  " + MapCatalog.CurrentInfo.name);
        ShowBusy("Maç aranıyor...");
        OnlineService.Quick(matchMode, info => OnMatchInfo(info, matchMode, false));
    }

    public void OpenCreate(MatchMode matchMode, System.Action closed)
    {
        onClose = closed;
        mode = matchMode;
        EnsureName();
        Show(Page.Status, "ÖZEL ODA  •  " + ModeLabel(matchMode) + "  •  " + MapCatalog.CurrentInfo.name);
        ShowBusy("Oda kuruluyor...");
        OnlineService.CreateRoom(matchMode, info => OnMatchInfo(info, matchMode, true));
    }

    public void OpenJoin(System.Action closed)
    {
        onClose = closed;
        EnsureName();
        code = "";
        joinError.text = "";
        RefreshDigits();
        Show(Page.Join, "ODAYA KATIL");
    }

    /// <summary>Joins a friend's private room by its code (invite / friends list).</summary>
    public void OpenJoinCode(string joinCode, System.Action closed)
    {
        onClose = closed;
        EnsureName();
        Show(Page.Status, "ÖZEL ODA  •  " + joinCode);
        ShowBusy("Oda aranıyor...");
        OnlineService.FindRoom(joinCode, info => OnMatchInfo(info, NetProtocol.ParseMode(info.mode), true, joinCode));
    }

    public void OpenRename(System.Action closed)
    {
        onClose = closed;
        nameField.text = GameManager.Instance.profile.playerName;
        Show(Page.Rename, "OYUNCU İSMİ");
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void Close()
    {
        OnlineService.CancelAll();
        waitingHttp = false;
        if (NetClient.Instance != null && !NetClient.Instance.InMatch)
            NetClient.Instance.Leave();
        Hide();
        if (onClose != null)
            onClose();
    }

    private static string ModeLabel(MatchMode m)
    {
        return m == MatchMode.Duo ? "DUO" : m == MatchMode.Squad ? "SQUAD" : "SOLO";
    }

    /// <summary>Players should not all be "Oyuncu": the first time, pick an animal name with a number.</summary>
    private static void EnsureName()
    {
        var p = GameManager.Instance.profile;
        if (!string.IsNullOrEmpty(p.playerName) && p.playerName != "Oyuncu")
            return;
        p.playerName = AnimalNames[Random.Range(0, AnimalNames.Length)] + Random.Range(10, 100);
        p.Save();
    }

    private void SaveName()
    {
        string n = (nameField.text ?? "").Trim();
        if (n.Length < 2)
        {
            GameManager.Instance.uiManager.Toast("İsim en az 2 harf olmalı");
            return;
        }
        var p = GameManager.Instance.profile;
        p.playerName = n.Length > 16 ? n.Substring(0, 16) : n;
        p.Save();
        OnlineService.SyncName();
        UiSound.Confirm();
        Close();
    }

    // ----- Number pad -----

    private void PressKey(string k)
    {
        if (k == "SİL")
        {
            if (code.Length > 0)
                code = code.Substring(0, code.Length - 1);
        }
        else if (k == "KATIL")
        {
            if (code.Length != 6)
            {
                joinError.text = "Kod 6 haneli olmalı";
                return;
            }
            string joinCode = code;
            Show(Page.Status, "ÖZEL ODA  •  " + joinCode);
            ShowBusy("Oda aranıyor...");
            OnlineService.FindRoom(joinCode, info => OnMatchInfo(info, NetProtocol.ParseMode(info.mode), true, joinCode));
            return;
        }
        else if (code.Length < 6)
            code += k;
        joinError.text = "";
        RefreshDigits();
    }

    private void RefreshDigits()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < 6; i++)
        {
            if (i == 3)
                sb.Append("  ");
            sb.Append(i < code.Length ? code[i] : '_');
            if (i != 2 && i < 5)
                sb.Append(' ');
        }
        joinDigits.text = sb.ToString();
    }

    // ----- Matchmaker answer -----

    private void OnMatchInfo(OnlineService.MatchInfo info, MatchMode matchMode, bool privateRoom, string joinCode = null)
    {
        waitingHttp = false;
        if (!gameObject.activeSelf || page != Page.Status)
            return;   // closed meanwhile
        if (!info.ok)
        {
            ShowError(info.error);
            return;
        }
        string roomMap = MapCatalog.IsValid(info.map) ? info.map : MapCatalog.DefaultId;
        if (roomMap != MapCatalog.Current)
        {
            if (joinCode == null)
            {
                ShowError("Sunucu bu haritayı henüz açamıyor, biraz sonra tekrar dene");
                return;
            }
            // A friend's room on another map: load that map, then join the room again from the lobby.
            ShowBusy(MapCatalog.Get(roomMap).name + " haritası yükleniyor...");
            waitingHttp = false;
            string again = joinCode;
            GameBootstrap.SwitchMap(roomMap, () =>
            {
                var gm = GameManager.Instance;
                if (gm != null && gm.uiManager != null)
                    gm.uiManager.JoinRoomByCode(again);
            });
            return;
        }
        mode = matchMode;
        title.text = (privateRoom ? "ÖZEL ODA  •  " : "ÇEVRİMİÇİ  •  ") + ModeLabel(matchMode) + "  •  " + MapCatalog.CurrentInfo.name;
        NetClient.Ensure().Connect(info.host, info.port, info.code, matchMode, privateRoom);
        shownRevision = -1;
    }

    private void ShowBusy(string text)
    {
        waitingHttp = true;
        showingError = false;
        status.text = text;
        status.color = Color.white;
        hint.text = "";
        codeGroup.SetActive(false);
        listGroup.SetActive(false);
        startButton.SetActive(false);
        okButton.SetActive(false);
        leaveButton.SetActive(true);
        voiceGroup.SetActive(false);
        inviteButton.SetActive(false);
    }

    private void ShowError(string text)
    {
        showingError = true;
        status.text = string.IsNullOrEmpty(text) ? "Bir sorun oluştu" : text;
        status.color = Theme.Bad;
        hint.text = "";
        codeGroup.SetActive(false);
        listGroup.SetActive(false);
        startButton.SetActive(false);
        okButton.SetActive(true);
        leaveButton.SetActive(false);
        voiceGroup.SetActive(false);
        inviteButton.SetActive(false);
    }

    // ----- Waiting room -----

    private void Update()
    {
        if (page != Page.Status || waitingHttp || showingError)
            return;
        var net = NetClient.Instance;
        if (net == null)
            return;
        if (net.InMatch)
        {
            Hide();   // the battle HUD takes over
            return;
        }
        if (net.State == NetClient.Phase.Idle)
        {
            ShowError(net.Error);
            return;
        }

        bool voice = net.State == NetClient.Phase.Lobby && net.PrivateRoom;
        if (voiceGroup.activeSelf != voice)
            voiceGroup.SetActive(voice);
        if (inviteButton.activeSelf != voice)
            inviteButton.SetActive(voice);
        if (voice)
        {
            UIManager.VoiceLabels(micLabel, speakerLabel);
            string talking = VoiceChat.TalkingNow();
            talkingText.text = talking.Length > 0 ? "Konuşuyor: " + talking : (net.LobbyIds.Count > 1 ? "Sesli sohbet açık" : "");
        }

        int seconds = net.LobbyPhase == NetProtocol.LobbyCountdown && net.CountdownEnds > 0
            ? Mathf.Max(0, Mathf.CeilToInt((float)(net.CountdownEnds - Time.realtimeSinceStartupAsDouble)))
            : -1;
        if (net.Revision == shownRevision && seconds == shownSeconds)
            return;
        shownRevision = net.Revision;
        shownSeconds = seconds;

        bool inRoom = net.State == NetClient.Phase.Lobby;
        status.color = Color.white;
        if (!inRoom)
            status.text = net.Status;
        else if (seconds >= 0)
            status.text = seconds > 0 ? "Maç " + seconds + " sn sonra başlıyor" : "Maç başlıyor...";
        else if (net.PrivateRoom)
            status.text = net.IsLeader ? "Herkes gelince BAŞLAT'a bas" : "Oda lideri maçı başlatacak";
        else
            status.text = "Oyuncular bekleniyor";

        hint.text = inRoom ? "Boş yerleri botlar doldurur  •  " + ModeLabel(net.Mode) + "  •  en fazla " + NetProtocol.MaxHumans + " oyuncu" : "";
        codeGroup.SetActive(inRoom && net.PrivateRoom);
        if (inRoom && net.PrivateRoom)
            codeText.text = net.RoomCode.Length == 6 ? net.RoomCode.Substring(0, 3) + " " + net.RoomCode.Substring(3) : net.RoomCode;
        listGroup.SetActive(inRoom);
        startButton.SetActive(inRoom && net.PrivateRoom && net.IsLeader && net.LobbyPhase == NetProtocol.LobbyWaiting);
        okButton.SetActive(false);
        leaveButton.SetActive(true);

        if (inRoom)
        {
            playersTitle.text = "OYUNCULAR  " + net.LobbyNames.Count + " / " + NetProtocol.MaxHumans;
            for (int i = 0; i < playerTexts.Count; i++)
            {
                if (i >= net.LobbyNames.Count)
                {
                    playerTexts[i].text = "";
                    continue;
                }
                int id = net.LobbyIds[i];
                string line = (i + 1) + ". " + net.LobbyNames[i];
                if (id == net.LeaderId && net.PrivateRoom)
                    line += "  (lider)";
                if (id == net.MyId)
                    line += "  (sen)";
                playerTexts[i].text = line;
                playerTexts[i].color = id == net.MyId ? Theme.Accent : Color.white;
            }
        }
    }
}
