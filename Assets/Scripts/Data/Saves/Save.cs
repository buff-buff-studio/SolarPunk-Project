using System;
using System.Linq;
using Solis.Data.JSON;
using Solis.Interface.Lobby;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Solis.Data.Saves
{
    /// <summary>
    /// Basic save class, contains the save data and the save itself
    /// </summary>
    [Serializable]
    public class Save
    {
        #region Private Static Fields
        private static string _savesFolder;
        #endregion

        #region Private Static Properties
        private static string SavesFolder
        {
            get
            {
                if (string.IsNullOrEmpty(_savesFolder))
                {
                    _savesFolder = Application.persistentDataPath + "/Saves";
                }

                return _savesFolder;
            }
        }
        #endregion

        #region Public Fields
        public Texture2D preview;
        public float playTime;
        public string name;

        [SerializeField]
        public SaveData data = new();
        #endregion

        #region Public Properties
        /// <summary>
        /// Returns true if the save is saved
        /// </summary>
        public bool IsSaved => !string.IsNullOrEmpty(name);
        #endregion

        /// <summary>
        /// Clear the save data to its initial state
        /// </summary>
        public void New()
        {
            data = new SaveData();
            name = null;
            preview = null;
            playTime = 0;

            // if (LobbyScreen.Instance != null) LobbyScreen.Instance.RefreshSave();
        }

        /// <summary>
        /// Load the save data from a snapshot.
        /// </summary>
        /// <param name="snapshot"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public void LoadData(SaveSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            preview = snapshot.preview;
            playTime = snapshot.playTime;
            name = snapshot.name;
            preview = snapshot.preview;

            var folder = SavesFolder + $"/{name}/";

            if (!System.IO.Directory.Exists(folder))
                throw new System.IO.DirectoryNotFoundException("Save not found");

            var json = JsonValue.Parse(System.IO.File.ReadAllText(folder + "data.json"));
            data.ReadFromJson(json as JsonObject);

            // if (LobbyScreen.Instance != null) LobbyScreen.Instance.RefreshSave();
        }

        /// <summary>
        /// Save the save data to its folder.
        /// </summary>
        /// <param name="callback"></param>
        public void SaveData(Action callback)
        {
            if (string.IsNullOrEmpty(name))
                name = CreateName();

            var folder = SavesFolder + $"/{name}/";

            if (!System.IO.Directory.Exists(folder))
                System.IO.Directory.CreateDirectory(folder);

            //Save main save info
            var json = new JsonObject
            {
                { "name", name },
                { "playTime", playTime }
            };
            System.IO.File.WriteAllText(folder + "save.json", json.ToString());

            //Save the data
            var jsonData = new JsonObject();
            data.WriteToJson(jsonData);
            System.IO.File.WriteAllText(folder + "data.json", jsonData.ToString());

            //Save the preview
            if (preview == null)
                preview = CreatePreview();

            if (preview != null)
            {
                var bytes = preview.EncodeToPNG();
                System.IO.File.WriteAllBytes(folder + "preview.png", bytes);
            }

            callback?.Invoke();

            //if (LobbyScreen.Instance != null) LobbyScreen.Instance.RefreshSave();

            Debug.Log($"Saved to {folder}!");
        }

        /// <summary>
        /// Remove all the save snapshots found in the saves folder.
        /// </summary>
        /// <param name="onFindSnapshot"></param>
        /// <param name="onFinish"></param>
        public static void GetAllSnapshots(Action<SaveSnapshot> onFindSnapshot, Action onFinish)
        {
            var folder = SavesFolder + "/";
            if (!System.IO.Directory.Exists(folder))
            {
                onFinish?.Invoke();
                return;
            }

            var directories = System.IO.Directory.GetDirectories(folder);
            directories = directories.OrderByDescending(System.IO.Directory.GetLastWriteTime).ToArray();
            
            foreach (var directory in directories)
            {
                try
                {
                    var json = (JsonValue.Parse(System.IO.File.ReadAllText(directory + "/save.json")) as JsonObject)!;

                    Texture2D preview = null;

                    if (System.IO.File.Exists(directory + "/preview.png"))
                    {
                        var bytes = System.IO.File.ReadAllBytes(directory + "/preview.png");
                        preview = new Texture2D(2, 2);
                        preview.LoadImage(bytes);
                    }

                    var snapshot = new SaveSnapshot
                    {
                        name = json.Get("name") as JsonString,
                        lastModificationTime = System.IO.File.GetLastWriteTime(directory + "/data.json"),
                        playTime = json.Get("playTime") as JsonNumber,
                        preview = preview
                    };
                    onFindSnapshot?.Invoke(snapshot);
                }
                catch
                {
                    //ignored
                }
            }

            onFinish?.Invoke();
        }

        /// <summary>
        /// Format the play time (seconds) to a string.
        /// </summary>
        /// <param name="playTime"></param>
        /// <returns></returns>
        public static string PlayTimeToString(float playTime)
        {
            var time = TimeSpan.FromSeconds(playTime);

            //if less than 1 hour, show only minutes and seconds
            if (time.Hours == 0)
                return time.ToString(@"mm\:ss");

            //if less than 1 day, show hours and minutes
            if (time.Days == 0)
                return time.ToString(@"hh\:mm\:ss");

            //if more than 1 day, show days, hours and minutes
            return time.ToString(@"dd\:hh\:mm\:ss");
        }

        /// <summary>
        /// Create a random name for the save
        /// </summary>
        /// <returns></returns>
        public static string CreateName()
        {
            string[] title =
            {
                "Master", "Boss", "Divine", "Different", "Special", "Amazing", "Blaster", "Exceptional",
                "Incredible", "Unbelievable", "Unreal", "Unrealistic", "Unthinkable"
            };

            string[] adjective =
            {
                "Tall", "Short", "Tiny", "Enormous", "Giant", "Red", "Green", "Blue", "Yellow", "Orange", "Brown",
                "Spicy", "Neutral", "Acid", "Basic", "Long", "Troubled", "Simple", "Generic", "Complex", "Complicated",
                "Gorgeous", "Beautiful", "Pretty", "Handsome", "Cute", "Adorable", "Attractive", "Angry", "Mad",
                "Furious",
            };
            string[] substantive =
            {
                "Cat", "Dog", "Elephant", "Mouse", "Rat", "Snake", "Lion", "Tiger", "Bear", "Wolf", "Fox", "Rabbit",
                "Hare", "Deer", "Horse", "Cow", "Pig", "Sheep", "Goat", "Chicken", "Duck", "Goose", "Turkey",
                "Pheasant",
                "Hawk", "Eagle", "Falcon", "Owl", "Parrot", "Penguin", "Seagull", "Dove", "Sparrow", "Robin",
                "Bluebird",
                "Car", "Bus", "Truck", "Van", "Bike", "Motorcycle", "Scooter", "Skateboard", "Rollerblades", "Train",
                "Subway", "Tram", "Trolley", "Monorail", "Plane", "Helicopter", "Jet", "Rocket", "Spaceship", "UFO",
                "Book", "Pen", "Pencil", "Eraser", "Ruler", "Compass", "Protractor", "Calculator", "Notebook", "Folder",
                "Sofa", "Chair", "Table", "Desk", "Bed", "Cupboard", "Closet", "Drawer", "Shelf", "Bookshelf",
                "Cabinet",
                "Fridge", "Freezer", "Oven", "Stove", "Microwave", "Dishwasher", "Dryer",
            };

            return
                $"{title[UnityEngine.Random.Range(0, title.Length)]} {adjective[UnityEngine.Random.Range(0, adjective.Length)]} {substantive[UnityEngine.Random.Range(0, substantive.Length)]}";
        }

        /// <summary>
        /// Creates the preview image.
        /// </summary>
        /// <returns></returns>
        public static Texture2D CreatePreview()
        {
            //create random image of 640 x 360
            var texture = new Texture2D(640, 360);

            for (var x = 0; x < texture.width; x++)
            {
                for (var y = 0; y < texture.height; y++)
                {
                    var color = new Color(
                        UnityEngine.Random.Range(0f, 1f),
                        UnityEngine.Random.Range(0f, 1f),
                        UnityEngine.Random.Range(0f, 1f),
                        1);
                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            return texture;
        }
    }
    
    #if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(Save))]
    public class SaveDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var data = property.FindPropertyRelative("data");
            var name = property.FindPropertyRelative("name");
            var isNew = string.IsNullOrEmpty(name.stringValue);
            var rect = position;

            rect.height = EditorGUIUtility.singleLineHeight;
            EditorGUI.LabelField(rect, label);
            rect.y += EditorGUIUtility.singleLineHeight;

            var childData = EditorGUI.GetPropertyHeight(data, true);
            rect.height = EditorGUIUtility.singleLineHeight * 2 + childData + 5;
            GUI.Box(rect, GUIContent.none);

            EditorGUI.BeginDisabledGroup(true);
            rect.height = EditorGUIUtility.singleLineHeight;
            EditorGUI.Toggle(rect, "Is New", isNew);
            rect.y += EditorGUIUtility.singleLineHeight;
            EditorGUI.TextField(rect, "Name", name.stringValue);
            EditorGUI.EndDisabledGroup();
            rect.y += EditorGUIUtility.singleLineHeight;
            rect.height = childData;
            EditorGUI.PropertyField(rect, data, true);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var data = property.FindPropertyRelative("data");
            return EditorGUIUtility.singleLineHeight * 3 + EditorGUI.GetPropertyHeight(data) + 5;
        }
    }
    #endif
}