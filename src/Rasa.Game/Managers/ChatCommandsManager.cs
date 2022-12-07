﻿using System;
using System.Collections.Generic;
using System.Numerics;

namespace Rasa.Managers
{
    using Data;
    using Database.Tables.World;
    using Game;
    using Memory;
    using Packets.Game.Server;
    using Packets.MapChannel.Server;
    using Structures;

    public class ChatCommandsManager
    {
        private static ChatCommandsManager _instance;
        private static readonly object InstanceLock = new object();
        private static readonly Dictionary<string, Action<string[]>> Commands = new Dictionary<string, Action<string[]>>();
        private static Client _client { get; set; }
        public static ChatCommandsManager Instance
        {
            get
            {
                // ReSharper disable once InvertIf
                if (_instance == null)
                {
                    lock (InstanceLock)
                    {
                        if (_instance == null)
                            _instance = new ChatCommandsManager();
                    }
                }

                return _instance;
            }
        }

        private ChatCommandsManager()
        {
        }

        public void ProcessCommand(Client client, string command)
        {
            _client = client;
            if (string.IsNullOrWhiteSpace(command))
                return;

            var parts = command.Split(' ');

            if (Commands.ContainsKey(parts[0]))
            {
                Commands[parts[0]](parts);
                return;
            }

            Logger.WriteLog(LogType.Command, $"Invalid command: {command}");
        }

        public void RegisterCommand(string name, Action<string[]> handler)
        {
            Commands.Add(name, handler);
        }

        public void RemoveCommand(string name)
        {
            if (Commands.ContainsKey(name))
                Commands.Remove(name);
        }

        public void RegisterChatCommands()
        {
            RegisterCommand(".addtitle", AddTitleCommand);
            RegisterCommand(".actorstate", ActorStateCommand);
            RegisterCommand(".bark", BarkCommand);
            RegisterCommand(".comehere", ComeHereCommand);
            RegisterCommand(".createobj", CreateObjectCommand);
            RegisterCommand(".creature", CreateCreatureCommand);
            RegisterCommand(".creatureappearance", SetCreatureAppearanceCommand);
            RegisterCommand(".creatureloc", SetCreatureLocation);
            RegisterCommand(".getdistance", GetDistanceCommand);
            RegisterCommand(".giveitem", GiveItemCommand);
            RegisterCommand(".givelogos", GiveLogosCommand);
            RegisterCommand(".givexp", GiveXpCommand);
            RegisterCommand(".gm", EnterGmModCommand);
            RegisterCommand(".forcestate", ForceStateCommand);
            RegisterCommand(".help", HelpGmCommand);
            RegisterCommand(".npcinfo", NpcInfoCommand);
            RegisterCommand(".reloadcreatures", ReloadCreaturesCommand);
            RegisterCommand(".removeobj", RemoveObjectCommand);
            RegisterCommand(".rqs", RqsWindowCommand);
            RegisterCommand(".tele", TeleCommand);
            RegisterCommand(".teleport", TeleportCommand);
            RegisterCommand(".teleup", TeleUpCommand);
            RegisterCommand(".setkillstreak", SetKillStreakCommand);
            RegisterCommand(".setregion", SetRegionCommand);
            RegisterCommand(".speed", SpeedCommand);
            RegisterCommand(".where", WhereCommand);
        }

        #region RegularUser

        #endregion

        #region GM

        private void AddTitleCommand(string[] parts)
        {
            if (parts.Length == 1)
            {
                CommunicatorManager.Instance.SystemMessage(_client, "usage: .addtitle titleId");
                return;
            }

            if (parts.Length == 2)
            {
                if (uint.TryParse(parts[1], out var titleId))
                {
                    _client.CallMethod(_client.MapClient.Player.Actor.EntityId, new TitleAddedPacket(titleId));
                }
            }
        }

        private void ActorStateCommand(string[] parts)
        {
            if (parts.Length == 1)
            {
                CommunicatorManager.Instance.SystemMessage(_client, "usage: .actorstate stateId speed");
                return;
            }

            if (parts.Length == 3)
            {
                if (Enum.TryParse(parts[1], out CharacterState stateId))
                    if (double.TryParse(parts[2], out var speed))
                    {
                        _client.CallMethod(_client.MapClient.Player.Actor.EntityId, new ActorInfoPacket(stateId, _client.MapClient.Player.TrackingTargetEntityId, _client.MovementData.ViewX, speed, _client.MapClient.Player.Actor.InCombatMode));
                    }
            }
        }

