using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Friends and community screen: your friend code, adding friends by code, requests, invites to a
/// private room; finding anyone by name or code and following them (followers / following); private
/// messages; gifts received (opened with the box animation) and sent; players you played with
/// (add / report / block / mute), the blocked list, and the bug report form. Works over the lobby and,
/// from the pause menu, during an online match.
/// </summary>
public class SocialScreen : MonoBehaviour
{
    public enum Tab { Friends, Follow, Messages, Gifts, Players, Bug }

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
    private Text reportTitle, reportHint;
    private InputField reportField;
    private readonly List<Image> reasonButtons = new List<Image>();
    private string reportTarget = "", reportMatch = "", reportReason = "";
    private float nextRefresh;
    private bool busy;
    private readonly List<NetClient.MetPlayer> scratch = new List<NetClient.MetPlayer>();
    private GameObject searchHeader;
    private InputField searchField;
    private OnlineService.PlayersView follows, found;
    private OnlineService.ChatView threads;
    private OnlineService.GiftsView gifts;
    private readonly List<Text> tabBadges = new List<Text>();
    // chat
    private GameObject chatPanel;
    private Text chatTitle, chatState;
    private RectTransform chatContent;
    private ScrollRect chatScroll;
    private InputField chatField;
    private string chatWith = "", chatName = "";
    private long chatLast;
    private float nextChatPoll;
    private bool chatBusy;

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

        string[] tabs = { "ARKADAŞLAR", "TAKİP", "MESAJLAR", "HEDİYELER", "OYUNCULAR", "HATA BİLDİR" };
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            var b = UIUtil.CreateButton(t, tabs[i], new Vector2(0.5f, 1f), new Vector2(-650f + i * 262f, -70f), new Vector2(250f, 80f), Theme.Panel, false, 25, out label);
            b.onClick.AddListener(() => Open((Tab)index, onClose));
            tabImages.Add(b.GetComponent<Image>());
            tabLabels.Add(label);
            var badge = UIUtil.CreateImage(b.transform, "Badge", new Vector2(1f, 1f), new Vector2(-6f, -6f), new Vector2(38f, 38f), Theme.Red, true);
            badge.raycastTarget = false;
            var badgeText = UIUtil.CreateText(badge.transform, "", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(38f, 38f), 20, TextAnchor.MiddleCenter);
            tabBadges.Add(badgeText);
            badge.gameObject.SetActive(false);
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

