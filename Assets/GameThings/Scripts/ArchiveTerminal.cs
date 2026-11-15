using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The Archivist's desk terminal, built to match the "glass panes" HTML version.
// LEFT page:  ARCHIVE CATALOG (section list, scrolling grid of records, scroll bar)
// RIGHT page: RECORD VIEWER   (the selected photo, its record and its line)
//
// Setup: make an empty "ScreenAnchor" on the front of each glass pane, with its blue
// arrow (Z) pointing AWAY from the player (into the glass). Drag them into Left/Right Page.
// The script builds both screens itself when you press Play.
//
// The screens are display only. Put TerminalKey on the BACK / PREV / NEXT keys.
// The slot: PolaroidFilingSlot (drag this terminal into it).
public class ArchiveTerminal : MonoBehaviour
{
    [Serializable]
    public class Section
    {
        public string title = "BEACH";
        [Tooltip("SHIP, SELF, CREW or STRANGER")]
        public string group = "SELF";
        [Tooltip("Must match the world's scene name (capitals don't matter).")]
        public string sceneName = "Memory_Beach";
        [Tooltip("How many story photos this world has.")]
        public int photoCount = 3;
    }

    [Header("Pages")]
    public Transform leftPage;
    public Transform rightPage;
    [Tooltip("Width of each screen in meters. Height follows (4:3).")]
    public float pageWidth = 0.38f;
    [Tooltip("Dark tint behind the text. Off = text floats right on the glass.")]
    public bool darkBackground = false;
    [Tooltip("Color of the screen text. Less important text uses a faded version of it.")]
    public Color textColor = Color.white;

    [Header("Catalog (in order)")]
    public List<Section> sections = new List<Section>
    {
        new Section { title = "SHIP",            group = "SHIP",     sceneName = "",                      photoCount = 12 },
        new Section { title = "BEACH",           group = "SELF",     sceneName = "Memory_Beach",          photoCount = 3 },
        new Section { title = "MOSS CITY",       group = "SELF",     sceneName = "Memory_MossCity",       photoCount = 3 },
        new Section { title = "SNOWY MOUNTAINS", group = "CREW",     sceneName = "Memory_SnowyMountains", photoCount = 4 },
        new Section { title = "SOLARPUNK CITY",  group = "CREW",     sceneName = "Memory_SolarpunkCity",  photoCount = 4 },
        new Section { title = "MARS COLONY",     group = "CREW",     sceneName = "Memory_MarsColony",     photoCount = 4 },
        new Section { title = "VAPORWAVE ZONE",  group = "STRANGER", sceneName = "Memory_VaporwaveZone",  photoCount = 3 },
        new Section { title = "GLITCH WORLD",    group = "STRANGER", sceneName = "Memory_GlitchWorld",    photoCount = 4 },
        new Section { title = "CANDY",           group = "STRANGER", sceneName = "Memory_Candy",          photoCount = 3 },
    };

    [Header("Fonts (optional, default TMP font if empty)")]
    [Tooltip("Headers. The HTML used Michroma.")]
    public TMP_FontAsset titleFont;
    [Tooltip("Everything else. The HTML used Share Tech Mono.")]
    public TMP_FontAsset bodyFont;

    [Header("Status lights (optional)")]
    [Tooltip("3 lights: always on, blinking, on while a record is open.")]
    public Renderer[] leds = new Renderer[3];
    public Material ledOnMaterial;
    public Material ledOffMaterial;

    [Header("Dial visual (optional)")]
    [Tooltip("Spins as the grid scrolls. Turns around its own Y axis.")]
    public Transform dialVisual;

    // ---------- Colors (from the HTML) ----------
    static readonly Color Cyan = Hex("#4fd1e0");
    static readonly Color Muted = Hex("#8aa0ae");
    static readonly Color Mid = Hex("#6f8796");
    static readonly Color Dim = Hex("#4d6372");
    static readonly Color Bright = Hex("#dbe7ef");
    static readonly Color Pale = Hex("#8fdbe6");
    static readonly Color Empty = Hex("#16242e");
    static readonly Color EmptyEdge = Hex("#2a3e4a");
    static readonly Color Paper = Hex("#eeeeea");
    static readonly Color Ink = Hex("#3a4654");
    static readonly Color PageBg = new Color(8 / 255f, 20 / 255f, 28 / 255f, 0.6f);
    static readonly Color Rule = new Color(79 / 255f, 209 / 255f, 224 / 255f, 0.3f);
    static readonly Color RuleSoft = new Color(79 / 255f, 209 / 255f, 224 / 255f, 0.2f);
    static readonly Color RowHi = new Color(79 / 255f, 209 / 255f, 224 / 255f, 0.15f);
    static readonly Color Clear = new Color(0, 0, 0, 0);

