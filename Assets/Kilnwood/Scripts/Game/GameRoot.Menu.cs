using System.Collections.Generic;
using System.Text;
using Kilnwood.Sim;
using UnityEngine;

namespace Kilnwood.Game
{
    /// <summary>
    /// 0.9 build menu and research panel. The build menu has a tab per tier (locked tabs say what opens them),
    /// picture tiles of each building (its real model), and a details card on hover: cost, power, recipes.
    /// The research panel (T) shows the tier tree: each tier's goal with progress bars, and what it unlocks.
    /// Every state has words as well as colour (DONE / NOW / LOCKED, numbers on every bar).
    /// </summary>
    public partial class GameRoot
    {
        bool _research;
        static readonly string[] TierNames = { "", "First Fire", "Water Power", "Steam", "Rails", "Electricity", "Machine Age", "Next era" };
        static readonly string[] PageNames = { "", "BASICS", "WATER POWER", "STEAM", "RAILS", "ELECTRICITY", "MACHINES" };
        List<BuildingType> _iconTypes;
        GUIStyle _tile, _tileOn, _tileLocked, _tiny, _tinyDim, _tab, _tabOn, _tabLocked, _cardTitle;
        Texture2D _barBack, _barFill, _barDone, _line;

        List<BuildingType> IconTypes()
        {
            if (_iconTypes != null) return _iconTypes;
            _iconTypes = new List<BuildingType>();
            for (int p = 1; p <= 6; p++)
                foreach (var t in PageTools(p)) _iconTypes.Add(TypeOf(t));
            _iconTypes.Add(BuildingType.Splitter);
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

        void DrawBuildMenu(float Wd, float Ht)
        {
            MenuStyles();
            const float tileW = 104f, tileH = 112f, gap = 5f;
            int pages = 6;
            var bar = Panel(new Rect(Wd / 2f - 780f, Ht - 162f, 1560f, 152f));

            // Tabs: one per tier page; locked ones say which tier opens them.
            float tabW = 228f;
            for (int p = 1; p <= pages; p++)
            {
                bool open = W.Tier >= p;
                var r = new Rect(bar.x + 8f + (p - 1) * (tabW + 4f), bar.y + 6f, tabW, 28f);
                string label = open ? (_page == p ? "> " : "") + PageNames[p] : "LOCKED: " + PageNames[p] + " (T" + p + ")";
                if (GUI.Button(r, label, !open ? _tabLocked : _page == p ? _tabOn : _tab))
                {
                    if (open) { _page = p; SetTool(Tool.Select); }
                    else Toast(PageNames[p] + " opens at Tier " + p + ": press T to see the goal");
                }
            }
            var researchR = new Rect(bar.xMax - 140f, bar.y + 6f, 132f, 28f);
            if (GUI.Button(researchR, (_research ? "> " : "") + "RESEARCH (T)", _research ? _tabOn : _tab)) _research = !_research;

            // Building tiles for this page.
            var list = new List<Tool>(PageTools(_page));
            if (_page == 1) list.Add(Tool.Splitter);
            float x = bar.x + 8f, y = bar.y + 38f;
            string hoverCard = null; BuildingType hoverType = BuildingType.Belt; bool hoverTool = false;
            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i];
                var bt = TypeOf(t);
                var d = B.Defs[bt];
                bool locked = d.Tier > W.Tier;
                string key = t == Tool.Splitter ? "9" : (i + 1).ToString();
                string label = (locked ? "LOCKED" : key + "  " + d.Name) + "\n<size=12>" + CostText(d) + "</size>";
                var icon = View != null ? View.IconIfReady(bt) : null;
                var r = new Rect(x, y, tileW, tileH);
                if (GUI.Button(r, label, locked ? _tileLocked : _tool == t ? _tileOn : _tile))
                {
                    if (locked) Toast(d.Name + " unlocks at Tier " + d.Tier);
                    else SetTool(_tool == t ? Tool.Select : t);
                }
                if (icon != null)
                {
                    var prevC = GUI.color;
                    if (locked) GUI.color = new Color(0.45f, 0.45f, 0.45f, 0.8f);
                    GUI.DrawTexture(new Rect(r.x + (r.width - 66f) / 2f, r.y + 3f, 66f, 66f), icon, ScaleMode.ScaleToFit, true);
                    GUI.color = prevC;
                }
                if (_tool == t) GUI.Box(new Rect(r.x, r.yMax - 3f, r.width, 3f), GUIContent.none, Underline());
                if (r.Contains(Event.current.mousePosition)) { hoverType = bt; hoverTool = true; }
                x += tileW + gap;
            }

            // Earthworks and demolish, always on the right.
            var tools = new[] { (Tool.Dig, "G  Dig", "mark ground"), (Tool.Dump, "V  Dump", "dirt goes here"), (Tool.Demolish, "X  Demolish", "full refund") };
            float rx = bar.xMax - 8f - tools.Length * (tileW + gap) + gap;
            for (int i = 0; i < tools.Length; i++)
            {
                var (t, name, sub) = tools[i];
                var r = new Rect(rx + i * (tileW + gap), y, tileW, tileH);
                if (GUI.Button(r, name + "\n<size=12>" + sub + "</size>", _tool == t ? _tileOn : _tile)) SetTool(_tool == t ? Tool.Select : t);
                var ti = t == Tool.Demolish || View == null ? null : View.ToolIcon(t == Tool.Dig);
                if (ti != null) GUI.DrawTexture(new Rect(r.x + (r.width - 66f) / 2f, r.y + 3f, 66f, 66f), ti, ScaleMode.ScaleToFit, true);
                else if (t == Tool.Demolish) GUI.Label(new Rect(r.x, r.y + 2f, r.width, 64f), "X", DemolishGlyph());
                if (_tool == t) GUI.Box(new Rect(r.x, r.yMax - 3f, r.width, 3f), GUIContent.none, Underline());
                if (r.Contains(Event.current.mousePosition))
                    hoverCard = t == Tool.Dig ? "<b>Dig</b>\nDrag over ore, coal, stone or grass. Excavators dig it layer by layer and trucks carry it to hoppers, the lodge or a dump."
                        : t == Tool.Dump ? "<b>Dump</b>\nDrag an area where trucks may drop dirt. It raises the ground up to 2 levels."
                        : "<b>Demolish</b>\nClick a building to remove it and get everything back. Drag over belts.";
            }

