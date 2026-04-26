using Engine;
using Engine.Graphics;
using Engine.Serialization;
using GameEntitySystem;
using TemplatesDatabase;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game
{
    public class ComponentCreatureClothing : Component, IInventory, IUpdateable
    {
        // Diccionario de ranuras
        public Dictionary<ClothingSlot, List<int>> m_clothes = new Dictionary<ClothingSlot, List<int>>();

        // Componentes necesarios
        public ComponentCreature m_componentCreature;
        public ComponentHumanModel m_componentHumanModel;
        public ComponentOuterClothingModel m_componentOuterClothingModel;
        public ComponentBody m_componentBody;
        public SubsystemTerrain m_subsystemTerrain;
        public SubsystemGameInfo m_subsystemGameInfo;
        public SubsystemModelsRenderer m_subsystemModelsRenderer;
        public SubsystemAudio m_subsystemAudio;
        public SubsystemParticles m_subsystemParticles;
        public Random m_random = new Random();

        // Texturas de ropa generadas
        public RenderTarget2D m_innerClothedTexture;
        public RenderTarget2D m_outerClothedTexture;
        public PrimitivesRenderer2D m_primitivesRenderer = new PrimitivesRenderer2D();
        public bool m_clothedTexturesValid;

        // Para IInventory
        Project IInventory.Project => Project;
        public int SlotsCount => 4;
        public int VisibleSlotsCount { get; set; } = 4;
        public int ActiveSlotIndex { get; set; } = -1;

        public UpdateOrder UpdateOrder => UpdateOrder.Default;

		// Mapeo de índices del inventario a ClothingSlot
		private static readonly ClothingSlot[] m_slotsOrder = new[]
		{
				ClothingSlot.Head,
				ClothingSlot.Torso,
				ClothingSlot.Legs,
				ClothingSlot.Feet
			};

		public static int GetClothingSlotIndex(ClothingSlot slot)
		{
			return Array.IndexOf(m_slotsOrder, slot);
		}

		// -------------------------------------------------------------
		// INICIALIZACIÓN
		// -------------------------------------------------------------
		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
        {
            m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
            m_subsystemModelsRenderer = Project.FindSubsystem<SubsystemModelsRenderer>(true);
            m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(true);
            m_subsystemParticles = Project.FindSubsystem<SubsystemParticles>(true);
            m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);

            m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
            m_componentBody = m_componentCreature.ComponentBody;
            m_componentHumanModel = Entity.FindComponent<ComponentHumanModel>(true);
            m_componentOuterClothingModel = Entity.FindComponent<ComponentOuterClothingModel>(true);

            // Inicializar ranuras vacías
            foreach (ClothingSlot slot in ClothingSlot.ClothingSlots.Values)
            {
                m_clothes[slot] = new List<int>();
            }

            // Cargar datos persistentes
            ValuesDictionary clothesData = valuesDictionary.GetValue<ValuesDictionary>("CreatureClothes", null);
            if (clothesData != null)
            {
                foreach (string key in ClothingSlot.ClothingSlots.Keys)
                {
                    List<int> loadedClothes = HumanReadableConverter.ValuesListFromString<int>(
                        ';', clothesData.GetValue<string>(key, "")).ToList();
                    m_clothes[ClothingSlot.ClothingSlots[key]] = loadedClothes;
                }
            }

            Display.DeviceReset += Display_DeviceReset;
        }

        public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
        {
            var clothesSave = new ValuesDictionary();
            foreach (var kv in m_clothes)
            {
                clothesSave.SetValue<string>(kv.Key.Name,
                    HumanReadableConverter.ValuesListToString<int>(';', kv.Value.ToArray()));
            }
            valuesDictionary.SetValue<ValuesDictionary>("CreatureClothes", clothesSave);
        }

        public override void Dispose()
        {
            base.Dispose();
            Utilities.Dispose(ref m_innerClothedTexture);
            Utilities.Dispose(ref m_outerClothedTexture);
            Display.DeviceReset -= Display_DeviceReset;
        }

        private void Display_DeviceReset()
        {
            m_clothedTexturesValid = false;
        }

        // -------------------------------------------------------------
        // COLOCAR / QUITAR ROPA
        // -------------------------------------------------------------
        public void SetClothes(ClothingSlot slot, IEnumerable<int> newClothes)
        {
            List<int> clothesList = newClothes.ToList();
            if (!m_clothes[slot].SequenceEqual(clothesList))
            {
                foreach (int oldValue in m_clothes[slot].Except(clothesList))
                {
                    ClothingData data = GetClothingData(oldValue);
                    data?.Dismount?.Invoke(oldValue, null);
                }

                foreach (int newValue in clothesList.Except(m_clothes[slot]))
                {
                    ClothingData data = GetClothingData(newValue);
                    data?.Mount?.Invoke(newValue, null);
                }

                m_clothes[slot] = clothesList;
                m_clothedTexturesValid = false;
            }
        }

        public ReadOnlyList<int> GetClothes(ClothingSlot slot)
        {
            return new ReadOnlyList<int>(m_clothes[slot]);
        }

        private ClothingData GetClothingData(int value)
        {
            Block block = BlocksManager.Blocks[Terrain.ExtractContents(value)];
            return block.GetClothingData(value);
        }

        public bool CanWearClothing(int value)
        {
            ClothingData data = GetClothingData(value);
            if (data == null) return false;
            List<int> current = m_clothes[data.Slot];
            if (current.Count == 0) return true;
            int lastValue = current[current.Count - 1];
            ClothingData lastData = GetClothingData(lastValue);
            return lastData != null && data.Layer > lastData.Layer;
        }

        // -------------------------------------------------------------
        // IMPLEMENTACIÓN DE IInventory
        // -------------------------------------------------------------
        public int GetSlotValue(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= 4) return 0;
            var list = m_clothes[m_slotsOrder[slotIndex]];
            return list.Count > 0 ? list[list.Count - 1] : 0;
        }

        public int GetSlotCount(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= 4) return 0;
            return m_clothes[m_slotsOrder[slotIndex]].Count > 0 ? 1 : 0;
        }

        public int GetSlotCapacity(int slotIndex, int value) => 0;

        public int GetSlotProcessCapacity(int slotIndex, int value)
        {
            Block block = BlocksManager.Blocks[Terrain.ExtractContents(value)];
            return (block.CanWear(value) && CanWearClothing(value)) ? 1 : 0;
        }

        public void AddSlotItems(int slotIndex, int value, int count) { }

        public void ProcessSlotItems(int slotIndex, int value, int count, int processCount, out int processedValue, out int processedCount)
        {
            processedValue = 0;
            processedCount = 0;
            if (slotIndex < 0 || slotIndex >= 4 || count <= 0 || processCount != 1) return;

            Block block = BlocksManager.Blocks[Terrain.ExtractContents(value)];
            if (block.CanWear(value) && CanWearClothing(value))
            {
                ClothingData data = GetClothingData(value);
                if (data != null)
                {
                    data.Mount?.Invoke(value, null);
                    ClothingSlot slot = m_slotsOrder[slotIndex];
                    List<int> newList = new List<int>(m_clothes[slot]);
                    newList.Add(value);
                    SetClothes(slot, newList);
                    processedValue = value;
                    processedCount = 1;
                }
            }
        }

        public int RemoveSlotItems(int slotIndex, int count)
        {
            if (slotIndex < 0 || slotIndex >= 4 || count != 1) return 0;
            ClothingSlot slot = m_slotsOrder[slotIndex];
            List<int> current = m_clothes[slot];
            if (current.Count == 0) return 0;

            int lastValue = current[current.Count - 1];
            ClothingData data = GetClothingData(lastValue);
            data?.Dismount?.Invoke(lastValue, null);

            List<int> newList = new List<int>(current);
            newList.RemoveAt(newList.Count - 1);
            SetClothes(slot, newList);
            return 1;
        }

        public void DropAllItems(Vector3 position)
        {
            Random rand = new Random();
            SubsystemPickables pickables = Project.FindSubsystem<SubsystemPickables>(true);
            for (int i = 0; i < 4; i++)
            {
                if (GetSlotCount(i) > 0)
                {
                    int value = GetSlotValue(i);
                    int removed = RemoveSlotItems(i, 1);
                    Vector3 velocity = rand.Float(5f, 10f) * Vector3.Normalize(
                        new Vector3(rand.Float(-1f, 1f), rand.Float(1f, 2f), rand.Float(-1f, 1f)));
                    pickables.AddPickable(value, removed, position, new Vector3?(velocity), null, Entity);
                }
            }
        }

        // -------------------------------------------------------------
        // IUpdateable - Actualizar texturas de ropa cada frame
        // -------------------------------------------------------------
        public void Update(float dt)
        {
            UpdateRenderTargets();
        }

        private void UpdateRenderTargets()
        {
            // CORRECTO: solo TextureOverride, que es la propiedad pública que asigna el motor
            Texture2D skinTexture = m_componentHumanModel.TextureOverride;
            if (skinTexture == null) return;

            // Crear o recrear RenderTargets si es necesario
            if (m_innerClothedTexture == null || m_innerClothedTexture.Width != skinTexture.Width || m_innerClothedTexture.Height != skinTexture.Height)
            {
                Utilities.Dispose(ref m_innerClothedTexture);
                m_innerClothedTexture = new RenderTarget2D(skinTexture.Width, skinTexture.Height, 1, ColorFormat.Rgba8888, DepthFormat.None);
                m_componentHumanModel.TextureOverride = m_innerClothedTexture;
                m_clothedTexturesValid = false;
            }
            if (m_outerClothedTexture == null || m_outerClothedTexture.Width != skinTexture.Width || m_outerClothedTexture.Height != skinTexture.Height)
            {
                Utilities.Dispose(ref m_outerClothedTexture);
                m_outerClothedTexture = new RenderTarget2D(skinTexture.Width, skinTexture.Height, 1, ColorFormat.Rgba8888, DepthFormat.None);
                m_componentOuterClothingModel.TextureOverride = m_outerClothedTexture;
                m_clothedTexturesValid = false;
            }

            if (!m_clothedTexturesValid)
            {
                m_clothedTexturesValid = true;
                Rectangle oldScissor = Display.ScissorRectangle;
                RenderTarget2D oldTarget = Display.RenderTarget;
                try
                {
                    // Generar textura interior (piel base + ropa interior)
                    Display.RenderTarget = m_innerClothedTexture;
                    Display.Clear(new Vector4?(new Vector4(Color.Transparent)), null, null);
                    int batchIndex = 0;
                    // Dibujar piel base
                    TexturedBatch2D batch = m_primitivesRenderer.TexturedBatch(skinTexture, false, batchIndex++, DepthStencilState.None, null, BlendState.NonPremultiplied, SamplerState.PointClamp);
                    batch.QueueQuad(Vector2.Zero, new Vector2(skinTexture.Width, skinTexture.Height), 0f, Vector2.Zero, Vector2.One, Color.White);

                    // Dibujar ropa interior (orden: m_innerSlotsOrder)
                    foreach (ClothingSlot slot in ComponentClothing.m_innerSlotsOrder)
                    {
                        foreach (int value in m_clothes[slot])
                        {
                            ClothingData data = GetClothingData(value);
                            if (data == null || data.IsOuter) continue;
                            if (data.Texture == null && !string.IsNullOrEmpty(data._textureName))
                                data.Texture = ContentManager.Get<Texture2D>(data._textureName);
                            Color color = data.GetColor(null, value);
                            batch = m_primitivesRenderer.TexturedBatch(data.Texture, false, batchIndex++, DepthStencilState.None, null, BlendState.NonPremultiplied, SamplerState.PointClamp);
                            batch.QueueQuad(Vector2.Zero, new Vector2(skinTexture.Width, skinTexture.Height), 0f, Vector2.Zero, Vector2.One, color);
                        }
                    }
                    m_primitivesRenderer.Flush(true, int.MaxValue);

                    // Generar textura exterior (solo ropa exterior)
                    Display.RenderTarget = m_outerClothedTexture;
                    Display.Clear(new Vector4?(new Vector4(Color.Transparent)), null, null);
                    batchIndex = 0;
                    foreach (ClothingSlot slot in ComponentClothing.m_outerSlotsOrder)
                    {
                        foreach (int value in m_clothes[slot])
                        {
                            ClothingData data = GetClothingData(value);
                            if (data == null || !data.IsOuter) continue;
                            if (data.Texture == null && !string.IsNullOrEmpty(data._textureName))
                                data.Texture = ContentManager.Get<Texture2D>(data._textureName);
                            Color color = data.GetColor(null, value);
                            batch = m_primitivesRenderer.TexturedBatch(data.Texture, false, batchIndex++, DepthStencilState.None, null, BlendState.NonPremultiplied, SamplerState.PointClamp);
                            batch.QueueQuad(Vector2.Zero, new Vector2(skinTexture.Width, skinTexture.Height), 0f, Vector2.Zero, Vector2.One, color);
                        }
                    }
                    m_primitivesRenderer.Flush(true, int.MaxValue);
                }
                finally
                {
                    Display.RenderTarget = oldTarget;
                    Display.ScissorRectangle = oldScissor;
                }
            }
        }
    }
}