    // ---------- Layout (pixels on a 1200 x 900 page, same numbers as the HTML) ----------
    const float PW = 1200, PH = 900;
    const float GX = 330, GY = 112, GW = 840, GH = 770;
    const float TW = 96, TH = 117, GAP = 14, HEAD = 48, ROW = TH + GAP;
    static readonly int COLS = Mathf.FloorToInt((GW + GAP) / (TW + GAP));

    // ---------- Runtime data ----------
    class Slot
    {
        public CatalogSection sec;
        public int number;                    // REC-###
        public GameProgress.PhotoRecord rec;  // null = not filed yet
        public float y;                       // top, in grid content pixels
        public Image selectedEdge, highlightEdge;
    }

    class CatalogSection
    {
        public Section def;
        public List<Slot> slots = new List<Slot>();
        public float y;
        public int Filed { get { int n = 0; foreach (var s in slots) if (s.rec != null) n++; return n; } }
        public Image rowHighlight;
        public TextMeshProUGUI rowName, rowCount;
    }

    readonly List<CatalogSection> catalog = new List<CatalogSection>();
    readonly List<Slot> allSlots = new List<Slot>();
    float contentH, maxScroll, scroll, scrollTarget;
    Slot detail, highlight;
    float highlightUntil;
    string message; float messageUntil;

    // UI pieces
    RectTransform leftRoot, rightRoot, gridContent, sectionList;
    TextMeshProUGUI filedCount, messageText;
    Image scrollThumb;
    GameObject emptyView, detailView;
    RawImage detailPhoto; Image detailPaper, detailEmpty;
    TextMeshProUGUI detailCaption, detailId, detailWorld, detailGroup, detailStatus, detailLine;

    // ================================================================
    void Start()
    {
        if (leftPage == null || rightPage == null)
        {
            Debug.LogWarning("[ArchiveTerminal] Drag the two screen anchors into Left Page and Right Page.");
            return;
        }
        leftRoot = MakeCanvas(leftPage, "CatalogScreen");
        rightRoot = MakeCanvas(rightPage, "RecordScreen");
        BuildLeftPage();
        BuildRightPage();
        RebuildCatalog();
        ShowDetail(null);
    }

    void Update()
    {
        if (leftRoot == null) return;

        // Smooth scrolling, like the HTML (it eases toward the target).
        scroll = Mathf.Lerp(scroll, scrollTarget, 1f - Mathf.Exp(-10f * Time.deltaTime));
        gridContent.anchoredPosition = new Vector2(8, scroll);
        UpdateScrollBar();
        UpdateSectionList();

        if (dialVisual != null)
            dialVisual.localRotation = Quaternion.Euler(0, -scroll * 0.004f * Mathf.Rad2Deg, 0);

        // Fade the white "just filed" outline after 4 seconds.
        foreach (var s in allSlots)
            if (s.highlightEdge != null) s.highlightEdge.enabled = (s == highlight && Time.time < highlightUntil);

        messageText.text = (message != null && Time.time < messageUntil) ? message : "";

        // Status lights: 1 always on, 2 blinks, 3 on while a record is open.
        SetLed(0, true);
        SetLed(1, ((int)(Time.time / 0.6f)) % 2 == 1);
        SetLed(2, detail != null);
    }

    // ================================================================
    //  Public controls (keys, dial, slot)
    // ================================================================

    // BACK key: clears the viewer.
    public void Back() { ShowDetail(null); }

    // PREV / NEXT keys: step one record at a time (filed or not), like flipping through a card catalog.
    public void Next() { Step(1); }
    public void Prev() { Step(-1); }

    // Old names, so any keys already wired to these still work.
    public void NextSection() { Next(); }
    public void PrevSection() { Prev(); }

