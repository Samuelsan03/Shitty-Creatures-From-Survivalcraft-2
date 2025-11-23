using Engine;
using Engine.Graphics;
using GameEntitySystem;
using System;
using System.Collections.Generic;
using TemplatesDatabase;


namespace Game
{
    public class ComponentCreatureCollect : Component, IUpdateable
    {
        public SubsystemGameInfo m_subsystemGameInfo;
        public SubsystemTerrain subsystemTerrain;
        public SubsystemPickables subsystemPickables;
        public ComponentCreature componentCreature;
        private ComponentPathfinding m_componentPathfinding;
        public ComponentMiner m_componentMiner;
        public bool Activate;
        public bool CanOpenInventory;
        public float DetectionDistance;
        public string SpecificItems;
        public bool IgnoreOrAcept;
        private HashSet<int> m_specificItemsSet = new HashSet<int>(); // Nuevo campo para almacenar IDs de bloques

        public UpdateOrder UpdateOrder => UpdateOrder.Default;

        public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
        {
            base.Load(valuesDictionary, idToEntityMap);
            this.m_componentMiner = this.Entity.FindComponent<ComponentMiner>(true);
            this.subsystemTerrain = this.Project.FindSubsystem<SubsystemTerrain>(true);
            this.subsystemPickables = this.Project.FindSubsystem<SubsystemPickables>(true);
            this.m_subsystemGameInfo = this.Project.FindSubsystem<SubsystemGameInfo>(true);
            this.componentCreature = this.Entity.FindComponent<ComponentCreature>();
            this.m_componentPathfinding = this.Entity.FindComponent<ComponentPathfinding>(true);
            this.Activate = valuesDictionary.GetValue<bool>("Activate");
            this.CanOpenInventory = valuesDictionary.GetValue<bool>("CanOpenInventory");
            this.DetectionDistance = valuesDictionary.GetValue<float>("DetectionDistance");
            this.SpecificItems = valuesDictionary.GetValue<string>("SpecificItems"); // Objetos especificos para recogerlos, valores definidos por ID o nombre de la clase del bloque, por ejemplo: 2 o DirtBlock. Se puede crear una cadena de bloques con valores mixtos, por ejemplo: DirtBlock,StoneBlock,3,4.
            this.IgnoreOrAcept = valuesDictionary.GetValue<bool>("IgnoreOrAcept"); // Ignorar o aceptar los objetos especificados de "SpecificItems", true para aceptar, false para ignorar.

            // Parsear SpecificItems y llenar el conjunto
            string[] specificItems = this.SpecificItems.Split(',');
            foreach (string item in specificItems)
            {
                if (string.IsNullOrWhiteSpace(item)) continue;

                if (int.TryParse(item, out int blockId))
                {
                    // Es un ID numérico
                    if (blockId > 0 && blockId < BlocksManager.Blocks.Length)
                    {
                        m_specificItemsSet.Add(blockId);
                    }
                }
                else
                {
                    // Es un nombre de clase (ej: "DirtBlock")
                    Type blockType = Type.GetType($"Game.{item}");
                    // CORRECCIÓN: Usar GetBlock en lugar de FindBlockByType
                    Block block = BlocksManager.GetBlock(blockType, false, true);
                    if (block != null)
                    {
                        m_specificItemsSet.Add(block.BlockIndex);
                    }
                }
            }
        }

        public void Update(float dt)
        {
            IInventory inventory = this.m_componentMiner.Inventory;
            ComponentInventory component1 = this.Entity.FindComponent<ComponentInventory>();
            ComponentChaseBehavior component2 = this.Entity.FindComponent<ComponentChaseBehavior>();

            if (!this.Activate)
                return;

            foreach (Pickable pickable in this.subsystemPickables.Pickables)
            {
                // ... (verificaciones existentes)

                int blockId = Terrain.ExtractContents(pickable.Value);
                bool isSpecificItem = m_specificItemsSet.Contains(blockId);

                // Aplicar filtro según configuración
                if (m_specificItemsSet.Count > 0)
                {
                    if (IgnoreOrAcept && !isSpecificItem) continue; // Solo aceptar específicos
                    if (!IgnoreOrAcept && isSpecificItem) continue; // Ignorar específicos
                }

                // ... (lógica existente de recolección)
                TerrainChunk chunkAtCell = this.subsystemTerrain.Terrain.GetChunkAtCell(Terrain.ToCell(pickable.Position.X), Terrain.ToCell(pickable.Position.Z));
                if (component1 != null && chunkAtCell != null && !pickable.FlyToPosition.HasValue && this.componentCreature.ComponentHealth.Health > 0.0 && component2.Target == null)
                {
                    Vector3 vector3_1 = this.componentCreature.ComponentBody.Position + new Vector3(0.0f, 0.8f, 0.0f);
                    Vector3 vector3_2 = vector3_1 - pickable.Position;
                    float num = vector3_2.LengthSquared();
                    float detectionSquared = this.DetectionDistance * this.DetectionDistance;

                    // Usar DetectionDistance para la detección inicial
                    if (num < detectionSquared)
                    {
                        for (int index = 0; index < inventory.SlotsCount; ++index)
                        {
                            int acquireSlotForItem = ComponentInventoryBase.FindAcquireSlotForItem(inventory, pickable.Value);
                            if (acquireSlotForItem >= 0)
                            {
                                this.m_componentPathfinding.SetDestination(new Vector3?(pickable.Position), 3f, 3.75f, 20, true, false, false, null);
                                break; // Salir después de encontrar un slot válido
                            }
                        }
                    }

                    // Mantener 4.0 como distancia fija para recoger (o ajustar si se desea)
                    if (num < 4.0)
                    {
                        for (int index = 0; index < inventory.SlotsCount; ++index)
                        {
                            int acquireSlotForItem = ComponentInventoryBase.FindAcquireSlotForItem(inventory, pickable.Value);
                            if (acquireSlotForItem >= 0)
                            {
                                pickable.ToRemove = true;
                                pickable.FlyToPosition = new Vector3?(vector3_1);
                                pickable.Count = ComponentInventoryBase.AcquireItems(component1, pickable.Value, pickable.Count);
                                break;
                            }
                        }
                    }
                }
            }
        }
    }
}
