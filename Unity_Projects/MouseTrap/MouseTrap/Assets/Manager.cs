using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

public class Manager : MonoBehaviour
{
    /* PUBLIC VARS */
    //*************************************************************************
    [System.NonSerialized] public GameObject mouse;
    [System.NonSerialized] public List<GameObject> mapHexes;
    [System.NonSerialized] public Graph graphHexes;
    [System.NonSerialized] public Level currentLevel;
    [System.NonSerialized] public GameObject wl_menu;
    [System.NonSerialized] public int level = 1;

    public bool userTurn;
    public bool mouseWin;
    public bool userWin;
    public bool retry;
    public bool quit;
    
    public GameObject mapHex_Prefab;
    public GameObject mouse_Prefab;
    public GameObject wl_menu_Prefab;
    //*************************************************************************

    /* PRIVATE VARS */
    //*************************************************************************
    private bool wl_menu_loaded = false;
    private const float sq3 = 1.7320508075688772935274463415059F;
    private static List<Level> levels = new List<Level>
    {
        new Level(1,25,11,5),
        new Level(2,15,10,6),
        new Level(3,20,8,6)
    };

    private List<int> levelsCompleted = new List<int>();
    //*************************************************************************

    // Start is called before the first frame update
    void Start()
    {
        // User's turn first
        userTurn = true;
        mouseWin = false;
        userWin  = false;

        // Set true when retry button clicked
        retry = false;

        // Instatiate vars
        mapHexes = new List<GameObject>();
        graphHexes = new Graph();

        // Load the game based on the level
        currentLevel = levels[level-1];
        LoadGame(currentLevel);

        // Save the game with the current level
        levelsCompleted.Add(level - 1);
        Save();

        Debug.Log(Application.persistentDataPath);
    }

    // Update is called once per frame
    void Update()
    {
        if ((userWin || mouseWin) && !wl_menu_loaded)
        {
            LoadWL_Menu();
            wl_menu_loaded = true;
        }

        if (retry)
            Retry();
     
        if (quit)
            QuitGame();
    }

    // Method to get all adjacent hexes from a given gameobject
    public List<MapHex> GetAdjacentHexes(GameObject originObject,
        float nominalColliderRadius,
        float expandedColliderRadius, bool allowClicked = false)
    {
        List<MapHex> adjacentHexes = new List<MapHex>();

        // Grow circle collider so it can find adjacent hexes
        originObject.GetComponent<CircleCollider2D>().radius =
            expandedColliderRadius;

        // Get list of overlapping colliders
        List<Collider2D> results = new List<Collider2D>();
        ContactFilter2D contactFilter = new ContactFilter2D();
        originObject.GetComponent<CircleCollider2D>().
            OverlapCollider(contactFilter.NoFilter(), results);

        foreach (Collider2D result in results)
        {
            // Check if the circle collider is overlapping a collider
            // belonging to a hex

            // then check to make sure it is not the hex the mouse is currently
            // on

            // then check to make sure the hex isn't clicked

            bool isHex = result.transform.name == "Hex";
            bool isAdjacent = (result.transform.position - transform.position)
                .magnitude > 1e-2f;

            if (isHex && isAdjacent)
            {
                MapHex mapHex = result.GetComponentInParent<MapHex>();
                if (allowClicked)
                {
                    adjacentHexes.Add(mapHex);
                }
                else if(!mapHex.isClicked)
                {
                    adjacentHexes.Add(mapHex);
                }
            }
        }

        // Shrink circle collider so mouse cannot strike it instead of
        // a hex
        originObject.GetComponent<CircleCollider2D>().radius =
            nominalColliderRadius;


        return adjacentHexes;
    }

    // Load the win/loss message menu
    void LoadWL_Menu()
    {
        wl_menu = Instantiate(wl_menu_Prefab,
            -3*Vector3.forward, Quaternion.identity, GetComponent<Transform>());
        wl_menu.name = "wl_menu";
        wl_menu.GetComponent<Canvas>().worldCamera = Camera.main;
        wl_menu.GetComponent<Canvas>().planeDistance = 1;
    }

    // Reload the game on the current level or next level
    void Retry()
    {
        wl_menu_loaded = false;
        Destroy(mouse);
        Destroy(wl_menu);
        foreach(GameObject mapHex in mapHexes)
        {
            Destroy(mapHex);
        }
        mapHexes.Clear();
        graphHexes = new Graph();
        Start();
    }

