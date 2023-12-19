
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

namespace LCOuijaBoard
{
    [BepInPlugin(modGUID, modName, modVersion)]
    public class Plugin : BaseUnityPlugin
    {
        private const string modGUID = "Electric.OuijaBoard";
        private const string modName = "OuijaBoard";
        private const string modVersion = "1.0.2";

        private readonly Harmony harmony = new Harmony(modGUID);
        private static MethodInfo chat;

        public static Item item;
        public static GameObject itemObject;
        public static int rarity;

        public static GameObject OuijaNetworkerPrefab;
        public static GameObject OuijaUIPrefab;
        public static GameObject OuijaUI;

        private void Awake()
        {
            AssetBundle data = AssetBundle.LoadFromMemory(Properties.Resources.fullboard);
            OuijaNetworkerPrefab = data.LoadAsset<GameObject>("Assets/OuijaNetworker.prefab");
            OuijaNetworkerPrefab.AddComponent<OuijaNetworker>();

            OuijaUIPrefab = data.LoadAsset<GameObject>("Assets/TextUI.prefab");
            itemObject = data.LoadAsset<GameObject>("Assets/OuijaBoard.prefab");

            NetworkPrefabs.RegisterNetworkPrefab(OuijaNetworkerPrefab);
            NetworkPrefabs.RegisterNetworkPrefab(itemObject);

            InputAction action = new InputAction(binding: "<Keyboard>/#(o)");
            action.performed += UIHandler.ToggleUI;
            action.Enable();

            chat = AccessTools.Method(typeof(HUDManager), "AddChatMessage");

            rarity = Config.Bind("General", "Rarity", 20, "Chance for a ouija board to spawn (Weighted)").Value;
            if (rarity < 0) { rarity = 0; }

            item = data.LoadAsset<Item>("Assets/OuijaBoardItem.asset");
            Items.RegisterScrap(item, rarity, Levels.LevelTypes.All);

            NetcodeWeaver();

            harmony.PatchAll();
            Logger.LogInfo($"{modName} loaded!");
        }

        private class UIHandler {

            public static GameObject inputObject;
            public static TMP_InputField input;
            public static bool registered;
            public static bool shown = false;

            public static TMP_InputField GetInput()
            {
                if (registered && !input) return input;
                inputObject = OuijaUI.transform.GetChild(2).gameObject;
                input = inputObject.GetComponent<TMP_InputField>();
                input.onSubmit.AddListener(SubmitUI);
                input.onEndEdit.AddListener(EndEditUI);
                registered = true;
                return input;
            }

            public static void ToggleUI(InputAction.CallbackContext context)
            {
                PlayerControllerB local = GameNetworkManager.Instance.localPlayerController;
                if (!local.isPlayerDead) { return; }
                if (!OuijaUI) { return; } // Return if UI does not exist
                bool shown = !OuijaUI.active;
                GetInput();
                if (shown == false)
                {
                    // Don't hide if still typing
                    if (input.isFocused) { return; }
                    OuijaUI.SetActive(shown);
                }
                else
                {
                    OuijaUI.SetActive(shown);
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
                OuijaUI.SetActive(false);
                input.text = "";
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
                    if (obj.name == "OuijaBoard(Clone)" && !((PhysicsProp)obj.GetComponent(typeof(PhysicsProp))).isHeld)
                    {
                        boards.Add(obj);
                    }
                }
                // Check if any boards exist
                if (boards.Count > 0)
                {
                    if (!PlayerPatch.complete)
                    {
                        // On cooldown
                        return false;
                    }
                    string message = String.Join("", args);
                    if (message.Length > 10)
                    {
                        // Character limit
                        return false;
                    }
                    OuijaNetworker.Instance.WriteOut(message);
                    return true;
                }
                else
                {
                    // No valid boards
                }
                return false;
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

        [HarmonyPatch(typeof(PlayerControllerB))]
        public class PlayerPatch
        {
            public static int writeIndex = 0;
            public static List<string> names = new List<string>();
            public static List<GameObject> boards = new List<GameObject>();
            public static float timer = 0;
            public static double amount = 0;
            public static bool complete = true;

            [HarmonyPatch("Update")]
            [HarmonyPostfix]
            static void UpdatePatch(ref PlayerControllerB __instance)
            {
                if (writeIndex < names.Count)
                {
                    amount = Mathf.Clamp(timer / 4f, 0, 1);
                    MoveUpdate(names[writeIndex]);
                    timer += Time.deltaTime;
                    if (timer < 8f) { return; }
                    amount = 1;
                    MoveUpdate(names[writeIndex]);
                    timer = 0;
                    writeIndex++;
                }
                else
                {
                    complete = true;
                    amount = 0f;
                }
                if (!__instance.isPlayerDead)
                {
                    UIHandler.shown = false;
                }
            }

            public static void MoveUpdate(string name)
            {
                int index = -1;
                bool anyValid = false;
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
                    if (amount == 0) { paddle.GetComponent<AudioSource>().Play(); } // Sliding Sound
                    Vector3 newPos = position + new Vector3(0f, 0.166f, 0f); // Position to move to + y offset
                    paddle.transform.localPosition = Vector3.Lerp(oldPosition, newPos, (float)amount);
                    if (amount == 1f) // Move the OldPaddle to the new location if at 100%
                    {
                        board.transform.GetChild(0).GetChild(4).localPosition = newPos;
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
                    if (obj.name == "OuijaBoard(Clone)" && !((PhysicsProp)obj.GetComponent(typeof(PhysicsProp))).isHeld)
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
                PlayerPatch.amount = 0f;
                PlayerPatch.complete = false;
                PlayerPatch.writeIndex = 0;
                PlayerPatch.names = final;
                PlayerPatch.boards = boards;
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
                OuijaUI = Instantiate<GameObject>(OuijaUIPrefab);
                OuijaUI.SetActive(false);
            }
        }
    }
}