        private void BarkCommand(string[] parts)
        {
            if (parts.Length == 1)
            {
                CommunicatorManager.Instance.SystemMessage(_client, "usage: .bark creatureEntityId barkId");
                return;
            }

            if (parts.Length == 3)
                if (ulong.TryParse(parts[1], out var creatureEntityId))
                    if (uint.TryParse(parts[2], out var barkId))
                        _client.CallMethod(creatureEntityId, new BarkPackage(barkId));
        }

        private void ComeHereCommand(string[] parts)
        {
            if (parts.Length == 1)
            {
                CommunicatorManager.Instance.SystemMessage(_client, "usage: .comehere creatureEntityId");
                return;
            }

            if (parts.Length == 2)
                if (ulong.TryParse(parts[1], out var entityId))
                {
                    var test = new Memory.MovementData(
                        _client.MovementData.PosX,
                        _client.MovementData.PosY,
                        _client.MovementData.PosZ,
                        _client.MovementData.ViewX)
                    {
                        Velocity = 6.5f
                    };

                    _client.MoveObject(entityId, test);
                }
        }

        private void CreateCreatureCommand(string[] parts)
        {
            if (parts.Length == 1)
            {
                CommunicatorManager.Instance.SystemMessage(_client, "usage: .creature dbId");
                return;
            }

            if (parts.Length == 2)
            {
                if (uint.TryParse(parts[1], out uint dbId))
                {
                    var creature = CreatureManager.Instance.CreateCreature(dbId, null);

                    if (creature != null)
                    {
                        CreatureManager.Instance.SetLocation(creature, new Vector3(_client.MovementData.PosX, _client.MovementData.PosY, _client.MovementData.PosZ), _client.MovementData.ViewX, _client.MapClient.Player.Actor.MapContextId);
                        CellManager.Instance.AddToWorld(_client.MapClient.MapChannel, creature);
                        CommunicatorManager.Instance.SystemMessage(_client, $"Created new creature with EntityId {creature.Actor.EntityId}");
                    }
                    else
                        CommunicatorManager.Instance.SystemMessage(_client, $"Creature with dbId={dbId} isn't in database");
                }
            }

            return;
        }

        private void CreateObjectCommand(string[] parts)
        {
            if (parts.Length == 1)
            {
                CommunicatorManager.Instance.SystemMessage(_client, "usage: .createobj entityClassId");
                return;
            }
            if (parts.Length == 2)
            {
                if (Enum.TryParse(parts[1], out EntityClassId entityClassId))
                {
                    var newObject = new DynamicObject
                    {
                        Position = new Vector3(_client.MovementData.PosX, _client.MovementData.PosY, _client.MovementData.PosZ),
                        Orientation = _client.MovementData.ViewX,
                        MapContextId = _client.MapClient.Player.Actor.MapContextId,
                        EntityClassId = entityClassId
                    };

                    CellManager.Instance.AddToWorld(_client.MapClient.MapChannel, newObject);
                    CommunicatorManager.Instance.SystemMessage(_client, $"Created object EntityId = {newObject.EntityId}");
                }
            }
            return;
        }

        private void EnterGmModCommand(string[] parts)
        {
            _client.CallMethod(SysEntity.ClientMethodId, new SetIsGMPacket(true));
            CommunicatorManager.Instance.SystemMessage(_client, "GM Mode enabled!");
            return;
        }

        private void ForceStateCommand(string[] parts)
        {
            if (parts.Length == 1)
            {
                CommunicatorManager.Instance.SystemMessage(_client, "usage: .forcestate entityId stateId");
                return;
            }
            // if only item template, give max stack size
            if (parts.Length == 3)
                if (ulong.TryParse(parts[1], out var entityId))
                    if (Enum.TryParse(parts[2], out UseObjectState state))
                        _client.CallMethod(entityId, new ForceStatePacket(state, 100));

            return;
        }