    // Generate the Hexes that make the grid 
    void GenerateMap()
    {
        // Set camera size to accomodate the map
        Camera.main.orthographicSize = currentLevel.cameraSize;

        // Make first hex @ origin
        // Vector should be z = 0 so it shows below mouse
        Vector3 spawnPosition = new Vector3(0f, 0f, 0f);
        GameObject originHex = Instantiate(mapHex_Prefab, spawnPosition,
            Quaternion.identity, GetComponent<Transform>());
        originHex.name = "Hex";
        mapHexes.Add(originHex);

        /*
        Code stolen from:
        https://www.codeproject.com/
        Articles/1249665/Generation-of-a-hexagonal-tessellation
        */

        //Spawn scheme: nDR, nDX, nDL, nUL, nUX, End??, UX, nUR
        Vector3[] mv = {
            new Vector3(1.5f,-sq3*0.5f, 0),       //DR
            new Vector3(0,-sq3, 0),               //DX
            new Vector3(-1.5f,-sq3*0.5f, 0),      //DL
            new Vector3(-1.5f,sq3*0.5f, 0),       //UL
            new Vector3(0,sq3, 0),                //UX
            new Vector3(1.5f,sq3*0.5f, 0)         //UR
        };

        // 2.8f found experimentally
        // would need to change when sprite changes
        int lmv = mv.Length;
        float HexSide = mapHex_Prefab.transform.localScale.x * 2.8f;

        // Make counter and calc. when on final radius to apply .isEdge param
        // in MapHex. Counter starts at one because we already made the origin
        // hex
        int counter = 1;
        int edgeBegins = 2;
        for (int ii = 1; ii < currentLevel.mapRadius; ii++)
        {
            edgeBegins += 6 * ii;
        }

        // Exec algorithm 
        Vector3 currentPoint = new Vector3(0f, 0f, 0f);
        for (int mult = 0; mult <= currentLevel.mapRadius; mult++)
        {
            for (int j = 0; j < lmv; j++)
            {
                for (int i = 0; i < mult; i++)
                {
                    currentPoint += (mv[j] * HexSide);
                    GameObject h = Instantiate(mapHex_Prefab, currentPoint,
                        mapHex_Prefab.transform.rotation, transform);
                    h.name = "Hex";
                    mapHexes.Add(h);
                    counter++;
                    if (counter >= edgeBegins)
                    {
                        h.GetComponent<MapHex>().isEdge = true;
                    }
                }
                if (j == 4)
                {
                    if (mult == currentLevel.mapRadius)
                        break;      //Finished
                    currentPoint += (mv[j] * HexSide);
                    GameObject h = Instantiate(mapHex_Prefab, currentPoint,
                        mapHex_Prefab.transform.rotation, transform);
                    h.name = "Hex";
                    mapHexes.Add(h);
                    counter++;
                    if (counter >= edgeBegins)
                    {
                        h.GetComponent<MapHex>().isEdge = true;
                    }
                }
            }
        }

        counter = 0;
        while (counter < currentLevel.numAlreadyClicked)
        {
            List<GameObject> tmp = mapHexes.FindAll(mapHex =>
                !mapHex.GetComponent<MapHex>().isClicked
                && mapHex.transform.position != new Vector3(0f, 0f, 0f));
            int rnd = Random.Range(0, tmp.Count);
            tmp[rnd].
                GetComponent<MapHex>().isClicked = true;
            tmp[rnd].
                GetComponent<SpriteRenderer>().color = Color.black;
            counter++;
        }
    }

    // Generate the mouse
    void SpawnMouse()
    {
        // Make first hex @ origin
        // Vector should be z = -1 so it shows on top of the hexes
        Vector3 spawnPosition = new Vector3(0f, 0f, -1f);
        mouse = Instantiate(mouse_Prefab, spawnPosition,
            Quaternion.identity, GetComponent<Transform>());
        mouse.name = "Mouse";
    }

    // Loads the game given the level input 
    void LoadGame(Level level)
    {
        // Generate Map
        GenerateMap();

        // Spawn the Mouse
        SpawnMouse();
    }

    // Save the game data
    void Save()
    {
        SaveData sd = new SaveData();
        sd.levelsCompleted = levelsCompleted;

        // Write the save file
        BinaryFormatter bf = new BinaryFormatter();
        FileStream file =
            File.Create(Application.persistentDataPath + "/gamesave.save");
        bf.Serialize(file, sd);
        file.Close();
    }

    // Load game from file
    void Load()
    {
        SaveData sd = new SaveData();

        // Check for save file
        if (File.Exists(Application.persistentDataPath + "/gamesave.save"))
        {
            BinaryFormatter bf = new BinaryFormatter();
            FileStream file =
                File.Open(Application.persistentDataPath +
                                            "/gamesave.save", FileMode.Open);
            sd = (SaveData)bf.Deserialize(file);
            levelsCompleted = sd.levelsCompleted;
            file.Close();
        }
        else
        {
            Debug.Log("No game saved!");
        }
    }

    // Quit the Game
    void QuitGame()
    {
        Save();
        Application.Quit();
    }
}

public class Level
{

    public int level;
    public int mouseBlunderPercentage;
    public int numAlreadyClicked;
    public int mapRadius;
    public int noMice;
    public float cameraSize;

    public Level(int lvl, int mbp, int numClicked, int mapRad)
    {
        level = lvl;
        mouseBlunderPercentage = mbp;
        numAlreadyClicked = numClicked;
        mapRadius = mapRad;
        cameraSize = 25f / 6f * (float) mapRad  + 3f;
    }
}

[System.Serializable]
public class SaveData
{
    public List<int> levelsCompleted = new List<int>();
}