using System;
using System.Collections.Generic;
using UnityEngine;

// A self-contained 2D side-scroller. The level art is loaded from Resources;
// characters, HUD and obstacles are drawn with Unity's built-in IMGUI.
public sealed class MiddleEarthGame : MonoBehaviour
{
    private enum GameScreen { Menu, Playing, Paused, Defeat, Victory }
    private enum ObstacleType { Boulder, Spikes, Pit }

    private sealed class Hero
    {
        public readonly string name, role;
        public readonly Color cloak, hair;
        public readonly int health, damage;
        public readonly float speed, jump, range;

        public Hero(string name, string role, string cloak, string hair,
            int health, int damage, float speed, float jump, float range)
        {
            this.name = name; this.role = role;
            this.cloak = Html(cloak); this.hair = Html(hair);
            this.health = health; this.damage = damage;
            this.speed = speed; this.jump = jump; this.range = range;
        }
    }

    private sealed class Obstacle
    {
        public float x, width, height;
        public ObstacleType type;
        public Obstacle(float x, float width, float height, ObstacleType type)
        { this.x = x; this.width = width; this.height = height; this.type = type; }
    }

    private sealed class Foe
    {
        public string name;
        public float x, origin, speed, direction;
        public int health, maxHealth, damage;
        public bool boss;
        public Foe(string name, float x, int health, float speed, int damage, bool boss = false)
        {
            this.name = name; this.x = x; origin = x;
            this.health = health; maxHealth = health; this.speed = speed;
            this.damage = damage; this.boss = boss; direction = -1;
        }
    }

    private sealed class Pickup
    {
        public float x, y;
        public bool taken;
        public Pickup(float x, float y) { this.x = x; this.y = y; }
    }

    private static readonly Hero[] Heroes =
    {
        new Hero("Фродо", "Хоббит · хранитель", "#687B54", "#49382B", 5, 1, 250, 520, 105),
        new Hero("Сэм", "Хоббит · верный друг", "#8B7651", "#694A2E", 6, 1, 245, 500, 105),
        new Hero("Арагорн", "Следопыт · меч", "#454F48", "#352820", 6, 2, 260, 510, 112),
        new Hero("Леголас", "Эльф · лук", "#5C715F", "#D8BD75", 4, 2, 280, 550, 190),
        new Hero("Гимли", "Гном · топор", "#7C5340", "#A55E32", 7, 2, 225, 470, 110),
        new Hero("Гэндальф", "Волшебник · посох", "#B9B5A5", "#D8D5C8", 5, 2, 240, 510, 175),
        new Hero("Боромир", "Воин · Гондор", "#604A45", "#6E4C2F", 7, 2, 245, 490, 110),
        new Hero("Мерри", "Хоббит · отвага", "#4E7156", "#6B4B2D", 5, 1, 260, 530, 105),
        new Hero("Пиппин", "Хоббит · любопытство", "#63718B", "#7B5130", 5, 1, 265, 535, 105),
        new Hero("Арвен", "Эльф · свет", "#5D6480", "#28272F", 5, 2, 265, 535, 150),
        new Hero("Эовин", "Воительница · Рохан", "#8A8055", "#C5A46B", 6, 2, 255, 510, 112),
        new Hero("Фарамир", "Следопыт · Гондор", "#4F6658", "#6E4C35", 6, 2, 255, 510, 145)
    };

    private static readonly string[] LevelNames = { "ШИР", "МОРИЯ", "МОРДОР" };
    private static readonly string[] LevelSubtitles =
    {
        "Путешествие начинается среди зелёных холмов.",
        "В глубине каменных залов слышны чужие шаги.",
        "Последний путь к огню Роковой горы."
    };

    private const float ViewWidth = 1280f;
    private const float ViewHeight = 720f;
    private const float GroundScreenY = 565f;
    private const float Gravity = 1250f;
    private const float PlayerHalfWidth = 21f;

    private GameScreen screen = GameScreen.Menu;
    private readonly List<Obstacle> obstacles = new List<Obstacle>();
    private readonly List<Foe> foes = new List<Foe>();
    private readonly List<Pickup> pickups = new List<Pickup>();
    private Texture2D[] backgrounds;
    private Texture2D circleTexture, ringTexture;
    private Font gameFont;
    private int selectedHero, levelIndex, health, collected;
    private float playerX, playerY, velocityY, cameraX, worldWidth, exitX;
    private float checkpointX;
    private float invulnerable, attackCooldown, attackFlash, noticeTimer;
    private float viewScale, viewOffsetX, viewOffsetY;
    private int facing = 1;
    private bool reachedCheckpoint, gollumTriggered;
    private bool moving, leftHeld, rightHeld, jumpPressed, attackPressed;
    private string notice = "";

