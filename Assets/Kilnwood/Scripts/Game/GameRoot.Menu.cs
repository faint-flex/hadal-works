using System.Collections.Generic;
using System.Text;
using Kilnwood.Sim;
using UnityEngine;

namespace Kilnwood.Game
{
    /// <summary>
    /// Build menu and research panel. 0.23: the build menu has a tab per category (Extraction, Production, Logistics,
    /// Power, Homes), a search box, and a hotbar row of 8 pinned buildings (keys 1-8; right-click a tile to pin or unpin).
    /// B collapses it to just the hotbar. Tiles show the building's real model, name and cost (locked: "Tier N"),
    /// and a details card on hover: cost, power, recipes.
    /// The research panel (T) shows the tier tree: each tier's goal with progress bars, and what it unlocks.
    /// Every state has words as well as colour (DONE / NOW / LOCKED, numbers on every bar).
    /// </summary>
    public partial class GameRoot
    {
        bool _research;
        static readonly string[] TierNames = { "", "First Fire", "Water Power", "Steam", "Rails", "Electricity", "Machine Age", "Assembly Line", "Flight", "Airwaves", "Drones", "Next era" };
        List<BuildingType> _iconTypes;
        GUIStyle _searchStyle, _tile, _tileOn, _tileLocked, _tileText, _tileTextOn, _tileTextLocked, _tiny, _tinyDim, _tab, _tabOn, _tabLocked, _cardTitle;
        Texture2D _barBack, _barFill, _barDone, _line;

        List<BuildingType> IconTypes()
        {
            if (_iconTypes != null) return _iconTypes;
            _iconTypes = new List<BuildingType>();
            foreach (var t in AllBuildTools()) _iconTypes.Add(TypeOf(t));
            return _iconTypes;
        }

        /// <summary>Called every frame from Update: makes one missing icon per frame.</summary>
        void WarmIcons()
        {
            if (View != null) View.WarmIcons(IconTypes());
        }

