using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Friends and community screen: your friend code, adding friends by code, requests, invites to a
/// private room, players you played with (add / report / block / mute), the blocked list, and the
/// bug report form. Works over the lobby and, from the pause menu, during an online match.
/// </summary>
public class SocialScreen : MonoBehaviour
{
    public enum Tab { Friends, Players, Bug }

    private Tab tab;
    private System.Action onClose;
    private readonly List<Text> tabLabels = new List<Text>();
    private readonly List<Image> tabImages = new List<Image>();
    private RectTransform content;
    private ScrollRect scroll;
    private Text codeText, statusText;
    private InputField addField;
    private GameObject friendsHeader, bugGroup, listView;
    private InputField bugField;
    private Toggle bugLogs;
    private GameObject reportPanel;
    private Text reportTitle;
    private InputField reportField;
    private readonly List<Image> reasonButtons = new List<Image>();
    private string reportTarget = "", reportMatch = "", reportReason = "";
    private float nextRefresh;
    private bool busy;
    private readonly List<NetClient.MetPlayer> scratch = new List<NetClient.MetPlayer>();

    private static readonly string[] ReasonIds = { "cheat", "abuse", "voice", "name", "afk", "other" };
    private static readonly string[] ReasonNames = { "Hile", "Küfür / hakaret", "Seste rahatsızlık", "Uygunsuz isim", "Oynamıyor", "Diğer" };

    public static SocialScreen Create(Transform canvas)
    {
        var rect = UIUtil.CreateStretch(canvas, "Social");
        var bg = rect.gameObject.AddComponent<Image>();
        bg.color = new Color(0.03f, 0.05f, 0.09f, 0.96f);
        var s = rect.gameObject.AddComponent<SocialScreen>();
        s.Build();
        rect.gameObject.AddComponent<PopIn>();
        rect.gameObject.SetActive(false);
        return s;
    }

    // ----- Building -----

    private void Build()
    {
        var t = transform;
        Text label;
        var c = new Vector2(0.5f, 0.5f);

        var back = UIUtil.CreateButton(t, "<", new Vector2(0f, 1f), new Vector2(80f, -70f), new Vector2(84f, 84f), Theme.Accent, false, 54, out label);
        label.color = new Color(0.1f, 0.1f, 0.1f);
        label.GetComponent<Shadow>().enabled = false;
        back.onClick.AddListener(Close);

        string[] tabs = { "ARKADAŞLAR", "OYUNCULAR", "HATA BİLDİR" };
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            var b = UIUtil.CreateButton(t, tabs[i], new Vector2(0.5f, 1f), new Vector2(-330f + i * 330f, -70f), new Vector2(310f, 80f), Theme.Panel, false, 30, out label);
            b.onClick.AddListener(() => Open((Tab)index, onClose));
            tabImages.Add(b.GetComponent<Image>());
            tabLabels.Add(label);
        }

