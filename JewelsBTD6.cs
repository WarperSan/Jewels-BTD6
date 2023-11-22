using BTD_Mod_Helper;
using BTD_Mod_Helper.Api.ModOptions;
using BTD_Mod_Helper.Extensions;
using Il2CppAssets.Scripts;
using Il2CppAssets.Scripts.Models;
using Il2CppAssets.Scripts.Models.Towers;
using Il2CppAssets.Scripts.Simulation.Input;
using Il2CppAssets.Scripts.Simulation.Objects;
using Il2CppAssets.Scripts.Simulation.Towers;
using Il2CppAssets.Scripts.Unity;
using Il2CppAssets.Scripts.Unity.Bridge;
using Il2CppAssets.Scripts.Unity.Map;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using JewelsBTD6;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using HarmonyPatch = HarmonyLib.HarmonyPatch;
using HarmonyPostfix = HarmonyLib.HarmonyPostfix;
using Math = System.Math;
using Vector2 = Il2CppAssets.Scripts.Simulation.SMath.Vector2;

[assembly: MelonInfo(typeof(JewelsBTD6.JewelsBTD6), ModHelperData.Name, ModHelperData.Version, ModHelperData.RepoOwner)]
[assembly: MelonGame("Ninja Kiwi", "BloonsTD6")]

namespace JewelsBTD6;

public class JewelsBTD6 : BloonsTD6Mod
{

    public override void OnGameModelLoaded(GameModel model)
    {
        base.OnGameModelLoaded(model);

        Il2CppReferenceArray<TowerModel> towers = Game.instance.model.towers;

        foreach (var item in towers)
        {
            item.cost = 0;
            item.blockSelling = true;

            if (item.IsHero() || item.isPowerTower || item.isSubTower)
                continue;

            if (item.name.Contains("-"))
                continue;

            if (!item.IsBaseTower)
                continue;

            allAvailableTowers.Add(item);
        }
    }

    private static List<TowerModel> allAvailableTowers = new List<TowerModel>();
    // Block upgrades
    [HarmonyPatch(typeof(TowerManager), nameof(TowerManager.IsTowerPathTierLocked))]
    internal class TowerManager_IsTowerPathTierLocked
    {
        [HarmonyPostfix]
        internal static void Postfix(TowerManager __instance, ref bool __result)
        {
            __result = true;
        }
    }

    // Generate Board
    public static readonly ModSettingInt Size = new ModSettingInt(7)
    {
        displayName = "Board Size",
        min = 7,
        max = 13,
        slider = true,
    };

    public static Vector2Int BoardSize;

    [HarmonyPatch(typeof(Map), "Start")]
    public class MapStart
    {
        static void Prefix()
        {
            hasPlacedBoard = false;
            otherID = null;
            int size = int.Parse(Size.GetValue().ToString());

            BoardSize = new Vector2Int(size, size);
            Board = new ObjectId[size, size];
            Cost = 50;
            moveCount = 0;
            movementLength = 30 - 5 * (size - 7) / 2;


            int difficultyScaleDown = 0;

            switch (InGame.instance.SelectedDifficulty)
            {
                case "Easy":
                    difficultyScaleDown += 3;
                    break;
                case "Medium":
                    difficultyScaleDown += 2;
                    break;
                case "Hard":
                    break;
                default:
                    break;
            }

            switch (InGame.instance.SelectedTrackDifficulty)
            {
                case Il2CppAssets.Scripts.Data.MapSets.MapDifficulty.Beginner:
                    difficultyScaleDown += 3;
                    break;
                case Il2CppAssets.Scripts.Data.MapSets.MapDifficulty.Intermediate:
                    difficultyScaleDown += 2;
                    break;
                case Il2CppAssets.Scripts.Data.MapSets.MapDifficulty.Advanced:
                    difficultyScaleDown += 1;
                    break;
                case Il2CppAssets.Scripts.Data.MapSets.MapDifficulty.Expert:
                    break;
                default:
                    break;
            }

            if (difficultyScaleDown == 0)
                difficultyScaleDown = 1;

            int gemCount = allAvailableTowers.Count / difficultyScaleDown;

            List<TowerModel> tempTowers = allAvailableTowers.Duplicate();

            if (gemCount != tempTowers.Count)
            {
                for (int i = 0; i < allAvailableTowers.Count - gemCount; i++)
                {
                    tempTowers.RemoveAt(rdm.Next(0, tempTowers.Count));
                }
            }

            ModHelper.Msg<JewelsBTD6>($"{tempTowers.Count} different base towers will be used !");

            availableTowers = tempTowers;
        }
    }