    void Step(int dir)
    {
        if (allSlots.Count == 0) return;

        int i;
        if (detail == null)
        {
            // Nothing open yet: start at the first record of the section you're looking at.
            i = allSlots.IndexOf(catalog[CurrentSection()].slots.Count > 0 ? catalog[CurrentSection()].slots[0] : allSlots[0]);
            if (dir < 0) i = Mathf.Max(0, i - 1);
        }
        else
        {
            i = Mathf.Clamp(allSlots.IndexOf(detail) + dir, 0, allSlots.Count - 1);
        }

        ShowDetail(allSlots[i]);
        KeepInView(allSlots[i]);
    }

    // Scrolls just enough to keep a record (and its section header, for the first row) on screen.
    void KeepInView(Slot s)
    {
        bool firstRow = s.y - s.sec.y <= HEAD + 1;
        float top = firstRow ? s.sec.y : s.y - 10;
        float bottom = s.y + TH + 10;
        if (top < scrollTarget) ScrollTo(top);
        else if (bottom > scrollTarget + GH) ScrollTo(bottom - GH);
    }

    // Dial / anything else: scroll the grid by this many pixels (positive = down).
    public void ScrollBy(float pixels) { ScrollTo(scrollTarget + pixels); }

    // Shows a short message top right of the record viewer, like "NO RECORD FOUND".
    public void Flash(string text, float seconds = 2.6f)
    {
        message = text;
        messageUntil = Time.time + seconds;
    }

    // PolaroidFilingSlot calls this after a story photo is filed.
    public void OnFiled(GameProgress.PhotoRecord record)
    {
        RebuildCatalog();
        Slot slot = allSlots.Find(s => s.rec != null && s.rec.photoId == record.photoId);
        if (slot == null) return;

        Flash("FILED " + RecId(slot.number));
        ShowDetail(slot);
        ScrollTo(slot.y - 200);
        highlight = slot;
        highlightUntil = Time.time + 4f;
    }

    // ================================================================
    //  Catalog data
    // ================================================================

    void RebuildCatalog()
    {
        string openId = detail != null && detail.rec != null ? detail.rec.photoId : null;

        catalog.Clear();
        allSlots.Clear();
        foreach (var def in sections) catalog.Add(new CatalogSection { def = def });

        // Put every filed record into its world's section (in filing order).
        foreach (var rec in GameProgress.FiledRecords)
        {
            CatalogSection sec = catalog.Find(c => Same(c.def.sceneName, rec.sceneName));
            if (sec == null)
            {
                // A world that isn't in the list yet: give it its own section at the end.
                sec = new CatalogSection { def = new Section { title = Pretty(rec.sceneName), group = "UNSORTED", sceneName = rec.sceneName, photoCount = 0 } };
                catalog.Add(sec);
            }
            sec.slots.Add(new Slot { sec = sec, rec = rec });
        }

        // Fill the rest of each section with empty (not yet filed) slots, and number everything.
        int number = 1;
        foreach (var sec in catalog)
        {
            while (sec.slots.Count < sec.def.photoCount) sec.slots.Add(new Slot { sec = sec });
            foreach (var s in sec.slots) { s.number = number++; allSlots.Add(s); }
        }

        BuildGrid();
        BuildSectionList();

        int filed = 0; foreach (var s in allSlots) if (s.rec != null) filed++;
        filedCount.text = filed + " / " + allSlots.Count + " FILED";

        // Keep the same record open after a rebuild.
        detail = openId != null ? allSlots.Find(s => s.rec != null && s.rec.photoId == openId) : null;
        ShowDetail(detail);
    }

    // ================================================================
    //  LEFT page: ARCHIVE CATALOG
    // ================================================================

    void BuildLeftPage()
    {
        if (darkBackground) Box("Background", leftRoot, 0, 0, PW, PH, PageBg);
        Label("Title", leftRoot, "ARCHIVE CATALOG", titleFont, 30, Cyan, 30, 62, 700);
        filedCount = Label("FiledCount", leftRoot, "", bodyFont, 26, Muted, 470, 62, 700, TextAlignmentOptions.TopRight);
        Box("Rule", leftRoot, 30, 84, 1140, 2, Rule);
        Box("Divider", leftRoot, 314, 100, 2, 790, RuleSoft);

        sectionList = Rect("Sections", leftRoot, 0, 0, 314, PH);

        // The grid scrolls inside a masked window.
        RectTransform window = Rect("GridWindow", leftRoot, GX - 8, GY, GW + 16, GH);
        window.gameObject.AddComponent<RectMask2D>();
        gridContent = Rect("GridContent", window, 8, 0, GW, GH);

        // Scroll bar
        Box("ScrollTrack", leftRoot, GX + GW + 12, GY, 6, GH, Empty);
        scrollThumb = Box("ScrollThumb", leftRoot, GX + GW + 12, GY, 6, 100, Cyan);
    }

