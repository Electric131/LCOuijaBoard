
// It's not really the cleanest, I didn't bother to split this into multiple files.
// Deal with it.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using HarmonyLib;
using BepInEx;
using UnityEngine.InputSystem;
using LethalLib.Modules;
using GameNetcodeStuff;
using System.Runtime.InteropServices;
using UnityEngine.EventSystems;
using System.Reflection;
using Object = UnityEngine.Object;
using Unity.Netcode;
using System.Xml.Linq;
using static LCOuijaBoard.Plugin;
using NetworkPrefabs = LethalLib.Modules.NetworkPrefabs;
using UnityEngine.UI;
using UnityEngine.InputSystem.LowLevel;
using System.Xml.Schema;
using TMPro;
using UnityEngine.Windows;
using static System.Net.Mime.MediaTypeNames;
using System.IO.Ports;
using System.Text.RegularExpressions;

namespace LCOuijaBoard
{
    [BepInPlugin(modGUID, modName, modVersion)]
    public class Plugin : BaseUnityPlugin
    {
        private const string modGUID = "Electric.OuijaBoard";
        private const string modName = "OuijaBoard";
        private const string modVersion = "1.4.0";

        private readonly Harmony harmony = new Harmony(modGUID);
        private static MethodInfo chat;

        public static bool storeEnabled;
        public static int storeCost;
        public static bool scrapEnabled;
        public static int scrapRarity;
        public static bool makesSound;

        private static bool DEVDEBUG = false; // Disable some checks to make it easier to debug

        public static GameObject OuijaNetworkerPrefab;
        public static GameObject OuijaTextUIPrefab;
        public static GameObject OuijaTextUI;
        public static GameObject OuijaErrorUIPrefab;
        public static GameObject OuijaErrorUI;

        private void Awake()
        {
            AssetBundle data = AssetBundle.LoadFromMemory(Properties.Resources.fullboard);
            OuijaNetworkerPrefab = data.LoadAsset<GameObject>("Assets/OuijaNetworker.prefab");
            OuijaNetworkerPrefab.AddComponent<OuijaNetworker>();

            OuijaTextUIPrefab = data.LoadAsset<GameObject>("Assets/OuijaTextUI.prefab");
            OuijaErrorUIPrefab = data.LoadAsset<GameObject>("Assets/OuijaErrorUI.prefab");
            GameObject itemStoreObject = data.LoadAsset<GameObject>("Assets/OuijaBoardStore.prefab");
            GameObject itemScrapObject = data.LoadAsset<GameObject>("Assets/OuijaBoardScrap.prefab");

            NetworkPrefabs.RegisterNetworkPrefab(OuijaNetworkerPrefab);
            NetworkPrefabs.RegisterNetworkPrefab(itemStoreObject);
            NetworkPrefabs.RegisterNetworkPrefab(itemScrapObject);

            InputAction action = new InputAction(binding: "<Keyboard>/#(o)");
            action.performed += UIHandler.ToggleUI;
            action.Enable();

            chat = AccessTools.Method(typeof(HUDManager), "AddChatMessage");

            storeEnabled = Config.Bind("Store", "Enabled", true, "Allow Ouija Board to be bought in the store").Value;
            storeCost = Config.Bind("Store", "Cost", 100, "Cost of a Ouija Board in the store (Store must be enabled)").Value;
            scrapEnabled = Config.Bind("Scrap", "Enabled", false, "Allow the Ouija Board to spawn in the facility").Value;
            scrapRarity = Config.Bind("Scrap", "Rarity Weight", 20, "Chance for a Ouija Board to spawn as scrap").Value;
            if (storeCost < 0) { storeCost = 0; }
            if (scrapRarity < 0) { scrapRarity = 0; }

            makesSound = Config.Bind("General", "Makes Sound", true, "Enables the Ouija Board's sliding to be heard by enemies").Value;

            Item storeItem = data.LoadAsset<Item>("Assets/OuijaBoardStoreItem.asset");
            Item scrapItem = data.LoadAsset<Item>("Assets/OuijaBoardScrapItem.asset");
            if (storeEnabled)
            {
                Debug.Log($"Ouija Board store enabled at {storeCost} credits");
                Items.RegisterShopItem(storeItem, storeCost);
            }
            if (scrapEnabled)
            {
                Debug.Log($"Ouija Board scrap spawn enabled at {scrapRarity} rarity weight");
                Items.RegisterScrap(scrapItem, scrapRarity, Levels.LevelTypes.All);
            }

            NetcodeWeaver();

            harmony.PatchAll();
            Logger.LogInfo($"{modName} loaded!");
        }

        private class UIHandler {

            public static GameObject inputObject;
            public static TMP_InputField input;
            public static bool registered;

            public static float lastError = 0;

            public static TMP_InputField GetInput()
            {
                if (registered && (input != null)) return input;
                inputObject = OuijaTextUI.transform.GetChild(2).gameObject;
                input = inputObject.GetComponent<TMP_InputField>();
                input.onSubmit.AddListener(SubmitUI);
                input.onEndEdit.AddListener(EndEditUI);
                registered = true;
                return input;
            }