        // Search players (TAKİP)
        searchHeader = UIUtil.CreateRect(t, "SearchHeader", new Vector2(0.5f, 1f), new Vector2(0f, -185f), new Vector2(1500f, 110f)).gameObject;
        var sh = searchHeader.transform;
        var sbg = UIUtil.CreateImage(sh, "Search", new Vector2(0f, 0.5f), new Vector2(520f, 0f), new Vector2(1040f, 90f), new Color(1f, 1f, 1f, 0.1f), false);
        searchField = MakeInput(sbg, "Oyuncu ara: isim ya da arkadaş kodu (ABC-123)", 16, 36);
        var go = UIUtil.CreateButton(sh, "ARA", new Vector2(1f, 0.5f), new Vector2(-230f, 0f), new Vector2(220f, 90f), Theme.Accent, false, 34, out label);
        label.color = new Color(0.1f, 0.08f, 0.02f);
        label.GetComponent<Shadow>().enabled = false;
        go.onClick.AddListener(DoSearch);

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
        BuildChat();
    }

    private void BuildChat()
    {
        Text label;
        var c = new Vector2(0.5f, 0.5f);
        var dim = UIUtil.CreateStretch(transform, "Chat");
        dim.gameObject.AddComponent<Image>().color = new Color(0.02f, 0.03f, 0.06f, 0.98f);
        chatPanel = dim.gameObject;
        var back = UIUtil.CreateButton(dim, "<", new Vector2(0f, 1f), new Vector2(80f, -70f), new Vector2(84f, 84f), Theme.Accent, false, 54, out label);
        label.color = new Color(0.1f, 0.1f, 0.1f);
        label.GetComponent<Shadow>().enabled = false;
        back.onClick.AddListener(CloseChat);
        Icons.Create(dim, "chat", new Vector2(0f, 1f), new Vector2(190f, -70f), new Vector2(80f, 80f));
        chatTitle = UIUtil.CreateText(dim, "", new Vector2(0f, 1f), new Vector2(560f, -56f), new Vector2(700f, 56f), 42, TextAnchor.MiddleLeft);
        chatTitle.fontStyle = FontStyle.Bold;
        chatState = UIUtil.CreateText(dim, "", new Vector2(0f, 1f), new Vector2(560f, -100f), new Vector2(700f, 34f), 24, TextAnchor.MiddleLeft);
        chatState.color = Theme.TextDim;
        var gift = UIUtil.CreateButton(dim, "HEDİYE", new Vector2(1f, 1f), new Vector2(-140f, -70f), new Vector2(220f, 80f), Theme.PanelLight, false, 28, out label);
        gift.onClick.AddListener(() => GiftPanel.ForPerson(chatWith, chatName, null));

        var view = UIUtil.CreateRect(dim, "Messages", c, new Vector2(0f, 0f), new Vector2(1500f, 700f));
        view.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.03f);
        view.gameObject.AddComponent<RectMask2D>();
        chatContent = UIUtil.CreateRect(view, "Content", new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1500f, 10f));
        chatContent.pivot = new Vector2(0.5f, 1f);
        chatScroll = view.gameObject.AddComponent<ScrollRect>();
        chatScroll.content = chatContent;
        chatScroll.viewport = view;
        chatScroll.horizontal = false;
        chatScroll.movementType = ScrollRect.MovementType.Clamped;
        chatScroll.scrollSensitivity = 40f;

        var fbg = UIUtil.CreateImage(dim, "Field", new Vector2(0.5f, 0f), new Vector2(-150f, 70f), new Vector2(1200f, 90f), new Color(1f, 1f, 1f, 0.1f), false);
        chatField = MakeInput(fbg, "Mesaj yaz...", 200, 34);
        var send = UIUtil.CreateButton(dim, "GÖNDER", new Vector2(0.5f, 0f), new Vector2(600f, 70f), new Vector2(280f, 90f), Theme.Accent, false, 34, out label);
        label.color = new Color(0.1f, 0.08f, 0.02f);
        label.GetComponent<Shadow>().enabled = false;
        send.onClick.AddListener(SendChat);
        chatPanel.SetActive(false);
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
        reportHint = UIUtil.CreateText(box, "Sebep seç", c, new Vector2(0f, 270f), new Vector2(1000f, 40f), 26, TextAnchor.MiddleCenter);
        reportHint.color = Theme.TextDim;
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
        searchHeader.SetActive(tab == Tab.Follow);
        listView.SetActive(tab != Tab.Bug);
        bugGroup.SetActive(tab == Tab.Bug);
        chatPanel.SetActive(false);
        bool header = tab == Tab.Friends || tab == Tab.Follow;
        var lr = (RectTransform)listView.transform;
        lr.anchoredPosition = header ? new Vector2(0f, -110f) : new Vector2(0f, -20f);
        lr.sizeDelta = header ? new Vector2(1500f, 660f) : new Vector2(1500f, 800f);
        scroll.verticalNormalizedPosition = 1f;
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
        if (chatPanel.activeSelf)
        {
            if (Time.unscaledTime >= nextChatPoll && !chatBusy)
                PollChat();
            return;
        }
        if (tab != Tab.Bug && Time.unscaledTime >= nextRefresh && !busy)
            Refresh();
    }

    private void Refresh()
    {
        nextRefresh = Time.unscaledTime + 8f;
        busy = true;
        // the tab's own list
        if (tab == Tab.Follow)
            OnlineService.Follows(v => { follows = v; if (gameObject.activeSelf) Rebuild(); });
        else if (tab == Tab.Messages)
            OnlineService.Threads(v => { threads = v; if (gameObject.activeSelf) Rebuild(); });
        else if (tab == Tab.Gifts)
            OnlineService.GiftInbox(v => { gifts = v; if (gameObject.activeSelf) Rebuild(); });
        var net = NetClient.Instance;
        bool room = net != null && net.State == NetClient.Phase.Lobby && net.PrivateRoom;
        // During a match the game server reports "in a match": send no status then.
        string status = InMatch ? "" : room ? "room" : "lobby";
        OnlineService.RefreshSocial(status, room ? net.RoomCode : "", net != null ? net.Mode : MatchMode.Solo, view =>
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

    private void Badges()
    {
        var sv = OnlineService.Social;
        int[] counts = new int[tabBadges.Count];
        if (sv != null)
        {
            counts[(int)Tab.Friends] = sv.incoming.Length + sv.invites.Length;
            counts[(int)Tab.Messages] = sv.unread;
            counts[(int)Tab.Gifts] = sv.gifts;
        }
        for (int i = 0; i < tabBadges.Count; i++)
        {
            tabBadges[i].transform.parent.gameObject.SetActive(counts[i] > 0);
            tabBadges[i].text = counts[i].ToString();
        }
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
        Badges();
        if (tab == Tab.Follow)
            BuildFollow();
        else if (tab == Tab.Messages)
            BuildThreads();
        else if (tab == Tab.Gifts)
            BuildGifts();

        if (tab == Tab.Friends)
        {
            if (social == null)
            {
                Section(OnlineService.HasAccount ? "Yükleniyor..." : "Bağlanılıyor...");
            }
            else
            {
                if (social.invites.Length > 0 && !InMatch)
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
                    if (!InMatch && p.online && p.status == "room" && p.room.Length == 6 && !(inRoom && net.RoomCode == p.room))
                        buttons.Add(Btn("KATIL", Theme.Accent, () => JoinRoom(p.room)));
                    if (p.online && inRoom)
                        buttons.Add(Btn("DAVET ET", new Color(0.16f, 0.45f, 0.95f, 0.95f), () => OnlineService.Invite(p.id, net.RoomCode, net.Mode, v => SetStatus(v.ok ? p.name + " davet edildi" : v.error))));
                    buttons.Add(Btn("MESAJ", new Color(0.2f, 0.55f, 0.35f, 0.95f), () => OpenChat(p.id, p.name)));
                    buttons.Add(Btn("HEDİYE", new Color(0.75f, 0.3f, 0.5f, 0.95f), () => GiftPanel.ForPerson(p.id, p.name, null)));
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

    // ----- Follow / search -----

    private void DoSearch()
    {
        string q = (searchField.text ?? "").Trim();
        if (q.Length < 2)
        {
            SetStatus("En az 2 harf yaz");
            return;
        }
        SetStatus("Aranıyor...");
        OnlineService.Search(q, v =>
        {
            found = v;
            SetStatus(v.ok ? (v.people.Length == 0 ? "Kimse bulunamadı" : "") : v.error);
            if (gameObject.activeSelf)
                Rebuild();
        });
    }

    private void BuildFollow()
    {
        if (found != null && found.ok && found.people.Length > 0)
        {
            Section("ARAMA SONUÇLARI");
            foreach (var p in found.people)
                PlayerRow(p);
        }
        if (follows == null)
        {
            Section("Yükleniyor...");
            return;
        }
        if (!follows.ok)
        {
            Note(follows.error);
            return;
        }
        Section("TAKİP ETTİKLERİN  " + follows.following.Length);
        if (follows.following.Length == 0)
            Note("Kimseyi takip etmiyorsun. Yukarıdan isim ya da kodla oyuncu ara.");
        foreach (var p in follows.following)
            PlayerRow(p);
        Section("TAKİPÇİLERİN  " + follows.followers.Length);
        if (follows.followers.Length == 0)
            Note("Henüz takipçin yok. Kodunu paylaş: " + OnlineService.FriendCode(OnlineService.AccountId));
        foreach (var p in follows.followers)
            PlayerRow(p);
    }

    private void PlayerRow(OnlineService.Player pl)
    {
        var p = pl;
        string sub = p.followers + " takipçi" + (p.friend ? "  •  arkadaşın" : "") + (p.followsMe ? "  •  seni takip ediyor" : "") +
                     (p.online ? "  •  çevrimiçi" : "");
        var buttons = new List<RowButton>();
        buttons.Add(p.followed
            ? Btn("BIRAK", Theme.PanelLight, () => OnlineService.Follow(p.id, false, v => { follows = v; p.followed = false; Rebuild(); }))
            : Btn("TAKİP ET", Theme.Accent, () => OnlineService.Follow(p.id, true, v =>
            {
                follows = v;
                p.followed = true;
                SetStatus(v.ok ? p.name + " takip ediliyor" : v.error);
                Rebuild();
            })));
        if (!p.friend)
            buttons.Add(Btn("EKLE", Theme.Good, () => OnlineService.AddFriend(p.id, Answer)));
        if (p.friend || (p.followed && p.followsMe))
        {
            buttons.Add(Btn("MESAJ", new Color(0.2f, 0.55f, 0.35f, 0.95f), () => OpenChat(p.id, p.name)));
            buttons.Add(Btn("HEDİYE", new Color(0.75f, 0.3f, 0.5f, 0.95f), () => GiftPanel.ForPerson(p.id, p.name, null)));
        }
        Row(p.name, sub, buttons.ToArray()).color = p.online ? new Color(0.1f, 0.2f, 0.14f, 0.9f) : Theme.Panel;
    }

    // ----- Messages -----

    private void BuildThreads()
    {
        Section("MESAJLAR");
        Note("Arkadaşlarına ve karşılıklı takipleştiğin oyunculara yazabilirsin. Küfürler yıldızlanır.");
        if (threads == null)
        {
            Section("Yükleniyor...");
            return;
        }
        if (!threads.ok)
        {
            Note(threads.error);
            return;
        }
        if (threads.threads.Length == 0)
            Note("Henüz mesajın yok. ARKADAŞLAR'da bir arkadaşının yanındaki MESAJ'a bas.");
        foreach (var th in threads.threads)
        {
            var p = th;
            string last = (p.mine ? "Sen: " : "") + p.last;
            if (last.Length > 46)
                last = last.Substring(0, 46) + "...";
            var row = Row(p.name + (p.unread > 0 ? "   (" + p.unread + " yeni)" : ""), last, Btn("AÇ", Theme.Accent, () => OpenChat(p.id, p.name)));
            if (p.unread > 0)
                row.color = new Color(0.12f, 0.2f, 0.32f, 0.95f);
        }
    }

    public void OpenChat(string id, string name)
    {
        chatWith = id;
        chatName = name;
        chatLast = 0;
        chatTitle.text = name;
        chatState.text = "";
        chatField.text = "";
        for (int i = chatContent.childCount - 1; i >= 0; i--)
            Destroy(chatContent.GetChild(i).gameObject);
        chatY = 0f;
        chatPanel.SetActive(true);
        chatPanel.transform.SetAsLastSibling();
        PollChat();
    }

    private void CloseChat()
    {
        chatPanel.SetActive(false);
        Refresh();
    }

    private void PollChat()
    {
        chatBusy = true;
        nextChatPoll = Time.unscaledTime + 3f;
        string with = chatWith;
        OnlineService.Thread(with, chatLast, v => ShowChat(with, v));
    }

    private void ShowChat(string with, OnlineService.ChatView v)
    {
        chatBusy = false;
        if (this == null || !chatPanel.activeSelf || with != chatWith)
            return;
        if (!v.ok)
        {
            chatState.text = v.error;
            return;
        }
        if (v.with != null)
            chatState.text = (v.with.online ? "çevrimiçi" : "çevrimdışı") + (v.canTalk ? "" : "  •  yazmak için arkadaş olun ya da karşılıklı takipleşin");
        bool added = false;
        foreach (var m in v.messages)
        {
            if (m.id <= chatLast)
                continue;
            chatLast = m.id;
            Bubble(m.text, m.mine, m.time);
            added = true;
        }
        if (added)
        {
            chatContent.sizeDelta = new Vector2(1500f, chatY + 20f);
            Canvas.ForceUpdateCanvases();
            chatScroll.verticalNormalizedPosition = 0f;
        }
    }

    private float chatY;

    private void Bubble(string text, bool mine, long time)
    {
        int lines = Mathf.Max(1, Mathf.CeilToInt(text.Length / 46f));
        float h = 30f + lines * 38f + 26f;
        var bg = UIUtil.CreateImage(chatContent, "Bubble", new Vector2(mine ? 1f : 0f, 1f), new Vector2(mine ? -400f : 400f, -chatY - h * 0.5f - 10f),
            new Vector2(760f, h), mine ? new Color(0.16f, 0.42f, 0.26f, 0.95f) : new Color(0.18f, 0.2f, 0.26f, 0.95f), false);
        var t = UIUtil.CreateText(bg.transform, text, new Vector2(0.5f, 1f), new Vector2(0f, -16f - lines * 19f), new Vector2(720f, lines * 38f), 30, TextAnchor.MiddleLeft);
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.supportRichText = false;
        var when = System.DateTimeOffset.FromUnixTimeSeconds(time).ToLocalTime();
        var tm = UIUtil.CreateText(bg.transform, when.ToString("HH:mm"), new Vector2(1f, 0f), new Vector2(-50f, 16f), new Vector2(90f, 24f), 18, TextAnchor.MiddleRight);
        tm.color = Theme.TextDim;
        chatY += h + 14f;
    }

    private void SendChat()
    {
        string text = (chatField.text ?? "").Trim();
        if (text.Length == 0)
            return;
        chatField.text = "";
        string with = chatWith;
        OnlineService.SendMessage(with, text, v =>
        {
            if (!v.ok)
            {
                if (this != null && chatPanel.activeSelf)
                    chatState.text = v.error;
                return;
            }
            ShowChat(with, v);
        });
    }

    // ----- Gifts -----

    private void BuildGifts()
    {
        Section("GELEN HEDİYELER");
        if (gifts == null)
        {
            Section("Yükleniyor...");
            return;
        }
        if (!gifts.ok)
        {
            Note(gifts.error);
            return;
        }
        if (gifts.gifts.Length == 0)
            Note("Şu an açılmamış hediyen yok. Arkadaşlarına sen de hediye gönderebilirsin (ARKADAŞLAR → HEDİYE).");
        foreach (var g in gifts.gifts)
        {
            var gift = g;
            var r = Shop.FromGift(gift);
            Row(gift.name + " sana hediye gönderdi", r.Name + (string.IsNullOrEmpty(gift.note) ? "" : "  •  \"" + gift.note + "\""),
                Btn("AÇ", Theme.Accent, () => ClaimGift(gift)));
        }
        Section("HEDİYE GÖNDER");
        Note("ARKADAŞLAR ya da TAKİP sekmesinde birinin yanındaki HEDİYE'ye bas; MAĞAZA'daki eşyaların yanındaki hediye simgesiyle de gönderebilirsin.");
    }

    private void ClaimGift(OnlineService.Gift g)
    {
        SetStatus("Açılıyor...");
        OnlineService.ClaimGift(g.id, v =>
        {
            gifts = v;
            if (!v.ok || v.claimed == null)
            {
                SetStatus(v.ok ? "" : v.error);
                if (gameObject.activeSelf) Rebuild();
                return;
            }
            SetStatus("");
            var p = GameManager.Instance.profile;
            var r = Shop.FromGift(v.claimed);
            string crate;
            List<GrantedReward> given;
            if (r.kind == RewardKind.Crate)
            {
                crate = r.id;
                Shop.AddCrate(crate, 1);
                given = Shop.OpenCrate(p, crate);
            }
            else
            {
                // Kredi, a character or a camo: still unwrapped from a box.
                crate = r.kind == RewardKind.Skin ? "gold" : r.kind == RewardKind.Credits ? "bronze" : "silver";
                given = new List<GrantedReward> { Shop.Give(p, r) };
            }
            string from = string.IsNullOrEmpty(g.name) ? "Arkadaşın" : g.name;
            CrateOpening.Show(crate, given, from.ToUpperInvariant() + " SANA HEDİYE GÖNDERDİ", () => { if (this != null && gameObject.activeSelf) Refresh(); });
            if (gameObject.activeSelf)
                Rebuild();
        });
    }

    /// <summary>Playing a match (online or with bots): joining another room from here is not allowed.</summary>
    private static bool InMatch
    {
        get
        {
            var gm = GameManager.Instance;
            return gm != null && gm.currentState != GameState.Lobby;
        }
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
        var n = UIUtil.CreateText(bg.transform, name, new Vector2(0f, 0.5f), new Vector2(255f, 16f), new Vector2(470f, 44f), 32, TextAnchor.MiddleLeft);
        n.fontStyle = FontStyle.Bold;
        var s = UIUtil.CreateText(bg.transform, sub, new Vector2(0f, 0.5f), new Vector2(255f, -22f), new Vector2(470f, 34f), 22, TextAnchor.MiddleLeft);
        s.color = Theme.TextDim;
        float x = -100f;
        for (int i = buttons.Length - 1; i >= 0; i--)
        {
            var rb = buttons[i];
            Text label;
            var b = UIUtil.CreateButton(bg.transform, rb.label, new Vector2(1f, 0.5f), new Vector2(x, 0f), new Vector2(150f, 72f), rb.color, false, 22, out label);
            if (rb.color == Theme.Accent || rb.color == Theme.Good)
            {
                label.color = new Color(0.08f, 0.08f, 0.06f);
                label.GetComponent<Shadow>().enabled = false;
            }
            var action = rb.action;
            b.onClick.AddListener(() => action());
            x -= 160f;
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
        reportHint.text = "Sebep seç";
        reportHint.color = Theme.TextDim;
        for (int i = 0; i < reasonButtons.Count; i++)
            reasonButtons[i].color = i == index ? new Color(0.75f, 0.45f, 0.1f, 0.95f) : Theme.Panel;
    }

    private void SendReport()
    {
        if (reportReason.Length == 0)
        {
            reportHint.text = "Önce bir sebep seç";
            reportHint.color = Theme.Bad;
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
