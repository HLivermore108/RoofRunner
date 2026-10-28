using System.Collections.Generic;
using UnityEngine;

namespace NeonRelay
{
    public sealed class RelayWorld : MonoBehaviour
    {
        public readonly Color Cyan = new Color(.30f, 1, .85f);
        public readonly Color Gold = new Color(1, .76f, .32f);
        public readonly Color Coral = new Color(1, .30f, .38f);
        public sealed class Section
        {
            public float start, end, next; public int kind;
            public GameObject root; public Rect hazard; public Vector2 cell; public GameObject cellArt; public bool collected;
        }
        public IReadOnlyList<Section> Sections => sections;
        readonly List<Section> sections = new List<Section>();
        readonly List<Transform> skyline = new List<Transform>();
        readonly List<float> skyDepth = new List<float>();
        readonly List<Particle> particles = new List<Particle>();
        struct Particle { public Transform t; public Vector2 velocity; public float life; public SpriteRenderer sprite; }
        RelayGame game; Sprite square, circle; Camera cam; Transform runner, torso, head, legA, legB, scarf;
        Transform backdrop; System.Random random; float nextStart; int sectionIndex, mode;

        public void Initialize(RelayGame owner)
        {
            game = owner;
            square = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1);
            var tex = new Texture2D(64, 64); var px = new Color[4096];
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
                px[y * 64 + x] = new Color(1, 1, 1, Mathf.Clamp01(32 - Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(32, 32))));
            tex.SetPixels(px); tex.Apply(); circle = Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(.5f, .5f), 64);
            cam = Camera.main;
            if (!cam) cam = new GameObject("Orbital camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = 7.5f; cam.backgroundColor = new Color(.025f, .045f, .095f);
            cam.transform.position = new Vector3(11, 3, -10);
            CreateSky(); CreateRunner();
        }

        Transform Box(string label, Transform parent, Vector2 pos, Vector2 size, Color color, int order = 0, bool round = false)
        {
            var go = new GameObject(label); go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localScale = size;
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = round ? circle : square; sr.color = color; sr.sortingOrder = order;
            return go.transform;
        }
        void CreateSky()
        {
            backdrop = new GameObject("Orbital city / parallax").transform;
            var rng = new System.Random(37);
            Box("Hazy planet", backdrop, new Vector2(19, 7), new Vector2(6, 6), new Color(.09f, .20f, .28f), -30, true);
            Box("Planet shadow", backdrop, new Vector2(20, 7.8f), new Vector2(5.5f, 5.5f), new Color(.035f, .07f, .13f), -29, true);
            var ring = Box("Orbital ring", backdrop, new Vector2(19, 7), new Vector2(9, .055f), new Color(.28f, .48f, .52f), -28);
            ring.localRotation = Quaternion.Euler(0, 0, 18);
            for (int i = 0; i < 95; i++)
                Box("Star", backdrop, new Vector2((float)rng.NextDouble() * 60 - 20, (float)rng.NextDouble() * 10),
                    Vector2.one * (i % 7 == 0 ? .045f : .02f), new Color(.35f, .54f, .65f), -35);
            for (int layer = 0; layer < 2; layer++)
            for (int i = 0; i < 32; i++)
            {
                float h = 2 + (float)rng.NextDouble() * (layer == 0 ? 6 : 4), w = 1 + (float)rng.NextDouble() * 2;
                float x = i * 3 - 25;
                var building = new GameObject("Distant tower").transform; building.position = new Vector2(x, -3);
                skyline.Add(building); skyDepth.Add(layer == 0 ? .16f : .32f);
                Color color = layer == 0 ? new Color(.065f, .105f, .17f) : new Color(.075f, .15f, .21f);
                Box("Tower", building, new Vector2(0, h / 2), new Vector2(w, h), color, -22 + layer * 3);
                Box("Antenna", building, new Vector2(0, h + .35f), new Vector2(.04f, .7f), color, -21 + layer * 3);
                for (int j = 0; j < (int)h; j++)
                    Box("Window ribbon", building, new Vector2(.1f, j + .5f), new Vector2(w * .55f, .035f),
                        new Color(.12f, .29f, .34f), -20 + layer * 3);
            }
        }
        void CreateRunner()
        {
            runner = new GameObject("Relay courier").transform;
            scarf = Box("Ion scarf", runner, new Vector2(-.5f, 1), new Vector2(.75f, .14f), Coral, 13);
            torso = Box("Flight suit", runner, new Vector2(0, .73f), new Vector2(.5f, .65f), new Color(.80f, .89f, .88f), 14);
            Box("Chest core", torso, new Vector2(.12f, .10f), new Vector2(.22f, .35f), Cyan, 15);
            head = Box("Helmet", runner, new Vector2(.04f, 1.25f), new Vector2(.56f, .48f), new Color(.86f, .93f, .91f), 14, true);
            Box("Visor", head, new Vector2(.18f, .06f), new Vector2(.74f, .34f), new Color(.03f, .16f, .22f), 15);
            Box("Visor light", head, new Vector2(.40f, .06f), new Vector2(.18f, .12f), Cyan, 16);
            legA = Box("Boot L", runner, new Vector2(-.15f, .25f), new Vector2(.19f, .5f), new Color(.23f, .48f, .53f), 12);
            legB = Box("Boot R", runner, new Vector2(.15f, .25f), new Vector2(.19f, .5f), new Color(.49f, .73f, .74f), 13);
        }
        public void ResetCourse(int difficulty)
        {
            foreach (var section in sections) { section.root.SetActive(false); Destroy(section.root); }
            foreach (var p in particles) Destroy(p.t.gameObject);
            particles.Clear(); sections.Clear(); mode = difficulty;
            random = new System.Random(game.Automated ? 104729 + difficulty * 79 : System.Environment.TickCount);
            nextStart = -8; sectionIndex = 0; Extend(3);
        }
        public void Extend(float playerX)
        {
            while (nextStart < playerX + 65) AddSection();
            for (int i = sections.Count - 1; i >= 0; i--)
                if (sections[i].next < playerX - 25) { Destroy(sections[i].root); sections.RemoveAt(i); }
        }
        void AddSection()
        {
            // Safe introductory order, then compatible flat-roof modules with generous landing zones.
            int kind = sectionIndex == 0 ? 0 : sectionIndex <= 4 ? sectionIndex : random.Next(0, mode == 0 ? 6 : 5);
            if (kind == 5) kind = 0;
            float length = sectionIndex == 0 ? 30 : 22;
            float gap = kind == 4 ? 9 : 3.4f;
            var s = new Section { start = nextStart, end = nextStart + length, kind = kind };
            s.next = s.end + gap; s.root = new GameObject("Roof module " + sectionIndex + " / " + kind);
            var root = s.root.transform;
            Box("Building", root, new Vector2((s.start + s.end) / 2, -4), new Vector2(length, 8), new Color(.055f, .11f, .16f));
            Box("Roof edge", root, new Vector2((s.start + s.end) / 2, -.12f), new Vector2(length, .24f), Cyan, 3);
            Box("Under edge", root, new Vector2((s.start + s.end) / 2, -.4f), new Vector2(length, .08f), new Color(.13f, .31f, .35f), 2);
            for (float x = s.start + 1; x < s.end; x += 3)
            {
                Box("Facade seam", root, new Vector2(x, -4), new Vector2(.035f, 6.9f), new Color(.11f, .21f, .26f), 1);
                Box("Facade light", root, new Vector2(x + .5f, -1.5f), new Vector2(.8f, .12f), new Color(.17f, .40f, .42f), 2);
            }
            float obstacleX = s.start + 10;
            if (kind == 1 || kind == 2)
            {
                bool overhead = kind == 2;
                s.hazard = new Rect(obstacleX, overhead ? .86f : 0, overhead ? 2.8f : 1.05f, overhead ? 1.8f : 1.18f);
                Box(overhead ? "Laser gate / SLIDE" : "Power junction / JUMP", root, s.hazard.center, s.hazard.size, new Color(.34f, .10f, .18f), 5);
                Box("Hazard edge", root, new Vector2(s.hazard.center.x, overhead ? .91f : 1.18f), new Vector2(s.hazard.width, .1f), Coral, 6);
                for (float x = s.hazard.x + .15f; x < s.hazard.xMax; x += .38f)
                {
                    var stripe = Box("Warning stripes", root, new Vector2(x, s.hazard.center.y), new Vector2(.09f, s.hazard.height * .65f), Coral, 6);
                    stripe.localRotation = Quaternion.Euler(0, 0, -18);
                }
                Sign(root, overhead ? "S  /  SLIDE" : "SPACE  /  JUMP", new Vector2(obstacleX - 3, 3), Coral);
            }
            if (kind == 4)
            {
                Box("Magnetic wall", root, new Vector2(s.end + gap / 2, 1.4f), new Vector2(gap + 4, 3.2f), new Color(.24f, .18f, .09f), -3);
                for (float x = s.end - 1.8f; x < s.next + 2; x += .7f)
                    Box("Wall conductor", root, new Vector2(x, 1.4f), new Vector2(.035f, 3.2f), new Color(.51f, .37f, .15f), -2);
                Box("Magnetic rail", root, new Vector2(s.end + gap / 2, .4f), new Vector2(gap + 4, .07f), Gold, -1);
                Sign(root, "HOLD W  /  WALL RUN", new Vector2(s.end - 1, 3.6f), Gold);
            }
            else Sign(root, "JUMP  >", new Vector2(s.end - 2, 1.9f), Cyan);
            s.cell = new Vector2(s.start + 16, 1.25f);
            s.cellArt = Box("Data cell +100", root, s.cell, Vector2.one * .35f, Gold, 8).gameObject;
            s.cellArt.transform.localRotation = Quaternion.Euler(0, 0, 45);
            nextStart = s.next; sectionIndex++; sections.Add(s);
        }
        void Sign(Transform parent, string caption, Vector2 position, Color color)
        {
            var go = new GameObject(caption); go.transform.SetParent(parent); go.transform.localPosition = position;
            var text = go.AddComponent<TextMesh>(); text.text = caption; text.fontSize = 48; text.characterSize = .055f;
            text.anchor = TextAnchor.MiddleCenter; text.color = color;
            go.GetComponent<MeshRenderer>().sortingOrder = 8;
        }
        public bool HasRoof(float x) { foreach (var s in sections) if (x >= s.start && x <= s.end) return true; return false; }
        public bool AtWall(float x, float y)
        { foreach (var s in sections) if (s.kind == 4 && x >= s.end - 2 && x <= s.next + 2 && y > -1.3f && y < 3) return true; return false; }
        public bool HitsHazard(Rect body)
        { foreach (var s in sections) if (s.hazard.width > 0 && s.hazard.Overlaps(body)) return true; return false; }
        public bool Collect(Rect body)
        {
            foreach (var s in sections)
                if (!s.collected && body.Overlaps(new Rect(s.cell - Vector2.one * .3f, Vector2.one * .6f)))
                { s.collected = true; s.cellArt.SetActive(false); return true; }
            return false;
        }
        public void Burst(Vector2 position, Color color, int count)
        {
            for (int i = 0; i < count && particles.Count < 150; i++)
            {
                var t = Box("Ion particle", null, position, Vector2.one * Random.Range(.025f, .1f), color, 20);
                particles.Add(new Particle { t = t, sprite = t.GetComponent<SpriteRenderer>(), velocity = new Vector2(Random.Range(-4f, 1f), Random.Range(.4f, 3f)), life = Random.Range(.2f, .65f) });
            }
        }
        void LateUpdate()
        {
            bool menu = game.State == RunState.Menu;
            float targetX = menu ? 10 : game.Position.x + 7;
            cam.transform.position = new Vector3(targetX, 3, -10);
            backdrop.position = new Vector3(targetX - 11, 0, 0);
            for (int i = 0; i < skyline.Count; i++)
            {
                float baseX = (i % 32) * 3 - 25;
                float x = Mathf.Repeat(baseX - targetX * skyDepth[i] + 40, 96) - 40;
                skyline[i].position = new Vector2(x + targetX, -3);
            }
            runner.position = menu ? new Vector2(15, 0) : game.Position;
            bool slide = !menu && game.Sliding;
            torso.localPosition = slide ? new Vector2(0, .3f) : new Vector2(0, .73f);
            torso.localScale = slide ? new Vector2(.75f, .3f) : new Vector2(.5f, .65f);
            head.localPosition = slide ? new Vector2(.4f, .34f) : new Vector2(.04f, 1.25f);
            head.localScale = slide ? new Vector2(.4f, .35f) : new Vector2(.56f, .48f);
            scarf.localPosition = slide ? new Vector2(-.5f, .3f) : new Vector2(-.5f, 1);
            float swing = game.State == RunState.Running || menu ? Mathf.Sin(Time.time * 19) * 28 : 0;
            legA.localRotation = Quaternion.Euler(0, 0, slide ? 75 : swing);
            legB.localRotation = Quaternion.Euler(0, 0, slide ? 85 : -swing);
            legA.localPosition = slide ? new Vector2(-.2f, .12f) : new Vector2(-.15f, .25f);
            legB.localPosition = slide ? new Vector2(-.4f, .12f) : new Vector2(.15f, .25f);
            if (game.State == RunState.Paused) return;
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                var p = particles[i]; p.life -= Time.deltaTime;
                if (p.life <= 0) { Destroy(p.t.gameObject); particles.RemoveAt(i); continue; }
                p.t.position += (Vector3)(p.velocity * Time.deltaTime); p.velocity += Vector2.down * (5 * Time.deltaTime);
                var c = p.sprite.color; c.a = Mathf.Min(1, p.life * 3); p.sprite.color = c; particles[i] = p;
            }
        }
    }
}
