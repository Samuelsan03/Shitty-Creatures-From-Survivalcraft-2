using System;
using System.Runtime.CompilerServices;
using Engine;
using Engine.Graphics;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
    public class ComponentNewChaseBehavior : ComponentBehavior, IUpdateable
    {
        // Propiedades existentes
        public ComponentCreature Target
        {
            get { return this.m_target; }
        }
        
        public UpdateOrder UpdateOrder
        {
            get { return UpdateOrder.Default; }
        }
        
        public override float ImportanceLevel
        {
            get { return this.m_importanceLevel; }
        }

        // Variables de memoria para bandidos
        private double m_nextBanditCheckTime = 0.0;
        private class BanditMemory
        {
            public ComponentCreature Creature;
            public double LastSeenTime;
            public bool IsThreatening;
        }
        private DynamicArray<BanditMemory> m_banditMemory = new DynamicArray<BanditMemory>();
        private const float BanditMemoryDuration = 30f;

        // Variables para combate a distancia
        private double m_lastActionTime;
        private FlameBulletBlock.FlameBulletType m_lastFlameBulletType = FlameBulletBlock.FlameBulletType.Flame;

        // Método Attack mejorado
        public virtual void Attack(ComponentCreature componentCreature, float maxRange, float maxChaseTime, bool isPersistent)
        {
            if (this.Suppressed) return;

            ComponentNewHerdBehavior componentNewHerdBehavior = base.Entity.FindComponent<ComponentNewHerdBehavior>();
            
            if (componentNewHerdBehavior != null)
            {
                if (!componentNewHerdBehavior.CanAttackCreature(componentCreature)) return;
                
                if (this.m_autoDismount)
                {
                    ComponentRider componentRider = base.Entity.FindComponent<ComponentRider>();
                    if (componentRider != null) componentRider.StartDismounting();
                }

                // Protección especial contra bandidos
                if (IsGuardianOrPlayerAlly() && IsBandit(componentCreature))
                {
                    maxRange *= 1.3f;
                    maxChaseTime *= 1.5f;
                    isPersistent = true;
                    
                    if (this.m_importanceLevel < 280f)
                        this.m_importanceLevel = 280f;
                }

                this.m_target = componentCreature;
                this.m_nextUpdateTime = 0.0;
                this.m_range = maxRange;
                this.m_chaseTime = maxChaseTime;
                this.m_isPersistent = isPersistent;
                this.m_importanceLevel = (isPersistent ? this.ImportanceLevelPersistent : this.ImportanceLevelNonPersistent);
                this.IsActive = true;
                this.m_stateMachine.TransitionTo("Chasing");
                
                if (this.m_target != null && this.m_componentPathfinding != null)
                {
                    this.m_componentPathfinding.Stop();
                    this.UpdateChasingStateImmediately();
                }
            }
            else
            {
                ComponentHerdBehavior componentHerdBehavior = base.Entity.FindComponent<ComponentHerdBehavior>();
                if (componentHerdBehavior != null)
                {
                    ComponentHerdBehavior targetHerd = componentCreature.Entity.FindComponent<ComponentHerdBehavior>();
                    if (targetHerd != null && !string.IsNullOrEmpty(targetHerd.HerdName) && !string.IsNullOrEmpty(componentHerdBehavior.HerdName))
                    {
                        bool isSameHerd = targetHerd.HerdName == componentHerdBehavior.HerdName;
                        bool isPlayerAlly = false;
                        
                        if (componentHerdBehavior.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase))
                        {
                            if (targetHerd.HerdName.ToLower().Contains("guardian"))
                                isPlayerAlly = true;
                        }
                        else if (componentHerdBehavior.HerdName.ToLower().Contains("guardian"))
                        {
                            if (targetHerd.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase))
                                isPlayerAlly = true;
                        }

                        if (isSameHerd || isPlayerAlly) return;
                    }

                    if (IsGuardianOrPlayerAlly() && IsBandit(componentCreature))
                    {
                        maxRange *= 1.3f;
                        maxChaseTime *= 1.5f;
                        isPersistent = true;
                        
                        if (this.m_importanceLevel < 280f)
                            this.m_importanceLevel = 280f;
                    }

                    if (this.m_autoDismount)
                    {
                        ComponentRider componentRider = base.Entity.FindComponent<ComponentRider>();
                        if (componentRider != null) componentRider.StartDismounting();
                    }
                    
                    this.m_target = componentCreature;
                    this.m_nextUpdateTime = 0.0;
                    this.m_range = maxRange;
                    this.m_chaseTime = maxChaseTime;
                    this.m_isPersistent = isPersistent;
                    this.m_importanceLevel = (isPersistent ? this.ImportanceLevelPersistent : this.ImportanceLevelNonPersistent);
                    this.IsActive = true;
                    this.m_stateMachine.TransitionTo("Chasing");
                    
                    if (this.m_target != null && this.m_componentPathfinding != null)
                    {
                        this.m_componentPathfinding.Stop();
                        this.UpdateChasingStateImmediately();
                    }
                }
            }
        }

        private void UpdateChasingStateImmediately()
        {
            if (this.m_target == null || !this.IsActive) return;
            
            Vector3 targetPosition = this.m_target.ComponentBody.Position;
            this.m_componentPathfinding.SetDestination(new Vector3?(targetPosition), 1f, 1.5f, 0, true, false, true, this.m_target.ComponentBody);
            this.m_componentCreature.ComponentCreatureModel.LookAtOrder = new Vector3?(this.m_target.ComponentCreatureModel.EyePosition);
        }

        public void RespondToCommandImmediately(ComponentCreature target)
        {
            if (this.Suppressed || target == null) return;

            ComponentNewHerdBehavior componentNewHerdBehavior = base.Entity.FindComponent<ComponentNewHerdBehavior>();
            ComponentHerdBehavior componentHerdBehavior = base.Entity.FindComponent<ComponentHerdBehavior>();

            bool canAttack = true;

            if (componentNewHerdBehavior != null)
            {
                canAttack = componentNewHerdBehavior.CanAttackCreature(target);
            }
            else if (componentHerdBehavior != null)
            {
                ComponentHerdBehavior targetHerd = target.Entity.FindComponent<ComponentHerdBehavior>();
                if (targetHerd != null && !string.IsNullOrEmpty(targetHerd.HerdName) && !string.IsNullOrEmpty(componentHerdBehavior.HerdName))
                {
                    bool isSameHerd = targetHerd.HerdName == componentHerdBehavior.HerdName;
                    bool isPlayerAlly = false;

                    if (componentHerdBehavior.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase))
                    {
                        if (targetHerd.HerdName.ToLower().Contains("guardian"))
                            isPlayerAlly = true;
                    }
                    else if (componentHerdBehavior.HerdName.ToLower().Contains("guardian"))
                    {
                        if (targetHerd.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase))
                            isPlayerAlly = true;
                    }

                    if (isSameHerd || isPlayerAlly)
                        canAttack = false;
                }
            }

            if (!canAttack) return;

            this.m_target = target;
            this.m_nextUpdateTime = 0.0;
            this.m_range = 20f;
            this.m_chaseTime = 30f;
            this.m_isPersistent = false;
            this.m_importanceLevel = this.ImportanceLevelNonPersistent;
            this.IsActive = true;
            this.m_stateMachine.TransitionTo("Chasing");

            if (this.m_target != null && this.m_componentPathfinding != null)
            {
                this.m_componentPathfinding.Stop();
                this.UpdateChasingStateImmediately();
            }
        }

        // Métodos de utilidad para bandidos
        private bool IsGuardianOrPlayerAlly()
        {
            ComponentNewHerdBehavior herdBehavior = base.Entity.FindComponent<ComponentNewHerdBehavior>();
            ComponentHerdBehavior oldHerdBehavior = base.Entity.FindComponent<ComponentHerdBehavior>();

            if (herdBehavior != null && !string.IsNullOrEmpty(herdBehavior.HerdName))
            {
                string herdName = herdBehavior.HerdName;
                return herdName.Equals("player", StringComparison.OrdinalIgnoreCase) ||
                       herdName.ToLower().Contains("guardian");
            }
            else if (oldHerdBehavior != null && !string.IsNullOrEmpty(oldHerdBehavior.HerdName))
            {
                string herdName = oldHerdBehavior.HerdName;
                return herdName.Equals("player", StringComparison.OrdinalIgnoreCase) ||
                       herdName.ToLower().Contains("guardian");
            }

            return false;
        }

        private bool IsBandit(ComponentCreature creature)
        {
            if (creature == null) return false;

            ComponentNewHerdBehavior newHerd = creature.Entity.FindComponent<ComponentNewHerdBehavior>();
            if (newHerd != null && !string.IsNullOrEmpty(newHerd.HerdName))
            {
                if (newHerd.HerdName.Equals("bandit", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            ComponentHerdBehavior oldHerd = creature.Entity.FindComponent<ComponentHerdBehavior>();
            if (oldHerd != null && !string.IsNullOrEmpty(oldHerd.HerdName))
            {
                if (oldHerd.HerdName.Equals("bandit", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            if (creature.Entity.FindComponent<ComponentBanditChaseBehavior>() != null)
                return true;

            return false;
        }

        private void CheckNearbyBandits(float dt)
        {
            try
            {
                if (!IsGuardianOrPlayerAlly()) return;

                double gameTime = this.m_subsystemTime.GameTime;

                for (int i = m_banditMemory.Count - 1; i >= 0; i--)
                {
                    if (gameTime - m_banditMemory[i].LastSeenTime > BanditMemoryDuration)
                        m_banditMemory.RemoveAt(i);
                }

                if (gameTime < this.m_nextBanditCheckTime) return;

                this.m_nextBanditCheckTime = gameTime + 0.5;

                float banditDetectionRange = 30f;
                ComponentCreature priorityBandit = null;
                float highestPriority = 0f;

                foreach (ComponentCreature creature in this.m_subsystemCreatureSpawn.Creatures)
                {
                    if (creature == null || creature.ComponentHealth == null || creature.ComponentHealth.Health <= 0f ||
                        creature == this.m_componentCreature || creature.ComponentBody == null)
                        continue;

                    if (!IsBandit(creature)) continue;

                    float distanceSq = Vector3.DistanceSquared(this.m_componentCreature.ComponentBody.Position, creature.ComponentBody.Position);
                    if (distanceSq > banditDetectionRange * banditDetectionRange) continue;

                    float priority = 1f;

                    bool inMemory = false;
                    foreach (var mem in m_banditMemory)
                    {
                        if (mem.Creature == creature)
                        {
                            inMemory = true;
                            if (mem.IsThreatening)
                                priority += 2f;
                            mem.LastSeenTime = gameTime;
                            break;
                        }
                    }

                    if (!inMemory)
                    {
                        bool isThreatening = IsBanditThreateningPlayer(creature);
                        m_banditMemory.Add(new BanditMemory { Creature = creature, LastSeenTime = gameTime, IsThreatening = isThreatening });
                        if (isThreatening)
                            priority += 2f;
                    }

                    float distance = (float)Math.Sqrt(distanceSq);
                    priority += (banditDetectionRange - distance) / banditDetectionRange * 3f;

                    if (priority > highestPriority)
                    {
                        highestPriority = priority;
                        priorityBandit = creature;
                    }
                }

                if (priorityBandit != null && (this.m_target == null || this.m_target != priorityBandit))
                {
                    this.Attack(priorityBandit, banditDetectionRange, 60f, true);

                    ComponentNewHerdBehavior herdBehavior = base.Entity.FindComponent<ComponentNewHerdBehavior>();
                    if (herdBehavior != null && herdBehavior.AutoNearbyCreaturesHelp)
                    {
                        herdBehavior.CallNearbyCreaturesHelp(priorityBandit, banditDetectionRange, 60f, false, true);
                    }
                }
            }
            catch { }
        }

        private bool IsBanditThreateningPlayer(ComponentCreature bandit)
        {
            try
            {
                ComponentChaseBehavior chase = bandit.Entity.FindComponent<ComponentChaseBehavior>();
                if (chase != null && chase.Target != null && chase.Target.Entity.FindComponent<ComponentPlayer>() != null)
                    return true;

                ComponentNewChaseBehavior newChase = bandit.Entity.FindComponent<ComponentNewChaseBehavior>();
                if (newChase != null && newChase.Target != null && newChase.Target.Entity.FindComponent<ComponentPlayer>() != null)
                    return true;

                ComponentBanditChaseBehavior banditChase = bandit.Entity.FindComponent<ComponentBanditChaseBehavior>();
                if (banditChase != null && banditChase.Target != null && banditChase.Target.Entity.FindComponent<ComponentPlayer>() != null)
                    return true;

                if (IsFacingAnyPlayer(bandit) && IsHoldingRangedWeapon(bandit))
                    return true;

                return false;
            }
            catch { return false; }
        }

        private bool IsFacingAnyPlayer(ComponentCreature creature)
        {
            foreach (ComponentPlayer player in this.m_subsystemPlayers.ComponentPlayers)
            {
                if (player.ComponentHealth.Health <= 0f) continue;
                if (IsFacingPlayer(creature, player))
                    return true;
            }
            return false;
        }

        private bool IsHoldingRangedWeapon(ComponentCreature creature)
        {
            ComponentMiner miner = creature.Entity.FindComponent<ComponentMiner>();
            if (miner == null || miner.Inventory == null) return false;
            int value = miner.ActiveBlockValue;
            if (value == 0) return false;
            Block block = BlocksManager.Blocks[Terrain.ExtractContents(value)];
            return block.IsAimable_(value);
        }

        private ComponentCreature FindNearbyBandit(float range)
        {
            try
            {
                Vector3 myPosition = this.m_componentCreature.ComponentBody.Position;
                float rangeSquared = range * range;

                foreach (ComponentCreature creature in this.m_subsystemCreatureSpawn.Creatures)
                {
                    if (creature != null && creature.ComponentHealth != null && 
                        creature.ComponentHealth.Health > 0f && creature != this.m_componentCreature && 
                        creature.ComponentBody != null && IsBandit(creature))
                    {
                        float distanceSquared = Vector3.DistanceSquared(myPosition, creature.ComponentBody.Position);
                        if (distanceSquared <= rangeSquared)
                            return creature;
                    }
                }
            }
            catch { }
            return null;
        }

        public virtual void StopAttack()
        {
            this.m_stateMachine.TransitionTo("LookingForTarget");
            this.IsActive = false;
            this.m_target = null;
            this.m_nextUpdateTime = 0.0;
            this.m_range = 0f;
            this.m_chaseTime = 0f;
            this.m_isPersistent = false;
            this.m_importanceLevel = 0f;
        }

        // MÉTODO UPDATE PRINCIPAL - MEJORADO
        public virtual void Update(float dt)
        {
            if (this.Suppressed)
            {
                this.StopAttack();
                return;
            }
            
            this.m_autoChaseSuppressionTime -= dt;

            if (IsGuardianOrPlayerAlly())
            {
                CheckNearbyBandits(dt);
            }

            // MEJORA: Manejo de noche verde mejorado
            if (this.m_subsystemGreenNightSky != null && this.m_subsystemGreenNightSky.IsGreenNightActive)
            {
                if (this.Suppressed)
                    this.Suppressed = false;

                if (this.IsActive && this.m_importanceLevel < 250f)
                    this.m_importanceLevel = 250f;

                // MEJORA: Forzar preparación de armas durante noche verde
                if (this.m_target != null && this.m_attackMode != AttackMode.OnlyHand)
                {
                    if (!this.FindAimTool(this.m_componentMiner) && this.m_componentMiner.Inventory != null)
                    {
                        for (int i = 0; i < this.m_componentMiner.Inventory.SlotsCount; i++)
                        {
                            int slotValue = this.m_componentMiner.Inventory.GetSlotValue(i);
                            if (slotValue != 0)
                            {
                                Block block = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)];
                                if (block.IsAimable_(slotValue) && block.GetCategory(slotValue) != "Terrain")
                                {
                                    this.m_componentMiner.Inventory.ActiveSlotIndex = i;
                                    break;
                                }
                            }
                        }
                    }
                }

                this.CheckHighAlertPlayerThreats(dt);
            }
            else
            {
                this.CheckDefendPlayer(dt);
            }

            // Lógica principal de combate - MEJORADA
            if (this.IsActive && this.m_target != null)
            {
                this.m_chaseTime -= dt;
                this.m_componentCreature.ComponentCreatureModel.LookAtOrder = new Vector3?(this.m_target.ComponentCreatureModel.EyePosition);
                
                float cooldown = (this.m_subsystemGameInfo.WorldSettings.GameMode == GameMode.Creative) ? 2.5f : 3f;
                float distanceToTarget;
                ComponentBody hitBodyByAim = this.GetHitBody1(this.m_target.ComponentBody, out distanceToTarget);

                // MEJORA: Combate a distancia - sin condición problemática de >5f
                if (this.m_attackMode != AttackMode.OnlyHand && this.FindAimTool(this.m_componentMiner))
                {
                    Vector3 aimDirection = Vector3.Normalize(this.m_target.ComponentCreatureModel.EyePosition - 
                                                              this.m_componentCreature.ComponentCreatureModel.EyePosition);
                    
                    if (hitBodyByAim != null)
                    {
                        float chaseExtension = this.m_isPersistent ? this.m_random.Float(8f, 10f) : 2f;
                        this.m_chaseTime = Math.Max(this.m_chaseTime, chaseExtension);
                        
                        if (distanceToTarget >= this.m_attackRange.X && distanceToTarget <= this.m_attackRange.Y)
                        {
                            // MEJORA: Umbral de alineación más estricto (0.9f)
                            bool isAligned = Vector3.Dot(this.m_componentBody.Matrix.Forward, aimDirection) > 0.9f;
                            
                            if (!isAligned)
                            {
                                this.m_componentPathfinding.SetDestination(
                                    new Vector3?(this.m_target.ComponentCreatureModel.EyePosition), 
                                    1f, 1f, 0, false, true, false, null);
                            }
                            else
                            {
                                this.m_componentPathfinding.Destination = null;
                                // MEJORA: Llamar al método de posicionamiento para combate a distancia
                                UpdateRangedCombatPositioning(dt);
                            }
                            
                            string category = BlocksManager.Blocks[Terrain.ExtractContents(
                                this.m_componentMiner.ActiveBlockValue)].GetCategory(
                                this.m_componentMiner.ActiveBlockValue);
                            
                            if (this.m_subsystemTime.GameTime - this.m_lastActionTime > (double)cooldown)
                            {
                                if (this.m_componentMiner.Use(new Ray3(
                                    this.m_componentCreature.ComponentCreatureModel.EyePosition, aimDirection)))
                                {
                                    this.m_lastActionTime = this.m_subsystemTime.GameTime;
                                }
                                else if (isAligned && category != "Terrain")
                                {
                                    this.m_componentMiner.Aim(new Ray3(
                                        this.m_componentCreature.ComponentCreatureModel.EyePosition, aimDirection), 
                                        AimState.Completed);
                                    this.m_lastActionTime = this.m_subsystemTime.GameTime;
                                }
                            }
                            else if (isAligned && category != "Terrain")
                            {
                                this.m_componentMiner.Aim(new Ray3(
                                    this.m_componentCreature.ComponentCreatureModel.EyePosition, aimDirection), 
                                    AimState.InProgress);
                            }
                        }
                        else
                        {
                            // Fuera de rango, moverse hacia el objetivo
                            UpdateRangedCombatPositioning(dt);
                        }
                    }
                }
                else // Combate cuerpo a cuerpo
                {
                    if (this.IsTargetInAttackRange(this.m_target.ComponentBody))
                    {
                        this.m_componentCreatureModel.AttackOrder = true;
                        if (this.m_attackMode != AttackMode.OnlyHand)
                        {
                            this.FindHitTool(this.m_componentMiner);
                        }
                    }
                    
                    if (this.m_componentCreatureModel.IsAttackHitMoment)
                    {
                        Vector3 hitPoint;
                        ComponentBody hitBody = this.GetHitBody(this.m_target.ComponentBody, out hitPoint);
                        if (hitBody != null)
                        {
                            float chaseExtension = this.m_isPersistent ? this.m_random.Float(8f, 10f) : 2f;
                            this.m_chaseTime = MathUtils.Max(this.m_chaseTime, chaseExtension);
                            this.m_componentMiner.Hit(hitBody, hitPoint, this.m_componentCreature.ComponentBody.Matrix.Forward);
                            this.m_componentCreature.ComponentCreatureSounds.PlayAttackSound();
                        }
                    }
                }
            }

            if (this.m_subsystemTime.GameTime >= this.m_nextUpdateTime)
            {
                this.m_dt = this.m_random.Float(0.25f, 0.35f) + MathUtils.Min((float)(this.m_subsystemTime.GameTime - this.m_nextUpdateTime), 0.1f);
                this.m_nextUpdateTime = this.m_subsystemTime.GameTime + (double)this.m_dt;
                this.m_stateMachine.Update();
            }
        }

        // NUEVO MÉTODO: Mejorar posicionamiento para combate a distancia
        private void UpdateRangedCombatPositioning(float dt)
        {
            if (this.m_target == null || this.m_componentPathfinding == null)
                return;
            
            float distanceToTarget = Vector3.Distance(this.m_componentCreature.ComponentBody.Position,
                                                      this.m_target.ComponentBody.Position);
            
            // Si estamos en rango óptimo de disparo, intentar mantener distancia
            if (distanceToTarget > this.m_attackRange.X && distanceToTarget < this.m_attackRange.Y)
            {
                float rayDistance;
                ComponentBody hitBody = this.GetHitBody1(this.m_target.ComponentBody, out rayDistance);
                
                if (hitBody != null && Math.Abs(rayDistance - distanceToTarget) < 1f)
                {
                    // Tenemos línea de visión clara, mantener posición
                    this.m_componentPathfinding.Destination = null;
                    return;
                }
            }
            
            // Si no estamos en buena posición, mover hacia el objetivo
            int maxPathfindingPositions = this.m_isPersistent ? 2000 : 500;
            Vector3 targetPos = this.m_target.ComponentBody.Position;
            this.m_componentPathfinding.SetDestination(new Vector3?(targetPos), 1f, 1.5f, 
                                                       maxPathfindingPositions, true, false, true, 
                                                       this.m_target.ComponentBody);
        }

        // MÉTODO MEJORADO: GetHitBody1 (basado en WonderfulEra)
        public ComponentBody GetHitBody1(ComponentBody target, out float distance)
        {
            Vector3 start = this.m_componentCreature.ComponentBody.BoundingBox.Center();
            Vector3 direction = Vector3.Normalize(target.BoundingBox.Center() - start);
            
            BodyRaycastResult? bodyResult = this.PickBody(start, direction, this.m_attackRange.Y);
            TerrainRaycastResult? terrainResult = this.PickTerrain(start, direction, this.m_attackRange.Y);
            
            distance = (bodyResult != null) ? bodyResult.GetValueOrDefault().Distance : float.PositiveInfinity;
            
            if (this.m_componentMiner.Inventory != null && bodyResult != null)
            {
                if (terrainResult != null && (terrainResult.Value.Distance + 0.1f) < bodyResult.Value.Distance)
                {
                    return null;
                }
                
                if (bodyResult.Value.ComponentBody == target || 
                    bodyResult.Value.ComponentBody.IsChildOfBody(target) || 
                    target.IsChildOfBody(bodyResult.Value.ComponentBody) || 
                    target.StandingOnBody == bodyResult.Value.ComponentBody)
                {
                    return bodyResult.Value.ComponentBody;
                }
            }
            return null;
        }

        private TerrainRaycastResult? PickTerrain(Vector3 position, Vector3 direction, float reach)
        {
            direction = Vector3.Normalize(direction);
            Vector3 creaturePosition = this.m_componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition;
            Vector3 end = position + direction * reach;
            return this.m_componentMiner.m_subsystemTerrain.Raycast(position, end, true, true, 
                (int value, float distance) => 
                (double)Vector3.Distance(position + distance * direction, creaturePosition) <= (double)reach && 
                BlocksManager.Blocks[Terrain.ExtractContents(value)].IsCollidable);
        }

        private BodyRaycastResult? PickBody(Vector3 position, Vector3 direction, float reach)
        {
            direction = Vector3.Normalize(direction);
            Vector3 creaturePosition = this.m_componentMiner.ComponentCreature.ComponentCreatureModel.EyePosition;
            Vector3 end = position + direction * reach;
            return this.m_subsystemBodies.Raycast(position, end, 0.35f, 
                (ComponentBody body, float distance) => 
                (double)Vector3.Distance(position + distance * direction, creaturePosition) <= (double)reach && 
                body.Entity != this.Entity && 
                !body.IsChildOfBody(this.m_componentMiner.ComponentCreature.ComponentBody) && 
                !this.m_componentMiner.ComponentCreature.ComponentBody.IsChildOfBody(body));
        }

        // MÉTODO MEJORADO: FindAimTool (basado en ExpandMod)
        public bool FindAimTool(ComponentMiner componentMiner)
        {
            if (componentMiner.Inventory == null) return false;
            
            int activeBlockValue = componentMiner.ActiveBlockValue;
            int contents = Terrain.ExtractContents(activeBlockValue);
            Block block = BlocksManager.Blocks[contents];
            
            if (block.IsAimable_(activeBlockValue) && block.GetCategory(activeBlockValue) != "Terrain")
            {
                if (!(block is FlameThrowerBlock) || this.IsReady(activeBlockValue))
                {
                    return true;
                }
            }
            
            for (int i = 0; i < componentMiner.Inventory.SlotsCount; i++)
            {
                int slotValue = componentMiner.Inventory.GetSlotValue(i);
                if (slotValue == 0) continue;
                
                int slotContents = Terrain.ExtractContents(slotValue);
                Block slotBlock = BlocksManager.Blocks[slotContents];
                
                if (slotBlock.IsAimable_(slotValue) && slotBlock.GetCategory(slotValue) != "Terrain")
                {
                    if (slotBlock is FlameThrowerBlock && !this.IsReady(slotValue))
                        continue;
                        
                    componentMiner.Inventory.ActiveSlotIndex = i;
                    return true;
                }
            }
            
            return false;
        }

        public bool IsReady(int slotValue)
        {
            int data = Terrain.ExtractData(slotValue);
            if (BlocksManager.Blocks[Terrain.ExtractContents(slotValue)] is FlameThrowerBlock)
            {
                return FlameThrowerBlock.GetLoadState(data) == FlameThrowerBlock.LoadState.Loaded &&
                       FlameThrowerBlock.GetBulletType(data) != null;
            }
            return true;
        }

        public bool IsAimToolNeedToReady(ComponentMiner componentMiner, int slotIndex)
        {
            int slotValue = componentMiner.Inventory.GetSlotValue(slotIndex);
            int data = Terrain.ExtractData(slotValue);
            Block block = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)];
            
            if (!(block is BowBlock))
            {
                if (!(block is CrossbowBlock))
                {
                    if (!(block is RepeatCrossbowBlock))
                    {
                        if (!(block is MusketBlock))
                        {
                            return false;
                        }
                        if (MusketBlock.GetLoadState(data) == MusketBlock.LoadState.Loaded && 
                            MusketBlock.GetBulletType(data) != null)
                        {
                            return false;
                        }
                    }
                    else if (RepeatCrossbowBlock.GetDraw(data) >= 15 && 
                             RepeatCrossbowBlock.GetArrowType(data) != null)
                    {
                        return false;
                    }
                }
                else if (CrossbowBlock.GetDraw(data) >= 15 && 
                         CrossbowBlock.GetArrowType(data) != null)
                {
                    return false;
                }
            }
            else if (BowBlock.GetDraw(data) >= 15 && BowBlock.GetArrowType(data) != null)
            {
                return false;
            }
            return true;
        }

        public void HandleComplexAimTool(ComponentMiner componentMiner, int slotIndex)
        {
            int slotValue = componentMiner.Inventory.GetSlotValue(slotIndex);
            int data = Terrain.ExtractData(slotValue);
            int contents = Terrain.ExtractContents(slotValue);
            Block block = BlocksManager.Blocks[contents];
            int newData = data;
            
            if (!(block is BowBlock))
            {
                if (!(block is CrossbowBlock))
                {
                    if (!(block is RepeatCrossbowBlock))
                    {
                        if (block is MusketBlock)
                        {
                            newData = MusketBlock.SetLoadState(
                                MusketBlock.SetBulletType(0, new BulletBlock.BulletType?(BulletBlock.BulletType.MusketBall)), 
                                MusketBlock.LoadState.Loaded);
                        }
                        else if (block is FlameThrowerBlock)
                        {
                            this.m_lastFlameBulletType = (this.m_lastFlameBulletType == FlameBulletBlock.FlameBulletType.Flame) 
                                ? FlameBulletBlock.FlameBulletType.Poison 
                                : FlameBulletBlock.FlameBulletType.Flame;
                            
                            int flameData = 0;
                            flameData = FlameThrowerBlock.SetBulletType(flameData, this.m_lastFlameBulletType);
                            flameData = FlameThrowerBlock.SetLoadState(flameData, FlameThrowerBlock.LoadState.Loaded);
                            flameData = FlameThrowerBlock.SetSwitchState(flameData, false);
                            
                            int flameThrowerValue = Terrain.MakeBlockValue(contents, 0, flameData);
                            flameThrowerValue = FlameThrowerBlock.SetLoadCount(flameThrowerValue, 15);
                            
                            componentMiner.Inventory.RemoveSlotItems(slotIndex, 1);
                            componentMiner.Inventory.AddSlotItems(slotIndex, flameThrowerValue, 1);
                            return;
                        }
                    }
                    else
                    {
                        Array repeatArrowTypes = Enum.GetValues(typeof(RepeatArrowBlock.ArrowType));
                        RepeatArrowBlock.ArrowType randomType = (RepeatArrowBlock.ArrowType)repeatArrowTypes.GetValue(
                            this.m_random.Int(0, repeatArrowTypes.Length));
                        newData = CrossbowBlock.SetDraw(
                            RepeatCrossbowBlock.SetArrowType(0, new RepeatArrowBlock.ArrowType?(randomType)), 15);
                    }
                }
                else
                {
                    ArrowBlock.ArrowType[] boltTypes = new ArrowBlock.ArrowType[]
                    {
                        ArrowBlock.ArrowType.IronBolt,
                        ArrowBlock.ArrowType.DiamondBolt,
                        ArrowBlock.ArrowType.ExplosiveBolt
                    };
                    ArrowBlock.ArrowType randomBoltType = boltTypes[this.m_random.Int(0, boltTypes.Length)];
                    newData = CrossbowBlock.SetDraw(
                        CrossbowBlock.SetArrowType(0, new ArrowBlock.ArrowType?(randomBoltType)), 15);
                }
            }
            else
            {
                ArrowBlock.ArrowType[] arrowTypes = new ArrowBlock.ArrowType[]
                {
                    ArrowBlock.ArrowType.WoodenArrow,
                    ArrowBlock.ArrowType.StoneArrow,
                    ArrowBlock.ArrowType.IronArrow,
                    ArrowBlock.ArrowType.DiamondArrow,
                    ArrowBlock.ArrowType.CopperArrow
                };
                ArrowBlock.ArrowType randomArrowType = arrowTypes[this.m_random.Int(0, arrowTypes.Length)];
                newData = BowBlock.SetDraw(
                    BowBlock.SetArrowType(0, new ArrowBlock.ArrowType?(randomArrowType)), 15);
            }

            int weaponValue = Terrain.MakeBlockValue(contents, 0, newData);
            componentMiner.Inventory.RemoveSlotItems(slotIndex, 1);
            componentMiner.Inventory.AddSlotItems(slotIndex, weaponValue, 1);
        }

        public bool FindHitTool(ComponentMiner componentMiner)
        {
            int activeBlockValue = componentMiner.ActiveBlockValue;
            if (componentMiner.Inventory == null) return false;
            
            if (BlocksManager.Blocks[Terrain.ExtractContents(activeBlockValue)].GetMeleePower(activeBlockValue) > 1f)
                return true;
            
            float bestPower = 1f;
            int bestSlot = componentMiner.Inventory.ActiveSlotIndex;
            
            for (int i = 0; i < componentMiner.Inventory.SlotsCount; i++)
            {
                int slotValue = componentMiner.Inventory.GetSlotValue(i);
                if (slotValue != 0)
                {
                    float meleePower = BlocksManager.Blocks[Terrain.ExtractContents(slotValue)].GetMeleePower(slotValue);
                    if (meleePower > bestPower)
                    {
                        bestPower = meleePower;
                        bestSlot = i;
                    }
                }
            }
            
            if (bestPower > 1f)
            {
                componentMiner.Inventory.ActiveSlotIndex = bestSlot;
                return true;
            }
            return false;
        }

        private void CheckHighAlertPlayerThreats(float dt)
        {
            try
            {
                if (!IsPlayerAlly()) return;
                if (this.m_subsystemTime.GameTime < this.m_nextHighAlertCheckTime) return;

                this.m_nextHighAlertCheckTime = this.m_subsystemTime.GameTime + 0.1;
                float highAlertRange = 40f;

                foreach (ComponentPlayer componentPlayer in this.m_subsystemPlayers.ComponentPlayers)
                {
                    if (componentPlayer.ComponentHealth.Health <= 0f) continue;

                    ComponentCreature mostDangerousThreat = FindMostDangerousThreatForPlayer(componentPlayer, highAlertRange);

                    if (mostDangerousThreat != null && (this.m_target == null || this.m_target != mostDangerousThreat))
                    {
                        this.Attack(mostDangerousThreat, highAlertRange, 60f, true);

                        ComponentNewHerdBehavior herdBehavior = base.Entity.FindComponent<ComponentNewHerdBehavior>();
                        if (herdBehavior != null && herdBehavior.AutoNearbyCreaturesHelp)
                        {
                            herdBehavior.CallNearbyCreaturesHelp(mostDangerousThreat, highAlertRange, 60f, false, true);
                        }
                        return;
                    }
                }
            }
            catch { }
        }

        private ComponentCreature FindMostDangerousThreatForPlayer(ComponentPlayer player, float range)
        {
            try
            {
                if (player == null || player.ComponentBody == null) return null;

                Vector3 playerPosition = player.ComponentBody.Position;
                float rangeSquared = range * range;
                ComponentCreature mostDangerous = null;
                float highestThreatLevel = 0f;

                foreach (ComponentCreature creature in this.m_subsystemCreatureSpawn.Creatures)
                {
                    if (creature == null || creature.ComponentHealth == null || creature.ComponentHealth.Health <= 0f ||
                        creature == this.m_componentCreature || creature == player || creature.ComponentBody == null)
                        continue;

                    if (!IsCreatureThreat(creature, player)) continue;

                    float distanceSquared = Vector3.DistanceSquared(playerPosition, creature.ComponentBody.Position);
                    if (distanceSquared > rangeSquared) continue;

                    float threatLevel = CalculateThreatLevel(creature, player, distanceSquared);

                    if (threatLevel > highestThreatLevel)
                    {
                        highestThreatLevel = threatLevel;
                        mostDangerous = creature;
                    }
                }
                return mostDangerous;
            }
            catch { }
            return null;
        }

        private float CalculateThreatLevel(ComponentCreature creature, ComponentPlayer player, float distanceSquared)
        {
            float distance = (float)Math.Sqrt(distanceSquared);
            float threatLevel = 100f / (distance + 1f);

            if (IsZombieOrInfected(creature))
                threatLevel *= 1.5f;
            else if (IsBandit(creature))
                threatLevel *= 1.8f;
            else
                threatLevel *= 1.3f;

            if (IsFacingPlayer(creature, player))
                threatLevel += 50f;

            if (IsMovingTowardPlayer(creature, player))
                threatLevel += 70f;

            if (distance < 10f)
                threatLevel += 100f;

            if (IsAttackingPlayer(creature, player))
                threatLevel += 200f;

            if (IsCreatureAggressive(creature))
                threatLevel += 80f;

            return threatLevel;
        }

        private bool IsCreatureAggressive(ComponentCreature creature)
        {
            ComponentChaseBehavior chase = creature.Entity.FindComponent<ComponentChaseBehavior>();
            ComponentNewChaseBehavior newChase = creature.Entity.FindComponent<ComponentNewChaseBehavior>();
            ComponentNewChaseBehavior2 newChase2 = creature.Entity.FindComponent<ComponentNewChaseBehavior2>();
            ComponentZombieChaseBehavior zombieChase = creature.Entity.FindComponent<ComponentZombieChaseBehavior>();
            ComponentBanditChaseBehavior banditChase = creature.Entity.FindComponent<ComponentBanditChaseBehavior>();

            return (chase != null && chase.Target != null) ||
                   (newChase != null && newChase.Target != null) ||
                   (newChase2 != null && newChase2.Target != null) ||
                   (zombieChase != null && zombieChase.Target != null) ||
                   (banditChase != null && banditChase.Target != null);
        }

        private void CheckDefendPlayer(float dt)
        {
            try
            {
                if (!IsPlayerAlly()) return;
                if (this.m_subsystemTime.GameTime < this.m_nextPlayerCheckTime) return;

                this.m_nextPlayerCheckTime = this.m_subsystemTime.GameTime + 0.5;

                foreach (ComponentPlayer componentPlayer in this.m_subsystemPlayers.ComponentPlayers)
                {
                    if (componentPlayer.ComponentHealth.Health <= 0f) continue;

                    ComponentCreature componentCreature = this.FindPlayerAttacker(componentPlayer);
                    if (componentCreature != null && (this.m_target == null || this.m_target != componentCreature))
                    {
                        this.Attack(componentCreature, 20f, 30f, false);
                    }
                }
            }
            catch { }
        }

        private ComponentCreature FindPlayerAttacker(ComponentPlayer player)
        {
            try
            {
                if (player == null || player.ComponentBody == null) return null;
                
                Vector3 position = player.ComponentBody.Position;
                float range = 20f;
                float rangeSq = range * range;
                
                foreach (ComponentCreature creature in this.m_subsystemCreatureSpawn.Creatures)
                {
                    if (creature != null && creature.ComponentHealth != null && 
                        creature.ComponentHealth.Health > 0f && creature != this.m_componentCreature && 
                        creature.ComponentBody != null)
                    {
                        float distSq = Vector3.DistanceSquared(position, creature.ComponentBody.Position);
                        if (distSq < rangeSq)
                        {
                            ComponentNewHerdBehavior newHerd = creature.Entity.FindComponent<ComponentNewHerdBehavior>();
                            ComponentHerdBehavior oldHerd = creature.Entity.FindComponent<ComponentHerdBehavior>();
                            
                            bool isPlayerAlly = false;

                            if (newHerd != null && !string.IsNullOrEmpty(newHerd.HerdName))
                            {
                                string herdName = newHerd.HerdName;
                                isPlayerAlly = herdName.Equals("player", StringComparison.OrdinalIgnoreCase) ||
                                              herdName.ToLower().Contains("guardian");
                            }
                            else if (oldHerd != null && !string.IsNullOrEmpty(oldHerd.HerdName))
                            {
                                string herdName = oldHerd.HerdName;
                                isPlayerAlly = herdName.Equals("player", StringComparison.OrdinalIgnoreCase) ||
                                              herdName.ToLower().Contains("guardian");
                            }

                            if (!isPlayerAlly)
                            {
                                ComponentChaseBehavior chase = creature.Entity.FindComponent<ComponentChaseBehavior>();
                                ComponentNewChaseBehavior newChase = creature.Entity.FindComponent<ComponentNewChaseBehavior>();
                                
                                if ((chase != null && chase.Target == player) ||
                                    (newChase != null && newChase.Target == player))
                                {
                                    return creature;
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private bool IsPlayerAlly()
        {
            ComponentNewHerdBehavior herdBehavior = base.Entity.FindComponent<ComponentNewHerdBehavior>();
            ComponentHerdBehavior oldHerdBehavior = base.Entity.FindComponent<ComponentHerdBehavior>();

            if (herdBehavior != null && !string.IsNullOrEmpty(herdBehavior.HerdName))
            {
                string herdName = herdBehavior.HerdName;
                return herdName.Equals("player", StringComparison.OrdinalIgnoreCase) ||
                       herdName.ToLower().Contains("guardian");
            }
            else if (oldHerdBehavior != null && !string.IsNullOrEmpty(oldHerdBehavior.HerdName))
            {
                string herdName = oldHerdBehavior.HerdName;
                return herdName.Equals("player", StringComparison.OrdinalIgnoreCase) ||
                       herdName.ToLower().Contains("guardian");
            }

            return false;
        }

        private bool IsCreatureThreat(ComponentCreature creature, ComponentPlayer player)
        {
            ComponentNewHerdBehavior creatureHerd = creature.Entity.FindComponent<ComponentNewHerdBehavior>();
            ComponentHerdBehavior creatureOldHerd = creature.Entity.FindComponent<ComponentHerdBehavior>();

            bool isPlayerAlly = false;

            if (creatureHerd != null && !string.IsNullOrEmpty(creatureHerd.HerdName))
            {
                string herdName = creatureHerd.HerdName;
                isPlayerAlly = herdName.Equals("player", StringComparison.OrdinalIgnoreCase) ||
                              herdName.ToLower().Contains("guardian");
            }
            else if (creatureOldHerd != null && !string.IsNullOrEmpty(creatureOldHerd.HerdName))
            {
                string herdName = creatureOldHerd.HerdName;
                isPlayerAlly = herdName.Equals("player", StringComparison.OrdinalIgnoreCase) ||
                              herdName.ToLower().Contains("guardian");
            }

            if (isPlayerAlly) return false;
            if (IsZombieOrInfected(creature)) return true;
            if (IsBandit(creature)) return true;
            if (IsAttackingPlayer(creature, player)) return true;

            return false;
        }

        private bool IsAttackingPlayer(ComponentCreature creature, ComponentPlayer player)
        {
            ComponentChaseBehavior chase = creature.Entity.FindComponent<ComponentChaseBehavior>();
            ComponentNewChaseBehavior newChase = creature.Entity.FindComponent<ComponentNewChaseBehavior>();
            ComponentNewChaseBehavior2 newChase2 = creature.Entity.FindComponent<ComponentNewChaseBehavior2>();
            ComponentZombieChaseBehavior zombieChase = creature.Entity.FindComponent<ComponentZombieChaseBehavior>();
            ComponentBanditChaseBehavior banditChase = creature.Entity.FindComponent<ComponentBanditChaseBehavior>();

            return (chase != null && chase.Target == player) ||
                   (newChase != null && newChase.Target == player) ||
                   (newChase2 != null && newChase2.Target == player) ||
                   (zombieChase != null && zombieChase.Target == player) ||
                   (banditChase != null && banditChase.Target == player);
        }

        private bool IsFacingPlayer(ComponentCreature creature, ComponentPlayer player)
        {
            if (creature.ComponentBody == null || player.ComponentBody == null) return false;

            Vector3 toPlayer = player.ComponentBody.Position - creature.ComponentBody.Position;
            if (toPlayer.LengthSquared() < 0.01f) return false;

            toPlayer = Vector3.Normalize(toPlayer);
            Vector3 creatureForward = creature.ComponentBody.Matrix.Forward;

            return Vector3.Dot(creatureForward, toPlayer) > 0.7f;
        }

        private bool IsZombieOrInfected(ComponentCreature creature)
        {
            ComponentZombieHerdBehavior zombieHerd = creature.Entity.FindComponent<ComponentZombieHerdBehavior>();
            ComponentZombieChaseBehavior zombieChase = creature.Entity.FindComponent<ComponentZombieChaseBehavior>();

            if (zombieHerd == null && zombieChase == null)
            {
                ComponentChaseBehavior chase = creature.Entity.FindComponent<ComponentChaseBehavior>();
                if (chase != null && chase.AttacksPlayer)
                {
                    ComponentHerdBehavior herd = creature.Entity.FindComponent<ComponentHerdBehavior>();
                    if (herd != null)
                    {
                        string herdName = herd.HerdName.ToLower();
                        if (herdName.Contains("hostile") || herdName.Contains("enemy") || herdName.Contains("monster"))
                        {
                            return true;
                        }
                    }
                }
            }

            return (zombieHerd != null || zombieChase != null);
        }

        private bool IsMovingTowardPlayer(ComponentCreature creature, ComponentPlayer player)
        {
            if (creature.ComponentBody == null || player.ComponentBody == null) return false;

            Vector3 toPlayer = player.ComponentBody.Position - creature.ComponentBody.Position;
            if (toPlayer.LengthSquared() < 0.01f) return false;

            toPlayer = Vector3.Normalize(toPlayer);
            Vector3 creatureVelocity = creature.ComponentBody.Velocity;

            if (creatureVelocity.LengthSquared() < 0.1f) return false;

            creatureVelocity = Vector3.Normalize(creatureVelocity);
            return Vector3.Dot(creatureVelocity, toPlayer) > 0.5f;
        }

        public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
        {
            this.m_subsystemGameInfo = base.Project.FindSubsystem<SubsystemGameInfo>(true);
            this.m_subsystemPlayers = base.Project.FindSubsystem<SubsystemPlayers>(true);
            this.m_subsystemSky = base.Project.FindSubsystem<SubsystemSky>(true);
            this.m_subsystemBodies = base.Project.FindSubsystem<SubsystemBodies>(true);
            this.m_subsystemTime = base.Project.FindSubsystem<SubsystemTime>(true);
            this.m_subsystemNoise = base.Project.FindSubsystem<SubsystemNoise>(true);
            this.m_subsystemCreatureSpawn = base.Project.FindSubsystem<SubsystemCreatureSpawn>(true);
            this.m_subsystemAudio = base.Project.FindSubsystem<SubsystemAudio>(true);
            this.m_subsystemParticles = base.Project.FindSubsystem<SubsystemParticles>(true);
            this.m_subsystemTerrain = base.Project.FindSubsystem<SubsystemTerrain>(true);
            this.m_subsystemGreenNightSky = base.Project.FindSubsystem<SubsystemGreenNightSky>(false);
            this.m_componentCreature = base.Entity.FindComponent<ComponentCreature>(true);
            this.m_componentPathfinding = base.Entity.FindComponent<ComponentPathfinding>(true);
            this.m_componentMiner = base.Entity.FindComponent<ComponentMiner>(true);
            this.m_componentFeedBehavior = base.Entity.FindComponent<ComponentRandomFeedBehavior>();
            this.m_componentCreatureModel = base.Entity.FindComponent<ComponentCreatureModel>(true);
            this.m_componentFactors = base.Entity.FindComponent<ComponentFactors>(true);
            this.m_componentBody = base.Entity.FindComponent<ComponentBody>(true);
            
            string attackModeString = valuesDictionary.GetValue<string>("AttackMode", "Default");
            this.m_attackMode = (AttackMode)Enum.Parse(typeof(AttackMode), attackModeString);
            this.m_attackRange = valuesDictionary.GetValue<Vector2>("AttackRange", new Vector2(2f, 15f));
            this.m_dayChaseRange = valuesDictionary.GetValue<float>("DayChaseRange");
            this.m_nightChaseRange = valuesDictionary.GetValue<float>("NightChaseRange");
            this.m_dayChaseTime = valuesDictionary.GetValue<float>("DayChaseTime");
            this.m_nightChaseTime = valuesDictionary.GetValue<float>("NightChaseTime");
            
            try { this.m_autoChaseMask = valuesDictionary.GetValue<CreatureCategory>("AutoChaseMask"); }
            catch { this.m_autoChaseMask = (CreatureCategory)0; }
            
            this.m_chaseNonPlayerProbability = valuesDictionary.GetValue<float>("ChaseNonPlayerProbability");
            this.m_chaseWhenAttackedProbability = valuesDictionary.GetValue<float>("ChaseWhenAttackedProbability");
            this.m_chaseOnTouchProbability = valuesDictionary.GetValue<float>("ChaseOnTouchProbability");
            this.m_autoDismount = valuesDictionary.GetValue<bool>("AutoDismount", true);
            
            this.m_nextPlayerCheckTime = 0.0;
            this.m_nextHighAlertCheckTime = 0.0;
            this.m_lastActionTime = 0.0;
            this.m_nextBanditCheckTime = 0.0;

            ComponentBody componentBody = this.m_componentCreature.ComponentBody;
            componentBody.CollidedWithBody = (Action<ComponentBody>)Delegate.Combine(componentBody.CollidedWithBody, 
                new Action<ComponentBody>(delegate (ComponentBody body)
                {
                    if (this.m_target == null && this.m_autoChaseSuppressionTime <= 0f && 
                        this.m_random.Float(0f, 1f) < this.m_chaseOnTouchProbability)
                    {
                        ComponentCreature componentCreature = body.Entity.FindComponent<ComponentCreature>();
                        if (componentCreature != null)
                        {
                            bool isPlayer = this.m_subsystemPlayers.IsPlayer(body.Entity);
                            bool hasAutoChaseMask = this.m_autoChaseMask > (CreatureCategory)0;
                            ComponentNewHerdBehavior herdBehavior = base.Entity.FindComponent<ComponentNewHerdBehavior>();
                            bool canAttack = (herdBehavior != null) ? herdBehavior.CanAttackCreature(componentCreature) : true;
                            
                            if (canAttack && ((this.AttacksPlayer && isPlayer && 
                                 this.m_subsystemGameInfo.WorldSettings.GameMode > GameMode.Harmless) || 
                                 (this.AttacksNonPlayerCreature && !isPlayer && hasAutoChaseMask)))
                            {
                                float chaseRange = this.ChaseRangeOnTouch;
                                float chaseTime = this.ChaseTimeOnTouch;

                                if (IsGuardianOrPlayerAlly() && IsBandit(componentCreature))
                                {
                                    chaseRange *= 1.2f;
                                    chaseTime *= 1.3f;
                                }

                                this.Attack(componentCreature, chaseRange, chaseTime, false);
                            }
                        }
                    }
                    
                    if (this.m_target != null && this.JumpWhenTargetStanding && 
                        body == this.m_target.ComponentBody && body.StandingOnBody == this.m_componentCreature.ComponentBody)
                    {
                        this.m_componentCreature.ComponentLocomotion.JumpOrder = 1f;
                    }
                }));

            ComponentHealth componentHealth = this.m_componentCreature.ComponentHealth;
            componentHealth.Injured = (Action<Injury>)Delegate.Combine(componentHealth.Injured, 
                new Action<Injury>(delegate (Injury injury)
                {
                    if (injury.Attacker == null || this.m_random.Float(0f, 1f) >= this.m_chaseWhenAttackedProbability)
                        return;
                    
                    float maxRange = this.ChaseRangeOnAttacked ?? ((this.m_chaseWhenAttackedProbability >= 1f) ? 30f : 7f);
                    float maxChaseTime = this.ChaseTimeOnAttacked ?? ((this.m_chaseWhenAttackedProbability >= 1f) ? 60f : 7f);
                    bool isPersistent = this.ChasePersistentOnAttacked ?? (this.m_chaseWhenAttackedProbability >= 1f);

                    if (IsGuardianOrPlayerAlly() && IsBandit(injury.Attacker))
                    {
                        maxRange *= 1.3f;
                        maxChaseTime *= 1.5f;
                        isPersistent = true;
                    }

                    ComponentNewHerdBehavior newHerd = base.Entity.FindComponent<ComponentNewHerdBehavior>();
                    if (newHerd != null)
                    {
                        if (newHerd.CanAttackCreature(injury.Attacker))
                            this.Attack(injury.Attacker, maxRange, maxChaseTime, isPersistent);
                    }
                    else
                    {
                        ComponentHerdBehavior oldHerd = base.Entity.FindComponent<ComponentHerdBehavior>();
                        if (oldHerd != null && injury.Attacker != null)
                        {
                            ComponentHerdBehavior attackerHerd = injury.Attacker.Entity.FindComponent<ComponentHerdBehavior>();
                            if (attackerHerd != null && !string.IsNullOrEmpty(attackerHerd.HerdName))
                            {
                                bool isSameHerd = attackerHerd.HerdName == oldHerd.HerdName;
                                bool isPlayerAlly = false;

                                if (oldHerd.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (attackerHerd.HerdName.ToLower().Contains("guardian"))
                                        isPlayerAlly = true;
                                }
                                else if (oldHerd.HerdName.ToLower().Contains("guardian"))
                                {
                                    if (attackerHerd.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase))
                                        isPlayerAlly = true;
                                }

                                if (!isSameHerd && !isPlayerAlly)
                                    this.Attack(injury.Attacker, maxRange, maxChaseTime, isPersistent);
                            }
                            else
                            {
                                this.Attack(injury.Attacker, maxRange, maxChaseTime, isPersistent);
                            }
                        }
                        else
                        {
                            this.Attack(injury.Attacker, maxRange, maxChaseTime, isPersistent);
                        }
                    }
                }));

            // Configuración de la máquina de estados
            this.m_stateMachine.AddState("LookingForTarget", delegate
            {
                this.m_importanceLevel = 0f;
                this.m_target = null;
            }, delegate
            {
                if (this.IsActive)
                {
                    this.m_stateMachine.TransitionTo("Chasing");
                }
                else
                {
                    if (!this.Suppressed && this.m_autoChaseSuppressionTime <= 0f && 
                        (this.m_target == null || this.ScoreTarget(this.m_target) <= 0f) && 
                        this.m_componentCreature.ComponentHealth.Health > this.MinHealthToAttackActively)
                    {
                        this.m_range = ((this.m_subsystemSky.SkyLightIntensity < 0.2f) ? 
                            this.m_nightChaseRange : this.m_dayChaseRange);
                        this.m_range *= this.m_componentFactors.GetOtherFactorResult("ChaseRange", false, false);
                        
                        ComponentCreature componentCreature = this.FindTarget();
                        
                        if (componentCreature != null)
                        {
                            this.m_targetInRangeTime += this.m_dt;
                        }
                        else
                        {
                            this.m_targetInRangeTime = 0f;
                        }
                        
                        if (this.m_targetInRangeTime > this.TargetInRangeTimeToChase)
                        {
                            bool isDay = this.m_subsystemSky.SkyLightIntensity >= 0.1f;
                            float maxRange = isDay ? (this.m_dayChaseRange + 6f) : (this.m_nightChaseRange + 6f);
                            float maxChaseTime = isDay ? 
                                (this.m_dayChaseTime * this.m_random.Float(0.75f, 1f)) : 
                                (this.m_nightChaseTime * this.m_random.Float(0.75f, 1f));
                            
                            this.Attack(componentCreature, maxRange, maxChaseTime, !isDay);
                        }
                    }
                }
            }, null);

            this.m_stateMachine.AddState("RandomMoving", delegate
            {
                this.m_componentPathfinding.SetDestination(new Vector3?(
                    this.m_componentCreature.ComponentBody.Position + 
                    new Vector3(6f * this.m_random.Float(-1f, 1f), 0f, 6f * this.m_random.Float(-1f, 1f))), 
                    1f, 1f, 0, false, true, false, null);
            }, delegate
            {
                if (!this.m_componentPathfinding.IsStuck || this.m_componentPathfinding.Destination == null)
                {
                    this.m_stateMachine.TransitionTo("Chasing");
                }
                if (!this.IsActive)
                {
                    this.m_stateMachine.TransitionTo("LookingForTarget");
                }
            }, delegate
            {
                this.m_componentPathfinding.Stop();
            });

            this.m_stateMachine.AddState("Chasing", delegate
            {
                this.m_subsystemNoise.MakeNoise(this.m_componentCreature.ComponentBody, 0.25f, 6f);
                if (this.PlayIdleSoundWhenStartToChase)
                {
                    this.m_componentCreature.ComponentCreatureSounds.PlayIdleSound(false);
                }
                this.m_nextUpdateTime = 0.0;
            }, delegate
            {
                if (!this.IsActive)
                {
                    this.m_stateMachine.TransitionTo("LookingForTarget");
                }
                else if (this.m_chaseTime <= 0f)
                {
                    this.m_autoChaseSuppressionTime = this.m_random.Float(10f, 60f);
                    this.m_importanceLevel = 0f;
                }
                else if (this.m_target == null)
                {
                    this.m_importanceLevel = 0f;
                }
                else if (this.m_target.ComponentHealth.Health <= 0f)
                {
                    if (this.m_componentFeedBehavior != null)
                    {
                        this.m_subsystemTime.QueueGameTimeDelayedExecution(
                            this.m_subsystemTime.GameTime + (double)this.m_random.Float(1f, 3f), delegate
                            {
                                if (this.m_target != null)
                                {
                                    this.m_componentFeedBehavior.Feed(this.m_target.ComponentBody.Position);
                                }
                            });
                    }
                    this.m_importanceLevel = 0f;
                }
                else if (this.m_componentPathfinding.IsStuck)
                {
                    this.m_stateMachine.TransitionTo("RandomMoving");
                }
                else
                {
                    this.m_targetUnsuitableTime = ((this.ScoreTarget(this.m_target) <= 0f) ? 
                        (this.m_targetUnsuitableTime + this.m_dt) : 0f);
                    
                    if (this.m_targetUnsuitableTime > 3f)
                    {
                        this.m_importanceLevel = 0f;
                    }
                    else
                    {
                        int maxPathfindingPositions = 0;
                        if (this.m_isPersistent)
                        {
                            maxPathfindingPositions = (this.m_subsystemTime.FixedTimeStep != null) ? 2000 : 500;
                        }
                        
                        BoundingBox selfBox = this.m_componentCreature.ComponentBody.BoundingBox;
                        BoundingBox targetBox = this.m_target.ComponentBody.BoundingBox;
                        
                        Vector3 selfCenter = 0.5f * (selfBox.Min + selfBox.Max);
                        Vector3 targetCenter = 0.5f * (targetBox.Min + targetBox.Max);
                        
                        float distance = Vector3.Distance(selfCenter, targetCenter);
                        float velocityFactor = (distance < 4f) ? 0.2f : 0f;
                        
                        if (this.m_attackMode != AttackMode.OnlyHand && distance > 5f && 
                            this.FindAimTool(this.m_componentMiner))
                        {
                            float rayDistance;
                            this.GetHitBody1(this.m_target.ComponentBody, out rayDistance);
                            
                            if (rayDistance < this.m_attackRange.X + 3f)
                            {
                                Vector2 dirFromTarget = Vector2.Normalize(
                                    this.m_componentCreature.ComponentBody.Position.XZ - 
                                    (this.m_target.ComponentBody.Position + 0.5f * this.m_target.ComponentBody.Velocity).XZ);
                                
                                Vector2 bestDir = Vector2.Zero;
                                float bestDot = float.MinValue;
                                
                                for (float angle = 0f; angle < 6.2831855f; angle += 0.1f)
                                {
                                    Vector2 testDir = Vector2.CreateFromAngle(angle);
                                    if (Vector2.Dot(testDir, dirFromTarget) > 0.2f)
                                    {
                                        float forwardDot = Vector2.Dot(
                                            this.m_componentCreature.ComponentBody.Matrix.Forward.XZ, testDir);
                                        if (forwardDot > bestDot)
                                        {
                                            bestDir = testDir;
                                            bestDot = forwardDot;
                                        }
                                    }
                                }
                                
                                float moveDistance = 4f;
                                this.m_componentPathfinding.SetDestination(new Vector3?(
                                    selfCenter + moveDistance * new Vector3(bestDir.X, 0f, bestDir.Y)), 
                                    1f, 1f, 0, true, true, false, null);
                            }
                            else if (rayDistance > this.m_attackRange.Y)
                            {
                                this.m_componentPathfinding.SetDestination(new Vector3?(
                                    targetCenter + velocityFactor * distance * this.m_target.ComponentBody.Velocity), 
                                    1f, 1.5f, maxPathfindingPositions, true, false, true, this.m_target.ComponentBody);
                            }
                        }
                        else
                        {
                            this.m_componentPathfinding.SetDestination(new Vector3?(
                                targetCenter + velocityFactor * distance * this.m_target.ComponentBody.Velocity), 
                                1f, 1.5f, maxPathfindingPositions, true, false, true, this.m_target.ComponentBody);
                        }
                        
                        if (this.PlayAngrySoundWhenChasing && 
                            this.m_random.Float(0f, 1f) < 0.33f * this.m_dt)
                        {
                            this.m_componentCreature.ComponentCreatureSounds.PlayAttackSound();
                        }
                    }
                }
            }, null);

            this.m_stateMachine.TransitionTo("LookingForTarget");
        }

        public virtual ComponentCreature FindTarget()
        {
            Vector3 position = this.m_componentCreature.ComponentBody.Position;
            ComponentCreature result = null;
            float bestScore = 0f;
            
            this.m_componentBodies.Clear();
            this.m_subsystemBodies.FindBodiesAroundPoint(new Vector2(position.X, position.Z), 
                this.m_range, this.m_componentBodies);
            
            for (int i = 0; i < this.m_componentBodies.Count; i++)
            {
                ComponentCreature creature = this.m_componentBodies.Array[i].Entity.FindComponent<ComponentCreature>();
                if (creature != null)
                {
                    float score = this.ScoreTarget(creature);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        result = creature;
                    }
                }
            }
            return result;
        }

        public virtual float ScoreTarget(ComponentCreature componentCreature)
        {
            float result = 0f;
            bool isPlayer = componentCreature.Entity.FindComponent<ComponentPlayer>() != null;
            bool isTargetEligible = componentCreature == this.Target || 
                this.m_subsystemGameInfo.WorldSettings.GameMode > GameMode.Harmless;
            bool hasAutoChaseMask = this.m_autoChaseMask > (CreatureCategory)0;
            bool isNonPlayerEligible = componentCreature == this.Target || 
                (hasAutoChaseMask && MathUtils.Remainder(
                    0.005 * this.m_subsystemTime.GameTime + 
                    (double)((float)(this.GetHashCode() % 1000) / 1000f) + 
                    (double)((float)(componentCreature.GetHashCode() % 1000) / 1000f), 1.0) < 
                    (double)this.m_chaseNonPlayerProbability);

            ComponentNewHerdBehavior newHerd = base.Entity.FindComponent<ComponentNewHerdBehavior>();
            bool canAttack = true;
            
            if (newHerd != null)
            {
                canAttack = newHerd.CanAttackCreature(componentCreature);
            }
            else
            {
                ComponentHerdBehavior oldHerd = base.Entity.FindComponent<ComponentHerdBehavior>();
                if (oldHerd != null)
                {
                    ComponentHerdBehavior targetHerd = componentCreature.Entity.FindComponent<ComponentHerdBehavior>();
                    if (targetHerd != null && !string.IsNullOrEmpty(targetHerd.HerdName))
                    {
                        bool isSameHerd = targetHerd.HerdName == oldHerd.HerdName;

                        if (!isSameHerd && oldHerd.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase))
                        {
                            if (targetHerd.HerdName.ToLower().Contains("guardian"))
                                canAttack = false;
                        }
                        else if (!isSameHerd && oldHerd.HerdName.ToLower().Contains("guardian"))
                        {
                            if (targetHerd.HerdName.Equals("player", StringComparison.OrdinalIgnoreCase))
                                canAttack = false;
                        }
                        else if (isSameHerd)
                        {
                            canAttack = false;
                        }
                    }
                }
            }

            bool isValid = componentCreature != this.m_componentCreature && canAttack && 
                          ((!isPlayer && isNonPlayerEligible) || (isPlayer && isTargetEligible)) && 
                          componentCreature.Entity.IsAddedToProject && 
                          componentCreature.ComponentHealth.Health > 0f;

            if (isValid)
            {
                float distance = Vector3.Distance(this.m_componentCreature.ComponentBody.Position, 
                    componentCreature.ComponentBody.Position);
                if (distance < this.m_range)
                {
                    result = this.m_range - distance;
                }
            }

            if (IsGuardianOrPlayerAlly() && IsBandit(componentCreature) && isValid)
            {
                result *= 1.5f;
            }

            return result;
        }

        public virtual bool IsTargetInWater(ComponentBody target)
        {
            return target.ImmersionDepth > 0f || 
                   (target.ParentBody != null && this.IsTargetInWater(target.ParentBody)) || 
                   (target.StandingOnBody != null && target.StandingOnBody.Position.Y < target.Position.Y && 
                    this.IsTargetInWater(target.StandingOnBody));
        }

        public virtual bool IsTargetInAttackRange(ComponentBody target)
        {
            if (this.IsBodyInAttackRange(target))
                return true;

            BoundingBox selfBox = this.m_componentCreature.ComponentBody.BoundingBox;
            BoundingBox targetBox = target.BoundingBox;
            
            Vector3 selfCenter = 0.5f * (selfBox.Min + selfBox.Max);
            Vector3 toTarget = 0.5f * (targetBox.Min + targetBox.Max) - selfCenter;
            
            float distance = toTarget.Length();
            Vector3 direction = toTarget / distance;
            
            float combinedHalfWidth = 0.5f * (selfBox.Max.X - selfBox.Min.X + targetBox.Max.X - targetBox.Min.X);
            float combinedHalfHeight = 0.5f * (selfBox.Max.Y - selfBox.Min.Y + targetBox.Max.Y - targetBox.Min.Y);

            if (Math.Abs(toTarget.Y) < combinedHalfHeight * 0.99f)
            {
                if (distance < combinedHalfWidth + 0.99f && 
                    Vector3.Dot(direction, this.m_componentCreature.ComponentBody.Matrix.Forward) > 0.25f)
                {
                    return true;
                }
            }
            else if (distance < combinedHalfHeight + 0.3f && 
                     Math.Abs(Vector3.Dot(direction, Vector3.UnitY)) > 0.8f)
            {
                return true;
            }

            return (target.ParentBody != null && this.IsTargetInAttackRange(target.ParentBody)) ||
                   (this.AllowAttackingStandingOnBody && target.StandingOnBody != null && 
                    target.StandingOnBody.Position.Y < target.Position.Y && 
                    this.IsTargetInAttackRange(target.StandingOnBody));
        }

        public virtual bool IsBodyInAttackRange(ComponentBody target)
        {
            BoundingBox selfBox = this.m_componentCreature.ComponentBody.BoundingBox;
            BoundingBox targetBox = target.BoundingBox;
            
            Vector3 selfCenter = 0.5f * (selfBox.Min + selfBox.Max);
            Vector3 toTarget = 0.5f * (targetBox.Min + targetBox.Max) - selfCenter;
            
            float distance = toTarget.Length();
            Vector3 direction = toTarget / distance;
            
            float combinedHalfWidth = 0.5f * (selfBox.Max.X - selfBox.Min.X + targetBox.Max.X - targetBox.Min.X);
            float combinedHalfHeight = 0.5f * (selfBox.Max.Y - selfBox.Min.Y + targetBox.Max.Y - targetBox.Min.Y);

            if (Math.Abs(toTarget.Y) < combinedHalfHeight * 0.99f)
            {
                if (distance < combinedHalfWidth + 0.99f && 
                    Vector3.Dot(direction, this.m_componentCreature.ComponentBody.Matrix.Forward) > 0.25f)
                {
                    return true;
                }
            }
            else if (distance < combinedHalfHeight + 0.3f && 
                     Math.Abs(Vector3.Dot(direction, Vector3.UnitY)) > 0.8f)
            {
                return true;
            }

            return false;
        }

        public virtual ComponentBody GetHitBody(ComponentBody target, out Vector3 hitPoint)
        {
            Vector3 start = this.m_componentCreature.ComponentBody.BoundingBox.Center();
            Vector3 toTarget = target.BoundingBox.Center() - start;
            Ray3 ray = new Ray3(start, Vector3.Normalize(toTarget));
            
            BodyRaycastResult? bodyResult = this.m_componentMiner.Raycast<BodyRaycastResult>(
                ray, RaycastMode.Interaction, true, true, true, null);
                
            if (bodyResult != null && bodyResult.Value.Distance < this.MaxAttackRange && 
                (bodyResult.Value.ComponentBody == target || 
                 bodyResult.Value.ComponentBody.IsChildOfBody(target) || 
                 target.IsChildOfBody(bodyResult.Value.ComponentBody) || 
                 (target.StandingOnBody == bodyResult.Value.ComponentBody && this.AllowAttackingStandingOnBody)))
            {
                hitPoint = bodyResult.Value.HitPoint();
                return bodyResult.Value.ComponentBody;
            }
            
            hitPoint = default(Vector3);
            return null;
        }

        // Variables miembro
        public SubsystemGameInfo m_subsystemGameInfo;
        public SubsystemPlayers m_subsystemPlayers;
        public SubsystemSky m_subsystemSky;
        public SubsystemBodies m_subsystemBodies;
        public SubsystemTime m_subsystemTime;
        public SubsystemNoise m_subsystemNoise;
        public SubsystemCreatureSpawn m_subsystemCreatureSpawn;
        public SubsystemAudio m_subsystemAudio;
        public SubsystemParticles m_subsystemParticles;
        public SubsystemTerrain m_subsystemTerrain;
        public SubsystemGreenNightSky m_subsystemGreenNightSky;
        public ComponentCreature m_componentCreature;
        public ComponentPathfinding m_componentPathfinding;
        public ComponentMiner m_componentMiner;
        public ComponentRandomFeedBehavior m_componentFeedBehavior;
        public ComponentCreatureModel m_componentCreatureModel;
        public ComponentFactors m_componentFactors;
        public ComponentBody m_componentBody;
        public DynamicArray<ComponentBody> m_componentBodies = new DynamicArray<ComponentBody>();
        public Random m_random = new Random();
        public StateMachine m_stateMachine = new StateMachine();
        public float m_dayChaseRange;
        public float m_nightChaseRange;
        public float m_dayChaseTime;
        public float m_nightChaseTime;
        public float m_chaseNonPlayerProbability;
        public float m_chaseWhenAttackedProbability;
        public float m_chaseOnTouchProbability;
        public CreatureCategory m_autoChaseMask;
        public float m_importanceLevel;
        public float m_targetUnsuitableTime;
        public float m_targetInRangeTime;
        public double m_nextUpdateTime;
        public double m_nextPlayerCheckTime;
        public double m_nextHighAlertCheckTime;
        public ComponentCreature m_target;
        public float m_dt;
        public float m_range;
        public float m_chaseTime;
        public bool m_isPersistent;
        public bool m_autoDismount = true;
        public float m_autoChaseSuppressionTime;
        private AttackMode m_attackMode = AttackMode.Default;
        private Vector2 m_attackRange = new Vector2(2f, 15f);
        public float ImportanceLevelNonPersistent = 200f;
        public float ImportanceLevelPersistent = 200f;
        public float MaxAttackRange = 1.75f;
        public bool AllowAttackingStandingOnBody = true;
        public bool JumpWhenTargetStanding = true;
        public bool AttacksPlayer = true;
        public bool AttacksNonPlayerCreature = true;
        public float ChaseRangeOnTouch = 7f;
        public float ChaseTimeOnTouch = 7f;
        public float? ChaseRangeOnAttacked;
        public float? ChaseTimeOnAttacked;
        public bool? ChasePersistentOnAttacked;
        public float MinHealthToAttackActively = 0.4f;
        public bool Suppressed;
        public bool PlayIdleSoundWhenStartToChase = true;
        public bool PlayAngrySoundWhenChasing = true;
        public float TargetInRangeTimeToChase = 3f;

        public enum AttackMode
        {
            Default,
            OnlyHand,
            Remote
        }
    }
}