    void BuildSectionList()
    {
        foreach (Transform c in sectionList) Destroy(c.gameObject);

        for (int i = 0; i < catalog.Count; i++)
        {
            CatalogSection sec = catalog[i];
            float baseY = 128 + i * 26;
            sec.rowHighlight = Box("Row " + sec.def.title, sectionList, 24, baseY - 19, 282, 25, Clear, true);
            sec.rowName = Label("Name", sec.rowHighlight.rectTransform, sec.def.title, bodyFont, 18, Muted, 8, 19, 200);
            sec.rowCount = Label("Count", sec.rowHighlight.rectTransform, "", bodyFont, 18, Muted, 160, 19, 114, TextAlignmentOptions.TopRight);
            sec.rowCount.text = sec.Filed + "/" + sec.slots.Count;

        }
    }

    void UpdateSectionList()
    {
        // Highlight the section of the record that's open. With nothing open,
        // fall back to whichever section is at the top of the grid.
        int cur = detail != null ? catalog.IndexOf(detail.sec) : CurrentSection();
        if (cur < 0) cur = CurrentSection();
        for (int i = 0; i < catalog.Count; i++)
        {
            CatalogSection sec = catalog[i];
            if (sec.rowHighlight == null) continue;
            bool on = i == cur;
            sec.rowHighlight.color = on ? RowHi : Clear;
            Color c = TextCol(on ? Cyan : (sec.Filed > 0 ? Muted : Dim));
            sec.rowName.color = c; sec.rowCount.color = c;
        }
    }

    void BuildGrid()
    {
        foreach (Transform c in gridContent) Destroy(c.gameObject);

        contentH = 0;
        foreach (var sec in catalog)
        {
            sec.y = contentH;

            // Section header: "BEACH   1 OF 3"
            var head = Label("Head " + sec.def.title, gridContent, sec.def.title, titleFont, 22, Cyan, 0, contentH + 30, 600);
            head.ForceMeshUpdate();
            float hw = head.preferredWidth;
            Label("HeadCount", gridContent, sec.Filed + " OF " + sec.slots.Count, bodyFont, 19, Mid, hw + 24, contentH + 30, 200);
            contentH += HEAD;

            for (int k = 0; k < sec.slots.Count; k++)
            {
                Slot s = sec.slots[k];
                float x = (k % COLS) * (TW + GAP);
                s.y = contentH + Mathf.Floor(k / (float)COLS) * ROW;
                BuildThumb(s, x, s.y);
            }
            contentH += Mathf.Ceil(sec.slots.Count / (float)COLS) * ROW + 14;
        }

        gridContent.sizeDelta = new Vector2(GW, Mathf.Max(contentH, GH));
        maxScroll = Mathf.Max(0, contentH - GH);
        ScrollTo(scrollTarget);
    }

    void BuildThumb(Slot s, float x, float y)
    {
        // Outlines sit behind the card, so they show as a border around it.
        s.highlightEdge = Box("Highlight", gridContent, x - 6, y - 6, TW + 12, TH + 12, Bright);
        s.highlightEdge.enabled = false;
        s.selectedEdge = Box("Selected", gridContent, x - 3, y - 3, TW + 6, TH + 6, Cyan);
        s.selectedEdge.enabled = false;

        Image card;
        if (s.rec != null)
        {
            // A small polaroid: white paper, square photo, REC id in the bottom margin.
            card = Box("Rec " + s.number, gridContent, x, y, TW, TH, Paper, true);
            float inset = TW * 12f / 256f, size = TW * 232f / 256f;
            RawImage img = Photo("Photo", card.rectTransform, inset, inset, size, size, s.rec.photoId);
            img.raycastTarget = false;
            Label("Id", card.rectTransform, RecId(s.number), bodyFont, 13, Ink, 6, TH - 8, TW - 8);
        }
        else
        {
            // Empty slot: dark card with an outline.
            card = Box("Empty " + s.number, gridContent, x, y, TW, TH, EmptyEdge, true);
            Box("Inner", card.rectTransform, 5, 5, TW - 10, TH - 10, Empty);
            Label("Id", card.rectTransform, RecId(s.number), bodyFont, 13, Dim, 6, TH - 8, TW - 8);
        }

    }