            public static void hide()
            {
                OuijaTextUI.SetActive(false);
                input.text = "";
            }

            public static void ToggleUI(InputAction.CallbackContext context)
            {
                PlayerControllerB local = GameNetworkManager.Instance.localPlayerController;
                Debug.Log("Ouija Text UI Toggle Requested");
                if (!DEVDEBUG && (local == null || !local.isPlayerDead)) { Debug.Log("Ouija Text UI Toggle Denied: Not Dead"); return; }
                if (OuijaTextUI == null) { Debug.LogError("Ouija Text UI Toggle Denied: No UI"); return; } // Return if UI does not exist
                bool shown = !OuijaTextUI.active;
                GetInput();
                Debug.Log($"Ouija Text UI Toggle Accepted: New State is {shown}");
                if (!shown)
                {
                    // Don't hide if still typing
                    if (input.isFocused && OuijaTextUI.active) { return; }
                    OuijaTextUI.SetActive(false);
                }
                else
                {
                    OuijaTextUI.SetActive(true);
                    input.ActivateInputField();
                    input.Select();
                }
            }

            public static void SubmitUI(string msg)
            {
                AttemptSend(msg);
            }

            public static void EndEditUI(string msg)
            {
                hide();
            }

            public static bool AttemptSend(string msg)
            {
                string[] args = msg.Split(' ').ToArray();
                Object[] objects = GameObject.FindObjectsOfType(typeof(GameObject));
                List<GameObject> boards = new List<GameObject>();
                // Build list of boards
                foreach (GameObject obj in objects)
                {
                    // Check if object is a ouija board and isn't held by a player
                    if ((obj.name == "OuijaBoardScrap(Clone)" || obj.name == "OuijaBoardStore(Clone)") && !((PhysicsProp)obj.GetComponent(typeof(PhysicsProp))).isHeld)
                    {
                        boards.Add(obj);
                    }
                }
                // Check if any boards exist
                if (boards.Count > 0)
                {
                    if (!StartOfRoundPatch.complete)
                    {
                        ShowError("Boards on cooldown");
                        return false;
                    }
                    string message = String.Join("", args);
                    if (Regex.Match(message, "([A-Za-z\\d ])+").Value.Length != message.Length)
                    {
                        ShowError("Invalid character(s)");
                        return false;
                    }
                    message = message.Replace(" ", "");
                    if (message.Length > 10)
                    {
                        ShowError("Too many characters");
                        return false;
                    }
                    OuijaNetworker.Instance.WriteOut(message);
                    return true;
                }
                else
                {
                    ShowError("No valid boards");
                }
                return false;
            }

            public static void ShowError(string msg)
            {
                if (OuijaErrorUI != null)
                {
                    Debug.Log($"Ouija Board showing erorr: {msg}");
                    OuijaErrorUI.transform.GetChild(0).GetComponent<TMP_Text>().text = msg;
                    OuijaErrorUI.SetActive(true);
                    lastError = Time.time;
                }
            }
        }

