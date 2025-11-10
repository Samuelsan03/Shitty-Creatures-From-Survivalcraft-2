using System;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
    public class ComponentRangedWeaponBehavior : ComponentBehavior, IUpdateable
    {
        public SubsystemTime m_subsystemTime;
        public SubsystemBodies m_subsystemBodies;
        public SubsystemPlayers m_subsystemPlayers;
        public SubsystemProjectiles m_subsystemProjectiles;
        public SubsystemAudio m_subsystemAudio;
        public ComponentCreature m_componentCreature;
        public ComponentMiner m_componentMiner;
        public ComponentInventory m_componentInventory;

        public float m_minAttackRange = 3f;
        public float m_maxAttackRange = 15f;
        public float m_attackInterval = 3f;
        public double m_lastAttackTime;
        public bool m_hasWeapon;
        public int m_currentWeaponSlot = -1;

        // Implementar la propiedad abstracta ImportanceLevel
        public override float ImportanceLevel
        {
            get { return m_hasWeapon ? 100f : 0f; }
        }

        public UpdateOrder UpdateOrder => UpdateOrder.Default;

        public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
        {
            base.Load(valuesDictionary, idToEntityMap);
            m_subsystemTime = Project.FindSubsystem<SubsystemTime>(true);
            m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(true);
            m_subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);
            m_subsystemProjectiles = Project.FindSubsystem<SubsystemProjectiles>(true);
            m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
            m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
            m_componentMiner = Entity.FindComponent<ComponentMiner>(true);
            m_componentInventory = Entity.FindComponent<ComponentInventory>(true);

            m_minAttackRange = valuesDictionary.GetValue<float>("MinAttackRange");
            m_maxAttackRange = valuesDictionary.GetValue<float>("MaxAttackRange");
            m_attackInterval = valuesDictionary.GetValue<float>("AttackInterval");
        }

        public void Update(float dt)
        {
            if (m_componentCreature.ComponentHealth.Health <= 0f)
                return;

            // Buscar arma en el inventario si no tenemos una
            if (!m_hasWeapon || m_currentWeaponSlot == -1)
            {
                FindRangedWeapon();
            }

            if (!m_hasWeapon)
                return;

            // Buscar objetivo
            ComponentBody target = FindTarget();
            if (target == null)
                return;

            // Verificar distancia
            float distance = Vector3.Distance(m_componentCreature.ComponentBody.Position, target.Position);
            if (distance < m_minAttackRange || distance > m_maxAttackRange)
                return;

            // Verificar si podemos atacar
            if (m_subsystemTime.GameTime - m_lastAttackTime < m_attackInterval)
                return;

            // Atacar al objetivo
            AttackTarget(target);
        }

        private void FindRangedWeapon()
        {
            for (int i = 0; i < m_componentInventory.SlotsCount; i++)
            {
                int slotValue = m_componentInventory.GetSlotValue(i);
                int contents = Terrain.ExtractContents(slotValue);

                if (contents == MusketBlock.Index || contents == BowBlock.Index || contents == CrossbowBlock.Index)
                {
                    m_currentWeaponSlot = i;
                    m_hasWeapon = true;
                    m_componentInventory.ActiveSlotIndex = i;
                    break;
                }
            }
        }

        private ComponentBody FindTarget()
        {
            Vector3 myPosition = m_componentCreature.ComponentBody.Position;
            ComponentBody nearestTarget = null;
            float nearestDistance = float.MaxValue;

            // Buscar jugadores enemigos
            foreach (ComponentPlayer player in m_subsystemPlayers.ComponentPlayers)
            {
                if (player.ComponentHealth.Health > 0f)
                {
                    float distance = Vector3.Distance(myPosition, player.ComponentBody.Position);
                    if (distance <= m_maxAttackRange && distance < nearestDistance)
                    {
                        nearestTarget = player.ComponentBody;
                        nearestDistance = distance;
                    }
                }
            }

            // También buscar otras criaturas que no sean de la misma manada
            // CORREGIDO: Usar Entities para buscar criaturas
            foreach (Entity entity in m_subsystemBodies.Project.Entities)
            {
                ComponentBody body = entity.FindComponent<ComponentBody>();
                if (body != null && body != m_componentCreature.ComponentBody && entity.FindComponent<ComponentCreature>() != null)
                {
                    ComponentCreature otherCreature = entity.FindComponent<ComponentCreature>();
                    if (otherCreature.ComponentHealth.Health > 0f)
                    {
                        // Verificar si es enemigo (no de la misma manada)
                        ComponentHerdBehavior myHerd = Entity.FindComponent<ComponentHerdBehavior>();
                        ComponentHerdBehavior otherHerd = entity.FindComponent<ComponentHerdBehavior>();

                        if (myHerd == null || otherHerd == null || myHerd.HerdName != otherHerd.HerdName)
                        {
                            float distance = Vector3.Distance(myPosition, body.Position);
                            if (distance <= m_maxAttackRange && distance < nearestDistance)
                            {
                                nearestTarget = body;
                                nearestDistance = distance;
                            }
                        }
                    }
                }
            }

            return nearestTarget;
        }

        private void AttackTarget(ComponentBody target)
        {
            int weaponValue = m_componentInventory.GetSlotValue(m_currentWeaponSlot);
            int weaponContents = Terrain.ExtractContents(weaponValue);
            int weaponData = Terrain.ExtractData(weaponValue);

            Vector3 myPosition = m_componentCreature.ComponentCreatureModel.EyePosition;
            Vector3 targetPosition = target.Position + new Vector3(0f, 1f, 0f); // Apuntar al centro del cuerpo
            Vector3 direction = Vector3.Normalize(targetPosition - myPosition);

            // Añadir algo de aleatoriedad para que no sea perfecto
            Random random = new Random();
            direction += new Vector3(
                random.Float(-0.1f, 0.1f),
                random.Float(-0.05f, 0.05f),
                random.Float(-0.1f, 0.1f)
            );
            direction = Vector3.Normalize(direction);

            switch (weaponContents)
            {
                case int _ when weaponContents == MusketBlock.Index:
                    FireMusket(myPosition, direction, weaponData);
                    break;
                case int _ when weaponContents == BowBlock.Index:
                    FireBow(myPosition, direction, weaponData);
                    break;
                case int _ when weaponContents == CrossbowBlock.Index:
                    FireCrossbow(myPosition, direction, weaponData);
                    break;
            }

            m_lastAttackTime = m_subsystemTime.GameTime;

            // Reproducir sonido de ataque
            ComponentCreatureSounds sounds = Entity.FindComponent<ComponentCreatureSounds>();
            if (sounds != null)
            {
                sounds.PlayAttackSound();
            }
        }

        private void FireMusket(Vector3 position, Vector3 direction, int data)
        {
            // Asegurar que el mosquete esté cargado y con el martillo montado
            if (MusketBlock.GetLoadState(data) != MusketBlock.LoadState.Loaded)
            {
                // Recargar automáticamente - simular munición infinita
                data = MusketBlock.SetLoadState(data, MusketBlock.LoadState.Loaded);
                data = MusketBlock.SetBulletType(data, BulletBlock.BulletType.MusketBall);
            }
            if (!MusketBlock.GetHammerState(data))
            {
                data = MusketBlock.SetHammerState(data, true);
            }

            // Actualizar el arma en el inventario
            m_componentInventory.RemoveSlotItems(m_currentWeaponSlot, 1);
            m_componentInventory.AddSlotItems(m_currentWeaponSlot, Terrain.MakeBlockValue(MusketBlock.Index, 0, data), 1);

            // Disparar proyectil
            int bulletValue = Terrain.MakeBlockValue(214, 0, BulletBlock.SetBulletType(0, BulletBlock.BulletType.MusketBall));
            Vector3 velocity = direction * 120f;

            Projectile projectile = m_subsystemProjectiles.FireProjectile(bulletValue, position, velocity, Vector3.Zero, m_componentCreature);
            if (projectile != null)
            {
                projectile.ProjectileStoppedAction = ProjectileStoppedAction.Disappear;
            }

            // Sonido y efectos
            m_subsystemAudio.PlaySound("Audio/MusketFire", 1f, 0f, position, 10f, true);
        }

        private void FireBow(Vector3 position, Vector3 direction, int data)
        {
            // Asegurar que el arco tenga flecha
            ArrowBlock.ArrowType? arrowType = BowBlock.GetArrowType(data);
            if (arrowType == null)
            {
                // Recargar automáticamente - simular munición infinita
                arrowType = ArrowBlock.ArrowType.WoodenArrow;
                data = BowBlock.SetArrowType(data, arrowType);
                data = BowBlock.SetDraw(data, 15); // Arco totalmente tensado
            }

            // Actualizar el arma en el inventario
            m_componentInventory.RemoveSlotItems(m_currentWeaponSlot, 1);
            m_componentInventory.AddSlotItems(m_currentWeaponSlot, Terrain.MakeBlockValue(BowBlock.Index, 0, data), 1);

            // Disparar flecha
            int arrowValue = Terrain.MakeBlockValue(192, 0, ArrowBlock.SetArrowType(0, arrowType.Value));
            Vector3 velocity = direction * 28f;

            m_subsystemProjectiles.FireProjectile(arrowValue, position, velocity, Vector3.Zero, m_componentCreature);
            m_subsystemAudio.PlaySound("Audio/Bow", 1f, 0f, position, 3f, true);
        }

        private void FireCrossbow(Vector3 position, Vector3 direction, int data)
        {
            // Asegurar que la ballesta esté tensada y tenga proyectil
            if (CrossbowBlock.GetDraw(data) != 15)
            {
                data = CrossbowBlock.SetDraw(data, 15);
            }

            ArrowBlock.ArrowType? arrowType = CrossbowBlock.GetArrowType(data);
            if (arrowType == null)
            {
                // Recargar automáticamente - simular munición infinita
                arrowType = ArrowBlock.ArrowType.IronBolt;
                data = CrossbowBlock.SetArrowType(data, arrowType);
            }

            // Actualizar el arma en el inventario
            m_componentInventory.RemoveSlotItems(m_currentWeaponSlot, 1);
            m_componentInventory.AddSlotItems(m_currentWeaponSlot, Terrain.MakeBlockValue(CrossbowBlock.Index, 0, data), 1);

            // Disparar proyectil
            int boltValue = Terrain.MakeBlockValue(192, 0, ArrowBlock.SetArrowType(0, arrowType.Value));
            Vector3 velocity = direction * 38f;

            m_subsystemProjectiles.FireProjectile(boltValue, position, velocity, Vector3.Zero, m_componentCreature);
            m_subsystemAudio.PlaySound("Audio/Bow", 1f, 0f, position, 3f, true);
        }
    }
}