using System.Collections.Generic;
using System.IO;
using System.Text;
using Kilnwood.Sim;
using UnityEngine;

namespace Kilnwood.Game
{
    /// <summary>
    /// The game loop and gray-box UI. Creates itself in any scene (the build has one empty scene).
    /// IMGUI is temporary; the UI Builder replaces it in the art pass.
    /// </summary>
    public class GameRoot : MonoBehaviour
    {
        enum State { Title, Playing }
        enum Tool { Select, Belt, Woodcutter, Mine, Kiln, Smelter, Workshop, Fisher, Cabin, Splitter, Demolish }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindAnyObjectByType<GameRoot>() != null) return;
            new GameObject("Kilnwood").AddComponent<GameRoot>();
        }

        readonly Balance B = new Balance();
        World W;
        WorldView View;
        CameraRig Rig;
        State _state = State.Title;
        Tool _tool = Tool.Select;
        int _facing = 1;
        int _selected = -1;
        bool _paused;
        bool _menu;
        int _speed = 1;
        bool _goalShown;
        bool _goalPanel;
        float _autosaveAt;
        bool _hasSave;

        string _toast;
        float _toastUntil;

        // Belt drag
        bool _dragging;
        int _dragX, _dragY;

        readonly List<Rect> _uiRects = new List<Rect>();
        readonly List<Rect> _uiRectsLast = new List<Rect>();
        float _uiScale = 1f;

        // Autopilot screenshot run: Kilnwood.exe -kwshots <folder>
        string _shotsDir;
        DemoFactory _demo;
        int _shotIndex;
        readonly float[] _shotTimes = { 5f, 90f, 240f, 420f, 560f, 620f };
        int _frames;
        float _frameTime;
        float _worstFrame;

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = 144;
            Rig = CameraRig.Create();
            Rig.transform.SetParent(transform, false);
            Rig.BoundsMin = new Vector2(-10f, -10f);
            Rig.BoundsMax = new Vector2(138f, 138f);

            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-kwshots") _shotsDir = args[i + 1];

            if (_shotsDir != null)
            {
                Directory.CreateDirectory(_shotsDir);
                SaveSystem.PathOverride = Path.Combine(_shotsDir, "save-shots.json");
                StartWorld(MapGen.NewTier1(B));
                _demo = new DemoFactory(B.PeopleEnabled);
                _speed = 8;
                File.WriteAllText(Path.Combine(_shotsDir, "shots-result.txt"), "Kilnwood autopilot run\n");
            }
            else
            {
                _hasSave = SaveSystem.Exists;
                StartWorld(MapGen.NewTier1(B));
                _state = State.Title;
                Rig.Frame(WorldView.FootCenter(MapGen.LodgeX, MapGen.LodgeY, 4), 20f, 40f, 70f);
                Rig.InputEnabled = false;
            }
        }

        void StartWorld(World w)
        {
            if (View != null) Destroy(View.gameObject);
            W = w;
            var go = new GameObject("Valley");
            go.transform.SetParent(transform, false);
            View = go.AddComponent<WorldView>();
            View.Build(w);
            _state = State.Playing;
            _tool = Tool.Select;
            _selected = -1;
            _goalShown = w.GoalReached;
            _autosaveAt = Time.unscaledTime + 120f;
            Rig.InputEnabled = true;
            Rig.Frame(WorldView.FootCenter(MapGen.LodgeX, MapGen.LodgeY, 4) + new Vector3(0f, 0f, 8f), 0f, 55f, 66f);
        }

        void NewGame() => StartWorld(MapGen.NewTier1(B));

        void ContinueGame()
        {
            var w = SaveSystem.Load(B);
            if (w == null) { Toast("Save couldn't be read. Starting fresh."); NewGame(); return; }
            StartWorld(w);
        }

        void OnApplicationQuit()
        {
            if (_shotsDir == null && _state == State.Playing && W != null) SaveSystem.Save(W);
        }

        void Toast(string s) { _toast = s; _toastUntil = Time.unscaledTime + 3.5f; }

        // ------------------------------------------------------------------ update

        void Update()
        {
            _uiRectsLast.Clear();
            _uiRectsLast.AddRange(_uiRects);

            if (_shotsDir != null) { UpdateShots(); return; }
            if (_state != State.Playing) return;

            if (GameInput.Down(GameKey.Escape))
            {
                if (_tool != Tool.Select || _dragging) { _tool = Tool.Select; _dragging = false; }
                else _menu = !_menu;
            }
            Rig.InputEnabled = !_menu && !_goalPanel;
            if (_menu || _goalPanel) { View.HideGhost(); return; }

            if (GameInput.Down(GameKey.Space)) _paused = !_paused;
            if (GameInput.Down(GameKey.F)) _speed = _speed == 1 ? 2 : 1;
            if (GameInput.Down(GameKey.Alpha1)) SetTool(Tool.Belt);
            if (GameInput.Down(GameKey.Alpha2)) SetTool(Tool.Woodcutter);
            if (GameInput.Down(GameKey.Alpha3)) SetTool(Tool.Mine);
            if (GameInput.Down(GameKey.Alpha4)) SetTool(Tool.Kiln);
            if (GameInput.Down(GameKey.Alpha5)) SetTool(Tool.Smelter);
            if (GameInput.Down(GameKey.Alpha6)) SetTool(Tool.Workshop);
            if (B.PeopleEnabled && GameInput.Down(GameKey.Alpha7)) SetTool(Tool.Fisher);
            if (B.PeopleEnabled && GameInput.Down(GameKey.Alpha8)) SetTool(Tool.Cabin);
            if (GameInput.Down(GameKey.Alpha9)) SetTool(Tool.Splitter);
            if (GameInput.Down(GameKey.X)) SetTool(_tool == Tool.Demolish ? Tool.Select : Tool.Demolish);
            if (GameInput.Down(GameKey.R))
            {
                if (_tool == Tool.Select && _selected >= 0) W.Rotate(_selected);
                else _facing = (_facing + 1) & 3;
            }

            if (!_paused) W.Tick(Mathf.Min(Time.unscaledDeltaTime, 0.1f) * _speed);

            HandleMouse();
            View.ShowSelection(_selected >= 0 && W.Buildings.TryGetValue(_selected, out var sel) ? sel : null);

            if (W.GoalReached && !_goalShown) { _goalShown = true; _goalPanel = true; SaveSystem.Save(W); }
            if (Time.unscaledTime > _autosaveAt) { _autosaveAt = Time.unscaledTime + 120f; SaveSystem.Save(W); }
        }

        void SetTool(Tool t)
        {
            _tool = t;
            _dragging = false;
            if (t != Tool.Select) _selected = -1;
        }

        static BuildingType TypeOf(Tool t)
        {
            switch (t)
            {
                case Tool.Belt: return BuildingType.Belt;
                case Tool.Woodcutter: return BuildingType.Woodcutter;
                case Tool.Mine: return BuildingType.Mine;
                case Tool.Kiln: return BuildingType.Kiln;
                case Tool.Smelter: return BuildingType.Smelter;
                case Tool.Fisher: return BuildingType.Fisher;
                case Tool.Cabin: return BuildingType.Cabin;
                case Tool.Splitter: return BuildingType.Splitter;
                default: return BuildingType.Workshop;
            }
        }

        bool MouseOverUI()
        {
            Vector2 m = GameInput.MousePosition;
            var gui = new Vector2(m.x / _uiScale, (Screen.height - m.y) / _uiScale);
            foreach (var r in _uiRectsLast) if (r.Contains(gui)) return true;
            return false;
        }

        bool MouseTile(out int tx, out int ty)
        {
            tx = ty = -1;
            var ray = Rig.Cam.ScreenPointToRay(GameInput.MousePosition);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out float d)) return false;
            var p = ray.GetPoint(d);
            tx = Mathf.FloorToInt(p.x / WorldView.T);
            ty = Mathf.FloorToInt(p.z / WorldView.T);
            return W.InBounds(tx, ty);
        }

        void HandleMouse()
        {
            bool overUI = MouseOverUI();
            bool onMap = MouseTile(out int tx, out int ty);
            if (GameInput.RightDown) { if (_tool != Tool.Select) SetTool(Tool.Select); else _selected = -1; }

            if (overUI || !onMap)
            {
                if (!_dragging) View.HideGhost();
                if (_dragging && !GameInput.LeftHeld) _dragging = false;
                return;
            }

            switch (_tool)
            {
                case Tool.Select:
                    View.HideGhost();
                    if (GameInput.LeftDown)
                    {
                        var b = W.At(tx, ty);
                        _selected = b != null ? b.Id : -1;
                    }
                    break;

                case Tool.Demolish:
                    View.HideGhost();
                    if (GameInput.LeftDown || (GameInput.LeftHeld && W.At(tx, ty)?.Type == BuildingType.Belt))
                    {
                        var b = W.At(tx, ty);
                        if (b != null && b.Type != BuildingType.Lodge) W.Demolish(b.Id);
                        else if (b != null && GameInput.LeftDown) Toast("The lodge can't be removed");
                    }
                    break;

                case Tool.Belt:
                    if (!_dragging)
                    {
                        var path = new List<(int, int, int)> { (tx, ty, _facing) };
                        CheckPath(path, out int ok, out string why);
                        View.ShowBeltGhosts(path, ok, why);
                        if (GameInput.LeftDown) { _dragging = true; _dragX = tx; _dragY = ty; }
                    }
                    if (_dragging)
                    {
                        var path = BeltPath(_dragX, _dragY, tx, ty);
                        CheckPath(path, out int ok, out string why);
                        View.ShowBeltGhosts(path, ok, why);
                        if (!GameInput.LeftHeld)
                        {
                            int placed = 0;
                            foreach (var (x, y, d) in path)
                            {
                                if (W.Place(BuildingType.Belt, x, y, d) == null) break;
                                placed++;
                            }
                            if (placed < path.Count) Toast("Built " + placed + " of " + path.Count + " belts: " + (why ?? "blocked"));
                            _dragging = false;
                        }
                    }
                    break;

                default:
                    var type = TypeOf(_tool);
                    int size = B.Defs[type].Size;
                    int ox = tx - (size - 1) / 2, oy = ty - (size - 1) / 2;
                    string reason = W.CanPlace(type, ox, oy);
                    View.ShowGhost(type, ox, oy, _facing, reason);
                    if (GameInput.LeftDown)
                    {
                        if (reason != null) Toast(reason);
                        else
                        {
                            var b = W.Place(type, ox, oy, _facing, 0);
                            if (b != null && B.Defs[type].Recipes.Length > 1)
                            {
                                _selected = b.Id;
                                _tool = Tool.Select;
                                Toast("Pick a recipe in the panel on the left");
                            }
                        }
                    }
                    break;
            }
        }

        /// <summary>L-shaped belt path: along the longer axis first, then the other. Each tile points to the next.</summary>
        static List<(int x, int y, int d)> BeltPath(int x0, int y0, int x1, int y1)
        {
            var cells = new List<(int x, int y)>();
            int x = x0, y = y0;
            cells.Add((x, y));
            bool xFirst = Mathf.Abs(x1 - x0) >= Mathf.Abs(y1 - y0);
            for (int pass = 0; pass < 2; pass++)
            {
                bool doX = (pass == 0) == xFirst;
                if (doX) while (x != x1) { x += x1 > x ? 1 : -1; cells.Add((x, y)); }
                else while (y != y1) { y += y1 > y ? 1 : -1; cells.Add((x, y)); }
            }
            var path = new List<(int, int, int)>();
            for (int i = 0; i < cells.Count; i++)
            {
                int d;
                if (i + 1 < cells.Count) d = DirTo(cells[i], cells[i + 1]);
                else if (i > 0) d = DirTo(cells[i - 1], cells[i]);
                else d = 1;
                path.Add((cells[i].x, cells[i].y, d));
            }
            return path;
        }

        static int DirTo((int x, int y) a, (int x, int y) b)
        {
            if (b.x > a.x) return 1;
            if (b.x < a.x) return 3;
            if (b.y > a.y) return 0;
            return 2;
        }

        void CheckPath(List<(int x, int y, int d)> path, out int ok, out string why)
        {
            ok = 0; why = null;
            int wood = W.Storage[Item.Wood];
            foreach (var (x, y, _) in path)
            {
                string r = W.CanPlace(BuildingType.Belt, x, y, true);
                if (r == null && wood < 1) r = "Out of wood";
                if (r != null) { why = r; break; }
                wood--;
                ok++;
            }
        }

        // ------------------------------------------------------------------ autopilot screenshot run

        void UpdateShots()
        {
            _frames++;
            _frameTime += Time.unscaledDeltaTime;
            if (_frames > 30) _worstFrame = Mathf.Max(_worstFrame, Time.unscaledDeltaTime);
            _demo.BuildWhatYouCan(W);
            W.Tick(Mathf.Min(Time.unscaledDeltaTime, 0.1f) * _speed);

            if (_shotIndex < _shotTimes.Length && W.Time >= _shotTimes[_shotIndex])
            {
                var lodge = WorldView.FootCenter(MapGen.LodgeX, MapGen.LodgeY, 4);
                if (_shotIndex == 1) Rig.Frame(lodge + new Vector3(-36f, 0f, -6f), 20f, 42f, 50f);      // river fisher, cabins, west woodcutter line
                else if (_shotIndex == 2) Rig.Frame(lodge + new Vector3(-1f, 0f, 32f), 160f, 38f, 32f);  // brick chain
                else if (_shotIndex == 3) Rig.Frame(lodge + new Vector3(24f, 0f, 2f), 290f, 32f, 34f);   // iron + gear chain
                else if (_shotIndex == 4) Rig.Frame(lodge + new Vector3(-1f, 0f, 30f), 200f, 12f, 26f);  // low angle: kilns, smoke, sky
                else Rig.Frame(lodge + new Vector3(0f, 0f, 8f), 0f, 55f, 80f);
                StartCoroutine(ShotNextFrame((_shotIndex + 1).ToString("00") + "-t" + Mathf.RoundToInt(W.Time) + "s"));
                _shotIndex++;
            }
            if (W.GoalReached && _goalPanel == false)
            {
                _goalPanel = true;
                StartCoroutine(ShotsFinish());
            }
            if (W.Time > 2400f && !_goalPanel) { _goalPanel = true; StartCoroutine(ShotsFinish()); }
        }

        System.Collections.IEnumerator ShotNextFrame(string name)
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(_shotsDir, name + ".png"));
        }

        System.Collections.IEnumerator ShotsFinish()
        {
            var lodge = WorldView.FootCenter(MapGen.LodgeX, MapGen.LodgeY, 4);
            Rig.Frame(lodge + new Vector3(0f, 0f, 8f), 0f, 55f, 80f);
            yield return null;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(_shotsDir, "09-goal.png"));
            var sb = new StringBuilder();
            sb.AppendLine("goal reached: " + W.GoalReached + " at " + W.GoalReachedAt.ToString("0") + " s game time");
            sb.AppendLine("buildings " + W.Buildings.Count + ", gears " + W.Delivered[Item.Gear] + ", bricks " + W.Delivered[Item.Brick] + ", wood in lodge " + W.Storage[Item.Wood]);
            sb.AppendLine("fps average " + (_frames / Mathf.Max(_frameTime, 1e-4f)).ToString("0") + ", worst frame " + (_worstFrame * 1000f).ToString("0.0") + " ms");
            foreach (var b in W.Buildings.Values)
                if (b.Type != BuildingType.Belt) sb.AppendLine("  " + b.Type + " at " + b.X + "," + b.Y + ": " + b.Status + " (fuel " + b.FuelCount + ")");
            File.AppendAllText(Path.Combine(_shotsDir, "shots-result.txt"), sb.ToString());
            SaveSystem.Save(W);
            var reloaded = SaveSystem.Load(B);
            File.AppendAllText(Path.Combine(_shotsDir, "shots-result.txt"), "save round trip: " + (reloaded != null && reloaded.Buildings.Count == W.Buildings.Count ? "OK" : "FAILED") + "\n");
            yield return new WaitForSecondsRealtime(1.5f);
            Application.Quit();
        }

        // ------------------------------------------------------------------ UI

        GUIStyle _h1, _h2, _body, _small, _panel, _btn, _btnOn, _toastStyle, _bad, _badSmall, _btnSmall, _btnSmallOn;

        void Styles()
        {
            if (_h1 != null) return;
            _h1 = new GUIStyle(GUI.skin.label) { fontSize = 64, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _h1.normal.textColor = new Color(1f, 0.74f, 0.35f);
            _h2 = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
            _h2.normal.textColor = Color.white;
            _body = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true, richText = true };
            _body.normal.textColor = new Color(0.97f, 0.94f, 0.88f);
            _small = new GUIStyle(_body) { fontSize = 15 };
            _bad = new GUIStyle(_body) { fontStyle = FontStyle.Bold };
            _bad.normal.textColor = new Color(1f, 0.55f, 0.45f);
            _badSmall = new GUIStyle(_small) { fontStyle = FontStyle.Bold };
            _badSmall.normal.textColor = new Color(1f, 0.62f, 0.4f);
            _btnSmall = new GUIStyle(GUI.skin.button) { fontSize = 14, padding = new RectOffset(4, 4, 4, 4), wordWrap = true };
            _btnSmallOn = new GUIStyle(_btnSmall) { fontStyle = FontStyle.Bold };
            _btnSmallOn.normal.textColor = Look.Accent;
            _btnSmallOn.hover.textColor = Look.Accent;
            _panel = new GUIStyle(GUI.skin.box);
            var bg = new Texture2D(1, 1);
            bg.SetPixel(0, 0, new Color(0.09f, 0.07f, 0.05f, 0.96f));
            bg.Apply();
            _panel.normal.background = bg;
            _btn = new GUIStyle(GUI.skin.button) { fontSize = 17, padding = new RectOffset(8, 8, 5, 5), wordWrap = true };
            _btnOn = new GUIStyle(_btn) { fontStyle = FontStyle.Bold };
            _btnOn.normal.textColor = Look.Accent;
            _btnOn.hover.textColor = Look.Accent;
            _toastStyle = new GUIStyle(_body) { fontSize = 21, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _toastStyle.normal.textColor = new Color(1f, 0.9f, 0.45f);
        }

        Rect Panel(Rect r)
        {
            GUI.Box(r, GUIContent.none, _panel);
            _uiRects.Add(r);
            return r;
        }

        void OnGUI()
        {
            Styles();
            if (Event.current.type == EventType.Layout) _uiRects.Clear();
            _uiScale = Mathf.Max(0.6f, Screen.height / 1080f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(_uiScale, _uiScale, 1f));
            float Wd = Screen.width / _uiScale, Ht = Screen.height / _uiScale;

            if (_state == State.Title && _shotsDir == null) { DrawTitle(Wd, Ht); return; }
            DrawHud(Wd, Ht);
            if (_menu) DrawMenu(Wd, Ht);
            if (_goalPanel && _shotsDir == null) DrawGoal(Wd, Ht);

            if (_toast != null && Time.unscaledTime < _toastUntil)
            {
                var r = new Rect(Wd / 2f - 420f, Ht - 200f, 840f, 48f);
                GUI.Box(r, GUIContent.none, _panel);
                GUI.Label(r, _toast, _toastStyle);
            }
        }

        void DrawTitle(float Wd, float Ht)
        {
            var r = Panel(new Rect(Wd / 2f - 330f, Ht / 2f - 250f, 660f, 500f));
            GUILayout.BeginArea(new Rect(r.x + 30f, r.y + 20f, r.width - 60f, r.height - 40f));
            GUILayout.Label("KILNWOOD", _h1, GUILayout.Height(90f));
            GUILayout.Label("Build an automated factory in a wild river valley. Cut wood, bake charcoal, and let the fires run your machines.", _body);
            GUILayout.Space(20f);
            if (_hasSave && GUILayout.Button("Continue", _btn, GUILayout.Height(54f))) ContinueGame();
            GUILayout.Space(6f);
            if (GUILayout.Button(_hasSave ? "New game (replaces your save on next autosave)" : "New game", _btn, GUILayout.Height(54f))) NewGame();
            GUILayout.Space(6f);
            if (GUILayout.Button("Quit", _btn, GUILayout.Height(44f))) Application.Quit();
            GUILayout.Space(14f);
            GUILayout.Label("Gray-box build: plain shapes on purpose. Art comes once the factory loop is fun.", _small);
            GUILayout.EndArea();
        }

        void DrawHud(float Wd, float Ht)
        {
            // Top bar: storage and goal.
            var top = Panel(new Rect(10f, 10f, Wd - 20f, 58f));
            GUI.Label(new Rect(top.x + 14f, top.y + 6f, 660f, 26f),
                "<b>Lodge</b>   W wood " + W.Storage[Item.Wood] + "   S stone " + W.Storage[Item.Stone] + "   I iron " + W.Storage[Item.Iron], _body);
            GUI.Label(new Rect(top.x + 14f, top.y + 32f, 680f, 24f),
                "C charcoal " + W.Storage[Item.Charcoal] + "   O ore " + W.Storage[Item.Ore] +
                (!B.PeopleEnabled ? "" : "   F fish " + W.Storage[Item.Fish] + "   People: " + W.WorkersUsed + " working, " + W.Population + " living" +
                (W.WorkersNeeded > W.Population ? "   ! NEED " + (W.WorkersNeeded - W.Population) + " MORE (build cabins)" : "") +
                (W.Hungry ? "   ! HUNGRY: machines at half speed, send fish to the lodge" : "")),
                W.Hungry || W.WorkersNeeded > W.Population ? _badSmall : _small);
            var goal = new Rect(top.x + 690f, top.y + 6f, 560f, 48f);
            GUI.Label(goal, "<b>Waterwheel parts</b>   G gears " + Mathf.Min(W.Delivered[Item.Gear], B.GoalGear) + "/" + B.GoalGear +
                "   B bricks " + Mathf.Min(W.Delivered[Item.Brick], B.GoalBrick) + "/" + B.GoalBrick +
                (W.GoalReached ? "   UNLOCKED" : ""), _body);
            if (_shotsDir == null)
            {
                float bx = top.xMax - 340f;
                if (GUI.Button(new Rect(bx, top.y + 9f, 105f, 40f), _paused ? "Resume" : "Pause", _btn)) _paused = !_paused;
                if (GUI.Button(new Rect(bx + 112f, top.y + 9f, 105f, 40f), _speed == 1 ? "Fast" : "Normal", _btn)) _speed = _speed == 1 ? 2 : 1;
                if (GUI.Button(new Rect(bx + 224f, top.y + 9f, 105f, 40f), "Menu", _btn)) _menu = true;
            }

            DrawBuildBar(Wd, Ht);
            if (_selected >= 0 && W.Buildings.TryGetValue(_selected, out var b)) DrawBuildingPanel(b, Wd, Ht);

            var hint = _tool == Tool.Select ? "Click a building to inspect it.  1-6 build, 9 splitter  ·  X demolish  ·  Space pause  ·  F fast  ·  WASD/edge pan  ·  scroll zoom  ·  Q/E rotate view"
                : _tool == Tool.Demolish ? "DEMOLISH: click to remove (full refund). Hold and drag over belts. Right click or Esc to stop."
                : _tool == Tool.Belt ? "BELTS: click and drag to lay a line; items flow toward where you drag. R turns a single belt. Right click to stop."
                : "Click to place  ·  R turns it (the yellow side is the output)  ·  right click to stop.";
            GUI.Label(new Rect(14f, Ht - 128f, Wd - 28f, 24f), hint, _small);
            if (_paused) GUI.Label(new Rect(Wd / 2f - 60f, 80f, 200f, 30f), "<b>PAUSED</b>", _body);
        }

        void DrawBuildBar(float Wd, float Ht)
        {
            var bar = Panel(new Rect(Wd / 2f - 655f, Ht - 100f, 1310f, 90f));
            Tool[] tools = B.PeopleEnabled
                ? new[] { Tool.Belt, Tool.Woodcutter, Tool.Mine, Tool.Kiln, Tool.Smelter, Tool.Workshop, Tool.Fisher, Tool.Cabin, Tool.Splitter, Tool.Demolish }
                : new[] { Tool.Belt, Tool.Woodcutter, Tool.Mine, Tool.Kiln, Tool.Smelter, Tool.Workshop, Tool.Splitter, Tool.Demolish };
            float w = B.PeopleEnabled ? 125f : 157f;
            for (int i = 0; i < tools.Length; i++)
            {
                var t = tools[i];
                string label;
                if (t == Tool.Demolish) label = "X  Demolish\nfull refund";
                else
                {
                    var d = B.Defs[TypeOf(t)];
                    var cost = new StringBuilder();
                    foreach (var c in d.Cost) cost.Append(cost.Length > 0 ? " + " : "").Append(c.count).Append(' ').Append(Items.Letter(c.item));
                    bool fuel = d.BurnsFuel || System.Array.Exists(d.Recipes, rc => rc.BurnsFuel);
                    label = d.Key + "  " + d.Name + "\n" + cost + (fuel ? " · fuel" : "") +
                            (!B.PeopleEnabled ? "" : d.Workers > 0 ? "\n" + d.Workers + (d.Workers == 1 ? " worker" : " workers") : d.Homes > 0 ? "\nhomes " + d.Homes : "");
                }
                if (GUI.Button(new Rect(bar.x + 8f + i * (w + 4f), bar.y + 8f, w, 74f), (_tool == t ? "> " : "") + label, _tool == t ? _btnSmallOn : _btnSmall))
                    SetTool(_tool == t ? Tool.Select : t);
            }
        }

        void DrawBuildingPanel(Building b, float Wd, float Ht)
        {
            var def = B.Defs[b.Type];
            var r = Panel(new Rect(10f, 78f, 380f, 470f));
            GUILayout.BeginArea(new Rect(r.x + 14f, r.y + 10f, r.width - 28f, r.height - 20f));
            GUILayout.Label(def.Name, _h2);
            if (b.Type == BuildingType.Lodge)
            {
                GUILayout.Label("Everything that reaches the lodge is stored here. Building costs come out of it. " +
                    (B.PeopleEnabled ? "Homes for " + def.Homes + " people; everyone eats 1 fish every " + B.EatSecondsPerPerson.ToString("0") + " s from here." : ""), _small);
                var sb = new StringBuilder();
                foreach (Item it in System.Enum.GetValues(typeof(Item)))
                    if (it != Item.None) sb.AppendLine(Items.Letter(it) + "  " + Items.Name(it) + ": " + W.Storage[it] + "   (delivered " + W.Delivered[it] + ")");
                GUILayout.Label(sb.ToString(), _body);
                GUILayout.EndArea();
                return;
            }
            GUILayout.Label("Status: <b>" + b.Status + "</b>", _body);
            if (B.PeopleEnabled && def.Workers > 0)
                GUILayout.Label("Workers: " + def.Workers + (b.Staffed ? " (on the job)" : "  ! not enough people: build a cabin (8)"), b.Staffed ? _small : _badSmall);
            if (b.Type == BuildingType.Woodcutter)
            {
                int trees = W.TreesInReach(b, out _);
                GUILayout.Label("Trees with logs in reach: " + trees + " (cuts its own spot plus 1 tile around; trees regrow 1 log per " + B.RegrowSecondsPerLog.ToString("0") + " s)", _small);
            }
            if (b.Type == BuildingType.Splitter)
                GUILayout.Label("Hands items out in turn: front, right, left. Feed it from the back or sides.", _small);
            if (b.Type != BuildingType.Belt && b.Type != BuildingType.Cabin)
                GUILayout.Label("Output side: " + Dir.Names[b.Facing] + " (yellow marker)", _small);
            if (b.Type == BuildingType.Belt)
                GUILayout.Label("Carrying: " + (b.BeltItem == Item.None ? "nothing" : Items.Name(b.BeltItem)) + "   flow: " + Dir.Names[b.Facing], _small);
            if (W.BurnsFuel(b))
                GUILayout.Label("Fuel: W wood " + b.FuelWood + "   C charcoal " + b.FuelCharcoal + "  (holds " + B.FuelCap + ", burns charcoal first)" +
                    (b.FuelLeft > 0f ? "\nBurning: " + b.FuelLeft.ToString("0") + " s left" : ""), _small);
            else if (b.Type == BuildingType.Kiln)
                GUILayout.Label("Charcoal needs no fuel: the wood smoulders itself. Bricks burn fuel.", _small);
            if (def.SitsOn != null && b.Type != BuildingType.Woodcutter)
            {
                var res = W.ExtractorResource(b, out int tiles);
                GUILayout.Label("Pulling " + Items.Name(res) + " from " + tiles + " tiles (" + W.ExtractRate(b.Type, tiles).ToString("0.0") + " per second)", _small);
            }
            if (def.Recipes.Length > 0)
            {
                GUILayout.Space(6f);
                GUILayout.Label("Recipe", _body);
                for (int i = 0; i < def.Recipes.Length; i++)
                {
                    var rc = def.Recipes[i];
                    var sb = new StringBuilder();
                    for (int k = 0; k < rc.InItems.Length; k++) sb.Append(k > 0 ? " + " : "").Append(rc.InCounts[k]).Append(' ').Append(Items.Letter(rc.InItems[k]));
                    string text = (b.Recipe == i ? "> " : "") + rc.Name + "  (" + sb + " -> 1 " + Items.Letter(rc.Out) + ", " + rc.Seconds + "s)";
                    if (GUILayout.Button(text, b.Recipe == i ? _btnOn : _btn, GUILayout.Height(38f))) W.SetRecipe(b.Id, i);
                }
                var cur = W.RecipeOf(b);
                var ins = new StringBuilder("Inside: ");
                for (int k = 0; k < cur.InItems.Length; k++)
                {
                    b.Input.TryGetValue(cur.InItems[k], out int have);
                    ins.Append(Items.Letter(cur.InItems[k])).Append(' ').Append(have).Append('/').Append(cur.InCounts[k]).Append("   ");
                }
                GUILayout.Label(ins.ToString(), _small);
                if (b.Crafting) GUILayout.Label("Progress " + Mathf.RoundToInt(100f * b.Work / cur.Seconds) + "%", _small);
            }
            if (b.OutCount > 0) GUILayout.Label("Waiting to send: " + b.OutCount + " " + Items.Name(b.OutItem), _small);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Turn (R)", _btn, GUILayout.Height(40f))) W.Rotate(b.Id);
            if (GUILayout.Button("Demolish", _btn, GUILayout.Height(40f))) { W.Demolish(b.Id); _selected = -1; }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        void DrawMenu(float Wd, float Ht)
        {
            var r = Panel(new Rect(Wd / 2f - 220f, Ht / 2f - 220f, 440f, 440f));
            GUILayout.BeginArea(new Rect(r.x + 24f, r.y + 18f, r.width - 48f, r.height - 36f));
            GUILayout.Label("Menu", _h2);
            if (GUILayout.Button("Resume", _btn, GUILayout.Height(46f))) _menu = false;
            if (GUILayout.Button("Save now", _btn, GUILayout.Height(40f))) { SaveSystem.Save(W); Toast("Saved"); }
            if (GUILayout.Button(Screen.fullScreen ? "Windowed" : "Fullscreen", _btn, GUILayout.Height(40f))) Screen.fullScreen = !Screen.fullScreen;
            var names = QualitySettings.names;
            int q = QualitySettings.GetQualityLevel();
            if (names.Length > 0 && GUILayout.Button("Graphics: " + names[q], _btn, GUILayout.Height(40f))) QualitySettings.SetQualityLevel((q + 1) % names.Length, true);
            GUILayout.Label("The game also saves every 2 minutes and when you quit.", _small);
            if (GUILayout.Button("Save and quit", _btn, GUILayout.Height(40f))) { SaveSystem.Save(W); Application.Quit(); }
            GUILayout.EndArea();
        }

        void DrawGoal(float Wd, float Ht)
        {
            var r = Panel(new Rect(Wd / 2f - 330f, Ht / 2f - 170f, 660f, 340f));
            GUILayout.BeginArea(new Rect(r.x + 28f, r.y + 20f, r.width - 56f, r.height - 40f));
            GUILayout.Label("TIER 2 UNLOCKED", _h1, GUILayout.Height(80f));
            GUILayout.Label("You have the gears and bricks for the valley's first waterwheel. Tier 2 brings water power down the river.", _body);
            GUILayout.Label("That's the end of this test build. You can keep building here as long as you like.", _small);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Keep building", _btn, GUILayout.Height(50f))) _goalPanel = false;
            GUILayout.EndArea();
        }
    }
}