        private static void NetcodeWeaver()
        {
            var types = Assembly.GetExecutingAssembly().GetTypes();
            foreach (var type in types)
            {
                var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                foreach (var method in methods)
                {
                    var attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                    if (attributes.Length > 0)
                    {
                        method.Invoke(null, null);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(StartOfRound))]
        public class StartOfRoundPatch
        {
            public static int writeIndex = 0;
            public static List<string> names = new List<string>();
            public static List<GameObject> boards = new List<GameObject>();
            public static float timer = 0;
            public static double amount = 0;
            public static bool complete = true;

            [HarmonyPatch("Update")]
            [HarmonyPostfix]
            static void Update(ref StartOfRound __instance)
            {
                PlayerControllerB local = __instance.localPlayerController;
                if ((!DEVDEBUG && !local.isPlayerDead) && OuijaTextUI && OuijaTextUI.active)
                {
                    Debug.Log("Ouija Text UI closed since player is not dead");
                    OuijaTextUI.SetActive(false);
                }
                if (OuijaErrorUI && OuijaErrorUI.active && (Time.time - UIHandler.lastError) > 2f)
                {
                    Debug.Log("Ouija Error UI closed");
                    OuijaErrorUI.SetActive(false);
                }
                if (writeIndex < names.Count)
                {
                    amount = Mathf.Clamp(timer / 1.2f, 0, 1);
                    MoveUpdate(names[writeIndex]);
                    timer += Time.deltaTime;
                    if (timer >= 3f)
                    {
                        amount = 1f;
                        MoveUpdate(names[writeIndex]);
                        timer = 0f;
                        writeIndex++;
                    }
                }
                else if (!complete)
                {
                    if (timer < 5f)
                    {
                        timer += Time.deltaTime;
                    } else
                    {
                        complete = true;
                        timer = 0f;
                        amount = 0f;
                    }
                }
                if (OuijaTextUI != null)
                {
                    OuijaTextUI.transform.GetChild(3).gameObject.SetActive(!complete);
                }
            }

            public static void MoveUpdate(string name)
            {
                int index = -1;
                bool anyValid = false;
                PlayerControllerB local = GameNetworkManager.Instance.localPlayerController;
                foreach (GameObject board in boards)
                {
                    if (!board) continue;
                    // Board cannot be held
                    if (((PhysicsProp)board.GetComponent(typeof(PhysicsProp))).isHeld)
                    {
                        continue;
                    }
                    anyValid = true;
                    if (index == -1) // No index found
                    {
                        index = 0;
                        foreach (Transform childTrans in board.transform.GetChild(0).GetChild(3))
                        {
                            GameObject child = childTrans.gameObject;
                            if (child.name == name)
                            {
                                break;
                            }
                            index++;
                        }
                        if (index == -1)
                        {
                            amount = 0f;
                            writeIndex++;
                            break;
                        }
                    }
                    // Use found index to make search much faster
                    Vector3 position = board.transform.GetChild(0).GetChild(3).GetChild(index).localPosition; // Selectors
                    Vector3 oldPosition = board.transform.GetChild(0).GetChild(4).localPosition; // OldPaddle
                    GameObject paddle = board.transform.GetChild(0).GetChild(2).gameObject; // Paddle
                    if (amount == 0) // Sliding Sound
                    {
                        paddle.GetComponent<AudioSource>().Play();
                        if (makesSound) RoundManager.Instance.PlayAudibleNoise(board.transform.position, 8f, 0.3f, noiseID: 8925);
                    }
                    Vector3 newPos = position + new Vector3(0f, 0.166f, 0f); // Position to move to + y offset
                    paddle.transform.localPosition = Vector3.Lerp(oldPosition, newPos, (float)amount);
                    if (amount == 1f) // Move the OldPaddle to the new location if at 100%
                    {
                        board.transform.GetChild(0).GetChild(4).localPosition = newPos;
                    }
                    if (Vector3.Distance(local.transform.position, board.transform.position) < 5f)
                    {
                        local.insanityLevel += Time.deltaTime * 0.5f;
                    }
                }
                if (!anyValid)
                {
                    // Stop message entirely
                    writeIndex = names.Count;
                }
            }
        }

        public class OuijaNetworker : NetworkBehaviour
        {

            public static OuijaNetworker Instance;

            private void Awake()
            {
                Instance = this;
            }

            // `names` should be the game object names under `Selectors` object
            public void WriteOut(string message)
            {
                if (base.IsOwner)
                {
                    WriteOutClientRpc(message);
                }
                else
                {
                    WriteOutServerRpc(message);
                }
            }

            [ClientRpc]
            public void WriteOutClientRpc(string message)
            {

                Object[] objects = GameObject.FindObjectsOfType(typeof(GameObject));
                List<GameObject> boards = new List<GameObject>();
                // Build list of boards
                foreach (GameObject obj in objects)
                {
                    // Check if object is a ouija board and isn't held by a player
                    if ((obj.name == "OuijaBoardScrap(Clone)" || obj.name == "OuijaBoardStore(Clone)") && !((PhysicsProp)obj.GetComponent(typeof(PhysicsProp))).isHeld)
                    {
                        boards.Add(obj);
                    }
                }
                List<string> final = new List<string>();
                switch (message)
                {
                    case "yes":
                    case "y":
                        final.Add("Yes");
                        break;
                    case "no":
                    case "n":
                        final.Add("No");
                        break;
                    case "goodbye":
                    case "bye":
                        final.Add("Goodbye");
                        break;
                    default:
                        // Split to char array, then join back into string array
                        final = message.ToUpper().ToCharArray().Select(c => c.ToString()).ToList();
                        break;
                }
                StartOfRoundPatch.amount = 0f;
                StartOfRoundPatch.complete = false;
                StartOfRoundPatch.writeIndex = 0;
                StartOfRoundPatch.names = final;
                StartOfRoundPatch.boards = boards;
            }

            [ServerRpc(RequireOwnership = false)]
            public void WriteOutServerRpc(string message)
            {
                WriteOutClientRpc(message);
            }
        }

        [HarmonyPatch(typeof(RoundManager))]
        internal class RoundManagerPatch
        {
            [HarmonyPatch("Start")]
            [HarmonyPostfix]
            static void StartPatch(ref RoundManager __instance)
            {
                if (__instance.IsServer && OuijaNetworker.Instance == null)
                {
                    GameObject ouijaNetworker = Instantiate<GameObject>(OuijaNetworkerPrefab);
                    ouijaNetworker.GetComponent<NetworkObject>().Spawn(true);
                }
                OuijaTextUI = Instantiate<GameObject>(OuijaTextUIPrefab);
                OuijaTextUI.SetActive(false);
                OuijaErrorUI = Instantiate<GameObject>(OuijaErrorUIPrefab);
                OuijaErrorUI.SetActive(false);
            }
        }
    }
}
