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
		public ComponentNewHumanModel m_componentNewHumanModel;
		public ComponentOuterClothingModel m_componentOuterClothingModel;
		public ComponentBody m_componentBody;
		public ComponentHealth m_componentHealth;
		public SubsystemTerrain m_subsystemTerrain;
		public SubsystemGameInfo m_subsystemGameInfo;
		public SubsystemModelsRenderer m_subsystemModelsRenderer;
		public SubsystemAudio m_subsystemAudio;
		public SubsystemParticles m_subsystemParticles;
		public SubsystemSoundMaterials m_subsystemSoundMaterials;
		public Random m_random = new Random();

		// Texturas de ropa generadas
		public RenderTarget2D m_innerClothedTexture;
		public RenderTarget2D m_outerClothedTexture;
		public PrimitivesRenderer2D m_primitivesRenderer = new PrimitivesRenderer2D();
		public bool m_clothedTexturesValid;
		public Texture2D m_originalSkinTexture;

		// Control de peso (densidad)
		public float m_baseDensity;
		public float m_totalDensityModifier;

		// Para IInventory
		Project IInventory.Project => Project;
		public int SlotsCount => 4;
		public int VisibleSlotsCount { get; set; } = 4;
		public int ActiveSlotIndex { get; set; } = -1;

		public UpdateOrder UpdateOrder => UpdateOrder.Default;

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
			m_subsystemSoundMaterials = Project.FindSubsystem<SubsystemSoundMaterials>(true);

			m_componentCreature = Entity.FindComponent<ComponentCreature>(true);
			m_componentBody = m_componentCreature.ComponentBody;
			m_componentHealth = Entity.FindComponent<ComponentHealth>(true);
			m_componentHumanModel = Entity.FindComponent<ComponentHumanModel>(true);
			m_componentNewHumanModel = Entity.FindComponent<ComponentNewHumanModel>(false);
			m_componentOuterClothingModel = Entity.FindComponent<ComponentOuterClothingModel>(true);

			// Guardar densidad base
			m_baseDensity = m_componentBody.Density;
			m_totalDensityModifier = 0f;

			// Guardar textura original
			m_originalSkinTexture = m_componentHumanModel.TextureOverride;

			foreach (ClothingSlot slot in ClothingSlot.ClothingSlots.Values)
			{
				m_clothes[slot] = new List<int>();
			}

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

			// Suscribirse al evento de lesión para aplicar protección de armadura
			if (m_componentHealth != null)
			{
				m_componentHealth.Injured += OnInjured;
			}

			Display.DeviceReset += Display_DeviceReset;
			RecalculateDensity();
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
			if (m_componentHealth != null)
			{
				m_componentHealth.Injured -= OnInjured;
			}
		}

		private void Display_DeviceReset()
		{
			m_clothedTexturesValid = false;
		}

		// -------------------------------------------------------------
		// MANEJADOR DE LESIONES (Protección de armadura)
		// -------------------------------------------------------------
		private void OnInjured(Injury injury)
		{
			if (injury == null || m_componentHealth == null || m_componentHealth.Health <= 0f)
				return;

			float damageToAbsorb = injury.Amount;

			// Intentar absorber daño con la armadura
			float remainingDamage = ApplyArmorProtection(damageToAbsorb);

			// Reducir la cantidad de daño de la lesión original
			injury.Amount = remainingDamage;
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
				RecalculateDensity();
			}
		}

		private void RecalculateDensity()
		{
			float totalModifier = 0f;
			foreach (var kv in m_clothes)
			{
				foreach (int value in kv.Value)
				{
					ClothingData data = GetClothingData(value);
					if (data != null)
					{
						totalModifier += data.DensityModifier;
					}
				}
			}
			m_totalDensityModifier = totalModifier;
			m_componentBody.Density = m_baseDensity + totalModifier;
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

		private Color GetClothingColor(int value)
		{
			int data = Terrain.ExtractData(value);
			return SubsystemPalette.GetFabricColor(m_subsystemTerrain, new int?(ClothingBlock.GetClothingColor(data)));
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
		// PROTECCIÓN DE ARMADURA
		// -------------------------------------------------------------
		public float ApplyArmorProtection(float attackPower)
		{
			float num = m_random.Float(0f, 1f);
			ClothingSlot slot = (num < 0.1f) ? ClothingSlot.Feet : ((num < 0.3f) ? ClothingSlot.Legs : ((num < 0.9f) ? ClothingSlot.Torso : ClothingSlot.Head));

			List<int> clothes = new List<int>(GetClothes(slot));
			List<int> afterProtection = new List<int>(clothes);
			float remainingPower = attackPower;

			for (int i = 0; i < clothes.Count; i++)
			{
				int value = clothes[i];
				ClothingData data = GetClothingData(value);
				if (data == null) continue;

				Block block = BlocksManager.Blocks[Terrain.ExtractContents(value)];
				float durability = (float)(block.GetDurability(value) + 1);
				float currentDamage = (float)block.GetDamage(value);
				float sturdiness = data.Sturdiness;
				float maxAbsorb = (durability - currentDamage) / durability * sturdiness;
				float absorb = MathF.Min(remainingPower * MathUtils.Saturate(data.ArmorProtection / 1f), maxAbsorb);

				if (absorb > 0f)
				{
					remainingPower -= absorb;
					{
						float rawDamage = absorb / sturdiness * durability + 0.001f;
						int damagePoints = (int)(MathF.Floor(rawDamage) + (m_random.Bool(MathUtils.Remainder(rawDamage, 1f)) ? 1 : 0));
						afterProtection[i] = BlocksManager.DamageItem(value, damagePoints, Entity);
					}
					// Reproducir sonido de impacto
					if (!string.IsNullOrEmpty(data.ImpactSoundsFolder))
					{
						m_subsystemAudio.PlayRandomSound(data.ImpactSoundsFolder, 1f, m_random.Float(-0.3f, 0.3f), m_componentBody.Position, 4f, 0.15f);
					}
					else
					{
						// Usar el sistema de materiales de sonido para la prenda
						m_subsystemSoundMaterials.PlayImpactSound(value, m_componentBody.Position, 1f);
					}
				}
			}

			// Eliminar prendas rotas
			for (int j = afterProtection.Count - 1; j >= 0; j--)
			{
				if (!BlocksManager.Blocks[Terrain.ExtractContents(afterProtection[j])].CanWear(afterProtection[j]))
				{
					afterProtection.RemoveAt(j);
					m_subsystemParticles.AddParticleSystem(
						new BlockDebrisParticleSystem(m_subsystemTerrain, m_componentBody.Position + m_componentBody.StanceBoxSize / 2f, 1f, 1f, Color.White, 0), false);
				}
			}

			// Reordenar por capa
			afterProtection.Sort((a, b) =>
			{
				ClothingData da = GetClothingData(a);
				ClothingData db = GetClothingData(b);
				return (da?.Layer ?? 0) - (db?.Layer ?? 0);
			});

			SetClothes(slot, afterProtection);
			return MathF.Max(remainingPower, 0f);
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

		private bool HasAnyClothes()
		{
			foreach (var list in m_clothes.Values)
			{
				if (list.Count > 0) return true;
			}
			return false;
		}

		private void UpdateRenderTargets()
		{
			Texture2D currentTex = m_componentHumanModel.TextureOverride;
			if (currentTex != m_innerClothedTexture && currentTex != null)
			{
				m_originalSkinTexture = currentTex;
			}

			if (!HasAnyClothes())
			{
				if (m_innerClothedTexture != null)
				{
					Utilities.Dispose(ref m_innerClothedTexture);
				}
				if (m_outerClothedTexture != null)
				{
					Utilities.Dispose(ref m_outerClothedTexture);
				}
				m_componentHumanModel.TextureOverride = m_originalSkinTexture;
				m_componentOuterClothingModel.TextureOverride = null;
				return;
			}

			Texture2D skinTexture = m_originalSkinTexture;
			if (skinTexture == null) return;

			if (m_innerClothedTexture == null || m_innerClothedTexture.Width != skinTexture.Width || m_innerClothedTexture.Height != skinTexture.Height)
			{
				Utilities.Dispose(ref m_innerClothedTexture);
				m_innerClothedTexture = new RenderTarget2D(skinTexture.Width, skinTexture.Height, 1, ColorFormat.Rgba8888, DepthFormat.None);
				m_clothedTexturesValid = false;
			}
			if (m_outerClothedTexture == null || m_outerClothedTexture.Width != skinTexture.Width || m_outerClothedTexture.Height != skinTexture.Height)
			{
				Utilities.Dispose(ref m_outerClothedTexture);
				m_outerClothedTexture = new RenderTarget2D(skinTexture.Width, skinTexture.Height, 1, ColorFormat.Rgba8888, DepthFormat.None);
				m_clothedTexturesValid = false;
			}

			m_componentHumanModel.TextureOverride = m_innerClothedTexture;
			m_componentOuterClothingModel.TextureOverride = m_outerClothedTexture;

			if (!m_clothedTexturesValid)
			{
				m_clothedTexturesValid = true;
				Rectangle oldScissor = Display.ScissorRectangle;
				RenderTarget2D oldTarget = Display.RenderTarget;
				try
				{
					Display.RenderTarget = m_innerClothedTexture;
					Display.Clear(new Vector4?(new Vector4(Color.Transparent)), null, null);
					int batchIndex = 0;
					TexturedBatch2D batch = m_primitivesRenderer.TexturedBatch(skinTexture, false, batchIndex++, DepthStencilState.None, null, BlendState.NonPremultiplied, SamplerState.PointClamp);
					batch.QueueQuad(Vector2.Zero, new Vector2(skinTexture.Width, skinTexture.Height), 0f, Vector2.Zero, Vector2.One, Color.White);

					foreach (ClothingSlot slot in ComponentClothing.m_innerSlotsOrder)
					{
						foreach (int value in m_clothes[slot])
						{
							ClothingData data = GetClothingData(value);
							if (data == null || data.IsOuter) continue;
							if (data.Texture == null && !string.IsNullOrEmpty(data._textureName))
								data.Texture = ContentManager.Get<Texture2D>(data._textureName);
							Color color = GetClothingColor(value);
							batch = m_primitivesRenderer.TexturedBatch(data.Texture, false, batchIndex++, DepthStencilState.None, null, BlendState.NonPremultiplied, SamplerState.PointClamp);
							batch.QueueQuad(Vector2.Zero, new Vector2(skinTexture.Width, skinTexture.Height), 0f, Vector2.Zero, Vector2.One, color);
						}
					}
					m_primitivesRenderer.Flush(true, int.MaxValue);

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
							Color color = GetClothingColor(value);
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