        private void GetDistanceCommand(string[] parts)
        {
            if (parts.Length == 1)
            {
                var msg = "Distance between you and target ";

                var entityId = _client.MapClient.Player.TargetEntityId;

                if (entityId == 0)
                {
                    CommunicatorManager.Instance.SystemMessage(_client, "Please select target to use .getdistance command");
                    return;
                }

                var entityType = EntityManager.Instance.GetEntityType(entityId);

                if (entityType == EntityType.Creature)
                    msg += $"\nEntityId = {entityId} is {Vector3.Distance(new Vector3(_client.MovementData.PosX, _client.MovementData.PosY, _client.MovementData.PosZ), EntityManager.Instance.GetCreature(entityId).SpawnPool.HomePosition)}\n";

                if (entityType == EntityType.Player)
                    msg += $"\nEntityId = {entityId} is {Vector3.Distance(new Vector3(_client.MovementData.PosX, _client.MovementData.PosY, _client.MovementData.PosZ), EntityManager.Instance.GetActor(entityId).Position)}\n";

                if (entityType == EntityType.Object)
                    msg = $"EntityId = {entityId} is object, ToDo\n";

                CommunicatorManager.Instance.SystemMessage(_client, msg);

                return;
            }
        }
        
        private void GiveItemCommand(string[] parts)
        {
            if (parts.Length == 1)
            {
                CommunicatorManager.Instance.SystemMessage(_client, "usage: .giveitem itemTemplateId quantity");
                return;
            }
            // if only item template, give max stack size
            if (parts.Length == 2)
                if (uint.TryParse(parts[1], out uint itemTemplateId))
                {
                    var classInfo = EntityClassManager.Instance.GetClassInfo(ItemManager.Instance.ItemTemplateItemClass[itemTemplateId]);
                    var item = ItemManager.Instance.CreateFromTemplateId(itemTemplateId, classInfo.ItemClassInfo.StackSize, _client.MapClient.Player.Actor.FamilyName);
                    item.CrafterName = _client.MapClient.Player.Actor.FamilyName;
                    InventoryManager.Instance.AddItemToInventory(_client, item);
                }
            if (parts.Length == 3)
                if (uint.TryParse(parts[1], out uint itemTemplateId))
                    if (uint.TryParse(parts[2], out uint quantity))
                    {
                        var item = ItemManager.Instance.CreateFromTemplateId(itemTemplateId, quantity, _client.MapClient.Player.Actor.FamilyName);
                        item.CrafterName = _client.MapClient.Player.Actor.FamilyName;
                        InventoryManager.Instance.AddItemToInventory(_client, item);
                    }

            return;
        }

        private void GiveLogosCommand(string[] parts)
        {
            if (parts.Length == 1)
            {
                CommunicatorManager.Instance.SystemMessage(_client, "usage: .givelogos logosId");
                return;
            }
            if (parts.Length == 2)
                if (int.TryParse(parts[1], out int logosId))
                    CharacterManager.Instance.UpdateCharacter(_client, CharacterUpdate.Logos, logosId);

            return;
        }

        private void GiveXpCommand(string[] parts)
        {
            if (parts.Length == 2)
            {
                if (int.TryParse(parts[1], out int xp))
                    ManifestationManager.Instance.GainExperience(_client, xp);
            }
            else
                CommunicatorManager.Instance.SystemMessage(_client, "usage: .givexp ammount");

            return;
        }

        private void HelpGmCommand(string[] parts)
        {
            CommunicatorManager.Instance.SystemMessage(_client, "GM Commands List:");
            foreach (var command in Commands)
                CommunicatorManager.Instance.SystemMessage(_client, $"{command.Key}");

            return;
        }
        
        private void NpcInfoCommand(string[] parts)
        {
            if (parts.Length == 1)
            {
                var msg = "target = (\n";

                var entityId = _client.MapClient.Player.TargetEntityId;

                if (entityId == 0)
                {
                    CommunicatorManager.Instance.SystemMessage(_client, "Please select target to use .npcinfo command");
                    return;
                }

                var entityType = EntityManager.Instance.GetEntityType(entityId);

                if (entityId != 0)
                    msg += $"EntityId = {entityId}\nEntityType = {entityType}\n";

                if (entityType == EntityType.Creature)
                {
                    var creature = EntityManager.Instance.GetCreature(entityId);

                    msg += $"CreatureDbId = {creature.DbId}\n";

                    if (creature.SpawnPool != null)
                        msg += $"SpawnPoolDbId = {creature.SpawnPool.DbId}\n";

                    msg += $"PosX = {creature.Actor.Position.X}\n";
                    msg += $"PosY = {creature.Actor.Position.Y}\n";
                    msg += $"PosZ = {creature.Actor.Position.Z}\n";
                }

                msg += ")\n";

                Logger.WriteLog(LogType.Debug, msg);
                CommunicatorManager.Instance.SystemMessage(_client, msg);

                return;
            }
        }