    void UpdateScrollBar()
    {
        float contentTotal = Mathf.Max(contentH, GH);
        float barH = GH * GH / contentTotal;
        float t = maxScroll > 0 ? scroll / maxScroll : 0;
        RectTransform rt = scrollThumb.rectTransform;
        rt.sizeDelta = new Vector2(6, barH);
        rt.anchoredPosition = new Vector2(GX + GW + 12, -(GY + (GH - barH) * t));
    }

    void ScrollTo(float y) { scrollTarget = Mathf.Clamp(y, 0, maxScroll); }

    int CurrentSection()
    {
        int i = 0;
        for (int k = 0; k < catalog.Count; k++) if (catalog[k].y <= scrollTarget + 4) i = k;
        return i;
    }

    // ================================================================
    //  RIGHT page: RECORD VIEWER
    // ================================================================

    void BuildRightPage()
    {
        if (darkBackground) Box("Background", rightRoot, 0, 0, PW, PH, PageBg);
        Label("Title", rightRoot, "RECORD VIEWER", titleFont, 30, Cyan, 30, 62, 700);
        messageText = Label("Message", rightRoot, "", bodyFont, 26, Bright, 470, 62, 700, TextAlignmentOptions.TopRight);
        Box("Rule", rightRoot, 30, 84, 1140, 2, Rule);

        // Nothing selected
        emptyView = Rect("EmptyView", rightRoot, 0, 0, PW, PH).gameObject;
        Label("Select", emptyView.transform, "SELECT A RECORD", titleFont, 38, Pale, 30, 300, 1100);
        var hint = Label("Hint", emptyView.transform, "Choose a photo on the left page, or file a new photograph in the slot.", bodyFont, 28, Mid, 30, 360, 900);
        hint.lineSpacing = 15;

        // A record selected
        detailView = Rect("DetailView", rightRoot, 0, 0, PW, PH).gameObject;
        Transform d = detailView.transform;

        // Big polaroid (600 x 731), same proportions as the cards on the desk.
        detailPaper = Box("Paper", d, 30, 120, 600, 731, Paper);
        float inset = 600f * 12f / 256f, size = 600f * 232f / 256f;
        detailPhoto = Photo("Photo", detailPaper.rectTransform, inset, inset, size, size, null);
        detailCaption = Label("Caption", detailPaper.rectTransform, "", bodyFont, 37, Ink, 33, 656, 540);

        // Empty slot version
        detailEmpty = Box("EmptyCard", d, 30, 120, 600, 731, EmptyEdge);
        Box("Inner", detailEmpty.rectTransform, 10, 10, 580, 711, Empty);

        float tx = 670;
        detailId = Label("Id", d, "", titleFont, 34, Cyan, tx, 170, 500);
        detailWorld = Label("World", d, "", bodyFont, 26, Mid, tx, 216, 500);
        detailGroup = Label("Group", d, "", bodyFont, 26, Mid, tx, 252, 500);
        detailStatus = Label("Status", d, "", bodyFont, 26, Mid, tx, 288, 500);
        Box("Rule", d, tx, 314, 490, 2, new Color(79 / 255f, 209 / 255f, 224 / 255f, 0.25f));
        detailLine = Label("Line", d, "", bodyFont, 30, Bright, tx, 366, 490);
        detailLine.rectTransform.sizeDelta = new Vector2(490, 500);
        detailLine.lineSpacing = 15;
    }

    void ShowDetail(Slot s)
    {
        if (detail != null && detail.selectedEdge != null) detail.selectedEdge.enabled = false;
        detail = s;
        if (detail != null && detail.selectedEdge != null) detail.selectedEdge.enabled = true;

        if (emptyView == null) return;
        emptyView.SetActive(s == null);
        detailView.SetActive(s != null);
        if (s == null) return;

        string world = s.sec.def.title.ToUpper();
        bool filed = s.rec != null;

        detailPaper.gameObject.SetActive(filed);
        detailEmpty.gameObject.SetActive(!filed);
        if (filed)
        {
            SetPhoto(detailPhoto, s.rec.photoId);
            detailCaption.text = RecId(s.number) + " // " + world;
        }

        detailId.text = RecId(s.number);
        detailWorld.text = world;
        detailGroup.text = s.sec.def.group.ToUpper();
        detailStatus.text = filed ? "STATUS: FILED" : "STATUS: NOT YET FILED";
        detailLine.text = filed ? s.rec.line : "No visual record on file.";
        detailLine.color = TextCol(filed ? Bright : Dim);
    }