    private static bool hasPlacedBoard = false;
    private static List<TowerModel> availableTowers = new List<TowerModel>();
    private static System.Random rdm = new System.Random();
    private static ObjectId[,] Board = new ObjectId[1, 1];

    public override void OnTowerCreated(Tower tower, Entity target, Model modelToUse)
    {
        base.OnTowerCreated(tower, target, modelToUse);

        Vector3 pos = tower.Position.ToUnity();
        Board[(int)(pos.x / movementLength + (BoardSize.x - 1) / 2), (int)(pos.y / movementLength + (BoardSize.y - 1) / 2)] = tower.Id;
    }

    private static void PlaceTower(int x, int y, TowerModel? tower = null)
    {
        if (tower == null)
            tower = availableTowers[rdm.Next(0, availableTowers.Count)];
        else
            availableTowers.Add(tower);

        InGame.Bridge.CreateTowerAt(new UnityEngine.Vector2(x, y) * movementLength, tower, InGame.instance.InputManager.placementTowerId, false, null, true, true);
    }

    public override void OnUpdate()
    {
        base.OnUpdate();

        if (InGame.instance == null || InGame.instance.bridge == null) return;

        if (hasPlacedBoard) return;

        if (InGame.instance.GetTowerManager() == null) return;

        try
        {
            List<TowerModel> boardGeneratingTowers = availableTowers.GetRange(0, availableTowers.Count);
            availableTowers.Clear();

            TowerManager towerManager = InGame.instance.GetTowerManager();

            for (int y = 0; y < BoardSize.y; y++)
            {
                for (int x = 0; x < BoardSize.x; x++)
                {
                    PlaceTower(x - (BoardSize.x - 1) / 2, y - (BoardSize.y - 1) / 2, boardGeneratingTowers[rdm.Next(0, boardGeneratingTowers.Count)]);
                }
            }

            ModHelper.Msg<JewelsBTD6>("Map loaded");
        }
        catch (Exception e)
        {
            ModHelper.Msg<JewelsBTD6>(e);
        }

        hasPlacedBoard = true;
    }

    private static ObjectId? otherID = null;

    [HarmonyPatch(typeof(Tower), "Hilight")]
    public class TowerHilight
    {
        static void Prefix(Tower __instance)
        {
            if (__instance.Id == otherID) return;
            else
            {
                if (otherID != null)
                {
                    SwapTowers(InGame.instance.GetTowerManager().GetTowerById((ObjectId)otherID), __instance);
                }
                otherID = __instance.Id;
            }
        }
    }

    [HarmonyPatch(typeof(Tower), "UnHighlight")]
    public class TowerUnHighlight
    {
        static void Prefix(Tower __instance)
        {
            otherID = null;
        }
    }

    public static float movementLength = 30;
    private static int Cost;
    private static uint moveCount;

    private static void SwapTowers(Tower tower1, Tower tower2)
    {
        Vector2Int tower1Coos = GetTowerCoos(tower1);
        Vector2Int tower2Coos = GetTowerCoos(tower2);

        Vector2Int difference = tower2Coos - tower1Coos;

        if (Math.Abs(difference.x) > 1 || Math.Abs(difference.y) > 1 || Math.Abs(difference.x) == Math.Abs(difference.y)) return;
        if (!InGame.Bridge.AreRoundsActive()) return;
        if (InGame.instance.bridge.GetCash() < Cost) return;

        Board[tower1Coos.x, tower1Coos.y] = tower2.Id;
        Board[tower2Coos.x, tower2Coos.y] = tower1.Id;

        Vector2 tower1Pos = tower1.Position.ToVector2();

        tower1.PositionTower(tower2.Position.ToVector2());
        tower2.PositionTower(tower1Pos);

        InGame.instance.bridge.SetCash(InGame.instance.bridge.GetCash() - Cost);

        CheckBoard(tower1);
        CheckBoard(tower2);

        moveCount++;

        if (moveCount % 10 == 0)
        {
            Cost += 50;
            ModHelper.Msg<JewelsBTD6>($"Moves now cost {Cost}$ !");
        }
    }