    private static Color Html(string hex)
    {
        Color color;
        return ColorUtility.TryParseHtmlString(hex, out color) ? color : Color.white;
    }

    private void Awake()
    {
        Application.targetFrameRate = 60;
        Screen.orientation = ScreenOrientation.LandscapeLeft;
        selectedHero = Mathf.Clamp(PlayerPrefs.GetInt("SelectedHero", 0), 0, Heroes.Length - 1);
        backgrounds = new[]
        {
            Resources.Load<Texture2D>("shire"),
            Resources.Load<Texture2D>("moria"),
            Resources.Load<Texture2D>("mordor")
        };
        gameFont = Resources.Load<Font>("DejaVuSans");
        circleTexture = MakeCircle(false);
        ringTexture = MakeCircle(true);
    }

    private static Texture2D MakeCircle(bool ring)
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float radius = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 31.5f;
            float edge = Mathf.Clamp01((1f - radius) * 10f);
            float alpha = ring ? edge * Mathf.Clamp01((radius - .59f) * 11f) : edge;
            texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        texture.Apply();
        return texture;
    }

    private void Update()
    {
        UpdateView();
        if (screen == GameScreen.Playing && Input.GetKeyDown(KeyCode.Escape))
        { screen = GameScreen.Paused; return; }
        if (screen == GameScreen.Paused && Input.GetKeyDown(KeyCode.Escape))
        { screen = GameScreen.Playing; return; }
        if (screen != GameScreen.Playing) return;

        float dt = Mathf.Min(Time.deltaTime, .04f);
        ReadControls();
        if (noticeTimer > 0f) noticeTimer -= dt;
        invulnerable = Mathf.Max(0f, invulnerable - dt);
        attackCooldown = Mathf.Max(0f, attackCooldown - dt);
        attackFlash = Mathf.Max(0f, attackFlash - dt);

        float move = (rightHeld ? 1f : 0f) - (leftHeld ? 1f : 0f);
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) move -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) move += 1f;
        move = Mathf.Clamp(move, -1f, 1f);
        moving = Mathf.Abs(move) > .01f;
        if (moving) facing = move > 0f ? 1 : -1;

        if ((jumpPressed || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) &&
            playerY <= .05f && !OverPit(playerX))
            velocityY = Heroes[selectedHero].jump;
        if (attackPressed || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.J)) Attack();

        float oldX = playerX;
        playerX = Mathf.Clamp(playerX + move * Heroes[selectedHero].speed * dt, 25f, worldWidth - 25f);
        foreach (Obstacle obstacle in obstacles)
        {
            if (obstacle.type != ObstacleType.Boulder) continue;
            if (playerX + PlayerHalfWidth > obstacle.x &&
                playerX - PlayerHalfWidth < obstacle.x + obstacle.width &&
                playerY < obstacle.height - 5f)
            { playerX = oldX; break; }
        }

        velocityY -= Gravity * dt;
        playerY += velocityY * dt;
        if (playerY < 0f && !OverPit(playerX)) { playerY = 0f; velocityY = 0f; }
        if (playerY < -170f) { DamagePlayer(1); Respawn(); }

        foreach (Obstacle obstacle in obstacles)
        {
            if (obstacle.type != ObstacleType.Spikes || playerY > 28f) continue;
            if (playerX + PlayerHalfWidth > obstacle.x + 8f &&
                playerX - PlayerHalfWidth < obstacle.x + obstacle.width - 8f)
                DamagePlayer(1);
        }

        foreach (Pickup pickup in pickups)
        {
            if (!pickup.taken && Mathf.Abs(playerX - pickup.x) < 31f &&
                Mathf.Abs(playerY + 43f - pickup.y) < 52f)
            { pickup.taken = true; collected++; ShowNotice("Кольцо найдено  +1"); }
        }

        UpdateFoes(dt);
        if (!reachedCheckpoint && playerX > worldWidth * .5f)
        {
            reachedCheckpoint = true;
            checkpointX = worldWidth * .5f + 40f;
            ShowNotice("Контрольная точка достигнута");
        }
        if (levelIndex == 1 && !gollumTriggered && Mathf.Abs(playerX - 2250f) < 105f)
        {
            gollumTriggered = true;
            ShowNotice("Голлум наблюдает из темноты...");
        }
        cameraX = Mathf.Clamp(playerX - 315f, 0f, worldWidth - ViewWidth);
        if (playerX >= exitX && !BossAlive()) CompleteLevel();
    }

    private void UpdateView()
    {
        viewScale = Mathf.Min(Screen.width / ViewWidth, Screen.height / ViewHeight);
        viewOffsetX = (Screen.width - ViewWidth * viewScale) * .5f;
        viewOffsetY = (Screen.height - ViewHeight * viewScale) * .5f;
    }

    private Vector2 GuiPoint(Vector2 screenPoint)
    {
        return new Vector2((screenPoint.x - viewOffsetX) / viewScale,
            (Screen.height - screenPoint.y - viewOffsetY) / viewScale);
    }

    private void ReadControls()
    {
        leftHeld = rightHeld = jumpPressed = attackPressed = false;
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) continue;
            Vector2 point = GuiPoint(touch.position);
            if (LeftButton.Contains(point)) leftHeld = true;
            if (RightButton.Contains(point)) rightHeld = true;
            if (touch.phase == TouchPhase.Began && JumpButton.Contains(point)) jumpPressed = true;
            if (touch.phase == TouchPhase.Began && AttackButton.Contains(point)) attackPressed = true;
        }
        if (Input.GetMouseButton(0))
        {
            Vector2 point = GuiPoint(Input.mousePosition);
            if (LeftButton.Contains(point)) leftHeld = true;
            if (RightButton.Contains(point)) rightHeld = true;
            if (Input.GetMouseButtonDown(0) && JumpButton.Contains(point)) jumpPressed = true;
            if (Input.GetMouseButtonDown(0) && AttackButton.Contains(point)) attackPressed = true;
        }
    }

    private static Rect LeftButton { get { return new Rect(28, 610, 74, 74); } }
    private static Rect RightButton { get { return new Rect(113, 610, 74, 74); } }
    private static Rect JumpButton { get { return new Rect(1005, 610, 104, 74); } }
    private static Rect AttackButton { get { return new Rect(1121, 610, 132, 74); } }

    private bool OverPit(float x)
    {
        foreach (Obstacle obstacle in obstacles)
            if (obstacle.type == ObstacleType.Pit && x > obstacle.x + 8f &&
                x < obstacle.x + obstacle.width - 8f) return true;
        return false;
    }

    private void Attack()
    {
        if (attackCooldown > 0f) return;
        attackCooldown = .42f;
        attackFlash = .2f;
        Hero hero = Heroes[selectedHero];
        foreach (Foe foe in foes)
        {
            if (foe.health <= 0) continue;
            float distance = foe.x - playerX;
            if (Mathf.Abs(distance) <= hero.range &&
                (Mathf.Sign(distance) == facing || Mathf.Abs(distance) < 32f) &&
                playerY < (foe.boss ? 130f : 100f))
            {
                foe.health -= hero.damage;
                if (foe.health <= 0)
                    ShowNotice(foe.boss ? "Тень побеждена! К порталу!" : foe.name + " повержен");
            }
        }
    }

    private void UpdateFoes(float dt)
    {
        foreach (Foe foe in foes)
        {
            if (foe.health <= 0) continue;
            float difference = playerX - foe.x;
            if (Mathf.Abs(difference) < (foe.boss ? 350f : 210f) && playerY < 100f)
                foe.direction = Mathf.Sign(difference);
            else if (Mathf.Abs(foe.x - foe.origin) > 95f)
                foe.direction = foe.x > foe.origin ? -1f : 1f;
            float nextX = foe.x + foe.direction * foe.speed * dt;
            if (OverPit(nextX))
            {
                foe.direction = -foe.direction;
                nextX = foe.x + foe.direction * foe.speed * dt;
            }
            foe.x = nextX;
            foe.x = Mathf.Clamp(foe.x, foe.origin - 130f, foe.origin + 130f);
            if (Mathf.Abs(playerX - foe.x) < (foe.boss ? 53f : 39f) &&
                playerY < (foe.boss ? 115f : 72f))
                DamagePlayer(foe.damage);
        }
    }

    private void DamagePlayer(int amount)
    {
        if (invulnerable > 0f || screen != GameScreen.Playing) return;
        health -= amount;
        invulnerable = 1.4f;
        if (health <= 0) { health = 0; screen = GameScreen.Defeat; }
        else ShowNotice("Осторожно! Здоровье: " + health);
    }

    private void Respawn()
    {
        playerX = checkpointX;
        playerY = 0f;
        velocityY = 0f;
        cameraX = Mathf.Clamp(playerX - 315f, 0f, worldWidth - ViewWidth);
    }

    private void ShowNotice(string message)
    { notice = message; noticeTimer = 2.4f; }

    private bool BossAlive()
    {
        foreach (Foe foe in foes) if (foe.boss && foe.health > 0) return true;
        return false;
    }

    private void CompleteLevel()
    {
        if (levelIndex == 2) { screen = GameScreen.Victory; return; }
        levelIndex++;
        health = Mathf.Min(Heroes[selectedHero].health, health + 2);
        LoadLevel();
    }

    private void StartGame()
    {
        collected = 0;
        levelIndex = 0;
        health = Heroes[selectedHero].health;
        LoadLevel();
        screen = GameScreen.Playing;
    }

    private void LoadLevel()
    {
        obstacles.Clear(); foes.Clear(); pickups.Clear();
        worldWidth = levelIndex == 2 ? 3400f : 3000f;
        exitX = worldWidth - 170f;
        playerX = 100f; playerY = 0f; velocityY = 0f; cameraX = 0f;
        checkpointX = 100f; reachedCheckpoint = false; gollumTriggered = false;
        invulnerable = 0f; attackCooldown = 0f;

        if (levelIndex == 0)
        {
            AddRocks(390, 970, 1560, 2380);
            AddSpikes(720, 1250, 1850);
            obstacles.Add(new Obstacle(2090, 125, 0, ObstacleType.Pit));
            foes.Add(new Foe("Орк", 570, 2, 68, 1));
            foes.Add(new Foe("Орк", 1390, 2, 75, 1));
            foes.Add(new Foe("Урук-хай", 2520, 3, 82, 1));
            AddRings(250, 610, 845, 1120, 1480, 1780, 2250, 2690);
        }
        else if (levelIndex == 1)
        {
            AddRocks(360, 895, 1700, 2640);
            AddSpikes(660, 1450, 2340);
            obstacles.Add(new Obstacle(1170, 125, 0, ObstacleType.Pit));
            obstacles.Add(new Obstacle(2010, 125, 0, ObstacleType.Pit));
            foes.Add(new Foe("Орк", 530, 2, 76, 1));
            foes.Add(new Foe("Урук-хай", 1060, 3, 85, 1));
            foes.Add(new Foe("Тролль", 1800, 5, 45, 2));
            foes.Add(new Foe("Орк", 2490, 2, 82, 1));
            AddRings(240, 760, 1050, 1370, 1570, 1880, 2250, 2770);
        }
        else
        {
            AddRocks(420, 980, 1760, 2880);
            AddSpikes(730, 1530, 2360);
            obstacles.Add(new Obstacle(1210, 130, 0, ObstacleType.Pit));
            obstacles.Add(new Obstacle(2050, 140, 0, ObstacleType.Pit));
            foes.Add(new Foe("Урук-хай", 580, 3, 92, 1));
            foes.Add(new Foe("Назгул", 1090, 4, 100, 1));
            foes.Add(new Foe("Орк", 1660, 2, 95, 1));
            foes.Add(new Foe("Назгул", 2470, 4, 105, 1));
            foes.Add(new Foe("Тень Саурона", 3110, 10, 65, 2, true));
            AddRings(250, 830, 1110, 1410, 1810, 2270, 2670, 3030);
        }
        ShowNotice(LevelNames[levelIndex] + " — " + LevelSubtitles[levelIndex]);
    }

    private void AddRocks(params float[] positions)
    { foreach (float x in positions) obstacles.Add(new Obstacle(x, 66, 56, ObstacleType.Boulder)); }
    private void AddSpikes(params float[] positions)
    { foreach (float x in positions) obstacles.Add(new Obstacle(x, 91, 30, ObstacleType.Spikes)); }
    private void AddRings(params float[] positions)
    { foreach (float x in positions) pickups.Add(new Pickup(x, 74)); }

    private void OnGUI()
    {
        UpdateView();
        Color previousColor = GUI.color;
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previousColor;
        Matrix4x4 previous = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(new Vector3(viewOffsetX, viewOffsetY, 0f),
            Quaternion.identity, new Vector3(viewScale, viewScale, 1f));
        if (screen == GameScreen.Menu) DrawMenu();
        else
        {
            DrawWorld();
            if (screen == GameScreen.Playing) DrawControls();
            if (screen == GameScreen.Paused) DrawOverlay("ПАУЗА", "Путь подождёт тебя.", "Продолжить", () => screen = GameScreen.Playing);
            if (screen == GameScreen.Defeat) DrawOverlay("ТЬМА БЛИЗКО", "Попробуй ещё раз — Средиземье ждёт.", "Повторить уровень", Retry);
            if (screen == GameScreen.Victory) DrawOverlay("КОЛЬЦО УНИЧТОЖЕНО", "Ты прошёл путь до конца. Собрано колец: " + collected, "Сыграть снова", StartGame);
        }
        GUI.matrix = previous;
    }

    private void DrawMenu()
    {
        DrawBackground(0);
        Fill(new Rect(0, 0, 1280, 720), new Color(.045f, .07f, .055f, .83f));
        Text("НЕОФИЦИАЛЬНАЯ 2D ФАН-ИГРА", new Rect(66, 29, 520, 28), 15, Html("#D9BD75"));
        Text("ПУТЬ КОЛЬЦА", new Rect(62, 62, 1030, 92), 76, Color.white);
        Text("Выбери героя и пройди через Шир, Морию и Мордор", new Rect(67, 147, 850, 35), 22, Html("#E1E7D8"));

        for (int i = 0; i < Heroes.Length; i++)
        {
            int index = i;
            Hero hero = Heroes[i];
            int col = i % 4, row = i / 4;
            Rect card = new Rect(65 + col * 289, 202 + row * 107, 270, 94);
            bool active = i == selectedHero;
            Fill(card, active ? Html("#D6B56B") : new Color(.1f, .14f, .12f, .85f));
            if (!active) Border(card, new Color(1f, 1f, 1f, .15f), 1f);
            Color ink = active ? Html("#1D241C") : Color.white;
            Avatar(hero, card.x + 31, card.y + 73, .7f, false, false);
            Text(hero.name.ToUpperInvariant(), new Rect(card.x + 62, card.y + 18, 197, 31), 21, ink);
            Text(hero.role, new Rect(card.x + 62, card.y + 54, 196, 25), 13,
                active ? Html("#485043") : Html("#B7C2B5"));
            if (InvisibleButton(card))
            { selectedHero = index; PlayerPrefs.SetInt("SelectedHero", index); PlayerPrefs.Save(); }
        }

        Fill(new Rect(0, 548, 1280, 172), new Color(.04f, .065f, .05f, .94f));
        Text("ТВОЙ ГЕРОЙ", new Rect(66, 572, 200, 26), 14, Html("#D9BD75"));
        Text(Heroes[selectedHero].name, new Rect(64, 598, 420, 55), 39, Color.white);
        Text("ЗДОРОВЬЕ  " + Heroes[selectedHero].health + "     СИЛА  " + Heroes[selectedHero].damage +
            "     СКОРОСТЬ  " + Mathf.RoundToInt(Heroes[selectedHero].speed),
            new Rect(465, 610, 440, 36), 15, Html("#CFD9C9"));
        if (Button(new Rect(965, 595, 248, 70), "НАЧАТЬ ПУТЬ  →", Html("#D6B56B"), Html("#1D241C"), 20)) StartGame();
        Text("© Неофициальный фан-прототип", new Rect(66, 681, 400, 20), 12, Html("#829184"));
    }

    private void DrawWorld()
    {
        DrawBackground(levelIndex);
        Color ground = levelIndex == 0 ? Html("#263B2B") : levelIndex == 1 ? Html("#27313A") : Html("#261F1F");
        Fill(new Rect(0, GroundScreenY, 1280, 155), ground);
        Fill(new Rect(0, GroundScreenY, 1280, 8), levelIndex == 0 ? Html("#7F9A5A") :
            levelIndex == 1 ? Html("#789096") : Html("#A55B3F"));

        float begin = Mathf.Floor(cameraX / 90f) * 90f;
        for (float x = begin; x < cameraX + ViewWidth + 90f; x += 90f)
            Fill(new Rect(x - cameraX + 15f, GroundScreenY + 57f, 53f, 2f),
                new Color(1f, 1f, 1f, .08f));

        foreach (Obstacle obstacle in obstacles) DrawObstacle(obstacle);
        foreach (Pickup pickup in pickups) if (!pickup.taken) DrawPickup(pickup);
        foreach (Foe foe in foes) if (foe.health > 0) DrawFoe(foe);
        DrawCheckpoint();
        if (levelIndex == 1) DrawGollum();
        DrawExit();

        if (!(invulnerable > 0f && Mathf.FloorToInt(Time.time * 12f) % 2 == 0))
            Avatar(Heroes[selectedHero], playerX - cameraX, GroundScreenY - playerY,
                1f, moving, false);
        if (attackFlash > 0f)
        {
            float attackX = playerX - cameraX + facing * 40f;
            Ring(new Rect(attackX - 35f, GroundScreenY - playerY - 74f, 70f, 70f),
                Html("#F0D382"));
        }

        Fill(new Rect(0, 0, 1280, 81), new Color(.035f, .05f, .05f, .87f));
        Text("ПУТЬ КОЛЬЦА", new Rect(28, 16, 290, 40), 25, Color.white);
        Text((levelIndex + 1) + " / 3   " + LevelNames[levelIndex],
            new Rect(352, 17, 355, 37), 22, Html("#E9D8A4"));
        Text("◯  " + collected, new Rect(805, 17, 135, 39), 25, Html("#E9D8A4"));
        for (int i = 0; i < Heroes[selectedHero].health; i++)
            Text(i < health ? "♥" : "♡", new Rect(969 + i * 30, 22, 34, 34), 23,
                i < health ? Html("#E58A76") : Html("#8A807D"));
        if (Button(new Rect(1202, 17, 51, 45), "Ⅱ", new Color(1f, 1f, 1f, .15f), Color.white, 22))
            screen = GameScreen.Paused;

        if (noticeTimer > 0f)
        {
            Fill(new Rect(315, 98, 650, 47), new Color(.04f, .07f, .07f, .87f));
            Text(notice, new Rect(330, 104, 620, 38), 18, Color.white, TextAnchor.MiddleCenter);
        }
    }

    private void DrawBackground(int index)
    {
        Texture2D background = backgrounds != null ? backgrounds[index] : null;
        if (background != null)
            GUI.DrawTexture(new Rect(0, 0, ViewWidth, GroundScreenY + 4f), background, ScaleMode.ScaleAndCrop);
        else Fill(new Rect(0, 0, ViewWidth, GroundScreenY), index == 0 ? Html("#79956F") :
            index == 1 ? Html("#334C5C") : Html("#6A382F"));
        Fill(new Rect(0, 0, ViewWidth, GroundScreenY),
            index == 1 ? new Color(.02f, .04f, .06f, .15f) :
            index == 2 ? new Color(.09f, .02f, .01f, .17f) : new Color(.01f, .05f, .02f, .08f));
    }

    private void DrawObstacle(Obstacle obstacle)
    {
        float sx = obstacle.x - cameraX;
        if (sx < -180f || sx > 1300f) return;
        if (obstacle.type == ObstacleType.Pit)
        {
            Fill(new Rect(sx, GroundScreenY, obstacle.width, 155), Html("#0B0D0D"));
            Fill(new Rect(sx, GroundScreenY, obstacle.width, 8), Html("#C7774A"));
            return;
        }
        if (obstacle.type == ObstacleType.Boulder)
        {
            Color rock = levelIndex == 0 ? Html("#697166") : Html("#55575A");
            Disc(new Rect(sx + 2, GroundScreenY - obstacle.height, obstacle.width - 4, obstacle.height + 8), rock);
            Disc(new Rect(sx + 12, GroundScreenY - obstacle.height + 7, 19, 13),
                new Color(1f, 1f, 1f, .12f));
        }
        else
        {
            Fill(new Rect(sx, GroundScreenY - 8, obstacle.width, 9), Html("#4C3934"));
            for (int i = 0; i < 5; i++)
                DrawSpike(sx + 7 + i * 17, GroundScreenY - 31, 17, 29, Html("#C5A989"));
        }
    }

    private void DrawSpike(float x, float y, float w, float h, Color color)
    {
        for (int row = 0; row < 12; row++)
        {
            float t = row / 12f;
            float half = w * t * .5f;
            Fill(new Rect(x + w * .5f - half, y + row * h / 12f, half * 2f, h / 12f + 1f), color);
        }
    }

    private void DrawPickup(Pickup pickup)
    {
        float sx = pickup.x - cameraX;
        if (sx < -50f || sx > 1330f) return;
        float bounce = Mathf.Sin(Time.time * 3.1f + pickup.x) * 5f;
        Ring(new Rect(sx - 17f, GroundScreenY - pickup.y - 17f + bounce, 34f, 34f), Html("#FFE29B"));
        Disc(new Rect(sx - 3f, GroundScreenY - pickup.y - 3f + bounce, 6f, 6f), Color.white);
    }

    private void DrawFoe(Foe foe)
    {
        float sx = foe.x - cameraX;
        if (sx < -120f || sx > 1400f) return;
        float scale = foe.boss ? 1.8f : foe.name == "Тролль" ? 1.4f : 1f;
        float baseY = GroundScreenY;
        Color body = foe.boss ? Html("#1A161A") : foe.name == "Назгул" ? Html("#20242B") :
            foe.name == "Тролль" ? Html("#685B51") : Html("#474A3C");
        float w = 34f * scale, h = 54f * scale;
        Fill(new Rect(sx - w * .5f, baseY - h, w, h), body);
        Disc(new Rect(sx - 17f * scale, baseY - 79f * scale, 34f * scale, 31f * scale),
            foe.boss ? Html("#211818") : Html("#79806E"));
        Fill(new Rect(sx - 10f * scale, baseY - 67f * scale, 6f * scale, 4f * scale), Html("#E47B56"));
        Fill(new Rect(sx + 4f * scale, baseY - 67f * scale, 6f * scale, 4f * scale), Html("#E47B56"));
        Fill(new Rect(sx - 14f * scale, baseY - 2f, 12f * scale, 4f), Html("#252221"));
        Fill(new Rect(sx + 3f * scale, baseY - 2f, 12f * scale, 4f), Html("#252221"));
        if (foe.boss)
        {
            DrawSpike(sx - 40, baseY - 155, 31, 32, Html("#252024"));
            DrawSpike(sx + 9, baseY - 155, 31, 32, Html("#252024"));
        }
        float barWidth = foe.boss ? 120f : 48f;
        Fill(new Rect(sx - barWidth * .5f, baseY - h - 48f * scale, barWidth, 5), Html("#1A1A1A"));
        Fill(new Rect(sx - barWidth * .5f, baseY - h - 48f * scale,
            barWidth * foe.health / foe.maxHealth, 5), Html("#D26750"));
        if (foe.boss) Text("ТЕНЬ САУРОНА", new Rect(sx - 105, baseY - h - 75f, 210, 26),
            17, Color.white, TextAnchor.MiddleCenter);
    }

    private void DrawExit()
    {
        float sx = exitX - cameraX;
        if (sx < -100f || sx > 1370f) return;
        Color glow = levelIndex == 2 ? Html("#ED8B54") : Html("#D5C587");
        Disc(new Rect(sx - 43f, GroundScreenY - 129f, 86f, 118f), new Color(glow.r, glow.g, glow.b, .33f));
        Fill(new Rect(sx - 32f, GroundScreenY - 92f, 64f, 92f), Html("#1F2526"));
        Disc(new Rect(sx - 32f, GroundScreenY - 119f, 64f, 65f), Html("#1F2526"));
        Fill(new Rect(sx - 20f, GroundScreenY - 87f, 40f, 87f), glow);
        Disc(new Rect(sx - 20f, GroundScreenY - 102f, 40f, 45f), glow);
        Text(levelIndex == 2 && BossAlive() ? "ПОБЕДИ ТЕНЬ" : "ВЫХОД →",
            new Rect(sx - 85f, GroundScreenY - 155f, 170f, 30f), 15,
            Color.white, TextAnchor.MiddleCenter);
    }

    private void DrawCheckpoint()
    {
        float sx = worldWidth * .5f - cameraX;
        if (sx < -30f || sx > 1310f) return;
        Fill(new Rect(sx, GroundScreenY - 115f, 5f, 115f), Html("#DDD6C1"));
        Fill(new Rect(sx + 5f, GroundScreenY - 113f, 55f, 33f),
            reachedCheckpoint ? Html("#D9BD75") : Html("#849396"));
        Text("✦", new Rect(sx + 12f, GroundScreenY - 111f, 43f, 28f),
            19, Html("#26313A"), TextAnchor.MiddleCenter);
    }

    private void DrawGollum()
    {
        float sx = 2250f - cameraX;
        if (sx < -60f || sx > 1340f) return;
        Disc(new Rect(sx - 14f, GroundScreenY - 61f, 28f, 27f), Html("#A8A89A"));
        Fill(new Rect(sx - 16f, GroundScreenY - 39f, 32f, 36f), Html("#686C64"));
        Disc(new Rect(sx - 9f, GroundScreenY - 53f, 5f, 5f), Html("#E8D596"));
        Disc(new Rect(sx + 4f, GroundScreenY - 53f, 5f, 5f), Html("#E8D596"));
        Text("ГОЛЛУМ", new Rect(sx - 48f, GroundScreenY - 95f, 96f, 25f),
            12, Color.white, TextAnchor.MiddleCenter);
    }

    private void Avatar(Hero hero, float x, float feetY, float scale, bool walk, bool hurt)
    {
        float s = scale;
        float bob = walk ? Mathf.Sin(Time.time * 13f) * 2.5f * s : 0f;
        Color skin = Html("#D5AD86");
        Fill(new Rect(x - 18f * s, feetY - 56f * s + bob, 36f * s, 50f * s), hero.cloak);
        Fill(new Rect(x - 15f * s, feetY - 8f * s, 12f * s, 8f * s), Html("#302F2C"));
        Fill(new Rect(x + 4f * s, feetY - 8f * s, 12f * s, 8f * s), Html("#302F2C"));
        Disc(new Rect(x - 14f * s, feetY - 82f * s + bob, 28f * s, 29f * s), skin);
        Disc(new Rect(x - 15f * s, feetY - 86f * s + bob, 30f * s, 17f * s), hero.hair);
        if (hero.name == "Гэндальф")
        {
            Fill(new Rect(x - 16f * s, feetY - 70f * s, 32f * s, 18f * s), Html("#DBDAD0"));
            Fill(new Rect(x + 22f * s, feetY - 93f * s, 4f * s, 89f * s), Html("#8F8164"));
        }
        else if (hero.name == "Леголас" || hero.name == "Арвен")
        {
            Fill(new Rect(x - 14f * s, feetY - 70f * s, 6f * s, 30f * s), hero.hair);
            Fill(new Rect(x + 9f * s, feetY - 70f * s, 6f * s, 30f * s), hero.hair);
        }
        else if (hero.name == "Гимли")
        {
            Fill(new Rect(x - 10f * s, feetY - 62f * s, 20f * s, 18f * s), hero.hair);
            Fill(new Rect(x + 22f * s, feetY - 70f * s, 4f * s, 53f * s), Html("#877A6A"));
        }
        else if (hero.name == "Арагорн" || hero.name == "Боромир" ||
                 hero.name == "Эовин" || hero.name == "Фарамир")
        {
            Fill(new Rect(x + 21f * s, feetY - 83f * s, 4f * s, 66f * s), Html("#D4D4C8"));
        }
        if (hurt) Fill(new Rect(x - 17f * s, feetY - 60f * s, 34f * s, 56f * s),
            new Color(1f, .1f, .1f, .4f));
    }

    private void DrawControls()
    {
        Control(LeftButton, "◀", leftHeld);
        Control(RightButton, "▶", rightHeld);
        Control(JumpButton, "ПРЫЖОК", false);
        Control(AttackButton, "АТАКА", false);
    }

    private void Control(Rect rect, string label, bool active)
    {
        Fill(rect, active ? new Color(.83f, .7f, .42f, .8f) : new Color(.07f, .1f, .1f, .7f));
        Border(rect, new Color(1f, 1f, 1f, .35f), 2f);
        Text(label, rect, label.Length > 2 ? 16 : 27, Color.white, TextAnchor.MiddleCenter);
    }

    private void DrawOverlay(string title, string subtitle, string action, Action callback)
    {
        Fill(new Rect(0, 0, 1280, 720), new Color(.02f, .04f, .04f, .82f));
        Text(title, new Rect(145, 173, 990, 90), 62, Html("#F0DFC1"), TextAnchor.MiddleCenter);
        Text(subtitle, new Rect(245, 270, 790, 60), 23, Color.white, TextAnchor.MiddleCenter);
        if (Button(new Rect(458, 366, 364, 68), action.ToUpperInvariant() + "  →",
            Html("#D6B56B"), Html("#1B211C"), 21)) callback();
        if (Button(new Rect(458, 447, 364, 58), "ВЫБРАТЬ ГЕРОЯ",
            new Color(1f, 1f, 1f, .17f), Color.white, 18)) screen = GameScreen.Menu;
        Text("ШИР  •  МОРИЯ  •  МОРДОР", new Rect(420, 568, 440, 29), 15,
            Html("#C9C7BB"), TextAnchor.MiddleCenter);
    }

    private void Retry()
    { health = Heroes[selectedHero].health; LoadLevel(); screen = GameScreen.Playing; }

    private static void Fill(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    private static void Border(Rect rect, Color color, float thickness)
    {
        Fill(new Rect(rect.x, rect.y, rect.width, thickness), color);
        Fill(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        Fill(new Rect(rect.x, rect.y, thickness, rect.height), color);
        Fill(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
    }

    private void Disc(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, circleTexture);
        GUI.color = previous;
    }
    private void Ring(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, ringTexture);
        GUI.color = previous;
    }

    private void Text(string value, Rect rect, int size, Color color,
        TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        GUIStyle style = new GUIStyle(GUI.skin.label);
        if (gameFont != null) style.font = gameFont;
        style.fontSize = size;
        style.fontStyle = FontStyle.Bold;
        style.alignment = alignment;
        style.wordWrap = true;
        style.normal.textColor = color;
        GUI.Label(rect, value, style);
    }

    private static bool InvisibleButton(Rect rect)
    {
        GUIStyle style = new GUIStyle(GUI.skin.button);
        style.normal.background = null;
        style.hover.background = null;
        style.active.background = null;
        style.normal.textColor = Color.clear;
        return GUI.Button(rect, GUIContent.none, style);
    }

    private bool Button(Rect rect, string label, Color background, Color foreground, int size)
    {
        Fill(rect, background);
        Text(label, rect, size, foreground, TextAnchor.MiddleCenter);
        return InvisibleButton(rect);
    }
}