            if (hoverTool) DrawBuildCard(hoverType, bar);
            else if (hoverCard != null) DrawTextCard(hoverCard, bar);
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
        void DrawBuildCard(BuildingType bt, Rect bar)
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

            var r = new Rect(bar.x + 8f, bar.y - 176f, 620f, 168f);
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
                case 4: return new[] { ("hauled by train", Item.None, W.RailHauled, B.GoalRail) };
                case 5: return new[] { ("steel wire", Item.Wire, W.Delivered[Item.Wire], B.GoalWire) };
                case 6: return new[] { ("motors", Item.Motor, W.Delivered[Item.Motor], B.GoalMotor) };
                default: return new (string, Item, int, int)[0];
            }
        }

        static readonly string[] TierBlurb =
        {
            "",
            "Wood, stone and ore. Charcoal kilns, smelters and a workshop. Excavators and trucks.",
            "A waterwheel turns line shafts that power a sawmill and a trip hammer.",
            "Dig the coal seam. A steam engine and a steel furnace.",
            "Track, stations and trains haul from the far ore field.",
            "Dynamos on the shafts, power poles, and an electric wire mill.",
            "Electric mines and a machine shop that builds motors.",
            "Coming in a later build.",
        };

        void DrawResearch(float Wd, float Ht)
        {
            MenuStyles();
            var p = Panel(new Rect(Wd / 2f - 800f, 84f, 1600f, Mathf.Min(640f, Ht - 260f)));
            GUI.Label(new Rect(p.x + 20f, p.y + 10f, 700f, 34f), "<b>Research</b>   reach each tier's goal to unlock the next", _h2);
            if (GUI.Button(new Rect(p.xMax - 130f, p.y + 10f, 116f, 34f), "Close (T)", _btn)) _research = false;

            const int tiers = 7;
            float nodeW = 209f, gapX = 16f, top = p.y + 60f, nodeH = p.height - 76f;
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
                string state = done ? "DONE" : now ? "NOW" : "LOCKED";
                GUI.Label(new Rect(nx + 12f, top + 8f, nodeW - 24f, 30f), "<b>Tier " + n + "</b>  " + (_cardTitle.CalcSize(new GUIContent("Tier " + n + "  " + TierNames[n])).x > nodeW - 24f ? "<size=15>" + TierNames[n] + "</size>" : TierNames[n]), _cardTitle);
                GUI.Label(new Rect(nx + 12f, top + 38f, nodeW - 24f, 22f), state, now ? _btnSmallOn : _tiny);
                GUI.Label(new Rect(nx + 12f, top + 60f, nodeW - 24f, 54f), TierBlurb[n], locked ? _tinyDim : _tiny);

                // Unlocks: building pictures with names.
                float uy = top + 118f;
                var unlocks = new List<BuildingType>();
                if (n <= 6) foreach (var t in PageTools(n)) unlocks.Add(TypeOf(t));
                if (n == 1) unlocks.Add(BuildingType.Splitter);
                if (unlocks.Count > 0) GUI.Label(new Rect(nx + 12f, uy, nodeW - 24f, 22f), "Unlocks", _tinyDim);
                uy += 22f;
                int col = 0; float ux = nx + 10f;
                foreach (var bt in unlocks)
                {
                    var icon = View != null ? View.IconIfReady(bt) : null;
                    var cell = new Rect(ux + col * 66f, uy, 64f, 62f);
                    if (icon != null) GUI.DrawTexture(new Rect(cell.x + 7f, cell.y, 50f, 50f), icon, ScaleMode.ScaleToFit, true);
                    GUI.Label(new Rect(cell.x - 1f, cell.y + 46f, 66f, 34f), "<size=11>" + B.Defs[bt].Name + "</size>", locked ? _tinyDim : _tiny);
                    col++;
                    if (col == 3) { col = 0; uy += 80f; }
                }
                if (col > 0) uy += 80f;
                if (n == 1) { GUI.Label(new Rect(nx + 12f, uy, nodeW - 24f, 20f), "+ excavators and trucks (at the garage)", _tiny); uy += 22f; }
                if (n == 4) { GUI.Label(new Rect(nx + 12f, uy, nodeW - 24f, 20f), "+ trains (at a station)", _tiny); uy += 22f; }

                // Goal to finish this tier, with bars and numbers.
                var goals = GoalOf(n);
                if (goals.Length > 0)
                {
                    float gy = node.yMax - 22f - 2 * 40f;   // goals line up across tiers
                    GUI.Label(new Rect(nx + 12f, gy - 24f, nodeW - 24f, 22f), "Goal: deliver to the lodge", _tinyDim);
                    foreach (var g in goals)
                    {
                        int have = Mathf.Min(g.have, g.need);
                        if (done) have = g.need;
                        string letter = g.item != Item.None ? Items.Letter(g.item) + " " : "";
                        GUI.Label(new Rect(nx + 12f, gy, nodeW - 24f, 22f), letter + g.name + "  " + (locked ? "0" : have.ToString()) + " / " + g.need, _tiny);
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