    private static Vector2Int GetTowerCoos(Tower tower)
    {
        Vector2Int boardPos = new Vector2Int(-1, -1);
        bool hasFound = false;
        ObjectId selectedID = tower.GetTowerToSim().id;

        for (int x = 0; x < Board.GetLength(0); x++)
        {
            for (int y = 0; y < Board.GetLength(1); y++)
            {
                if (Board[x, y] == selectedID)
                {
                    boardPos = new Vector2Int(x, y);
                    hasFound = true;
                    break;
                }
            }

            if (hasFound)
                break;
        }
        return boardPos;
    }

    private static void CheckBoard(Tower tower)
    {
        string name = tower.towerModel.name;

        List<ObjectId> foundMatches = new List<ObjectId>() { tower.Id };

        Directions dir = Directions.None;

        bool hasChosenDirection = false;
        for (int i = 0; i < foundMatches.Count; i++)
        {
            Vector2Int coos = GetTowerCoos(InGame.Bridge.GetTowerFromId(foundMatches[foundMatches.Count - 1]));
            List<KeyValuePair<ObjectId, string>> listOfPairs = GetNeighbors(coos.x, coos.y, dir).ToList();

            for (int j = 0; j < listOfPairs.Count; j++)
            {
                if (listOfPairs[j].Value == name)
                {
                    ObjectId id = listOfPairs[j].Key;

                    if (!foundMatches.Contains(id))
                    {
                        if (foundMatches.Count == 1 && !hasChosenDirection)
                        {
                            Vector2Int result = coos - GetTowerCoos(InGame.Bridge.GetTowerFromId(id));

                            dir = Directions.Horizontal;

                            if (result.x == 0)
                                dir = Directions.Vertical;

                            hasChosenDirection = true;
                            i--;
                            break;
                        }

                        foundMatches.Add(id);
                    }
                }
            }
        }

        if (foundMatches.Count <= 2) return;

        int matchCount = foundMatches.Count;

        for (int i = foundMatches.Count - 1; i > 0; i--)
        {
            Tower towerToSell = InGame.Bridge.GetTowerFromId(foundMatches[i]);
            Vector2Int coos = GetTowerCoos(towerToSell);
            Board[coos.x, coos.y] = ObjectId.Invalid;

            availableTowers.Remove(towerToSell.towerModel);
            InGame.Bridge.SellTower(foundMatches[i]);
        }

        string randomUpgrade = RandomlyUpgrade(tower);
        TowerModel newModel = Game.instance.model.GetTowerWithName(randomUpgrade);


        if (randomUpgrade != "MAX")
        {
            tower.SetTowerModel(newModel);

            availableTowers.Add(newModel);
        }
        else
        {
            // Bonus
            try
            {
                float chance = UnityEngine.Random.Range(0, 1f);

                if (chance < 0.25f)
                {
                    ModHelper.Msg<JewelsBTD6>("Cash+++");
                    InGame.instance.bridge.SetCash(InGame.instance.bridge.GetCash() + Cost * 10);
                }
                else if (chance < 0.50f)
                {
                    ModHelper.Msg<JewelsBTD6>("Bye bye Bloons !");
                    List<BloonToSimulation> bloons = InGame.instance.bridge.GetAllBloons().ToList();

                    foreach (BloonToSimulation item in bloons)
                    {
                        item.GetSimBloon().Damage(Cost / 50, null, true, false, false);
                    }
                }
                else if (chance < 0.75f)
                {
                    ModHelper.Msg<JewelsBTD6>("Lucky Fella");
                    availableTowers[rdm.Next(0, availableTowers.Count)] = newModel;
                }
                else if (chance < 0.99f)
                {
                    ModHelper.Msg<JewelsBTD6>("Healthier person");
                    InGame.instance.AddHealth(Cost / 3);
                }
                else if (chance < 0.999f)
                {
                    ModHelper.Msg<JewelsBTD6>("Oh no...");
                    InGame.instance.SpawnBloons(100);
                }
            }
            catch (Exception e)
            {
                ModHelper.Error<JewelsBTD6>(e);
            }
        }

        BoardUpdated();
    }