        private void ReloadCreaturesCommand(string[] obj)
        {
            CreatureManager.Instance.LoadedCreatures.Clear();
            CreatureManager.Instance.CreatureActions.Clear();
            CreatureManager.Instance.CreatureInit();
            CommunicatorManager.Instance.SystemMessage(_client, "Creatures Reloaded.");
        }

        private void RemoveObjectCommand(string[] parts)
        {
            if (parts.Length == 1)
            {
                CommunicatorManager.Instance.SystemMessage(_client, "usage: .removeobj entityId");
                return;
            }
            if (parts.Length == 2)
            {
                if (ulong.TryParse(parts[1], out var entityId))
                {
                    CellManager.Instance.RemoveFromWorld(_client.MapClient.MapChannel, entityId);
                    CommunicatorManager.Instance.SystemMessage(_client, $"Removed object EntityId = {entityId}");
                }
            }
            return;
        }

        private void RqsWindowCommand(string[] parts)
        {
            if (parts.Length == 1)
            {
                _client.CallMethod(SysEntity.ClientMethodId, new DevRQSWindowPacket());
                return;
            }
        }

        private void SetKillStreakCommand(string[] parts)
        {
            if (parts.Length == 1)
            {
                CommunicatorManager.Instance.SystemMessage(_client, "usage: .setkillstreak streakCount");
                return;
            }
            if (parts.Length == 2)
                if (int.TryParse(parts[1], out int count))
                    _client.CallMethod(SysEntity.ClientMethodId, new SetKillStreakPacket(count));

            return;
        }
        
        private void TeleCommand(string[] parts)
        {
            if (parts.Length != 4)
            {
                CommunicatorManager.Instance.SystemMessage(_client, "usage: .tele posX posY posZ");
                return;
            }

            if (float.TryParse(parts[1], out float posX))
                if (float.TryParse(parts[2], out float posY))
                    if (float.TryParse(parts[3], out float posZ))
                        _client.MoveObject(_client.MapClient.Player.Actor.EntityId, new MovementData(posX, posY, posZ, 0));
        }

        private void TeleportCommand(string[] parts)
        {
            if (parts.Length == 1)
            {
                CommunicatorManager.Instance.SystemMessage(_client, "usage: .teleport posX posY posZ mapId");
                return;
            }
            if (parts.Length == 5)
            {
                if (float.TryParse(parts[1], out float posX))
                    if (float.TryParse(parts[2], out float posY))
                        if (float.TryParse(parts[3], out float posZ))
                            if (uint.TryParse(parts[4], out uint mapId))
                            {
                                // init loading screen
                                _client.CallMethod(SysEntity.ClientMethodId, new PreWonkavatePacket());
                                _client.State = ClientState.Loading;
                                // Remove player
                                MapChannelManager.Instance.RemovePlayer(_client, false);
                                // send Wonkavate
                                var mapChannel = MapChannelManager.Instance.MapChannelArray[mapId];
                                _client.LoadingMap = mapId;

                                var packet = new WonkavatePacket(
                                    mapChannel.MapInfo.MapContextId,
                                    0,                  // ToDo MapInstanceId
                                    mapChannel.MapInfo.MapVersion,
                                    new Vector3(posX, posY, posZ),
                                    _client.MovementData.ViewX
                                    );

                                _client.CallMethod(SysEntity.CurrentInputStateId, packet);
                                // Update Db, this position will be loaded in MapLoadedPacket
                                CharacterManager.Instance.UpdateCharacter(_client, CharacterUpdate.Position, packet);
                                mapChannel.ClientList.Add(_client);
                            }

            }

            return;
        }

        private void TeleUpCommand(string[] parts)
        {
            if (parts.Length != 2)
            {
                CommunicatorManager.Instance.SystemMessage(_client, "usage: .teleup posY");
                return;
            }

            if (float.TryParse(parts[1], out float posY))
                _client.MoveObject(_client.MapClient.Player.Actor.EntityId, new MovementData(_client.MapClient.Player.Actor.Position.X, posY, _client.MapClient.Player.Actor.Position.Z, 0));
        }

