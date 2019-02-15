﻿using System;
using System.Collections.Generic;
using System.Numerics;

namespace Rasa.Managers
{
    using Data;
    using Packets.MapChannel.Server;
    using Timer;
    using Structures;
    using Rasa.Game;
    using Rasa.Packets.MapChannel.Client;

    public class MissileManager
    {
        private static MissileManager _instance;
        private static readonly object InstanceLock = new object();
        public readonly Timer Timer = new Timer();

        public static MissileManager Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                lock (InstanceLock)
                {
                    if (_instance == null)
                        _instance = new MissileManager();
                }

                return _instance;
            }
        }

        private MissileManager()
        {
        }

        private void DoDamageToCreature(MapChannel mapChannel, Missile missile)
        {
            var creature = EntityManager.Instance.GetCreature(missile.TargetEntityId);

            if (creature.Actor.State == ActorState.Dead)
                return;

            // decrease armor first
            var armorDecrease = (int)Math.Min(missile.DamageA, creature.Actor.Attributes[Attributes.Armor].Current);

            creature.Actor.Attributes[Attributes.Armor].Current -= armorDecrease;

            // decrease health (if armor is depleted)
            creature.Actor.Attributes[Attributes.Health].Current -= (int)(missile.DamageA - armorDecrease);

            if (creature.Actor.Attributes[Attributes.Health].Current <= 0)
            {
                // fix health so it dont regenerate after death
                missile.TargetActor.Attributes[Attributes.Health].Current = 0;
                missile.TargetActor.Attributes[Attributes.Health].RefreshAmount = 0;
                missile.TargetActor.Attributes[Attributes.Health].RefreshPeriod = 0;

                // fix armor so it dont regenerate after death
                missile.TargetActor.Attributes[Attributes.Armor].Current = 0;
                missile.TargetActor.Attributes[Attributes.Armor].RefreshAmount = 0;
                missile.TargetActor.Attributes[Attributes.Armor].RefreshPeriod = 0;
                // kill craeture
                CreatureManager.Instance.HandleCreatureKill(mapChannel, creature, missile.Source);
            }
            else
            {
                // shooting at wandering creatures makes them ANGRY
                if (creature.Controller.CurrentAction == BehaviorManager.BehaviorActionWander || creature.Controller.CurrentAction == BehaviorManager.BehaviorActionFollowingPath)
                    BehaviorManager.Instance.SetActionFighting(creature, missile.Source.EntityId);
            }

            // update health
            CellManager.Instance.CellCallMethod(mapChannel, creature.Actor, new UpdateHealthPacket(creature.Actor.Attributes[Attributes.Health], creature.Actor.EntityId));

            // update armor
            CellManager.Instance.CellCallMethod(mapChannel, creature.Actor, new UpdateArmorPacket(creature.Actor.Attributes[Attributes.Armor], creature.Actor.EntityId));
        }

        private void DoDamageToPlayer()
        {
            // ToDo
        }

        public void RequestWeaponAttack(Client client, RequestWeaponAttackPacket packet)
        {
            ManifestationManager.Instance.PlayerTryFireWeapon(client);

                /*

                var weapon = InventoryManager.Instance.CurrentWeapon(client);

                if (weapon == null)
                {
                    Logger.WriteLog(LogType.Error, "no weapon armed but player tries to shoot");
                    return;
                }

                var weaponClassInfo = EntityClassManager.Instance.GetWeaponClassInfo(weapon);
                var targetType = EntityManager.Instance.GetEntityType((uint)packet.TargetId);
                var target = new Actor();

                switch (targetType)
                {
                    case EntityType.Creature:
                        target = EntityManager.Instance.GetCreature((uint)packet.TargetId).Actor;
                        break;
                    default:
                        Logger.WriteLog(LogType.Error, $"RequestWeaponAttack:\nUnsuported targetType = {targetType}");
                        return; ;
                }

                var distance = Vector3.Distance(client.MapClient.Player.Actor.Position, target.Position);
                var triggerTime = (int)Math.Round(distance, 0);

                var missile = new Missile
                {
                    ActionId = packet.ActionId,
                    ActionArgId = packet.ActionArgId,
                    TargetEntityId = (uint)packet.TargetId,
                    DamageA = weaponClassInfo.DamageType,
                    IsAbility = false,
                    Source = client,
                    TriggerTime = triggerTime
                };


                missile.TriggerTime = triggerTime;


                QueuedMissiles.Add(missile);
                */
        }

        public void DoWork(MapChannel mapChannel, long delta)
        {
            // ToDo: add check for triggerMissile timer
            foreach (var missile in mapChannel.QueuedMissiles)
                MissileTrigger(mapChannel, missile);

            // empty List
            mapChannel.QueuedMissiles.Clear();
        }

        public void MissileLaunch(MapChannel mapChannel, Actor origin, uint targetEntityId, long damage, ActionId actionId, uint actionArgId)
        {
            var missile = new Missile
            {
                DamageA = damage,
                Source = origin
            };

            // get distance between actors
            Actor targetActor = null;
            var triggerTime = 0; // time between windup and recovery

            if (targetEntityId != 0)
            {
                // target on entity
                var targetType = EntityManager.Instance.GetEntityType(targetEntityId);

                if (targetType == 0)
                {
                    Logger.WriteLog(LogType.Error, $"The missile target doesnt exist: {targetEntityId}");
                    // entity does not exist
                    return;
                }
                switch (targetType)
                {
                    case EntityType.Creature:
                        {
                            var creature = EntityManager.Instance.GetCreature(targetEntityId);
                            targetActor = creature.Actor;
                            missile.TargetEntityId = targetEntityId;
                        }
                        break;
                    case EntityType.Player:
                        {
                            var player = EntityManager.Instance.GetPlayer(targetEntityId);
                            targetActor = player.Player.Actor;
                            missile.TargetEntityId = targetEntityId;
                        }
                        break;
                    default:
                        Logger.WriteLog(LogType.Error, $"Can't shoot that object");
                        return;
                };

                if (targetActor.State == ActorState.Dead)
                    return; // actor is dead, cannot be shot at

                var distance = Vector3.Distance(targetActor.Position, origin.Position);
                triggerTime = (int)(distance * 0.5f);
            }
            else
            {
                // has no target -> Shoot towards looking angle
                targetActor = null;
                triggerTime = 0;
            }

            // is the missile/action an ability that need needs to use Recv_PerformAbility?
            var isAbility = false;
            if (actionId == ActionId.AaRecruitLightning) // recruit lighting ability
                isAbility = true;
            
            missile.TargetActor = targetActor;
            missile.TriggerTime = triggerTime;
            missile.ActionId = actionId;
            missile.ActionArgId = actionArgId;
            missile.IsAbility = isAbility;

            // send windup and append to queue (only for non-abilities)
            if (isAbility == false)
            {
                CellManager.Instance.CellCallMethod(mapChannel, origin, new PerformWindupPacket(PerformType.ThreeArgs, missile.ActionId, missile.ActionArgId, missile.TargetEntityId));
                
                // add to list
                mapChannel.QueuedMissiles.Add(missile);
            }
            else
            {
                // abilities get applied directly without delay
                MissileTrigger(mapChannel, missile);
            }
        }

        public void MissileTrigger(MapChannel mapChannel, Missile missile)
        {
            switch (missile.ActionId)
            {
                case ActionId.WeaponAttack:
                    WeaponAttack(mapChannel, missile);
                    break;
                //else if (missile->actionId == 174)
                //    missile_ActionRecoveryHandler_WeaponMelee(mapChannel, missile);
                //else if (missile->actionId == 194)
                //    missile_ActionHandler_Lighting(mapChannel, missile);
                //else if (missile->actionId == 203)
                //    missile_ActionHandler_CR_FOREAN_LIGHTNING(mapChannel, missile);
                //else if (missile->actionId == 211)
                //    missile_ActionHandler_CR_AMOEBOID_SLIME(mapChannel, missile);
                //else if (missile->actionId == 397)
                //    missile_ActionRecoveryHandler_ThraxKick(mapChannel, missile);
                default:
                    Logger.WriteLog(LogType.Debug, $"MissileLaunch: unsupported missile actionId {missile.ActionId} - using default: WeaponAttack");
                    WeaponAttack(mapChannel, missile);
                    break;
            }
        }

        #region MissileHandler
        private void WeaponAttack(MapChannel mapChannel, Missile missile)
        {
            // ToDo: Some weapons can hit multiple targets
            var targetType = EntityManager.Instance.GetEntityType(missile.TargetEntityId);
            var missileArgs = new MissileArgs();
            var hitData = new HitData
            {
                DamageType = DamageType.Physical,
                FinalAmt = missile.DamageA
            };

            missileArgs.HitEntities.Add(missile.TargetEntityId);
            missileArgs.HitData = new List<HitData> { hitData };     // ToDo: add suport for multiple targets

            /* Execute action */
            CellManager.Instance.CellCallMethod(mapChannel, missile.Source, new PerformRecoveryPacket(PerformType.ListOfArgs, missile.ActionId, missile.ActionArgId, missileArgs));

            switch (targetType)
            {
                case 0:
                    // no target => ToDo
                    break;
                case EntityType.Creature:
                    DoDamageToCreature(mapChannel, missile);
                    break;
                case EntityType.Player:
                    DoDamageToPlayer();
                    break;
                default:
                    Logger.WriteLog(LogType.Error, $"WeaponAttack: Unsuported targetType {targetType}.");
                    break;
            }
        }
        #endregion
    }
}