    private static void BoardUpdated()
    {
        try
        {
            bool hasModified = false;
            for (int x = 0; x < Board.GetLength(0); x++)
            {
                for (int y = 0; y < Board.GetLength(1); y++)
                {
                    if (Board[x, y] == ObjectId.Invalid)
                    {
                        if (y - 1 < 0)
                        {
                            Board[x, y] = ObjectId.Create(1);
                            PlaceTower(x - (BoardSize.x - 1) / 2, y - (BoardSize.y - 1) / 2);
                        }
                        else
                        {
                            Tower tower = InGame.Bridge.GetTowerFromId(Board[x, y - 1]);
                            if (tower == null)
                            {
                                Board[x, y] = ObjectId.Create(1);
                                PlaceTower(x - (BoardSize.x - 1) / 2, y - (BoardSize.y - 1) / 2);
                            }
                            else
                            {
                                Board[x, y] = Board[x, y - 1];
                                Board[x, y - 1] = ObjectId.Invalid;

                                tower.MoveTower(new Vector2(0, movementLength));
                                hasModified = true;
                            }
                        }
                    }
                }
            }

            if (hasModified)
                BoardUpdated();
        }
        catch (Exception e)
        {
            ModHelper.Error<JewelsBTD6>(e);
        }

    }

    private enum Directions
    {
        Vertical,
        Horizontal,
        None
    }

    private static Dictionary<ObjectId, string> GetNeighbors(int X, int Y, Directions direction = Directions.None)
    {
        Dictionary<ObjectId, string> neighbors = new Dictionary<ObjectId, string>();

        for (int x = -1; x < 2; x++)
        {
            for (int y = -1; y < 2; y++)
            {
                if (y == 0 && x == 0) continue;
                if (Math.Abs(x) == Math.Abs(y) && x != 0) continue;
                if (X + x < 0 || X + x >= BoardSize.x || Y + y < 0 || Y + y >= BoardSize.y) continue;
                if (direction == Directions.Horizontal && y != 0) continue;
                if (direction == Directions.Vertical && x != 0) continue;

                if (Board[X + x, Y + y] != ObjectId.Invalid)
                {
                    neighbors.Add(Board[X + x, Y + y], InGame.Bridge.GetTowerFromId(Board[X + x, Y + y]).towerModel.name);
                }
            }
        }
        return neighbors;
    }

    private static string RandomlyUpgrade(Tower tower)
    {
        string upgrade = tower.towerModel.name;

        int[] tiers = tower.towerModel.tiers.ToArray();
        List<int> tierAvailable = new List<int>() { 0, 1, 2 };

        int tierCount = 0;
        for (int i = 0; i < tiers.Length; i++)
        {
            tierCount += tiers[i];
        }

        if (tierCount >= 7) return "MAX";

        // Check for T5
        int[] t = Enumerable.Range(0, tiers.Length).Where(i => tiers[i] >= 5).ToArray();

        if (t.Length != 0)
        {
            tierAvailable.Remove(t[0]);
        }

        // Crosspath check
        t = Enumerable.Range(0, tiers.Length).Where(i => tiers[i] == 0).ToArray();
        if (t.Length == 1)
        {
            tierAvailable.Remove(t[0]);
        }

        tiers[tierAvailable[rdm.Next(0, tierAvailable.Count)]]++;

        return tower.towerModel.baseId + "-" + tiers[0] + tiers[1] + tiers[2];
    }
}