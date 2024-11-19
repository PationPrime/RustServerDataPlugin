using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Game.Rust.Cui;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Oxide.Core.Libraries;
using System.IO;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Libraries.Covalence;
using System.Globalization;
using Oxide.Core.ServerConsole;

// ReSharper disable once CheckNamespace
namespace Oxide.Plugins
{
    public class RustItemData
    {
        [JsonProperty("id")] public string? Id { get; set; }
        [JsonProperty("image")] public string? Image { get; set; }
        [JsonProperty("displayName")] public string? DisplayName { get; set; }
        [JsonProperty("shortName")] public string? ShortName { get; set; }
        [JsonProperty("category")] public string? Category { get; set; }
    }


    public class PlayerPosition
    {
        [JsonProperty("coordinates")] public EntityPositionCoordinates? Coordinates { get; set; }
        [JsonProperty("rotation")] public EntityPositionRotation? Rotation { get; set; }
        [JsonProperty("playerId")] public string? PlayerId { get; set; }

        public PlayerPosition(
            EntityPositionCoordinates? coordinates,
            EntityPositionRotation? rotation,
            string? playerId
        )
        {
            Coordinates = coordinates;
            Rotation = rotation;
            PlayerId = playerId;
        }
    }


    public class EntityPositionCoordinates
    {
        [JsonProperty("xPos")] public double? X { get; set; }
        [JsonProperty("yPos")] public double? Y { get; set; }
        [JsonProperty("zPos")] public double? Z { get; set; }


        public EntityPositionCoordinates(
            double? x,
            double? y,
            double? z
        )
        {
            X = x;
            Y = y;
            Z = z;
        }
    }


    public class EntityPositionRotation
    {
        [JsonProperty("xPos")] public double? X { get; set; }
        [JsonProperty("yPos")] public double? Y { get; set; }
        [JsonProperty("zPos")] public double? Z { get; set; }
        [JsonProperty("wPos")] public double? W { get; set; }


