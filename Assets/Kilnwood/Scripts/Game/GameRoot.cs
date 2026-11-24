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
        enum Tool { Select, Belt, Woodcutter, Mine, Kiln, Smelter, Workshop, Fisher, Cabin, Splitter, Garage, Hopper, Dig, Dump, Demolish, Shaft, Waterwheel, Sawmill, TripHammer, SteamEngine, Furnace, Rail, Station }

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
        int _page = 1;          // build bar page: 1 = Tier 1, 2 = Tier 2 water power, 3 = Tier 3 steam
        int _tierShown;         // highest tier whose unlock panel was shown
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
        readonly float[] _shotTimes = { 5f, 90f, 240f, 420f, 560f, 620f, 800f, 900f, 1000f, 1100f };
        bool _ironGift3, _tier3Shots, _tier2Shot;
        int _t3Shot; float _t3At;
        bool _ironGift;
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
                if (args[i] == "-kwshots") _shotsDir = Path.GetFullPath(args[i + 1]);   // screenshots resolve relative paths elsewhere

            if (_shotsDir != null)
            {
                Directory.CreateDirectory(_shotsDir);
                SaveSystem.PathOverride = Path.Combine(_shotsDir, "save-shots.json");
                StartWorld(MapGen.NewTier1(B));
                _demo = new DemoFactory(B.PeopleEnabled, pit: true, tier2: true, tier3: true, tier4: true);
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
            _tierShown = w.Tier;
            _page = 1;
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
            if (GameInput.Down(GameKey.Tab)) SwitchPage();
            var pageTools = PageTools(_page);
            var digits = new[] { GameKey.Alpha1, GameKey.Alpha2, GameKey.Alpha3, GameKey.Alpha4, GameKey.Alpha5, GameKey.Alpha6, GameKey.Alpha7, GameKey.Alpha8 };
            for (int k = 0; k < digits.Length; k++)
                if (GameInput.Down(digits[k]) && k < pageTools.Length) SetTool(pageTools[k]);
            if (GameInput.Down(GameKey.G)) SetTool(_tool == Tool.Dig ? Tool.Select : Tool.Dig);
            if (GameInput.Down(GameKey.V)) SetTool(_tool == Tool.Dump ? Tool.Select : Tool.Dump);
            if (GameInput.Down(GameKey.Alpha9) && _page == 1) SetTool(Tool.Splitter);
            if (GameInput.Down(GameKey.X)) SetTool(_tool == Tool.Demolish ? Tool.Select : Tool.Demolish);
            if (GameInput.Down(GameKey.R))
            {
                if (_tool == Tool.Select && _selected >= 0) W.Rotate(_selected);
                else _facing = (_facing + 1) & 3;
            }

            if (!_paused) W.Tick(Mathf.Min(Time.unscaledDeltaTime, 0.1f) * _speed);

            HandleMouse();
            View.ShowSelection(_selected >= 0 && W.Buildings.TryGetValue(_selected, out var sel) ? sel : null);

            if (W.Tier > _tierShown) { _tierShown = W.Tier; _goalPanel = true; SaveSystem.Save(W); }
            if (Time.unscaledTime > _autosaveAt) { _autosaveAt = Time.unscaledTime + 120f; SaveSystem.Save(W); }
        }

        /// <summary>Numbered tools on each build page (key 1 = first). Page 1 also has 9 = splitter.</summary>
        Tool[] PageTools(int page) => page == 4
            ? new[] { Tool.Rail, Tool.Station }
            : page == 3
            ? new[] { Tool.SteamEngine, Tool.Furnace }
            : page == 2
            ? new[] { Tool.Shaft, Tool.Waterwheel, Tool.Sawmill, Tool.TripHammer }
            : B.PeopleEnabled
                ? new[] { Tool.Belt, Tool.Woodcutter, Tool.Mine, Tool.Kiln, Tool.Smelter, Tool.Workshop, Tool.Fisher, Tool.Cabin }
                : new[] { Tool.Belt, Tool.Woodcutter, Tool.Mine, Tool.Kiln, Tool.Smelter, Tool.Workshop, Tool.Garage, Tool.Hopper };

        void SwitchPage()
        {
            if (W.Tier < 2) { Toast("Page 2 (water power) opens at Tier 2: deliver the waterwheel parts first"); return; }
            int pages = Mathf.Min(W.Tier, 4);
            _page = _page % pages + 1;
            SetTool(Tool.Select);
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
                case Tool.Garage: return BuildingType.Garage;
                case Tool.Hopper: return BuildingType.Hopper;
                case Tool.Shaft: return BuildingType.Shaft;
                case Tool.Waterwheel: return BuildingType.Waterwheel;
                case Tool.Sawmill: return BuildingType.Sawmill;
                case Tool.TripHammer: return BuildingType.TripHammer;
                case Tool.SteamEngine: return BuildingType.SteamEngine;
                case Tool.Furnace: return BuildingType.Furnace;
                case Tool.Rail: return BuildingType.Rail;
                case Tool.Station: return BuildingType.Station;
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
                        var b = W.AtOrShaft(tx, ty);
                        _selected = b != null ? b.Id : -1;
                    }
                    break;

                case Tool.Demolish:
                    View.HideGhost();
                    var under = W.AtOrShaft(tx, ty);
                    if (GameInput.LeftDown || (GameInput.LeftHeld && under != null && (under.Type == BuildingType.Belt || under.Type == BuildingType.Shaft)))
                    {
                        // Over a belt with a shaft above it: the shaft comes off first (it's on top).
                        var b = W.ShaftAt(tx, ty) ?? W.At(tx, ty);
                        if (b != null && b.Type != BuildingType.Lodge) W.Demolish(b.Id);
                        else if (b != null && GameInput.LeftDown) Toast("The lodge can't be removed");
                        else if (b == null) W.ClearMarks(tx, ty, tx, ty);
                    }
                    break;

                case Tool.Dig:
                case Tool.Dump:
                {
                    bool dig = _tool == Tool.Dig;
                    if (!_dragging)
                    {
                        View.ShowAreaGhost(tx, ty, tx, ty, dig, dig ? W.CanMarkDig(tx, ty) : W.CanMarkDump(tx, ty));
                        if (GameInput.LeftDown) { _dragging = true; _dragX = tx; _dragY = ty; }
                    }
                    if (_dragging)
                    {
                        bool clear = GameInput.Held(GameKey.Shift);
                        View.ShowAreaGhost(_dragX, _dragY, tx, ty, dig, clear ? "Shift: clearing marks" : null);
                        if (!GameInput.LeftHeld)
                        {
                            int n = clear ? W.ClearMarks(_dragX, _dragY, tx, ty)
                                  : dig ? W.MarkDig(_dragX, _dragY, tx, ty) : W.MarkDump(_dragX, _dragY, tx, ty);
                            int total = (Mathf.Abs(tx - _dragX) + 1) * (Mathf.Abs(ty - _dragY) + 1);
                            if (clear) Toast("Cleared " + n + " marked tiles");
                            else if (n == 0) Toast(dig ? "Nothing here can be dug (water, trees, buildings or already at the bottom)" : "Nothing here can take dirt");
                            else if (dig && W.CountVehicles(VehicleType.Excavator) == 0) Toast("Marked " + n + " tiles. Build a garage (7) and buy an excavator and a truck to dig them");
                            else if (n < total) Toast("Marked " + n + " of " + total + " tiles (skipped water, trees and buildings)");
                            _dragging = false;
                        }
                    }
                    break;
                }

                case Tool.Belt:
                case Tool.Shaft:
                case Tool.Rail:
                {
                    var lineType = _tool == Tool.Shaft ? BuildingType.Shaft : _tool == Tool.Rail ? BuildingType.Rail : BuildingType.Belt;
                    string noun = _tool == Tool.Shaft ? "shafts" : _tool == Tool.Rail ? "rails" : "belts";
                    if (!_dragging)
                    {
                        var path = new List<(int, int, int)> { (tx, ty, _facing) };
                        CheckPath(path, lineType, out int ok, out string why);
                        View.ShowBeltGhosts(path, ok, why, noun);
                        if (GameInput.LeftDown) { _dragging = true; _dragX = tx; _dragY = ty; }
                    }
                    if (_dragging)
                    {
                        var path = BeltPath(_dragX, _dragY, tx, ty);
                        CheckPath(path, lineType, out int ok, out string why);
                        View.ShowBeltGhosts(path, ok, why, noun);
                        if (!GameInput.LeftHeld)
                        {
                            int placed = 0;
                            foreach (var (x, y, d) in path)
                            {
                                if (W.Place(lineType, x, y, d) == null) break;
                                placed++;
                            }
                            if (placed < path.Count) Toast("Built " + placed + " of " + path.Count + " " + noun + ": " + (why ?? "blocked"));
                            _dragging = false;
                        }
                    }
                    break;
                }

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

        void CheckPath(List<(int x, int y, int d)> path, BuildingType type, out int ok, out string why)
        {
            ok = 0; why = null;
            int wood = W.Storage[Item.Wood], steel = W.Storage[Item.Steel];
            foreach (var (x, y, _) in path)
            {
                string r = W.CanPlace(type, x, y, true);
                if (r == null && B.Defs[type].Tier > W.Tier) r = "Unlocks at Tier " + B.Defs[type].Tier;
                if (r == null && wood < 1) r = "Out of wood";
                if (r == null && type == BuildingType.Rail && steel < 1) r = "Out of steel (T)";
                if (r != null) { why = r; break; }
                wood--; steel--;
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
            _demo.PitActions(W);
            _demo.RailActions(W);
            if (!_ironGift && W.Time >= 200f)
            {
                // The demo factory sends all its iron to gears, so the screenshot run hands it the iron for the vehicles.
                _ironGift = true;
                W.Storage[Item.Iron] += 60;
                File.AppendAllText(Path.Combine(_shotsDir, "shots-result.txt"), "autopilot: +60 iron at 200 s for the pit vehicles\n");
            }
            if (!_ironGift3 && W.Tier >= 3)
            {
                _ironGift3 = true;
                W.Storage[Item.Iron] += 40;
                File.AppendAllText(Path.Combine(_shotsDir, "shots-result.txt"), "autopilot: +40 iron at Tier 3 (" + W.Time.ToString("0") + " s) for the extra pit vehicles\n");
            }
            W.Tick(Mathf.Min(Time.unscaledDeltaTime, 0.1f) * _speed);
            if (W.Tier >= 3 && _t3At <= 0f) _t3At = W.Time + 330f;   // give the coal pit time to open
            if (W.Tier >= 3 && !_tier3Shots && _t3Shot < 4 && _t3At > 0f && W.Time >= _t3At)
            {
                // Tier 3 shots: coal pit, steam engine, furnace, then the whole valley.
                if (_t3Shot == 0) Rig.Frame(WorldView.FootCenter(56, 29, 2), 300f, 38f, 26f);
                else if (_t3Shot == 1) Rig.Frame(WorldView.FootCenter(25, 26, 2), 210f, 32f, 20f);
                else if (_t3Shot == 2) Rig.Frame(WorldView.FootCenter(39, 29, 2), 150f, 34f, 22f);
                else Rig.Frame(WorldView.FootCenter(40, 28, 2), 20f, 45f, 75f);
                string[] t3Names = { "coal-pit", "steam-engine", "steel-furnace", "valley" };
                StartCoroutine(ShotNextFrame((20 + _t3Shot) + "-tier3-" + t3Names[_t3Shot] + "-t" + Mathf.RoundToInt(W.Time) + "s"));
                _t3Shot++;
                _t3At = W.Time + 130f;
                if (_t3Shot >= 4) _tier3Shots = true;
            }

            if (_shotIndex < _shotTimes.Length && W.Time >= _shotTimes[_shotIndex])
            {
                var lodge = WorldView.FootCenter(MapGen.LodgeX, MapGen.LodgeY, 4);
                if (_shotIndex == 1) Rig.Frame(lodge + new Vector3(-36f, 0f, -6f), 20f, 42f, 50f);      // river fisher, cabins, west woodcutter line
                else if (_shotIndex == 2) Rig.Frame(lodge + new Vector3(-1f, 0f, 32f), 160f, 38f, 32f);  // brick chain
                else if (_shotIndex == 3) Rig.Frame(lodge + new Vector3(30f, 0f, -6f), 300f, 40f, 30f);  // open pit: excavator + trucks
                else if (_shotIndex == 4) Rig.Frame(lodge + new Vector3(32f, -2f, -6f), 235f, 20f, 26f); // low angle into the pit, sky
                else if (_shotIndex == 6) Rig.Frame(WorldView.FootCenter(5, 18, 2), 60f, 30f, 18f);          // Tier 2: waterwheel on the bank
                else if (_shotIndex == 7) Rig.Frame(WorldView.FootCenter(16, 22, 2), 200f, 35f, 22f);        // sawmill under its woodcutter, shaft
                else if (_shotIndex == 8) Rig.Frame(WorldView.FootCenter(42, 34, 2), 250f, 32f, 20f);        // trip hammer, shaft over the belt
                else if (_shotIndex == 9) Rig.Frame(WorldView.FootCenter(22, 22, 2), 0f, 50f, 70f);          // the long shaft across the valley
                else Rig.Frame(lodge + new Vector3(0f, 0f, 8f), 0f, 55f, 80f);
                StartCoroutine(ShotNextFrame((_shotIndex + 1).ToString("00") + "-t" + Mathf.RoundToInt(W.Time) + "s"));
                _shotIndex++;
            }
            if (W.GoalReached && !_goalShown)
            {
                // Tier 1 goal: one overview shot, then carry on into Tier 2.
                _goalShown = true;
                StartCoroutine(TierShot("09-tier1-goal-t" + Mathf.RoundToInt(W.Time) + "s"));
                File.AppendAllText(Path.Combine(_shotsDir, "shots-result.txt"), "tier 1 goal at " + W.GoalReachedAt.ToString("0") + " s game time\n");
            }
            if (W.Tier >= 3 && W.Tier2GoalAt > 0f && !_tier2Shot) { _tier2Shot = true; StartCoroutine(TierShot("19-tier2-goal-t" + Mathf.RoundToInt(W.Time) + "s")); }
            if (W.Tier >= 4 && _railAt <= 0f) _railAt = W.Time + 150f;
            bool trainOut = false;
            foreach (var tv in W.Vehicles) if (tv.Type == VehicleType.Train && tv.State == VehicleState.Driving && tv.PathStep > 4 && tv.PathStep < tv.Path.Count - 4) trainOut = true;
            if (_railAt > 0f && _railShot < 3 && W.Time >= _railAt && (_railShot != 1 || trainOut || W.Time >= _railAt + 60f))
            {
                // Tier 4 shots: the far station and its mines, a train on the line, the lodge yard.
                string[] names = { "field-station", "train", "yard" };
                if (_railShot == 0) Rig.Frame(WorldView.FootCenter(DemoFactory.FieldStationX, DemoFactory.FieldStationY, 2), 200f, 35f, 26f);
                else if (_railShot == 1) Rig.Frame(TrainFocus(), 140f, 30f, 22f);
                else Rig.Frame(WorldView.FootCenter(DemoFactory.YardStationX, DemoFactory.YardStationY, 2), 30f, 38f, 30f);
                StartCoroutine(ShotNextFrame((24 + _railShot) + "-tier4-" + names[_railShot] + "-t" + Mathf.RoundToInt(W.Time) + "s"));
                _railShot++;
                _railAt = W.Time + 40f;
            }
            if ((W.Tier >= 5 || W.Time > 4800f) && !_goalPanel) { _goalPanel = true; StartCoroutine(ShotsFinish()); }
        }

        float _railAt; int _railShot;

        Vector3 TrainFocus()
        {
            // A train out on the line if there is one, else any train.
            foreach (var v in W.Vehicles) if (v.Type == VehicleType.Train && v.State == VehicleState.Driving && v.PathStep > 4 && v.PathStep < v.Path.Count - 4) return new Vector3(v.X * WorldView.T, 0f, v.Y * WorldView.T);
            foreach (var v in W.Vehicles) if (v.Type == VehicleType.Train) return new Vector3(v.X * WorldView.T, 0f, v.Y * WorldView.T);
            return WorldView.FootCenter(45, 14, 1);
        }

        System.Collections.IEnumerator ShotNextFrame(string name)
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(_shotsDir, name + ".png"));
        }

        System.Collections.IEnumerator TierShot(string name)
        {
            var lodge = WorldView.FootCenter(MapGen.LodgeX, MapGen.LodgeY, 4);
            Rig.Frame(lodge + new Vector3(0f, 0f, 8f), 0f, 55f, 80f);
            yield return null;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(_shotsDir, name + ".png"));
        }

        System.Collections.IEnumerator ShotsFinish()
        {
            var lodge = WorldView.FootCenter(MapGen.LodgeX, MapGen.LodgeY, 4);
            Rig.Frame(lodge + new Vector3(-12f, 0f, 2f), 0f, 55f, 95f);
            yield return null;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(_shotsDir, "29-final.png"));
            var sb = new StringBuilder();
            sb.AppendLine("goal reached: " + W.GoalReached + " at " + W.GoalReachedAt.ToString("0") + " s game time");
            sb.AppendLine("tier " + W.Tier + (W.Tier >= 3 ? ", tier 2 goal at " + W.Tier2GoalAt.ToString("0") + " s" : ", tier 2 goal NOT reached") +
                ", planks " + W.Delivered[Item.Plank] + ", plates " + W.Delivered[Item.Plate]);
            sb.AppendLine(W.Tier >= 4 ? "tier 3 goal at " + W.Tier3GoalAt.ToString("0") + " s" : "tier 3 goal NOT reached" + ", steel " + W.Delivered[Item.Steel] + ", coal " + W.Delivered[Item.Coal]);
            sb.AppendLine(W.Tier >= 5 ? "tier 4 goal at " + W.Tier4GoalAt.ToString("0") + " s, hauled by rail " + W.RailHauled + ", trains " + W.CountTrains() : "tier 4 goal NOT reached, hauled by rail " + W.RailHauled + ", trains " + W.CountTrains());
            int dugTiles = 0; foreach (var u in W.Units) if (u < 0) dugTiles++;
            sb.AppendLine("vehicles " + W.Vehicles.Count + ", dug tiles " + dugTiles + ", ore delivered " + W.Delivered[Item.Ore]);
            sb.AppendLine("buildings " + W.Buildings.Count + ", gears " + W.Delivered[Item.Gear] + ", bricks " + W.Delivered[Item.Brick] + ", wood in lodge " + W.Storage[Item.Wood]);
            sb.AppendLine("fps average " + (_frames / Mathf.Max(_frameTime, 1e-4f)).ToString("0") + ", worst frame " + (_worstFrame * 1000f).ToString("0.0") + " ms");
            foreach (var b in W.Buildings.Values)
                if (b.Type != BuildingType.Belt && b.Type != BuildingType.Shaft && b.Type != BuildingType.Rail) sb.AppendLine("  " + b.Type + " at " + b.X + "," + b.Y + ": " + b.Status + " (fuel " + b.FuelCount + ", power " + b.Power.ToString("0.00") + ")");
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
            GUILayout.Label("Test build: Tier 1, the open pit (excavators and trucks), Tier 2 water power, Tier 3 steam and Tier 4 rails.", _small);
            GUILayout.EndArea();
        }

        void DrawHud(float Wd, float Ht)
        {
            // Top bar: storage and goal.
            var top = Panel(new Rect(10f, 10f, Wd - 20f, 58f));
            GUI.Label(new Rect(top.x + 14f, top.y + 6f, 660f, 26f),
                "<b>Lodge</b>   W wood " + W.Storage[Item.Wood] + "   S stone " + W.Storage[Item.Stone] + "   I iron " + W.Storage[Item.Iron], _body);
            GUI.Label(new Rect(top.x + 14f, top.y + 32f, 860f, 24f),
                "C charcoal " + W.Storage[Item.Charcoal] + "   O ore " + W.Storage[Item.Ore] +
                (W.Tier >= 2 ? "   B brick " + W.Storage[Item.Brick] + "   G gear " + W.Storage[Item.Gear] + "   P plank " + W.Storage[Item.Plank] + "   L plate " + W.Storage[Item.Plate] : "") +
                (W.Tier >= 3 ? "   K coal " + W.Storage[Item.Coal] + "   T steel " + W.Storage[Item.Steel] : "") +
                (!B.PeopleEnabled ? "" : "   F fish " + W.Storage[Item.Fish] + "   People: " + W.WorkersUsed + " working, " + W.Population + " living" +
                (W.WorkersNeeded > W.Population ? "   ! NEED " + (W.WorkersNeeded - W.Population) + " MORE (build cabins)" : "") +
                (W.Hungry ? "   ! HUNGRY: machines at half speed, send fish to the lodge" : "")),
                W.Hungry || W.WorkersNeeded > W.Population ? _badSmall : _small);
            var goal = new Rect(top.x + 690f, top.y + 6f, 600f, 26f);
            string goalText = W.Tier == 1
                ? "<b>Tier 1 goal: waterwheel parts</b>   G gears " + Mathf.Min(W.Delivered[Item.Gear], B.GoalGear) + "/" + B.GoalGear +
                  "   B bricks " + Mathf.Min(W.Delivered[Item.Brick], B.GoalBrick) + "/" + B.GoalBrick
                : W.Tier == 2
                ? "<b>Tier 2 goal: boiler parts</b>   P planks " + Mathf.Min(W.Delivered[Item.Plank], B.GoalPlank) + "/" + B.GoalPlank +
                  "   L plates " + Mathf.Min(W.Delivered[Item.Plate], B.GoalPlate) + "/" + B.GoalPlate
                : W.Tier == 3
                ? "<b>Tier 3 goal: rail parts</b>   T steel " + Mathf.Min(W.Delivered[Item.Steel], B.GoalSteel) + "/" + B.GoalSteel +
                  "   K coal " + Mathf.Min(W.Delivered[Item.Coal], B.GoalCoal) + "/" + B.GoalCoal
                : W.Tier == 4
                ? "<b>Tier 4 goal: first railway</b>   hauled to the lodge by train " + Mathf.Min(W.RailHauled, B.GoalRail) + "/" + B.GoalRail
                : "<b>Tier 5 unlocked</b>   (the next tier comes in a later build)";
            GUI.Label(goal, goalText, _body);
            if (_shotsDir == null)
            {
                float bx = top.xMax - 340f;
                if (GUI.Button(new Rect(bx, top.y + 9f, 105f, 40f), _paused ? "Resume" : "Pause", _btn)) _paused = !_paused;
                if (GUI.Button(new Rect(bx + 112f, top.y + 9f, 105f, 40f), _speed == 1 ? "Fast" : "Normal", _btn)) _speed = _speed == 1 ? 2 : 1;
                if (GUI.Button(new Rect(bx + 224f, top.y + 9f, 105f, 40f), "Menu", _btn)) _menu = true;
            }

            DrawBuildBar(Wd, Ht);
            if (_selected >= 0 && W.Buildings.TryGetValue(_selected, out var b)) DrawBuildingPanel(b, Wd, Ht);

            var hint = _tool == Tool.Select ? "Click a building to inspect it.  1-8 build, 9 splitter" + (W.Tier >= 2 ? ", Tab next build page" : "") + "  ·  G dig  ·  V dump  ·  X demolish  ·  Space pause  ·  F fast  ·  WASD/edge pan  ·  scroll zoom  ·  Q/E rotate view"
                : _tool == Tool.Demolish ? "DEMOLISH: click to remove (full refund). Hold and drag over belts. Right click or Esc to stop."
                : _tool == Tool.Dig ? "DIG: drag over ore, stone or grass. Excavators dig it layer by layer; trucks carry it off. Hold Shift while dragging to clear marks."
                : _tool == Tool.Dump ? "DUMP: drag an area where trucks may drop dirt (raises the ground up to 2 levels). Hold Shift to clear marks."
                : _tool == Tool.Belt ? "BELTS: click and drag to lay a line; items flow toward where you drag. R turns a single belt. Right click to stop."
                : _tool == Tool.Shaft ? "LINE SHAFTS: drag a line from a waterwheel to machines. They run overhead, so they can cross belts. Machines touching a shaft share its power."
                : _tool == Tool.Rail ? "RAILS: drag a line of track (1 T steel + 1 W wood each). Trucks can cross it. Trains stop on the rail right beside a station."
                : _tool == Tool.Station ? "STATION: put it beside the track. Near the lodge it unloads straight into the lodge (the yard). Click it to buy a train."
                : _tool == Tool.SteamEngine ? "STEAM ENGINE: burns coal or charcoal fed by belt and gives " + B.EngineHp + " hp to its shafts. It only burns fuel while something uses the power."
                : _tool == Tool.Furnace ? "STEEL FURNACE: needs power from a shaft. Feed it 2 ore and 1 coal for each steel bar."
                : _tool == Tool.Waterwheel ? "WATERWHEEL: place it on the river bank (it must touch the water). Gives " + B.WheelHp + " hp to everything on its shafts."
                : "Click to place  ·  R turns it (the yellow side is the output)  ·  right click to stop.";
            // Help line sits on its own dark strip so it reads over bright ground.
            float hintW = Mathf.Min(Wd - 20f, _small.CalcSize(new GUIContent(hint)).x + 24f);
            GUI.Box(new Rect(Wd / 2f - hintW / 2f, Ht - 132f, hintW, 30f), GUIContent.none, _panel);
            GUI.Label(new Rect(Wd / 2f - hintW / 2f + 12f, Ht - 128f, hintW - 16f, 24f), hint, _small);
            if (_paused) GUI.Label(new Rect(Wd / 2f - 60f, 80f, 200f, 30f), "<b>PAUSED</b>", _body);
        }

        void DrawBuildBar(float Wd, float Ht)
        {
            var bar = Panel(new Rect(Wd / 2f - 715f, Ht - 100f, 1430f, 90f));
            // Page switch on the left: the second page is the Tier 2 water power buildings.
            string pageLabel = "Tab  page " + _page + "/" + Mathf.Clamp(W.Tier, 1, 4) + "\n" + (_page == 1 ? "Tier 1" : _page == 2 ? "Water power" : _page == 3 ? "Steam" : "Rails") + (W.Tier < 2 ? "\n(Tier 2 locked)" : "");
            if (GUI.Button(new Rect(bar.x + 8f, bar.y + 8f, 110f, 74f), pageLabel, W.Tier >= 2 ? _btnSmallOn : _btnSmall)) SwitchPage();
            var list = new List<Tool>(PageTools(_page));
            if (_page == 1) list.Add(Tool.Splitter);
            if (!B.PeopleEnabled) { list.Add(Tool.Dig); list.Add(Tool.Dump); }
            list.Add(Tool.Demolish);
            var tools = list.ToArray();
            float x0 = bar.x + 122f;
            float w = (bar.width - 130f) / 12f - 4f;
            for (int i = 0; i < tools.Length; i++)
            {
                var t = tools[i];
                string label;
                if (t == Tool.Demolish) label = "X  Demolish\nfull refund";
                else if (t == Tool.Dig) label = "G  Dig\ndrag an area";
                else if (t == Tool.Dump) label = "V  Dump\ndirt goes here";
                else
                {
                    var d = B.Defs[TypeOf(t)];
                    var cost = new StringBuilder();
                    foreach (var c in d.Cost) cost.Append(cost.Length > 0 ? " + " : "").Append(c.count).Append(' ').Append(Items.Letter(c.item));
                    bool fuel = d.BurnsFuel || System.Array.Exists(d.Recipes, rc => rc.BurnsFuel);
                    string key = t == Tool.Splitter ? "9" : (System.Array.IndexOf(PageTools(_page), t) + 1).ToString();
                    string power = t == Tool.Waterwheel ? " · gives " + d.Hp + " hp" : d.NeedsPower ? " · " + d.Hp + " hp" : "";
                    label = (d.Tier > W.Tier ? "LOCKED  " : key + "  ") + d.Name + "\n" + cost + (fuel ? " · fuel" : "") + power +
                            (!B.PeopleEnabled ? "" : d.Workers > 0 ? "\n" + d.Workers + (d.Workers == 1 ? " worker" : " workers") : d.Homes > 0 ? "\nhomes " + d.Homes : "");
                }
                if (GUI.Button(new Rect(x0 + i * (w + 4f), bar.y + 8f, w, 74f), (_tool == t ? "> " : "") + label, _tool == t ? _btnSmallOn : _btnSmall))
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
            if (b.Type == BuildingType.SteamEngine)
                GUILayout.Label("Gives " + def.Hp + " hp while it has fuel (coal lasts longest). Burns " + B.EngineBurnRate + "x as fast as a kiln, and only while machines on its shafts need power. Network: " +
                    b.NetDemand + " hp wanted, " + b.NetSupply + " hp supplied. Fuel: " + b.FuelCount, _small);
            if (b.Type == BuildingType.Waterwheel)
                GUILayout.Label("Gives " + def.Hp + " hp to everything joined to it by line shafts (or touching it). Its network: " +
                    b.NetDemand + " hp wanted, " + b.NetSupply + " hp from wheels. Over the limit, every machine on it slows down evenly.", _small);
            if (b.Type == BuildingType.Shaft)
                GUILayout.Label(b.Power >= 0f
                    ? "Turning. This network: " + b.NetSupply + " hp from waterwheels, " + b.NetDemand + " hp wanted" + (b.NetDemand > b.NetSupply ? " (overloaded: machines slow down)" : "") + "."
                    : "Not joined to a waterwheel yet. Shafts carry power from a wheel to machines touching them; they run overhead, so belts can pass under.", _small);
            if (def.Hp > 0 && b.Type != BuildingType.Waterwheel && b.Type != BuildingType.SteamEngine)
            {
                string pw = b.Power > 0f
                    ? "Power: running on shaft power at " + Mathf.RoundToInt(b.Power * 100f) + "% (" + def.Hp + " hp)" + (def.NeedsPower ? "" : ", no fuel needed")
                    : def.NeedsPower ? "Power: needs " + def.Hp + " hp. Join it to a waterwheel or steam engine with line shafts (Tab, page 2, key 1)."
                    : W.Tier >= 2 ? "Power: burning fuel. Touch a powered line shaft and it runs on water instead (" + def.Hp + " hp)." : "";
                if (pw.Length > 0) GUILayout.Label(pw, b.Power > 0f || !def.NeedsPower ? _small : _badSmall);
            }
            if (b.Type == BuildingType.Splitter)
                GUILayout.Label("Hands items out in turn: front, right, left. Feed it from the back or sides.", _small);
            if (b.Type == BuildingType.Garage) DrawGaragePanel(b);
            if (b.Type == BuildingType.Station) DrawStationPanel(b);
            if (b.Type == BuildingType.Hopper)
            {
                var sbh = new StringBuilder("Holding " + W.HopperStored(b) + " / " + B.HopperCap + ":  ");
                foreach (var kv in b.Input) if (kv.Value > 0) sbh.Append(Items.Letter(kv.Key)).Append(' ').Append(kv.Value).Append("   ");
                GUILayout.Label(sbh.ToString(), _small);
                GUILayout.Label("Trucks tip ore and stone in here. It puts them on a belt from its front, " + B.HopperOutPerSecond.ToString("0") + " per second (or straight into a machine or the lodge it touches).", _small);
            }
            if (b.Type != BuildingType.Belt && b.Type != BuildingType.Cabin)
                GUILayout.Label("Output side: " + Dir.Names[b.Facing] + " (yellow marker)", _small);
            if (b.Type == BuildingType.Belt)
                GUILayout.Label("Carrying: " + (b.BeltItem == Item.None ? "nothing" : Items.Name(b.BeltItem)) + "   flow: " + Dir.Names[b.Facing], _small);
            if (W.BurnsFuel(b) && b.Power <= 0f)
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
                    string text = (b.Recipe == i ? "> " : "") + rc.Name + "  (" + sb + " -> " + rc.OutCount + " " + Items.Letter(rc.Out) + ", " + rc.Seconds + "s)";
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

        void DrawStationPanel(Building st)
        {
            int waiting = World.StationStored(st.Input), outgoing = World.StationStored(st.Output);
            GUILayout.Label(W.IsYard(st)
                ? "Lodge yard: trains unload straight into the lodge here, and take coal or charcoal out for machines at the far station."
                : "Belts and machines facing it fill its loading bay (" + waiting + "/" + B.StationCap + "). Goods a train drops here go out the front; fuel goes to fuel-burning machines touching it (" + outgoing + " waiting).", _small);
            var cost = new StringBuilder();
            foreach (var c in B.TrainCost) cost.Append(cost.Length > 0 ? " + " : "").Append(c.count).Append(' ').Append(Items.Letter(c.item));
            string why = W.CanBuy(st, VehicleType.Train);
            if (GUILayout.Button("Buy a train  (" + cost + ")" + (why != null ? "\n" + why : ""), why == null ? _btn : _btnSmall, GUILayout.Height(why == null ? 38f : 46f)))
            {
                if (why != null) Toast(why); else W.Buy(st.Id, VehicleType.Train);
            }
            foreach (var v in W.Vehicles)
            {
                if (v.Type != VehicleType.Train || (v.GarageId != st.Id && v.TargetStation != st.Id)) continue;
                GUILayout.Label("Train: " + v.Status + "  (" + v.CargoCount + "/" + B.TrainCap + " on board)", _small);
            }
        }

        void DrawGaragePanel(Building g)
        {
            GUILayout.Label("Vehicle fuel: W wood " + g.FuelWood + "   C charcoal " + g.FuelCharcoal + "  (holds " + B.GarageFuelCap + "). Belt wood or charcoal in.", _small);
            foreach (VehicleType t in new[] { VehicleType.Excavator, VehicleType.Truck })
            {
                var cost = new StringBuilder();
                foreach (var c in B.VehicleCost(t)) cost.Append(cost.Length > 0 ? " + " : "").Append(c.count).Append(' ').Append(Items.Letter(c.item));
                string why = W.CanBuy(g, t);
                string name = t == VehicleType.Excavator ? "Buy excavator" : "Buy truck";
                if (GUILayout.Button(name + "  (" + cost + ")" + (why != null ? "\n" + why : ""), why == null ? _btn : _btnSmall, GUILayout.Height(why == null ? 38f : 46f)))
                {
                    if (why != null) Toast(why); else W.Buy(g.Id, t);
                }
            }
            var list = new StringBuilder();
            int n = 0;
            foreach (var v in W.Vehicles)
            {
                if (v.GarageId != g.Id) continue;
                n++;
                list.Append(v.Type == VehicleType.Excavator ? "Excavator: " : "Truck: ").Append(v.Status)
                    .Append("  (fuel ").Append(Mathf.RoundToInt(100f * v.Tank / B.TankSeconds)).Append("%)\n");
            }
            GUILayout.Label(n == 0 ? "No vehicles yet. Buy an excavator and a truck, then drag a dig area (G)." : list.ToString(), _small);
        }

        void DrawMenu(float Wd, float Ht)
        {
            var r = Panel(new Rect(Wd / 2f - 220f, Ht / 2f - 245f, 440f, 490f));
            GUILayout.BeginArea(new Rect(r.x + 24f, r.y + 18f, r.width - 48f, r.height - 36f));
            GUILayout.Label("Menu", _h2);
            if (GUILayout.Button("Resume", _btn, GUILayout.Height(46f))) _menu = false;
            if (GUILayout.Button("Save now", _btn, GUILayout.Height(40f))) { SaveSystem.Save(W); Toast("Saved"); }
            if (GUILayout.Button(Screen.fullScreen ? "Windowed" : "Fullscreen", _btn, GUILayout.Height(40f))) Screen.fullScreen = !Screen.fullScreen;
            var names = QualitySettings.names;
            int q = QualitySettings.GetQualityLevel();
            if (names.Length > 0 && GUILayout.Button("Graphics: " + names[q], _btn, GUILayout.Height(40f))) QualitySettings.SetQualityLevel((q + 1) % names.Length, true);
            if (GUILayout.Button("Film look (shading, glow): " + (PostFX.Enabled ? "on" : "off"), _btn, GUILayout.Height(40f))) PostFX.Enabled = !PostFX.Enabled;
            GUILayout.Label("The game also saves every 2 minutes and when you quit.", _small);
            if (GUILayout.Button("Save and quit", _btn, GUILayout.Height(40f))) { SaveSystem.Save(W); Application.Quit(); }
            GUILayout.EndArea();
        }

        void DrawGoal(float Wd, float Ht)
        {
            var r = Panel(new Rect(Wd / 2f - 330f, Ht / 2f - 170f, 660f, 340f));
            GUILayout.BeginArea(new Rect(r.x + 28f, r.y + 20f, r.width - 56f, r.height - 40f));
            if (W.Tier >= 5)
            {
                GUILayout.Label("TIER 5 UNLOCKED", _h1, GUILayout.Height(80f));
                GUILayout.Label("The railway is running. The next tier arrives in a later build.", _body);
                GUILayout.Label("You can keep building here as long as you like.", _small);
            }
            else if (W.Tier == 4)
            {
                GUILayout.Label("TIER 4 UNLOCKED", _h1, GUILayout.Height(80f));
                GUILayout.Label("Rails. Page 4 has track (1 steel + 1 wood a tile) and stations. Lay a line out to the far iron ore field in the north-east, put a station at each end (one near the lodge), and buy a train at a station. Mines touching the far station fill it, and trains take coal out to keep them burning.", _body);
                GUILayout.Label("Next goal: " + B.GoalRail + " goods hauled to the lodge by train.", _small);
            }
            else if (W.Tier <= 2)
            {
                GUILayout.Label("TIER 2 UNLOCKED", _h1, GUILayout.Height(80f));
                GUILayout.Label("Water power. Press Tab for the new build page: set a waterwheel on the river bank and run line shafts to a sawmill (planks) and a trip hammer (iron plates). Mines and workshops on a shaft run without fuel.", _body);
                GUILayout.Label("Next goal: " + B.GoalPlank + " planks and " + B.GoalPlate + " iron plates for the first boiler.", _small);
            }
            else
            {
                GUILayout.Label("TIER 3 UNLOCKED", _h1, GUILayout.Height(80f));
                GUILayout.Label("Steam. Coal (K) lies two levels down in the dark seam east of the lodge: dig it with the excavators. Page 3 has the steam engine (" + B.EngineHp + " hp, burns coal) and the steel furnace (2 ore + 1 coal = 1 steel).", _body);
                GUILayout.Label("Next goal: " + B.GoalSteel + " steel and " + B.GoalCoal + " coal delivered to the lodge for the first rails.", _small);
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Keep building", _btn, GUILayout.Height(50f))) _goalPanel = false;
            GUILayout.EndArea();
        }
    }
}