        void MenuStyles()
        {
            if (_tile != null) return;
            _tile = new GUIStyle(GUI.skin.button) { fontSize = 13, alignment = TextAnchor.LowerCenter, wordWrap = true, richText = true,
                padding = new RectOffset(3, 3, 3, 4) };
            _tileOn = new GUIStyle(_tile) { fontStyle = FontStyle.Bold };
            _tileOn.normal.textColor = Look.Accent; _tileOn.hover.textColor = Look.Accent;
            _tileOn.normal.background = _tile.active.background;
            _tileLocked = new GUIStyle(_tile);
            _tileLocked.normal.textColor = new Color(0.6f, 0.58f, 0.55f);
            // 0.23.1: tile words are drawn as labels over a blank button (the locked button style showed no text at all).
            _tileText = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.LowerCenter, wordWrap = true, richText = true,
                padding = new RectOffset(2, 2, 2, 3), clipping = TextClipping.Clip };
            _tileText.normal.textColor = new Color(0.95f, 0.92f, 0.86f);
            _tileTextOn = new GUIStyle(_tileText) { fontStyle = FontStyle.Bold };
            _tileTextOn.normal.textColor = Look.Accent;
            _tileTextLocked = new GUIStyle(_tileText);
            _tileTextLocked.normal.textColor = new Color(0.78f, 0.75f, 0.7f);
            _tiny = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true, wordWrap = true };
            _tiny.normal.textColor = new Color(0.95f, 0.92f, 0.86f);
            _tiny.clipping = TextClipping.Overflow;
            _tinyDim = new GUIStyle(_tiny);
            _tinyDim.normal.textColor = new Color(0.62f, 0.6f, 0.56f);
            _tab = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold, richText = true };
            _tabOn = new GUIStyle(_tab);
            _tabOn.normal.textColor = Look.Accent; _tabOn.hover.textColor = Look.Accent;
            _tabOn.normal.background = _tab.active.background;
            _tabLocked = new GUIStyle(_tab) { fontStyle = FontStyle.Normal };
            _tabLocked.normal.textColor = new Color(0.8f, 0.77f, 0.72f);
            _tabLocked.hover.textColor = new Color(0.9f, 0.87f, 0.82f);
            _searchStyle = new GUIStyle(GUI.skin.textField) { fontSize = 15, alignment = TextAnchor.MiddleLeft };
            _cardTitle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, richText = true };
            _cardTitle.normal.textColor = Color.white;
            _cardTitle.clipping = TextClipping.Overflow;
            _barBack = Solid(new Color(0.22f, 0.19f, 0.16f, 1f));
            _barFill = Solid(new Color(0.85f, 0.6f, 0.25f, 1f));
            _barDone = Solid(new Color(0.45f, 0.7f, 0.4f, 1f));
            _line = Solid(new Color(0.75f, 0.68f, 0.55f, 0.8f));
        }

        static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        static string CostText(BuildingDef d)
        {
            var cost = new StringBuilder();
            foreach (var c in d.Cost) cost.Append(cost.Length > 0 ? " " : "").Append(c.count).Append(Items.Letter(c.item));
            return cost.ToString();
        }

        // ------------------------------------------------------------------ build menu

        // 0.23: the build menu groups buildings by what they do, not by tier. Later updates add Fluids and Roads here
        // (append to BuildCat, CatNames and CatBase; a category with no buildings gets no tab).
        enum BuildCat { Extraction, Production, Logistics, Power, Homes }
        static readonly string[] CatNames = { "EXTRACTION", "PRODUCTION", "LOGISTICS", "POWER", "HOMES" };
        const int HotbarSlots = 8;
        const string PrefHotbar = "kw.hotbar", PrefMenuOpen = "kw.buildmenu.open";

        BuildCat _cat = BuildCat.Extraction;
        bool _menuOpen = true;          // false = only the hotbar row shows (B toggles)
        string _search = "";
        bool _typing, _dropFocus;       // the search box has the keyboard; Update skips hotkeys while it does
        Tool[] _favs;                   // hotbar slots; Tool.Select = empty
        Dictionary<BuildCat, List<Tool>> _catTools;
        float _buildTop;                // top edge of the build menu (or the hotbar when collapsed), for the hint line and toasts

        /// <summary>Every building tool in each category, before sorting by tier. Each tool sits in exactly one.</summary>
        Tool[] CatBase(BuildCat c)
        {
            switch (c)
            {
                case BuildCat.Extraction:
                    return B.PeopleEnabled
                        ? new[] { Tool.Woodcutter, Tool.Mine, Tool.Fisher, Tool.ElectricMine }
                        : new[] { Tool.Woodcutter, Tool.Mine, Tool.Garage, Tool.Hopper, Tool.ElectricMine };
                case BuildCat.Production:
                    return new[] { Tool.Kiln, Tool.Smelter, Tool.Workshop, Tool.Sawmill, Tool.TripHammer, Tool.Furnace, Tool.WireMill, Tool.MachineShop,
                        Tool.ArcFurnace, Tool.Assembly, Tool.RollingMill, Tool.Hangar, Tool.Glassworks, Tool.RadioWorks, Tool.DroneWorks };
                case BuildCat.Logistics:
                    return new[] { Tool.Belt, Tool.Splitter, Tool.Rail, Tool.Station, Tool.DronePort };
                case BuildCat.Power:
                    return new[] { Tool.Shaft, Tool.Waterwheel, Tool.SteamEngine, Tool.Dynamo, Tool.Pole, Tool.Turbine, Tool.HydroDam };
                case BuildCat.Homes:
                    return B.PeopleEnabled ? new[] { Tool.Cabin } : new Tool[0];
                default: return new Tool[0];
            }
        }

        /// <summary>Categories with something in them, in tab order.</summary>
        List<BuildCat> _cats;
        List<BuildCat> Cats()
        {
            if (_cats != null) return _cats;
            _cats = new List<BuildCat>();
            foreach (BuildCat c in System.Enum.GetValues(typeof(BuildCat))) if (CatTools(c).Count > 0) _cats.Add(c);
            return _cats;
        }

        // 0.23.1: everything the menu writes is made once per tier (or per search), not on every OnGUI event,
        // so drawing the menu allocates nothing frame to frame.
        int _labelTier = -1;
        readonly Dictionary<Tool, string> _tileLabels = new Dictionary<Tool, string>(), _hotLabels = new Dictionary<Tool, string>();
        readonly Dictionary<BuildCat, string> _tabLabels = new Dictionary<BuildCat, string>(), _tabLabelsOn = new Dictionary<BuildCat, string>();
        string _listQuery;
        BuildCat _listCat;
        readonly List<Tool> _list = new List<Tool>();
        static readonly string[] SlotKeys = { "1", "2", "3", "4", "5", "6", "7", "8" };
        static readonly string[] EmptySlot = { "1\n<size=11>empty</size>", "2\n<size=11>empty</size>", "3\n<size=11>empty</size>", "4\n<size=11>empty</size>",
            "5\n<size=11>empty</size>", "6\n<size=11>empty</size>", "7\n<size=11>empty</size>", "8\n<size=11>empty</size>" };

        void RefreshLabels()
        {
            if (_labelTier == W.Tier) return;
            _labelTier = W.Tier;
            _tileLabels.Clear(); _hotLabels.Clear(); _tabLabels.Clear(); _tabLabelsOn.Clear();
            _listQuery = null;
        }

        /// <summary>Menu tile words: name, then cost, or "LOCKED Tier N" (words, not just a dim colour).</summary>
        string TileLabel(Tool t)
        {
            if (_tileLabels.TryGetValue(t, out var s)) return s;
            var d = B.Defs[TypeOf(t)];
            s = d.Name + "\n" + (d.Tier > W.Tier ? "<b>LOCKED</b> Tier " + d.Tier : CostText(d));
            _tileLabels[t] = s;
            return s;
        }

        string HotLabel(Tool t, int slot)
        {
            if (_hotLabels.TryGetValue(t, out var s)) return SlotKeys[slot] + "  " + s;
            var d = B.Defs[TypeOf(t)];
            s = d.Name + (d.Tier > W.Tier ? "\nLOCKED T" + d.Tier : "");
            _hotLabels[t] = s;
            return SlotKeys[slot] + "  " + s;   // one short concat per pinned slot; slots change rarely
        }

        string TabLabel(BuildCat c, bool on, float tabW)
        {
            var cache = on ? _tabLabelsOn : _tabLabels;
            if (cache.TryGetValue(c, out var s)) return s;
            var ct = CatTools(c);
            int open = 0;
            foreach (var t in ct) if (!Locked(t)) open++;
            s = (on ? "> " : "") + CatNames[(int)c] + "  <size=11>" + open + "/" + ct.Count + "</size>";
            if (_tab.CalcSize(new GUIContent(s)).x > tabW - 6f) s = "<size=11>" + s + "</size>";
            cache[c] = s;
            return s;
        }

        /// <summary>The tiles to show: the open category, or every building whose name matches the search. Cached.</summary>
        List<Tool> MenuList(string q)
        {
            if (_listQuery == q && (q.Length > 0 || _listCat == _cat)) return _list;
            _listQuery = q; _listCat = _cat;
            _list.Clear();
            if (q.Length > 0)
            {
                foreach (var t in AllBuildTools())
                    if (B.Defs[TypeOf(t)].Name.IndexOf(q, System.StringComparison.OrdinalIgnoreCase) >= 0) _list.Add(t);
            }
            else _list.AddRange(CatTools(_cat));
            return _list;
        }

        /// <summary>A category's tools sorted by tier (stable: same-tier tools keep their listed order).</summary>
        List<Tool> CatTools(BuildCat c)
        {
            if (_catTools == null) _catTools = new Dictionary<BuildCat, List<Tool>>();
            if (_catTools.TryGetValue(c, out var l)) return l;
            l = new List<Tool>(CatBase(c));
            for (int i = 1; i < l.Count; i++)   // insertion sort is stable
            {
                var t = l[i];
                int tier = B.Defs[TypeOf(t)].Tier, j = i - 1;
                while (j >= 0 && B.Defs[TypeOf(l[j])].Tier > tier) { l[j + 1] = l[j]; j--; }
                l[j + 1] = t;
            }
            _catTools[c] = l;
            return l;
        }

        List<Tool> _allTools;
        List<Tool> AllBuildTools()
        {
            if (_allTools != null) return _allTools;
            _allTools = new List<Tool>();
            foreach (BuildCat c in System.Enum.GetValues(typeof(BuildCat))) _allTools.AddRange(CatTools(c));
            return _allTools;
        }

        /// <summary>Tools from the tier lists that no category holds, or that two categories hold (written to the shots log). Empty = fine.</summary>
        string MenuCheck()
        {
            var all = AllBuildTools();
            var sb = new StringBuilder();
            var want = new List<Tool> { Tool.Splitter };
            for (int p = 1; p <= 10; p++) want.AddRange(TierTools(p));
            foreach (var t in want) if (!all.Contains(t)) sb.Append(" missing ").Append(t);
            for (int i = 0; i < all.Count; i++) if (all.IndexOf(all[i]) != i) sb.Append(" twice ").Append(all[i]);
            return sb.ToString();
        }

        bool Locked(Tool t) => B.Defs[TypeOf(t)].Tier > W.Tier;

        /// <summary>Pick a building tool; a locked one says which tier opens it instead.</summary>
        void PickTool(Tool t, bool toggle)
        {
            var d = B.Defs[TypeOf(t)];
            if (d.Tier > W.Tier) { Toast(d.Name + " unlocks at Tier " + d.Tier + ": press T to see the goal"); return; }
            SetTool(toggle && _tool == t ? Tool.Select : t);
        }

        void EnsureFavs()
        {
            if (_favs != null) return;
            _favs = new Tool[HotbarSlots];
            var defaults = B.PeopleEnabled
                ? new[] { Tool.Belt, Tool.Splitter, Tool.Woodcutter, Tool.Mine, Tool.Fisher, Tool.Cabin, Tool.Kiln, Tool.Smelter }
                : new[] { Tool.Belt, Tool.Splitter, Tool.Woodcutter, Tool.Mine, Tool.Garage, Tool.Kiln, Tool.Smelter, Tool.Workshop };
            for (int i = 0; i < HotbarSlots; i++) _favs[i] = i < defaults.Length ? defaults[i] : Tool.Select;
            if (_shotsDir != null) return;   // the autopilot always shows the defaults and never touches prefs
            _menuOpen = PlayerPrefs.GetInt(PrefMenuOpen, 1) == 1;
            string saved = PlayerPrefs.GetString(PrefHotbar, "");
            if (string.IsNullOrEmpty(saved)) return;
            var all = AllBuildTools();
            var parts = saved.Split(',');
            for (int i = 0; i < HotbarSlots; i++)
            {
                _favs[i] = Tool.Select;
                if (i < parts.Length && parts[i].Length > 0 && System.Enum.TryParse(parts[i], out Tool t)
                    && all.Contains(t) && System.Array.IndexOf(_favs, t) < 0) _favs[i] = t;
            }
        }

        void SaveFavs()
        {
            if (_shotsDir != null) return;
            var sb = new StringBuilder();
            for (int i = 0; i < HotbarSlots; i++) sb.Append(i > 0 ? "," : "").Append(_favs[i] == Tool.Select ? "" : _favs[i].ToString());
            PlayerPrefs.SetString(PrefHotbar, sb.ToString());
            PlayerPrefs.SetInt(PrefMenuOpen, _menuOpen ? 1 : 0);
            PlayerPrefs.Save();
        }

        void ToggleFav(Tool t)
        {
            EnsureFavs();
            string name = B.Defs[TypeOf(t)].Name;
            int at = System.Array.IndexOf(_favs, t);
            if (at >= 0) { _favs[at] = Tool.Select; Toast(name + " unpinned from hotbar key " + (at + 1)); SaveFavs(); return; }
            int free = System.Array.IndexOf(_favs, Tool.Select);
            if (free < 0) { Toast("Hotbar full: right-click a hotbar slot to unpin one first"); return; }
            _favs[free] = t;
            Toast(name + " pinned to hotbar key " + (free + 1));
            SaveFavs();
        }

        void ToggleBuildMenu()
        {
            EnsureFavs();
            _menuOpen = !_menuOpen;
            if (!_menuOpen) _dropFocus = true;
            SaveFavs();
        }

        /// <summary>Tab: the next category (opens the menu if it was collapsed).</summary>
        void NextCategory()
        {
            var cats = Cats();
            int i = cats.IndexOf(_cat);
            _cat = cats[(i + 1) % cats.Count];
            _search = "";
            if (!_menuOpen) ToggleBuildMenu();
            SetTool(Tool.Select);
        }

        /// <summary>The first category holding a building of the current tier (the autopilot shows the newest buildings).</summary>
        BuildCat NewestCat()
        {
            foreach (var c in Cats())
                foreach (var t in CatTools(c))
                    if (B.Defs[TypeOf(t)].Tier == W.Tier) return c;
            return _cat;
        }

        /// <summary>True when a right click lands in r this event; eats it so the button under it doesn't fire.</summary>
        static bool RightClicked(Rect r)
        {
            var e = Event.current;
            if (e.type != EventType.MouseDown || e.button != 1 || !r.Contains(e.mousePosition)) return false;
            e.Use();
            return true;
        }

        void DrawBuildMenu(float Wd, float Ht)
        {
            MenuStyles();
            EnsureFavs();
            RefreshLabels();
            if (_dropFocus) { GUI.FocusControl(null); GUIUtility.keyboardControl = 0; _dropFocus = false; _typing = false; }

            const float gap = 5f;
            float bw = Mathf.Min(1800f, Wd - 40f);
            // Hotbar row: 8 pinned slots on the left; earthworks, then the build menu and research toggles, on the right.
            // 0.23.1: shorter slots and tiles so the open menu takes about a fifth of the screen, not over a quarter.
            float slotW = Mathf.Min(92f, Mathf.Floor((bw - 16f - 12f - 140f) / (HotbarSlots + 4)) - gap), slotH = 80f;
            var hot = Panel(new Rect(Wd / 2f - bw / 2f, Ht - slotH - 16f, bw, slotH + 10f));
            string hoverCard = null, hoverExtra = null; BuildingType hoverType = BuildingType.Belt; bool hoverTool = false;
            float y = hot.y + 5f;
            for (int i = 0; i < HotbarSlots; i++)
            {
                var r = new Rect(hot.x + 8f + i * (slotW + gap), y, slotW, slotH);
                var t = _favs[i];
                if (t == Tool.Select)
                {
                    GUI.Box(r, GUIContent.none, _tile);
                    GUI.Label(r, EmptySlot[i], _tileTextLocked);
                    if (r.Contains(Event.current.mousePosition)) hoverCard = "<b>Hotbar key " + SlotKeys[i] + ": empty</b>\nRight-click a building in the build menu (B) to pin it here.";
                    continue;
                }
                if (RightClicked(r)) { ToggleFav(t); continue; }
                var bt = TypeOf(t);
                var d = B.Defs[bt];
                bool locked = d.Tier > W.Tier;
                if (GUI.Button(r, GUIContent.none, _tool == t ? _tileOn : _tile)) PickTool(t, true);
                DrawTileIcon(r, bt, locked, 44f);
                GUI.Label(r, HotLabel(t, i), locked ? _tileTextLocked : _tool == t ? _tileTextOn : _tileText);
                if (_tool == t) GUI.Box(new Rect(r.x, r.yMax - 3f, r.width, 3f), GUIContent.none, Underline());
                if (r.Contains(Event.current.mousePosition)) { hoverType = bt; hoverTool = true; hoverExtra = "Hotbar key " + SlotKeys[i] + ". Right-click to unpin."; }
            }

            var tools = EarthTools;
            float bx = hot.xMax - 8f - 140f;
            var menuR = new Rect(bx, y, 140f, slotH / 2f - 3f);
            if (GUI.Button(menuR, (_menuOpen ? "> " : "") + "BUILD (B)", _menuOpen ? _tabOn : _tab)) ToggleBuildMenu();
            if (menuR.Contains(Event.current.mousePosition))
                hoverCard = _menuOpen ? "<b>Build menu (B)</b>\nHide the menu and keep just the hotbar." : "<b>Build menu (B)</b>\nShow every building, grouped by what it does.";
            var researchR = new Rect(bx, y + slotH / 2f + 3f, 140f, slotH / 2f - 3f);
            if (GUI.Button(researchR, (_research ? "> " : "") + "RESEARCH (T)", _research ? _tabOn : _tab)) _research = !_research;
            float rx = bx - 12f - tools.Length * (slotW + gap) + gap;
            for (int i = 0; i < tools.Length; i++)
            {
                var (t, label) = tools[i];
                var r = new Rect(rx + i * (slotW + gap), y, slotW, slotH);
                if (GUI.Button(r, GUIContent.none, _tool == t ? _tileOn : _tile)) SetTool(_tool == t ? Tool.Select : t);
                var ti = t == Tool.Demolish || View == null ? null : t == Tool.Level ? View.DozerIcon : View.ToolIcon(t == Tool.Dig);
                if (ti != null) GUI.DrawTexture(new Rect(r.x + (r.width - 44f) / 2f, r.y + 3f, 44f, 44f), ti, ScaleMode.ScaleToFit, true);
                else if (t == Tool.Demolish) GUI.Label(new Rect(r.x, r.y + 2f, r.width, 44f), "X", DemolishGlyph());
                GUI.Label(r, label, _tool == t ? _tileTextOn : _tileText);
                if (_tool == t) GUI.Box(new Rect(r.x, r.yMax - 3f, r.width, 3f), GUIContent.none, Underline());
                if (r.Contains(Event.current.mousePosition))
                    hoverCard = t == Tool.Dig ? "<b>Dig</b>\nDrag over ore, coal, stone or grass. Excavators dig it layer by layer and trucks carry it to hoppers, the lodge or a dump."
                        : t == Tool.Dump ? "<b>Dump</b>\nDrag an area where trucks may drop dirt. It raises the ground up to 2 levels."
                        : t == Tool.Level ? "<b>Level</b>\nDrag over spoil heaps, old pits or woods. A bulldozer (buy one at a garage) clears the trees, shaves heaps and pushes the dirt into holes until the ground is flat enough to build on."
                        : "<b>Demolish</b>\nClick a building to remove it and get everything back. Drag over belts.";
            }
            _buildTop = hot.y;

            if (_menuOpen)
            {
                // Which tiles: the open category, or every building whose name matches the search.
                string q = _search ?? "";
                if (q.Length > 0 && (q[0] == ' ' || q[q.Length - 1] == ' ')) q = q.Trim();
                var list = MenuList(q);

                const float tileW = 92f, tileH = 90f;
                const int maxRows = 2;
                int cols = Mathf.Max(1, Mathf.FloorToInt((bw - 16f + gap) / (tileW + gap)));
                int rows = Mathf.Clamp((list.Count + cols - 1) / cols, 1, maxRows);
                float menuH = 36f + rows * (tileH + gap) + 3f;
                var bar = Panel(new Rect(hot.x, hot.y - 5f - menuH, bw, menuH));
                _buildTop = bar.y;

                // Category tabs: words plus how many are unlocked; "> " marks the open one (not colour only).
                var cats = Cats();
                const float searchW = 330f;
                float tabW = Mathf.Min(210f, Mathf.Floor((bar.width - 16f - searchW - 12f - (cats.Count - 1) * 4f) / cats.Count));
                for (int i = 0; i < cats.Count; i++)
                {
                    var c = cats[i];
                    bool on = q.Length == 0 && _cat == c;
                    var r = new Rect(bar.x + 8f + i * (tabW + 4f), bar.y + 5f, tabW, 26f);
                    if (GUI.Button(r, TabLabel(c, on, tabW), on ? _tabOn : _tab)) { _cat = c; _search = ""; _dropFocus = true; SetTool(Tool.Select); }
                }

                // Search box: filters tiles across every category by name.
                float sx = bar.xMax - 8f - searchW;
                GUI.Label(new Rect(sx, bar.y + 7f, 62f, 24f), "Search", _tiny);
                GUI.SetNextControlName("kwSearch");
                _search = GUI.TextField(new Rect(sx + 62f, bar.y + 5f, searchW - 62f - 36f, 26f), _search ?? "", 32, _searchStyle);
                if (Event.current.type == EventType.Repaint) _typing = GUI.GetNameOfFocusedControl() == "kwSearch";
                if (GUI.Button(new Rect(bar.xMax - 8f - 30f, bar.y + 5f, 30f, 26f), "X", _tab)) { _search = ""; _dropFocus = true; }

                float y0 = bar.y + 36f;
                if (list.Count == 0)
                    GUI.Label(new Rect(bar.x + 14f, y0 + 10f, bar.width - 28f, 30f), "No building matches \"" + q + "\". Clear the search (X) to see the categories.", _small);
                int shown = Mathf.Min(list.Count, rows * cols);
                for (int i = 0; i < shown; i++)
                {
                    var t = list[i];
                    var bt = TypeOf(t);
                    var d = B.Defs[bt];
                    bool locked = d.Tier > W.Tier;
                    var r = new Rect(bar.x + 8f + (i % cols) * (tileW + gap), y0 + (i / cols) * (tileH + gap), tileW, tileH);
                    if (RightClicked(r)) ToggleFav(t);
                    int fav = System.Array.IndexOf(_favs, t);
                    if (GUI.Button(r, GUIContent.none, _tool == t ? _tileOn : _tile)) PickTool(t, true);
                    DrawTileIcon(r, bt, locked, 50f);
                    GUI.Label(r, TileLabel(t), locked ? _tileTextLocked : _tool == t ? _tileTextOn : _tileText);
                    if (fav >= 0) GUI.Label(new Rect(r.x + 5f, r.y + 2f, 30f, 20f), SlotKeys[fav], _tiny);   // its hotbar key
                    if (_tool == t) GUI.Box(new Rect(r.x, r.yMax - 3f, r.width, 3f), GUIContent.none, Underline());
                    if (r.Contains(Event.current.mousePosition))
                    {
                        hoverType = bt; hoverTool = true;
                        hoverExtra = fav >= 0 ? "On hotbar key " + SlotKeys[fav] + ". Right-click to unpin." : "Right-click to pin it to the hotbar.";
                    }
                }
                if (shown < list.Count)
                    GUI.Label(new Rect(bar.xMax - 230f, bar.yMax - 22f, 222f, 20f), "+" + (list.Count - shown) + " more: search to narrow", _tinyDim);
            }
            else if (_typing) _dropFocus = true;

            var top = new Rect(hot.x, _buildTop, bw, 10f);
            if (hoverTool) DrawBuildCard(hoverType, top, hoverExtra);
            else if (hoverCard != null) DrawTextCard(hoverCard, top);
        }

        static readonly (Tool, string)[] EarthTools =
        {
            (Tool.Dig, "G  Dig\nmark ground"), (Tool.Dump, "V  Dump\ndirt goes here"), (Tool.Level, "L  Level\nflatten, clear"), (Tool.Demolish, "X  Demolish\nfull refund"),
        };

        void DrawTileIcon(Rect r, BuildingType bt, bool locked, float size)
        {
            var icon = View != null ? View.IconIfReady(bt) : null;
            if (icon == null) return;
            var prevC = GUI.color;
            if (locked) GUI.color = new Color(0.55f, 0.55f, 0.55f, 0.75f);
            GUI.DrawTexture(new Rect(r.x + (r.width - size) / 2f, r.y + 3f, size, size), icon, ScaleMode.ScaleToFit, true);
            GUI.color = prevC;
        }

        GUIStyle _glyph;
        GUIStyle DemolishGlyph()
        {
            if (_glyph != null) return _glyph;
            _glyph = new GUIStyle(GUI.skin.label) { fontSize = 50, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _glyph.normal.textColor = new Color(0.9f, 0.42f, 0.3f);
            return _glyph;
        }

        GUIStyle _underline;
        GUIStyle Underline()
        {
            if (_underline != null) return _underline;
            _underline = new GUIStyle(GUI.skin.box);
            _underline.normal.background = Solid(Look.Accent);
            return _underline;
        }

        void DrawTextCard(string text, Rect bar)
        {
            var r = new Rect(bar.x + 8f, bar.y - 112f, 440f, 104f);
            GUI.Box(r, GUIContent.none, _panel);
            GUI.Label(new Rect(r.x + 12f, r.y + 8f, r.width - 24f, r.height - 16f), text, _small);
        }

        /// <summary>Hover card: the building, what it costs, needs and makes.</summary>
        void DrawBuildCard(BuildingType bt, Rect bar, string extra = null)
        {
            var d = B.Defs[bt];
            var sb = new StringBuilder();
            sb.Append("Cost: ");
            foreach (var c in d.Cost) sb.Append(c.count).Append(' ').Append(Items.Name(c.item).ToLowerInvariant()).Append(" (").Append(Items.Letter(c.item)).Append(")  ");
            if (d.Cost.Length == 0) sb.Append("free");
            sb.Append("   Size ").Append(d.Size).Append('x').Append(d.Size).Append('\n');
            if (bt == BuildingType.Waterwheel || bt == BuildingType.SteamEngine) sb.Append("Gives ").Append(d.Hp).Append(" hp to its line shafts\n");
            else if (d.NeedsPower) sb.Append("Needs ").Append(d.Hp).Append(" hp from a line shaft\n");
            if (d.BurnsFuel) sb.Append("Burns fuel: wood, charcoal or coal\n");
            if (d.Recipes != null)
                foreach (var rc in d.Recipes)
                {
                    sb.Append("Makes ").Append(rc.OutCount > 1 ? rc.OutCount + " " : "").Append(rc.Name.ToLowerInvariant()).Append(" (").Append(Items.Letter(rc.Out)).Append(") from ");
                    for (int k = 0; k < rc.InItems.Length; k++) sb.Append(k > 0 ? " + " : "").Append(rc.InCounts[k]).Append(' ').Append(Items.Letter(rc.InItems[k]));
                    sb.Append(" in ").Append(rc.Seconds.ToString("0.#")).Append(" s").Append(rc.BurnsFuel ? ", burns fuel" : "").Append('\n');
                }
            string blurb = Blurb(bt);
            if (blurb != null) sb.Append(blurb);
            if (d.Tier > W.Tier) sb.Append("\nLOCKED: unlocks at Tier ").Append(d.Tier).Append(" (").Append(TierNames[d.Tier]).Append(")");
            if (extra != null) sb.Append('\n').Append(extra);

            var r = new Rect(bar.x + 8f, bar.y - 196f, 620f, 188f);
            GUI.Box(r, GUIContent.none, _panel);
            var icon = View != null ? View.IconIfReady(bt) : null;
            float tx = r.x + 12f;
            if (icon != null) { GUI.DrawTexture(new Rect(r.x + 8f, r.y + 14f, 140f, 140f), icon, ScaleMode.ScaleToFit, true); tx = r.x + 156f; }
            GUI.Label(new Rect(tx, r.y + 8f, r.xMax - tx - 10f, 28f), d.Name, _cardTitle);
            GUI.Label(new Rect(tx, r.y + 38f, r.xMax - tx - 10f, r.height - 44f), sb.ToString(), _tiny);
        }

        static string Blurb(BuildingType t)
        {
            switch (t)
            {
                case BuildingType.Belt: return "Carries items one way. Drag to lay a line.";
                case BuildingType.Woodcutter: return "Cuts trees around it. Trees grow back.";
                case BuildingType.Mine: return "Digs stone or iron ore from the ground it stands on.";
                case BuildingType.Splitter: return "Shares items between its front, left and right.";
                case BuildingType.Garage: return "Buys and refuels excavators and trucks.";
                case BuildingType.Hopper: return "Trucks tip ore and coal in; it feeds the machine it faces.";
                case BuildingType.Shaft: return "Overhead line shaft: carries power. Can cross belts and roads.";
                case BuildingType.Waterwheel: return "Must touch the river. Always turning.";
                case BuildingType.Station: return "Beside the track. Near the lodge it unloads into the lodge. Click it to buy a train.";
                case BuildingType.Rail: return "Drag a line of track. Trucks can cross it.";
                case BuildingType.Dynamo: return "Touch it to a line shaft. Turns shaft power into electricity.";
                case BuildingType.Pole: return "Carries electricity. Poles link up to 8 tiles apart; machines 2 tiles away join.";
                case BuildingType.ElectricMine: return "Electric. Digs ore or stone twice as fast, no fuel. Needs a pole close by.";
                case BuildingType.MachineShop: return "Electric. Builds motors from wire and iron plate.";
                case BuildingType.WireMill: return "Electric. Draws steel into wire. Needs a pole close by.";
                case BuildingType.Turbine: return "Burns wood, charcoal or coal for electricity. No shaft needed. A woodcutter can feed it.";
                case BuildingType.ArcFurnace: return "Electric. Melts iron ore into steel with no coal.";
                case BuildingType.Assembly: return "Electric. Builds tractors from motors and steel.";
                case BuildingType.RollingMill: return "Electric. Rolls 1 steel into 2 iron plates.";
                case BuildingType.Hangar: return "Electric. Builds aeroplanes from motors and plates. They fly off its front.";
                case BuildingType.HydroDam: return "Sits on the river bank. Electricity all day and night, no fuel.";
                case BuildingType.Glassworks: return "Electric. Melts stone into glass.";
                case BuildingType.DroneWorks: return "Electric. Builds radio drones from 2 radio sets; each flies itself home to the lodge.";
                case BuildingType.DronePort: return "Flies belted goods to the lodge by drone, 4 a trip. Keeps 4 drones from the lodge store.";
                case BuildingType.RadioWorks: return "Electric. Builds radio sets from glass valves and a wooden cabinet.";
                default: return null;
            }
        }

        // ------------------------------------------------------------------ research panel

        (string name, Item item, int have, int need)[] GoalOf(int tier)
        {
            switch (tier)
            {
                case 1: return new[] { ("gears", Item.Gear, W.Delivered[Item.Gear], B.GoalGear), ("bricks", Item.Brick, W.Delivered[Item.Brick], B.GoalBrick) };
                case 2: return new[] { ("planks", Item.Plank, W.Delivered[Item.Plank], B.GoalPlank), ("iron plates", Item.Plate, W.Delivered[Item.Plate], B.GoalPlate) };
                case 3: return new[] { ("steel", Item.Steel, W.Delivered[Item.Steel], B.GoalSteel), ("coal", Item.Coal, W.Delivered[Item.Coal], B.GoalCoal) };
                case 4: return new[] { ("by train", Item.None, W.RailHauled, B.GoalRail) };
                case 5: return new[] { ("steel wire", Item.Wire, W.Delivered[Item.Wire], B.GoalWire) };
                case 6: return new[] { ("motors", Item.Motor, W.Delivered[Item.Motor], B.GoalMotor) };
                case 7: return new[] { ("tractors", Item.Tractor, W.Delivered[Item.Tractor], B.GoalTractor) };
                case 8: return new[] { ("planes flown", Item.Plane, W.Delivered[Item.Plane], B.GoalPlane) };
                case 9: return new[] { ("radio sets", Item.Radio, W.Delivered[Item.Radio], B.GoalRadio) };
                case 10: return new[] { ("drones", Item.Drone, W.Delivered[Item.Drone], B.GoalDrone) };
                default: return new (string, Item, int, int)[0];
            }
        }

        GUIStyle _cardTitleOneLine;

        /// <summary>The tier's name on its own line: steps down a font size at a time until it fits with a margin.</summary>
        string FitName(int n, float width)
        {
            if (_cardTitleOneLine == null) _cardTitleOneLine = new GUIStyle(_cardTitle) { wordWrap = false, clipping = TextClipping.Clip };
            foreach (int size in new[] { 18, 16, 15, 14, 13, 12, 11 })
            {
                string t = "<size=" + size + ">" + TierNames[n] + "</size>";
                if (_cardTitleOneLine.CalcSize(new GUIContent(t)).x <= width - 8f) return t;
            }
            return "<size=11>" + TierNames[n] + "</size>";
        }

        static readonly string[] TierBlurb =
        {
            "",
            "Wood, stone and ore: kilns, smelters and a workshop.",
            "A waterwheel turns line shafts that power a sawmill and a trip hammer.",
            "Dig the coal seam. A steam engine and a steel furnace.",
            "Track, stations and trains haul from the far ore field. Pit machines go diesel.",
            "Dynamos on the shafts, power poles, and an electric wire mill.",
            "Electric mines and a machine shop that builds motors.",
            "A steam turbine, an electric furnace, and an assembly plant that builds tractors. Hydraulic diggers dig a deeper pit; dump trucks, diesel trains.",
            "A rolling mill and a hangar whose aeroplanes fly.",
            "A hydro dam on the river, a glassworks and a radio works.",
            "Radio drones: a drone works, and drone ports that fly goods home.",
            "Coming in a later build.",
        };

        void DrawResearch(float Wd, float Ht)
        {
            MenuStyles();
            float pw = Mathf.Min(1840f, Wd - 40f);
            var p = Panel(new Rect(Wd / 2f - pw / 2f, 84f, pw, Mathf.Min(640f, Ht - 260f)));
            GUI.Label(new Rect(p.x + 20f, p.y + 10f, 700f, 34f), "<b>Research</b>   reach each tier's goal to unlock the next", _h2);
            if (GUI.Button(new Rect(p.xMax - 130f, p.y + 10f, 116f, 34f), "Close (T)", _btn)) _research = false;

            const int tiers = 11;
            float gapX = 10f, nodeW = Mathf.Floor((pw - 40f - (tiers - 1) * gapX) / tiers), top = p.y + 60f, nodeH = p.height - 76f;
            for (int n = 1; n <= tiers; n++)
            {
                float nx = p.x + 20f + (n - 1) * (nodeW + gapX);
                bool done = W.Tier > n, now = W.Tier == n, locked = W.Tier < n;
                var node = new Rect(nx, top, nodeW, nodeH);
                GUI.Box(node, GUIContent.none, _panel);
                if (now) GUI.Box(new Rect(node.x, node.y, node.width, 4f), GUIContent.none, Underline());
                // Connector arrow to the next tier.
                if (n < tiers)
                {
                    GUI.DrawTexture(new Rect(node.xMax + 2f, top + 40f, gapX - 8f, 3f), _line);
                    GUI.Label(new Rect(node.xMax + gapX - 18f, top + 26f, 24f, 28f), ">", _cardTitle);
                }
                // "Tier n" and its state on the first line, the tier's name under it, then the blurb.
                string state = done ? "DONE" : now ? "NOW" : "LOCKED";
                GUI.Label(new Rect(nx + 12f, top + 6f, nodeW - 24f, 30f), "<b>Tier " + n + "</b>", _cardTitleOneLine ?? _cardTitle);
                GUI.Label(new Rect(node.xMax - 12f - 66f, top + 10f, 66f, 22f), state, now ? _btnSmallOn : _tiny);
                GUI.Label(new Rect(nx + 12f, top + 34f, nodeW - 24f, 26f), FitName(n, nodeW - 24f), _cardTitleOneLine);
                string blurb = "<size=12>" + TierBlurb[n] + "</size>";
                var blurbStyle = locked ? _tinyDim : _tiny;
                float blurbH = blurbStyle.CalcHeight(new GUIContent(blurb), nodeW - 24f);
                GUI.Label(new Rect(nx + 12f, top + 62f, nodeW - 24f, blurbH + 2f), blurb, blurbStyle);

                // Unlocks: building pictures with names, always below the blurb.
                float uy = Mathf.Max(top + 118f, top + 62f + blurbH + 8f);
                var unlocks = new List<BuildingType>();
                if (n <= 9) foreach (var t in TierTools(n)) unlocks.Add(TypeOf(t));
                if (n == 1) unlocks.Add(BuildingType.Splitter);
                if (unlocks.Count > 0) GUI.Label(new Rect(nx + 12f, uy, nodeW - 24f, 22f), "Unlocks", _tinyDim);
                uy += 22f;
                // Pictures in a grid (4 across), then their names as one wrapped line: names never break mid-word.
                int col = 0; float ux = nx + 10f, cw = Mathf.Floor((nodeW - 20f) / 4f), isz = cw - 4f;
                var names = new StringBuilder();
                foreach (var bt in unlocks)
                {
                    var icon = View != null ? View.IconIfReady(bt) : null;
                    if (icon != null) GUI.DrawTexture(new Rect(ux + col * cw + 2f, uy, isz, isz), icon, ScaleMode.ScaleToFit, true);
                    if (names.Length > 0) names.Append(", ");
                    names.Append(B.Defs[bt].Name);
                    col++;
                    if (col == 4) { col = 0; uy += isz + 4f; }
                }
                if (col > 0) uy += isz + 4f;
                if (names.Length > 0)
                {
                    string list = "<size=12>" + names + "</size>";
                    var ls = locked ? _tinyDim : _tiny;
                    float lh = ls.CalcHeight(new GUIContent(list), nodeW - 24f);
                    GUI.Label(new Rect(nx + 12f, uy, nodeW - 24f, lh + 2f), list, ls);
                    uy += lh + 6f;
                }
                if (n == 1) { GUI.Label(new Rect(nx + 12f, uy, nodeW - 24f, 20f), "+ excavators and trucks (at the garage)", _tiny); uy += 22f; }
                if (n == 4) { GUI.Label(new Rect(nx + 12f, uy, nodeW - 24f, 20f), "+ trains (at a station)", _tiny); uy += 22f; }

                // Goal to finish this tier, with bars and numbers.
                var goals = GoalOf(n);
                if (goals.Length > 0)
                {
                    float gy = node.yMax - 22f - 2 * 40f;   // goals line up across tiers
                    GUI.Label(new Rect(nx + 12f, gy - 24f, nodeW - 24f, 22f), n == 8 ? "Goal: fly from a hangar" : "Goal: deliver to the lodge", _tinyDim);
                    foreach (var g in goals)
                    {
                        int have = Mathf.Min(g.have, g.need);
                        if (done) have = g.need;
                        string letter = g.item != Item.None ? Items.Letter(g.item) + " " : "";
                        string line = letter + g.name + "  " + (locked ? "0" : have.ToString()) + " / " + g.need;
                        if (_tiny.CalcSize(new GUIContent(line)).x > nodeW - 24f) line = "<size=11>" + line + "</size>";   // totals never clip
                        GUI.Label(new Rect(nx + 12f, gy, nodeW - 24f, 22f), line, _tiny);
                        var bar = new Rect(nx + 12f, gy + 20f, nodeW - 24f, 10f);
                        GUI.DrawTexture(bar, _barBack);
                        float f = locked ? 0f : have / (float)g.need;
                        GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * f, bar.height), done ? _barDone : _barFill);
                        gy += 40f;
                    }
                }
            }
        }
    }
}