        private void SetCreatureAppearanceCommand(string[] parts)
        {
            if (parts.Length == 1)
            {
                CommunicatorManager.Instance.SystemMessage(_client, "usage: .creatureappearance creatureEntityId slotId classId color");
                return;
            }
            if (parts.Length == 5)
            {
                if (ulong.TryParse(parts[1], out var entityId))
                    if (Enum.TryParse(parts[2], out EquipmentData slotId))
                        if (Enum.TryParse(parts[3], out EntityClassId classId))
                            if (uint.TryParse(parts[4], out uint color))
                            {
                                // get creature from entityId
                                var creature = EntityManager.Instance.GetCreature(entityId);
                                var newAppearance = false;
                                if (!creature.AppearanceData.ContainsKey(slotId))
                                {
                                    // new appearance
                                    creature.AppearanceData.Add(slotId, new AppearanceData { SlotId = slotId, Class = classId, Color = new Color(color), Hue2 = new Color(2139062144) });
                                    newAppearance = true;
                                }
                                if (classId < 0) // update only color of slot item
                                {
                                    creature.AppearanceData[slotId].Color = new Color(color);
                                    _client.CellCallMethod(_client, entityId, new AppearanceDataPacket(creature.AppearanceData));
                                }
                                else
                                {
                                    creature.AppearanceData[slotId].Class = classId;
                                    creature.AppearanceData[slotId].Color = new Color(color);
                                }

                                // update creature appearance on client's
                                _client.CellCallMethod(_client, entityId, new AppearanceDataPacket(creature.AppearanceData));

                                // update creature in database
                                if (newAppearance)
                                    CreatureAppearanceTable.SetCreatureAppearance(creature.DbId, (uint)slotId, (uint)creature.AppearanceData[slotId].Class, creature.AppearanceData[slotId].Color.Hue);
                                else
                                    CreatureAppearanceTable.UpdateCreatureAppearance(creature.DbId, (uint)slotId, (uint)creature.AppearanceData[slotId].Class, creature.AppearanceData[slotId].Color.Hue);

                                Logger.WriteLog(LogType.Debug, "Creature Look updated");
                            }
            }

            return;
        }

        private void SetCreatureLocation(string[] parts)
        {
            /*if (parts.Length == 1)
            {
                CommunicatorManager.Instance.SystemMessage(_client.MapClient, "usage: .creatureloc entityClassId, posX, posY, posZ");
                return;
            }
            if (parts.Length == 5)
            {
                double posX, posY, posZ;
                int entityClassId;
                if (int.TryParse(parts[1], out entityClassId))
                    if (double.TryParse(parts[2], out posX))
                        if (double.TryParse(parts[3], out posY))
                            if (double.TryParse(parts[4], out posZ))
                            {
                                var creatureType = new CreatureType();
                                var position = new Position { PosX = posX, PosY = posY, PosZ = posZ };
                                
                                creatureType.NameId = 0;
                                creatureType.Name = "test Npc";

                                var creature = CreatureManager.Instance.CreateCreature(creatureType, entityClassId, _client.MapClient.Player.AppearanceData, null);
                                CreatureManager.Instance.SetLocation(creature, position, _client.MapClient.Player.Actor.Rotation);
                                CellManager.Instance.AddToWorld(_client.MapClient.MapChannel, creature);
                                CommunicatorManager.Instance.SystemMessage(_client.MapClient, $"Created new creature with EntityId {creature.Actor.EntityId}");
                            }
            }
            return;*/
        }

        private void SetRegionCommand(string[] parts)
        {
            if (parts.Length == 1)
            {
                CommunicatorManager.Instance.SystemMessage(_client, "usage: .setregion regionId");
                return;
            }
            if (parts.Length == 2)
            {
                if (int.TryParse(parts[1], out int regionId))
                    _client.CallMethod(_client.MapClient.Player.Actor.EntityId, new UpdateRegionsPacket { RegionIdList = regionId });
            }
            return;
        }

        private void SpeedCommand(string[] parts)
        {
            if (parts.Length == 1)
            {
                CommunicatorManager.Instance.SystemMessage(_client, "usage: .speed value");
                return;
            }
            if (parts.Length == 2)
            {
                if (double.TryParse(parts[1], out double speed))
                    // ToDO send on cell domain
                    _client.CallMethod(_client.MapClient.Player.Actor.EntityId, new MovementModChangePacket(speed));
            }
            return;
        }

        private void WhereCommand(string[] parts)
        {
            CommunicatorManager.Instance.SystemMessage(_client, $"PosX = {_client.MovementData.PosX}\nPosY = "
                +$"{_client.MovementData.PosY}\nPosZ = {_client.MovementData.PosZ}\nOrientation = {_client.MovementData.ViewX}"
                +$"\nMapId = {_client.MapClient.Player.Actor.MapContextId}");
            return;
        }
        
        #endregion
    }
}