        public EntityPositionRotation(
            double? x,
            double? y,
            double? z,
            double? w
        )
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }
    }


    public class MonumentData
    {
        [JsonProperty("name")] public string? Name { get; }

        [JsonProperty("type")] public string? Type { get; }


        [JsonProperty("prefab")] public string? Prefab { get; }

        [JsonProperty("coordinates")] public EntityPositionCoordinates? Coordinates { get; }


        public MonumentData(
            string? name,
            string? type,
            string? prefab,
            EntityPositionCoordinates coordinates
        )
        {
            Name = name;
            Type = type;
            Prefab = prefab;
            Coordinates = coordinates;
        }
    }

    [Info("UserDataPlugin", "Exolidity", "1.0.0")]
    public class UserDataPlugin : RustPlugin
    {
        public void Init()
        {
            GetMapPath();
        }

        public string? GetMapPath()
        {
            var pattern = "map";
            var directoryInfo = new DirectoryInfo(System.Environment.CurrentDirectory);
            var files = directoryInfo.GetFiles()
                .Where(path => path.Name.StartsWith(pattern, StringComparison.OrdinalIgnoreCase)).ToList();

            SendResponse($"files count: {files.Count()}");

            if (files.IsEmpty())
            {
                Server.Command("world.rendermap");
            }

            return files.First().FullName ?? null;
        }

        private static void SendResponse(string response)
        {
            Interface.Oxide.LogInfo(response);
        }

        private bool CheckArgs(ConsoleSystem.Arg arg)
        {
            if (!arg.HasArgs())
            {
                Puts(
                    "Command must contain parameters (in order listed): SteamID[type:String]\nCommand example: get.username 00000000000000000"
                );

                return false;
            }

            if (arg.Args[0] != null && arg.Args[0]?.Length != 0) return true;

            Puts("Command must contain a valid SteamID[type:String] as a first parameter");
            return false;
        }

        private BasePlayer? GetPlayerById(ConsoleSystem.Arg arg)
        {
            var allPlayers = BasePlayer.allPlayerList.ToList();

            try
            {
                if (!CheckArgs(arg))
                {
                    return null;
                }

                var userId = arg.Args[0];

                BasePlayer? player = Array.Find(allPlayers.ToArray(), player => player?.UserIDString == userId);

                if (player != null) return player;

                Puts($"User with id: {userId} not found");

                return null;
            }
            catch (Exception exception)
            {
                SendResponse(exception.ToString());
                throw;
            }
        }

        private List<RustItemData> ParseItems()
        {
            var jsonData = GetRustItems();
            return jsonData != null
                ? JsonConvert.DeserializeObject<List<RustItemData>>(jsonData)
                : new List<RustItemData>();
        }


        private string? GetRustItems()
        {
            string? jsonData = null;

            const float timeout = 200f;

            webrequest.Enqueue(
                "https://github.com/PationPrime/RustServerDataPlugin/blob/main/assets/rust_items.json",
                null,
                (code, response) => { jsonData = GetCallback(code, response); },
                this,
                RequestMethod.GET,
                null,
                timeout
            );


            return jsonData;
        }

        private string? GetCallback(int code, string? response)
        {
            if (response != null && code == 200) return response;
            Puts($"GetCallback Error: {code}");
            return null;
        }

        private static List<Dictionary<string, object?>> AddInventoryItems(
            List<Item>? items,
            List<RustItemData> rustItems
        )
        {
            var itemsList = new List<Dictionary<string, object?>>();

            if (items == null) return itemsList;

            foreach (Item? item in items)
            {
                var image = rustItems.Find(rustItem => rustItem.Id == item.info.itemid.ToString())?.Image;

                itemsList.Add(
                    new Dictionary<string, object?>()
                    {
                        ["id"] = item?.info.itemid.ToString(),
                        ["shortName"] = item?.info.shortname,
                        ["amount"] = item?.amount,
                        ["category"] = item?.info.category.ToString(),
                        ["occupySlots"] = item?.info.occupySlots,
                        ["displayName"] = item?.info.displayName.english,
                        ["image"] = image,
                    }
                );
            }

            return itemsList;
        }

        [ConsoleCommand("inventory.get")]
        protected void GetInventory(ConsoleSystem.Arg arg)
        {
            var rustItems = ParseItems();

            BasePlayer? player = GetPlayerById(arg);

            if (player == null)
            {
                return;
            }

            try
            {
                PlayerInventory? playerInventory = player!.inventory;
                var mainContainerItems = playerInventory.GetContainer(PlayerInventory.Type.Main).itemList;
                var wearContainerItems = playerInventory.GetContainer(PlayerInventory.Type.Wear).itemList;
                var beltContainerItems = playerInventory.GetContainer(PlayerInventory.Type.Belt).itemList;

                var mainItems = AddInventoryItems(mainContainerItems, rustItems);
                var beltItems = AddInventoryItems(beltContainerItems, rustItems);
                var wearItems = AddInventoryItems(wearContainerItems, rustItems);

                var responseData = new Dictionary<string, object>
                {
                    {
                        "items",
                        new Dictionary<string, object>
                        {
                            {
                                "mainItems", mainItems
                            },
                            {
                                "beltItems", beltItems
                            },
                            {
                                "wearItems", wearItems
                            }
                        }
                    }
                };

                var jsonResponse = JsonConvert.SerializeObject(responseData);

                SendResponse(jsonResponse);
            }
            catch (Exception exception)
            {
                Puts($"Error: {exception.Message}");
            }
        }

        [ConsoleCommand("inventory.remove.item")]
        private void RemoveItem(ConsoleSystem.Arg arg)
        {
            BasePlayer? player = GetPlayerById(arg);

            if (player == null)
            {
                return;
            }

            var itemId = arg.Args[1];
            var itemAmount = arg.Args[2];

            if (string.IsNullOrEmpty(itemId))
            {
                SendResponse(
                    $"Invalid second argument - itemId. Must be at second position and data type is String"
                );

                return;
            }

            if (string.IsNullOrEmpty(itemAmount))
            {
                SendResponse(
                    $"Invalid second argument - itemAmount. Must be at third position and data type is String"
                );

                return;
            }

            try
            {
                PlayerInventory? playerInventory = player!.inventory;

                var allItems = new List<Item>();

                playerInventory.GetAllItems(allItems);

                Item? item = allItems.Find(rustItem => rustItem.info.itemid.ToString() == itemId);

                item?.UseItem(int.Parse(itemAmount));

                SendResponse(JsonConvert.SerializeObject(
                        new Dictionary<string, object>()
                        {
                            ["success"] = true
                        }
                    )
                );
            }
            catch (Exception)
            {
                SendResponse(JsonConvert.SerializeObject(new Dictionary<string, object>()
                        {
                            ["success"] = false
                        }
                    )
                );
                throw;
            }
        }

        [ConsoleCommand("map.get.png")]
        private void GetServerMap()
        {
            var mapPath = GetMapPath();

            if (mapPath != null)
            {
                using FileStream fs = File.OpenRead(mapPath);

                using var ms = new MemoryStream(
                    2048
                );

                fs.CopyTo(ms);

                var mapToString = Convert.ToBase64String(ms.ToArray());

                SendResponse($"map data: {mapToString}");
            }
        }

        [ConsoleCommand("player.get.position")]
        private void GetPlayerPosition(ConsoleSystem.Arg arg)
        {
            BasePlayer? player = GetPlayerById(arg);
            GenericPosition? playerPosition = player?.IPlayer.Position();

            var responseData = new Dictionary<string, object?>
            {
                {
                    "position", new PlayerPosition(
                        new EntityPositionCoordinates(
                            playerPosition?.X,
                            playerPosition?.Y,
                            playerPosition?.Z
                        ),
                        new EntityPositionRotation(
                            0,
                            0,
                            0,
                            0
                        ),
                        player?.UserIDString
                    )
                }
            };

            var jsonResponse = JsonConvert.SerializeObject(responseData);

            SendResponse(jsonResponse);
        }

        [ConsoleCommand("player.get.positions")]
        private void GetPlayerPositions()
        {
            var players = BasePlayer.activePlayerList.ToList();

            var playerPositions = new List<PlayerPosition>();

            foreach (BasePlayer? player in players)
            {
                GenericPosition gamePosition = player.IPlayer.Position();
                Quaternion rotation = player.GetNetworkRotation();
                player.GetNetworkRotation().Normalize();

                playerPositions.Add(
                    new PlayerPosition(
                        new EntityPositionCoordinates(
                            gamePosition?.X,
                            gamePosition?.Y,
                            gamePosition?.Z
                        ),
                        new EntityPositionRotation(
                            rotation.x,
                            rotation.y,
                            rotation.z,
                            rotation.w
                        ),
                        player?.UserIDString
                    )
                );
            }

            var responseData = new Dictionary<string, object?>
            {
                ["positions"] = playerPositions
            };

            var jsonResponse = JsonConvert.SerializeObject(responseData);

            SendResponse(jsonResponse);
        }

        [ConsoleCommand("player.teleport.map.center")]
        private void TeleportPlayerToMapCenter(ConsoleSystem.Arg arg)
        {
            BasePlayer? player = GetPlayerById(arg);
            player?.Teleport(new Vector3(0, 120, 0));
        }

        [ConsoleCommand("world.monuments.get.all")]
        private void GetAllWorldMonuments()
        {
            var monuments = new List<MonumentData>();

            foreach (var monument in TerrainMeta.Path.Monuments)
            {
                monuments.Add(
                    new MonumentData(
                        monument.displayPhrase.english,
                        monument.Type.ToString(),
                        monument.name,
                        new EntityPositionCoordinates(
                            monument.transform.position.x,
                            monument.transform.position.y,
                            monument.transform.position.z
                        )
                    )
                );
            }

            var jsonResponse = JsonConvert.SerializeObject(monuments);

            SendResponse(jsonResponse);
        }
    }
}