    // ================================================================
    //  UI helpers (everything is placed from the top-left, in page pixels)
    // ================================================================

    RectTransform MakeCanvas(Transform anchor, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas));
        var rt = (RectTransform)go.transform;

        // The screen is NOT parented to the anchor. It just copies the anchor's position
        // and rotation. That way a scaled or flipped desk (common with Blender imports)
        // can't stretch it: Page Width is always the real size in meters.
        rt.SetParent(null, false);
        rt.position = anchor.position;
        rt.rotation = anchor.rotation;
        rt.sizeDelta = new Vector2(PW, PH);
        rt.localScale = Vector3.one * (pageWidth / PW);

        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;

        // A page-sized root that everything else is placed in.
        RectTransform root = Rect("Page", rt, 0, 0, PW, PH);
        return root;
    }

    static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
        return rt;
    }

    static Image Box(string name, Transform parent, float x, float y, float w, float h, Color color, bool clickable = false)
    {
        var img = Rect(name, parent, x, y, w, h).gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = clickable;
        return img;
    }

    // y is the text BASELINE, like the HTML's fillText, so the numbers match.
    TextMeshProUGUI Label(string name, Transform parent, string text, TMP_FontAsset font, float size, Color color,
                          float x, float baseline, float width, TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
    {
        var t = Rect(name, parent, x, baseline - size * 0.95f, width, size * 1.6f).gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = TextCol(color);
        t.alignment = align;
        t.enableWordWrapping = true;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static RawImage Photo(string name, Transform parent, float x, float y, float w, float h, string photoId)
    {
        var img = Rect(name, parent, x, y, w, h).gameObject.AddComponent<RawImage>();
        img.raycastTarget = false;
        SetPhoto(img, photoId);
        return img;
    }

    // Shows the saved photo, cropped to a square from the middle (like the HTML cards).
    static void SetPhoto(RawImage img, string photoId)
    {
        Texture2D tex = string.IsNullOrEmpty(photoId) ? null : GameProgress.GetPhoto(photoId);
        img.texture = tex;
        img.color = tex != null ? Color.white : Ink;
        if (tex == null) { img.uvRect = new UnityEngine.Rect(0, 0, 1, 1); return; }

        float a = tex.width / (float)tex.height;
        img.uvRect = a >= 1f
            ? new UnityEngine.Rect((1f - 1f / a) / 2f, 0, 1f / a, 1)
            : new UnityEngine.Rect(0, (1f - a) / 2f, 1, a);
    }

    void SetLed(int i, bool on)
    {
        if (leds == null || i >= leds.Length || leds[i] == null) return;
        Material m = on ? ledOnMaterial : ledOffMaterial;
        if (m != null && leds[i].sharedMaterial != m) leds[i].sharedMaterial = m;
    }

    // Turns the HTML's text colors into the chosen text color (white by default):
    // main text at full strength, secondary text a bit faded, "not filed yet" text more faded.
    // Text printed on the white polaroid paper stays dark.
    Color TextCol(Color original)
    {
        if (original == Ink) return Ink;
        Color c = textColor;
        if (original == Mid) c.a *= 0.75f;
        else if (original == Dim) c.a *= 0.45f;
        return c;
    }

    static string RecId(int n) => "REC-" + n.ToString("000");

    static bool Same(string a, string b) =>
        string.Equals((a ?? "").Trim(), (b ?? "").Trim(), StringComparison.OrdinalIgnoreCase);

    // "Memory_CyberpunkCity" -> "CYBERPUNK CITY"
    static string Pretty(string scene)
    {
        if (string.IsNullOrEmpty(scene)) return "UNKNOWN";
        if (scene.StartsWith("Memory_")) scene = scene.Substring(7);
        scene = Regex.Replace(scene.Replace('_', ' '), "(?<=[a-z])(?=[A-Z])", " ");
        return scene.Trim().ToUpper();
    }

    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out Color c); return c; }
}