        statusText = UIUtil.CreateText(t, "", new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1600f, 40f), 26, TextAnchor.MiddleCenter);
        statusText.color = Theme.Accent;

        // Friend code + add by code
        friendsHeader = UIUtil.CreateRect(t, "FriendsHeader", new Vector2(0.5f, 1f), new Vector2(0f, -185f), new Vector2(1500f, 110f)).gameObject;
        var fh = friendsHeader.transform;
        var codeBox = Theme.Box(fh, "Code", new Vector2(0f, 0.5f), new Vector2(250f, 0f), new Vector2(500f, 104f), Theme.Panel, true);
        var cl = UIUtil.CreateText(codeBox.transform, "ARKADAŞ KODUN", c, new Vector2(-40f, 26f), new Vector2(380f, 30f), 22, TextAnchor.MiddleLeft);
        cl.color = Theme.TextDim;
        codeText = UIUtil.CreateText(codeBox.transform, "...", c, new Vector2(-40f, -14f), new Vector2(380f, 56f), 46, TextAnchor.MiddleLeft);
        codeText.fontStyle = FontStyle.Bold;
        codeText.color = Theme.Accent;
        var copy = UIUtil.CreateButton(codeBox.transform, "KOPYALA", new Vector2(1f, 0.5f), new Vector2(-80f, 0f), new Vector2(140f, 64f), Theme.PanelLight, false, 22, out label);
        copy.onClick.AddListener(() =>
        {
            GUIUtility.systemCopyBuffer = OnlineService.FriendCode(OnlineService.AccountId);
            SetStatus("Kodun kopyalandı, arkadaşına gönder");
        });

        var fieldBg = UIUtil.CreateImage(fh, "AddField", new Vector2(1f, 0.5f), new Vector2(-560f, 0f), new Vector2(400f, 90f), new Color(1f, 1f, 1f, 0.1f), false);
        addField = MakeInput(fieldBg, "Arkadaşının kodu", 7, 40);
        addField.contentType = InputField.ContentType.Alphanumeric;
        var add = UIUtil.CreateButton(fh, "EKLE", new Vector2(1f, 0.5f), new Vector2(-230f, 0f), new Vector2(220f, 90f), Theme.Accent, false, 34, out label);
        label.color = new Color(0.1f, 0.08f, 0.02f);
        label.GetComponent<Shadow>().enabled = false;
        add.onClick.AddListener(AddByCode);

        // Scrolling list
        listView = UIUtil.CreateRect(t, "List", new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), new Vector2(1500f, 700f)).gameObject;
        listView.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
        listView.AddComponent<RectMask2D>();
        content = UIUtil.CreateRect(listView.transform, "Content", new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1500f, 10f));
        content.pivot = new Vector2(0.5f, 1f);
        scroll = listView.AddComponent<ScrollRect>();
        scroll.content = content;
        scroll.viewport = (RectTransform)listView.transform;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        // Bug report
        bugGroup = UIUtil.CreateRect(t, "Bug", c, new Vector2(0f, -20f), new Vector2(1400f, 760f)).gameObject;
        var bg = bugGroup.transform;
        var bt = UIUtil.CreateText(bg, "Ne oldu? Ne yapıyordun, ne bekliyordun, ne oldu?", new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(1300f, 50f), 32, TextAnchor.MiddleLeft);
        bt.color = Theme.TextDim;
        var bf = UIUtil.CreateImage(bg, "BugField", new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(1300f, 360f), new Color(1f, 1f, 1f, 0.08f), false);
        bugField = MakeInput(bf, "Örn: helikoptere binince oyun dondu", 1500, 34);
        bugField.lineType = InputField.LineType.MultiLineNewline;
        bugField.textComponent.alignment = TextAnchor.UpperLeft;
        ((Text)bugField.placeholder).alignment = TextAnchor.UpperLeft;
        var tg = UIUtil.CreateRect(bg, "Logs", new Vector2(0.5f, 1f), new Vector2(-380f, -480f), new Vector2(560f, 60f));
        var box = UIUtil.CreateImage(tg, "Box", new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(46f, 46f), new Color(1f, 1f, 1f, 0.2f), false);
        var check = UIUtil.CreateImage(box.transform, "Check", c, Vector2.zero, new Vector2(30f, 30f), Theme.Accent, false);
        bugLogs = tg.gameObject.AddComponent<Toggle>();
        bugLogs.targetGraphic = box;
        bugLogs.graphic = check;
        bugLogs.isOn = true;
        var tl = UIUtil.CreateText(tg, "Hata kayıtlarını ekle (önerilir)", new Vector2(0f, 0.5f), new Vector2(320f, 0f), new Vector2(520f, 50f), 28, TextAnchor.MiddleLeft);
        tl.color = Color.white;
        var send = UIUtil.CreateButton(bg, "GÖNDER", new Vector2(0.5f, 1f), new Vector2(430f, -480f), new Vector2(380f, 100f), Theme.Accent, false, 40, out label);
        label.color = new Color(0.1f, 0.08f, 0.02f);
        label.GetComponent<Shadow>().enabled = false;
        send.onClick.AddListener(SendBug);

        BuildReportPanel();
    }

    private static InputField MakeInput(Image bg, string hint, int limit, int fontSize)
    {
        var size = bg.rectTransform.sizeDelta;
        var text = UIUtil.CreateText(bg.transform, "", new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(30f, 16f), fontSize, TextAnchor.MiddleLeft);
        text.supportRichText = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        var ph = UIUtil.CreateText(bg.transform, hint, new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(30f, 16f), fontSize, TextAnchor.MiddleLeft);
        ph.color = new Color(1f, 1f, 1f, 0.35f);
        var field = bg.gameObject.AddComponent<InputField>();
        field.textComponent = text;
        field.placeholder = ph;
        field.characterLimit = limit;
        field.lineType = InputField.LineType.SingleLine;
        return field;
    }

    private void BuildReportPanel()
    {
        Text label;
        var c = new Vector2(0.5f, 0.5f);
        var dim = UIUtil.CreateStretch(transform, "Report");
        dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);
        reportPanel = dim.gameObject;
        var box = UIUtil.CreateImage(dim, "Box", c, Vector2.zero, new Vector2(1100f, 820f), new Color(0.07f, 0.1f, 0.15f, 0.98f), false).transform;
        reportTitle = UIUtil.CreateText(box, "", c, new Vector2(0f, 340f), new Vector2(1000f, 60f), 40, TextAnchor.MiddleCenter);
        reportTitle.fontStyle = FontStyle.Bold;
        var why = UIUtil.CreateText(box, "Sebep", c, new Vector2(0f, 270f), new Vector2(1000f, 40f), 26, TextAnchor.MiddleCenter);
        why.color = Theme.TextDim;
        for (int i = 0; i < ReasonIds.Length; i++)
        {
            int index = i;
            float x = (i % 3 - 1) * 330f;
            float y = 190f - (i / 3) * 100f;
            var b = UIUtil.CreateButton(box, ReasonNames[i], c, new Vector2(x, y), new Vector2(310f, 84f), Theme.Panel, false, 26, out label);
            b.onClick.AddListener(() => SelectReason(index));
            reasonButtons.Add(b.GetComponent<Image>());
        }
        var fbg = UIUtil.CreateImage(box, "Text", c, new Vector2(0f, -60f), new Vector2(980f, 160f), new Color(1f, 1f, 1f, 0.08f), false);
        reportField = MakeInput(fbg, "Ayrıntı (isteğe bağlı)", 400, 28);
        reportField.lineType = InputField.LineType.MultiLineNewline;
        reportField.textComponent.alignment = TextAnchor.UpperLeft;
        ((Text)reportField.placeholder).alignment = TextAnchor.UpperLeft;
        var cancel = UIUtil.CreateButton(box, "VAZGEÇ", c, new Vector2(-230f, -290f), new Vector2(400f, 100f), Theme.PanelLight, false, 34, out label);
        cancel.onClick.AddListener(() => reportPanel.SetActive(false));
        var send = UIUtil.CreateButton(box, "ŞİKAYET ET", c, new Vector2(230f, -290f), new Vector2(400f, 100f), Theme.Red, false, 34, out label);
        send.onClick.AddListener(SendReport);
        reportPanel.SetActive(false);
    }

    // ----- Opening -----

    public void Open(Tab which, System.Action closed)
    {
        tab = which;
        onClose = closed;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        reportPanel.SetActive(false);
        for (int i = 0; i < tabImages.Count; i++)
        {
            bool sel = i == (int)tab;
            tabImages[i].color = sel ? Theme.Selected : Theme.Panel;
            tabLabels[i].color = sel ? new Color(0.08f, 0.08f, 0.1f) : Color.white;
            tabLabels[i].GetComponent<Shadow>().enabled = !sel;
        }
        friendsHeader.SetActive(tab == Tab.Friends);
        listView.SetActive(tab != Tab.Bug);
        bugGroup.SetActive(tab == Tab.Bug);
        var lr = (RectTransform)listView.transform;
        lr.anchoredPosition = tab == Tab.Friends ? new Vector2(0f, -110f) : new Vector2(0f, -20f);
        lr.sizeDelta = tab == Tab.Friends ? new Vector2(1500f, 660f) : new Vector2(1500f, 800f);
        codeText.text = OnlineService.HasAccount ? OnlineService.FriendCode(OnlineService.AccountId) : "...";
        SetStatus("");
        Rebuild();
        Refresh();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void Close()
    {
        Hide();
        if (onClose != null)
            onClose();
    }

    private void SetStatus(string text)
    {
        statusText.text = text;
    }

    private void Update()
    {
        if (tab != Tab.Bug && Time.unscaledTime >= nextRefresh && !busy)
            Refresh();
    }

    private void Refresh()
    {
        nextRefresh = Time.unscaledTime + 8f;
        busy = true;
        var net = NetClient.Instance;
        bool room = net != null && net.State == NetClient.Phase.Lobby && net.PrivateRoom;
        OnlineService.RefreshSocial(room ? "room" : "lobby", room ? net.RoomCode : "", net != null ? net.Mode : MatchMode.Solo, view =>
        {
            busy = false;
            if (!view.ok && gameObject.activeSelf)
                SetStatus(view.error);
            if (gameObject.activeSelf)
            {
                codeText.text = OnlineService.HasAccount ? OnlineService.FriendCode(OnlineService.AccountId) : "...";
                Rebuild();
            }
        });
    }

    private void Answer(OnlineService.SocialView view)
    {
        if (view.ok && !string.IsNullOrEmpty(view.message))
            SetStatus(view.message);
        else if (!view.ok)
            SetStatus(view.error);
        Refresh();
    }

    // ----- List -----

    private float y;

    private void Rebuild()
    {
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);
        y = 0f;
        var social = OnlineService.Social;
        var net = NetClient.Instance;
        bool inRoom = net != null && net.State == NetClient.Phase.Lobby && net.PrivateRoom;

        if (tab == Tab.Friends)
        {
            if (social == null)
            {
                Section(OnlineService.HasAccount ? "Yükleniyor..." : "Bağlanılıyor...");
            }
            else
            {
                if (social.invites.Length > 0)
                {
                    Section("ODA DAVETLERİ");
                    foreach (var inv in social.invites)
                    {
                        var p = inv;
                        Row(p.name, "seni odasına çağırıyor  •  " + (p.mode ?? "").ToUpper(),
                            Btn("KATIL", Theme.Accent, () => JoinRoom(p.room)),
                            Btn("YOKSAY", Theme.PanelLight, () => OnlineService.DismissInvite(p.id, Answer)));
                    }
                }
                if (social.incoming.Length > 0)
                {
                    Section("ARKADAŞLIK İSTEKLERİ");
                    foreach (var r in social.incoming)
                    {
                        var p = r;
                        Row(p.name, OnlineService.FriendCode(p.id),
                            Btn("KABUL", Theme.Good, () => OnlineService.AcceptFriend(p.id, Answer)),
                            Btn("RED", Theme.PanelLight, () => OnlineService.RemoveFriend(p.id, Answer)));
                    }
                }
                Section("ARKADAŞLAR  " + social.friends.Length);
                if (social.friends.Length == 0)
                    Note("Henüz arkadaşın yok. Kodunu paylaş ya da arkadaşının kodunu yukarı yaz.");
                foreach (var f in social.friends)
                {
                    var p = f;
                    string state = !p.online ? "çevrimdışı" : p.status == "match" ? "maçta" : p.status == "room" ? "odada (" + p.room + ")" : "lobide";
                    var buttons = new List<RowButton>();
                    if (p.online && p.status == "room" && p.room.Length == 6 && !(inRoom && net.RoomCode == p.room))
                        buttons.Add(Btn("KATIL", Theme.Accent, () => JoinRoom(p.room)));
                    if (p.online && inRoom)
                        buttons.Add(Btn("DAVET ET", new Color(0.16f, 0.45f, 0.95f, 0.95f), () => OnlineService.Invite(p.id, net.RoomCode, net.Mode, v => SetStatus(v.ok ? p.name + " davet edildi" : v.error))));
                    buttons.Add(Btn("SİL", Theme.PanelLight, () => OnlineService.RemoveFriend(p.id, Answer)));
                    buttons.Add(Btn("ENGELLE", Theme.Red, () => OnlineService.Block(p.id, Answer)));
                    Row(p.name, state, buttons.ToArray()).color = p.online ? new Color(0.1f, 0.2f, 0.14f, 0.9f) : Theme.Panel;
                }
                if (social.outgoing.Length > 0)
                {
                    Section("GÖNDERİLEN İSTEKLER");
                    foreach (var o in social.outgoing)
                    {
                        var p = o;
                        Row(p.name, "yanıt bekleniyor", Btn("İPTAL", Theme.PanelLight, () => OnlineService.RemoveFriend(p.id, Answer)));
                    }
                }
                if (social.blocked.Length > 0)
                {
                    Section("ENGELLENENLER");
                    foreach (var b in social.blocked)
                    {
                        var p = b;
                        Row(p.name, "mesaj, istek ve ses gelmez", Btn("KALDIR", Theme.PanelLight, () => OnlineService.Unblock(p.id, Answer)));
                    }
                }
            }
        }
        else if (tab == Tab.Players)
        {
            bool live = net != null && (net.State == NetClient.Phase.Playing || net.State == NetClient.Phase.Lobby);
            if (live)
                net.ListPlayers(scratch);
            else
            {
                scratch.Clear();
                scratch.AddRange(NetClient.RecentPlayers);
            }
            Section(live ? (net.State == NetClient.Phase.Playing ? "BU MAÇTAKİ OYUNCULAR" : "ODADAKİ OYUNCULAR") : "SON MAÇTAKİ OYUNCULAR");
            if (scratch.Count == 0)
                Note("Çevrimiçi bir maç oynadığında birlikte oynadığın oyuncular burada çıkar.");
            foreach (var m in scratch)
            {
                var p = m;
                bool hasAccount = !string.IsNullOrEmpty(p.account);
                bool friend = IsFriend(p.account);
                bool muted = hasAccount && VoiceChat.Muted.Contains(p.account);
                var buttons = new List<RowButton>();
                if (hasAccount && !friend && p.account != OnlineService.AccountId)
                    buttons.Add(Btn("EKLE", Theme.Good, () => OnlineService.AddFriend(p.account, Answer)));
                if (hasAccount && p.teammate)
                    buttons.Add(Btn(muted ? "SESİ AÇ" : "SUSTUR", Theme.PanelLight, () =>
                    {
                        if (muted) VoiceChat.Muted.Remove(p.account); else VoiceChat.Muted.Add(p.account);
                        Rebuild();
                    }));
                if (hasAccount)
                {
                    buttons.Add(Btn("ŞİKAYET", new Color(0.75f, 0.45f, 0.1f, 0.95f), () => OpenReport(p.name, p.account, p.match)));
                    buttons.Add(Btn("ENGELLE", Theme.Red, () => OnlineService.Block(p.account, v => { SetStatus(v.ok ? p.name + " engellendi" : v.error); Refresh(); })));
                }
                Row(p.name, (p.teammate ? "takım arkadaşı  •  " : "") + (friend ? "arkadaşın" : hasAccount ? OnlineService.FriendCode(p.account) : "hesapsız"), buttons.ToArray());
            }
        }
        content.sizeDelta = new Vector2(1500f, y + 20f);
    }

    private static bool IsFriend(string account)
    {
        var s = OnlineService.Social;
        if (s == null || string.IsNullOrEmpty(account))
            return false;
        foreach (var f in s.friends)
            if (f.id == account)
                return true;
        return false;
    }

    private struct RowButton
    {
        public string label;
        public Color color;
        public System.Action action;
    }

    private static RowButton Btn(string label, Color color, System.Action action)
    {
        return new RowButton { label = label, color = color, action = action };
    }

    private void Section(string title)
    {
        var tx = UIUtil.CreateText(content, title, new Vector2(0.5f, 1f), new Vector2(0f, -y - 30f), new Vector2(1460f, 44f), 26, TextAnchor.MiddleLeft);
        tx.color = Theme.TextDim;
        tx.fontStyle = FontStyle.Bold;
        y += 60f;
    }

    private void Note(string text)
    {
        var tx = UIUtil.CreateText(content, text, new Vector2(0.5f, 1f), new Vector2(0f, -y - 30f), new Vector2(1460f, 50f), 26, TextAnchor.MiddleLeft);
        tx.color = new Color(1f, 1f, 1f, 0.5f);
        y += 64f;
    }

    private Image Row(string name, string sub, params RowButton[] buttons)
    {
        var bg = UIUtil.CreateImage(content, "Row", new Vector2(0.5f, 1f), new Vector2(0f, -y - 50f), new Vector2(1480f, 96f), Theme.Panel, false);
        var n = UIUtil.CreateText(bg.transform, name, new Vector2(0f, 0.5f), new Vector2(260f, 16f), new Vector2(480f, 44f), 32, TextAnchor.MiddleLeft);
        n.fontStyle = FontStyle.Bold;
        var s = UIUtil.CreateText(bg.transform, sub, new Vector2(0f, 0.5f), new Vector2(260f, -22f), new Vector2(480f, 34f), 22, TextAnchor.MiddleLeft);
        s.color = Theme.TextDim;
        float x = -100f;
        for (int i = buttons.Length - 1; i >= 0; i--)
        {
            var rb = buttons[i];
            Text label;
            var b = UIUtil.CreateButton(bg.transform, rb.label, new Vector2(1f, 0.5f), new Vector2(x, 0f), new Vector2(180f, 72f), rb.color, false, 24, out label);
            if (rb.color == Theme.Accent || rb.color == Theme.Good)
            {
                label.color = new Color(0.08f, 0.08f, 0.06f);
                label.GetComponent<Shadow>().enabled = false;
            }
            var action = rb.action;
            b.onClick.AddListener(() => action());
            x -= 194f;
        }
        y += 106f;
        return bg;
    }

    // ----- Actions -----

    private void AddByCode()
    {
        string code = (addField.text ?? "").ToUpperInvariant().Replace("-", "").Replace(" ", "");
        if (code.Length != 6)
        {
            SetStatus("Kod 6 karakter olmalı (örn. ABC-123)");
            return;
        }
        if (code == OnlineService.AccountId)
        {
            SetStatus("Bu senin kendi kodun");
            return;
        }
        addField.text = "";
        OnlineService.AddFriend(code, Answer);
    }

    private void JoinRoom(string code)
    {
        Hide();
        var ui = GameManager.Instance.uiManager;
        ui.JoinRoomByCode(code);
    }

    private void OpenReport(string name, string account, string match)
    {
        reportTarget = account;
        reportMatch = match ?? "";
        reportReason = "";
        reportTitle.text = name + " şikayet ediliyor";
        reportField.text = "";
        SelectReason(-1);
        reportPanel.SetActive(true);
        reportPanel.transform.SetAsLastSibling();
    }

    private void SelectReason(int index)
    {
        reportReason = index >= 0 ? ReasonIds[index] : "";
        for (int i = 0; i < reasonButtons.Count; i++)
            reasonButtons[i].color = i == index ? new Color(0.75f, 0.45f, 0.1f, 0.95f) : Theme.Panel;
    }

    private void SendReport()
    {
        if (reportReason.Length == 0)
        {
            SetStatus("Bir sebep seç");
            return;
        }
        reportPanel.SetActive(false);
        OnlineService.ReportPlayer(reportTarget, reportReason, reportField.text, reportMatch, v => SetStatus(v.ok ? (v.message ?? "Şikayetin alındı") : v.error));
    }

    private void SendBug()
    {
        string text = (bugField.text ?? "").Trim();
        if (text.Length < 5)
        {
            SetStatus("Biraz daha ayrıntı yaz");
            return;
        }
        string log = bugLogs.isOn ? ErrorReporter.RecentLog(30000) : "";
        SetStatus("Gönderiliyor...");
        OnlineService.BugReport(text, log, v =>
        {
            SetStatus(v.ok ? (v.message ?? "Gönderildi, teşekkürler") : v.error);
            if (v.ok)
                bugField.text = "";
        });
    }
}
